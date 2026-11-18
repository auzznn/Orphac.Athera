# ATH-S06 Design — Dock Custody and Receiving Gate One

**Slice:** S06 (`docs/implementation/slices/S06_dock_custody_and_receiving_gate_one.md`)  
**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**  
**Prerequisites:** S05 (`Purchase Orders, Approved Revisions and Four-Eyes`)  
**Open Decision Escalations:** [ATH-CR-003 (OD-01)](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) (`EXACT_APPROVED_QTY_ONLY` safe default active — excess physical deliveries remain held in unvalued dock custody and cannot be released for Gate 2 valuation without approved PO amendment), [ATH-CR-001 (OD-02)](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md)

---

## 1. Business Behavior & User Journey
1. **Unvalued Gate 1 Arrival Logging (`receiving.manage`):** An authorized receiving operator selects an entity-scoped `APPROVED` Purchase Order (whose `CurrentApprovedRevisionId` is active) and records a physical supplier delivery arrival (*Surat Jalan* / delivery note number, receipt date, station, vehicle/seal reference, optional document evidence ref, and per-line quantities in an active item UoM).
2. **Automatic Overdelivery Segregation (`OD-01` Safe Default):** For each delivered PO line (tracked consistently in base UoM by `LineageLineKey`), arriving quantity up to remaining approved commitment (`OrderedBaseQuantity - sum(PendingCheck + DockReleased across prior receipts)`) enters `PendingCheckBaseQuantity` (`ARRIVED` or `DOCK_CHECK_PENDING`), while any excess quantity is **never rejected from physical dock logging** and is automatically segregated into `DockHeldBaseQuantity` (`OverdeliveryExcessBaseQuantity > 0`, `DiscrepancyReasonCode = OVERDELIVERY_OD01_HOLD`, `CustodyStatus = DOCK_HELD` / `DOCK_CHECK_PENDING`).
3. **Dock Inspection & Disposition (`DOCK_CHECK_PENDING`, `DOCK_HELD`, `DOCK_REJECTED`, `DOCK_RELEASED`):** Warehouse/QC operators record append-only dock checks splitting delivered base quantity across `PendingCheckBaseQuantity`, `DockHeldBaseQuantity`, `DockReleasedBaseQuantity`, and `DockRejectedBaseQuantity` with mandatory discrepancy codes and optional evidence references for held/rejected quantities. Releasing more than remaining approved PO commitment is blocked (`409 od01_overdelivery_release_blocked`) unless an independent four-eyes PO amendment (`S05`) increases approved PO quantity or excess goods are rejected unvalued (`DOCK_REJECTED`).
4. **Zero Financial / Inventory Effect Boundary:** Gate 1 never writes to `inventory` stock positions/valuation pools or `gl.journal_entries` (`HasFinancialPosting = false`, `ValuedAcceptedBaseQuantity = 0` enforced in domain + PostgreSQL CHECK constraints). Recording an arrival calls `PurchaseOrder.RegisterDownstreamReference(0m, nowUtc)` so the parent PO cannot be cancelled after physical custody starts, while open commitment reflects `AcceptedBaseQuantityTotal == 0m` until Gate 2 (`S07`).

### Explicit Non-Goals
- Zero valued acceptance, zero moving-average stock pool updates, zero `Dr Inventory Asset / Cr GRNI` journals, and zero automatic replacement POs/deliveries (deferred to S07–S10).

---

## 2. Module Ownership & Bounded Contexts (`ATH-DOMAIN-BOUNDARIES-001`)

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `Procurement` (Receiving sub-context) | `procurement.receipt_sequences`, `procurement.goods_receipts`, `procurement.goods_receipt_lines`, `procurement.dock_checks`, `GoodsReceiptWorkflowService`, `ReceivingHash` | Unvalued Gate 1 custody ledger, delivery document uniqueness, exact line conservation, `OD-01` excess holding, append-only dock check evidence | Valued inventory stock balances (`S07`), weighted-average cost pools (`S07`), GL journals/GRNI (`S04`/`S07`), supplier AP bills (`S09`) |
| `Procurement` (PO side — S05) | `PurchaseOrder.RegisterDownstreamReference(0m, nowUtc)` | Downstream reference lock blocking PO cancellation while preserving `AcceptedBaseQuantityTotal = 0` | Mutating frozen PO revisions |
| `Catalog` (Read-only) | `UomConverter.CaptureSnapshot` | Active item UoM factor lookup & discrete integer base quantity validation | Mutating UoM masters from receiving |
| `Identity` (Extension) | `PermissionCodes`, `RoleTemplates` | `receiving.read`, `receiving.manage` (`InventoryClerk` gets `read`+`manage`; `ProcurementOfficer` & `FinanceOfficer` get `receiving.read`) | Goods receipt custody records |
| `Audit` / `Integration` | `AuditEvent`, `OutboxMessage` | `GOODS_RECEIPT_RECORDED`, `GOODS_RECEIPT_DOCK_CHECK_RECORDED` & transactional outbox events (`procurement.dockreceipt.recorded.v1`, `procurement.dockcheck.recorded.v1`) | Gate 2 GL postings |

---

## 3. Conservation Invariant, State Machines & Concurrency

### Quantity Conservation Law (Base UoM, `numeric(18,6)`)
For every `GoodsReceiptLine`:
- `PhysicallyDeliveredBaseQuantity == DockRejectedBaseQuantity + ValuedAcceptedBaseQuantity + UnresolvedCustodyBaseQuantity`
- `UnresolvedCustodyBaseQuantity == PendingCheckBaseQuantity + DockHeldBaseQuantity + (DockReleasedBaseQuantity - ValuedAcceptedBaseQuantity)`
- Strict S06 invariant: `ValuedAcceptedBaseQuantity == 0m` and `HasFinancialPosting == false`.

### State Machines
- **`GoodsReceiptLine.CustodyStatus`:**
  - `ARRIVED` (eligible qty arrived clean without hold/flag -> full qty in `PendingCheckBaseQuantity`)
  - `DOCK_CHECK_PENDING` (`PendingCheckBaseQuantity > 0` with discrepancy/inspection flag or mixed excess split)
  - `DOCK_HELD` (`PendingCheckBaseQuantity == 0` and `DockHeldBaseQuantity > 0`, including `OD-01` excess holds)
  - `DOCK_REJECTED` (`DockRejectedBaseQuantity == PhysicallyDeliveredBaseQuantity` — fully rejected unvalued)
  - `DOCK_RELEASED` (`PendingCheckBaseQuantity == 0`, `DockHeldBaseQuantity == 0`, and `DockReleasedBaseQuantity > 0` — eligible portion released for Gate 2 valuation)
- **`GoodsReceipt.Status` (derived from lines):**
  - `FULLY_REJECTED_UNVALUED` (if `TotalRejectedBaseQuantity == TotalDeliveredBaseQuantity`)
  - `ARRIVED` (if all lines remain `ARRIVED`)
  - `IN_DOCK_INSPECTION` (if any line has `PendingCheckBaseQuantity > 0` or `DockHeldBaseQuantity > 0`)
  - `RELEASED_FOR_ACCEPTANCE` (all non-rejected quantities released to Gate 2 queue).

### Concurrency & Retry Behavior
- Both `RecordArrivalAsync` and `RecordDockCheckAsync` acquire `SELECT 1 FROM procurement.purchase_orders WHERE tenant_id = @t AND legal_entity_id = @e AND purchase_order_id = @p FOR UPDATE` so concurrent arrivals/releases serialize strictly per PO.
- Idempotency keys (`IdempotencyKey` + SHA-256 `ArrivalPayloadHash` / `PayloadHash`) replay identically (`200 OK`, no duplicate reference increment/outbox) and reject mismatched reuse (`409 duplicate_receipt_idempotency_conflict` / `duplicate_dock_check_idempotency_conflict`).
- Duplicate supplier *Surat Jalan* (`(tenant_id, legal_entity_id, supplier_partner_id, supplier_delivery_note_number)`) fails cleanly (`409 duplicate_supplier_delivery_note`) without partial inserts or reference inflation.

---

## 4. Database Schema, Forced RLS & Privileges (`M06_DockCustodyAndReceivingGateOne`)

1. **`procurement.receipt_sequences`**: PK `(tenant_id, legal_entity_id, fiscal_year)`, `last_number bigint CHECK (last_number >= 0)`.
2. **`procurement.goods_receipts`**:
   - PK `goods_receipt_id uuid`, AK `(tenant_id, legal_entity_id, goods_receipt_id)`.
   - Composite FKs to `platform.legal_entities`, `procurement.purchase_orders`, `procurement.purchase_order_revisions`, `partners.partners`, `identity.tenant_memberships`.
   - Unique indexes: `(tenant_id, legal_entity_id, receipt_number)`, `(tenant_id, legal_entity_id, idempotency_key)`, `(tenant_id, legal_entity_id, supplier_partner_id, supplier_delivery_note_number)`.
   - CHECK constraints: `ck_goods_receipts_status`, `ck_goods_receipts_unvalued_gate1` (`has_financial_posting = false AND total_valued_accepted_base_quantity = 0`), `ck_goods_receipts_conservation` (`total_delivered_base_quantity > 0 AND total_delivered_base_quantity = total_rejected_base_quantity + total_valued_accepted_base_quantity + total_unresolved_custody_base_quantity`).
3. **`procurement.goods_receipt_lines`**:
   - PK `goods_receipt_line_id uuid`, AK `(tenant_id, legal_entity_id, goods_receipt_line_id)`.
   - Composite FKs to `procurement.goods_receipts`, `procurement.purchase_order_lines` (`tenant_id, legal_entity_id, po_line_id` AK added on `PurchaseOrderLine`), `catalog.items`, `catalog.units_of_measure`.
   - CHECK constraints: `ck_gr_lines_custody_status`, `ck_gr_lines_positive_delivered`, `ck_gr_lines_s06_unvalued` (`valued_accepted_base_quantity = 0`), `ck_gr_lines_conservation` (`physically_delivered_base_quantity = dock_rejected_base_quantity + valued_accepted_base_quantity + unresolved_custody_base_quantity AND unresolved_custody_base_quantity = pending_check_base_quantity + dock_held_base_quantity + (dock_released_base_quantity - valued_accepted_base_quantity)`).
4. **`procurement.dock_checks`** (strictly append-only):
   - PK `dock_check_id uuid`, composite FKs to `goods_receipts`, `goods_receipt_lines`, `tenant_memberships`.
   - Unique `(tenant_id, legal_entity_id, goods_receipt_line_id, idempotency_key)`.
- **RLS & Privileges:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 4 tables; `athera_app` gets `SELECT, INSERT, UPDATE` ONLY (`DELETE` revoked) on `receipt_sequences`, `goods_receipts`, `goods_receipt_lines`, and `SELECT, INSERT` ONLY (`UPDATE`/`DELETE` revoked) on `dock_checks`.

---

## 5. Permissions, API Endpoints & Next.js UX Contracts
- **Permissions:** `receiving.read`, `receiving.manage`.
- **Endpoints (`/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving`):**
  - `GET /eligible-pos` (approved POs with per-line approved qty, delivered/pending/held/released/rejected custody sums, remaining arrival capacity, and active UoM conversions)
  - `GET /receipts` (filterable receipt register + entity custody totals + verified `totalLinkedGlJournals = 0`)
  - `GET /receipts/{receiptId:guid}` (receipt detail, line conservation buckets, append-only `dockChecks`, and `linkedGlJournalCount = 0`)
  - `POST /receipts` (`receiving.manage`, CSRF validated -> `201 Created` or `200 OK` replay)
  - `POST /receipts/{receiptId:guid}/lines/{lineId:guid}/dock-checks` (`receiving.manage`, CSRF validated -> `200 OK`)
- **Next.js UX (`/w/[tenantId]/[entityId]/receiving`):**
  - Shop-floor receiving & dock inspection station in `DESIGN.md` archival ledger aesthetic (`min-h-11` shop-floor touch targets, bilingual `id-ID` / `en-US`, instant UoM base-qty & `OD-01` excess hold preview, quick dock disposition presets + split quantities + discrepancy reason & evidence ref, append-only `dockChecks` history, explicit `Jurnal GL / GRNI Terbentuk: 0` banner, mobile `390x844` zero overflow, read-only `receiving.read` view).

---

## 6. Mandatory Executable Acceptance Tests
- **Unit (`S06DockReceivingTests.cs`):** Conservation formula validation, rejection of unbalanced disposition buckets, `OD-01` automatic excess hold & release block, discrepancy reason enforcement, deterministic `ReceivingHash`.
- **PostgreSQL 18 Integration (`S06ReceivingPostgresTests.cs`):** `S06_PG_01` (forced RLS missing context & cross-tenant isolation + cross-tenant composite FK `23503`), `S06_PG_02` (`M05 -> M06 -> M05 -> M06` migration round-trip), `S06_PG_03` (`dock_checks` append-only `42501`, `DELETE` denied `42501`, SQL conservation check constraint `23514`), `S06_API_01` (partial delivery + `55` released / `5` damaged reject split + PO cancel blocked + `0` GL/GRNI rows), `S06_API_02` (`OD-01` `60 vs 48` auto-hold + `409` over-release blocked + release `48`/hold `12` + release held `12` after PO Rev 2 approved), `S06_API_03` (duplicate *Surat Jalan* `409`, arrival & check idempotency replay/conflict, unapproved PO `409`, concurrent `Task.WhenAll` releases capped to approved qty), `S06_API_04` (RBAC read-only `403`, missing grant `403`, cross-tenant `404` zero leak).
- **Frontend & E2E (`s06-receiving-dock.test.tsx`, `s06-receiving-dock.spec.ts`):** `U1` approves PO via `U2`, logs partial delivery, splits damaged vs released, triggers `OD-01` excess hold + blocked release, `U2` verifies `0` GRNI journal in `/finance`, `NUSA-SBY` read-only view, `SMDR-BDG` `403` (`U2`) & `404` (`U1`), desktop (`1280x800`) + mobile (`390x844`) PNGs.
