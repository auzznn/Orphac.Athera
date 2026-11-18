# ATH-S12 Design — Customer Sales Orders and Commercial Commitments

**Slice:** S12 (`docs/implementation/slices/S12_customer_sales_orders_and_commercial_commitments.md`)  
**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**  
**Prerequisites:** S03 (`287d7e5`), S04 (`0fcb7b5`), S11 (`1b84521`)  
**Open Decision Escalations:** [ATH-CR-001 (OD-02)](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md), [ATH-CR-002 (OD-07 / OD-13)](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md), [ATH-CR-009 (OD-09)](../../decisions/ATH-CR-009-OD09-sales-order-commercial-terms-and-credit-policy.md)

---

## 1. Business Behavior & User Journey
1. **Commercial Sales Order Drafting (`sales.manage`):** A sales officer opens `/w/[tenantId]/[entityId]/sales-orders`, inspects entity stock availability per item read from Inventory (`InventoryAvailabilityService.GetItemAvailabilityMapAsync`), and drafts a `SALES_ORDER` (`SO-DRF-{yyyyMMdd}-{6char}`) inside one legal entity (`IDR`) selecting:
   - An active, entity-eligible **Customer** (`PartnerEligibilityEvaluator.Evaluate(partner, profile).CustomerEligible == true`), defaulting `PaymentTermsDays` from `PartnerEntityProfile.DefaultPaymentTermsDays`,
   - Valid `OrderDate` & optional `RequestedDeliveryDate >= OrderDate`, `DeliveryAddressSnapshot`, optional `CustomerReference`,
   - One or more lines referencing active, entity-sales-allowed catalogue items (`ItemEligibilityEvaluator.Evaluate(item, policy).SalesAllowed == true`), active item-scoped UoMs (`UomConverter.CaptureSnapshot`), positive `OrderedQuantity > 0`, positive decimal `UnitPrice > 0`, **`DiscountAmount == 0` (`OD-09`)**, and optional explicit `TaxCodeId` validated as `TaxDirections.Output` and active/effective on `OrderDate` (`TaxDeterminationEvaluator.Evaluate`).
2. **Submit & Four-Eyes Governance (`sales.manage` -> `sales.approve`):** Submitting revision `N` freezes `IsFrozen = true` with deterministic SHA-256 `SnapshotHash`, creates a `PENDING` `approval.document_approvals` record under entity policy `SALES_ORDER_COMMITMENT` (`RequiresFourEyes = true`), and emits `AuditEvent` + `OutboxMessage` (`sales.order.submitted.v1`). Before approval, `SoNumber` remains `null` (`DraftCode` only).
3. **Independent Four-Eyes Approval (`OD-02`):** Self-approval by submitter/creator is blocked (`403 four_eyes_self_approval_forbidden` + SQL CHECK `decided_by_user_id <> requested_by_user_id`). Only a distinct `sales.approve` actor may decide (`APPROVED` or `REJECTED` with reason). On approval:
   - Re-validates customer/item/output-tax eligibility and `OD-09` rules;
   - Assigns sequential legal-entity fiscal-year `SO-{Year:D4}-{Seq:D5}` from `sales.sales_order_sequences` (first approval only; optional `soNumberOverride` verifies duplicate guard `409 duplicate_so_number`);
   - Supersedes any previous `APPROVED` revision (`SUPERSEDED`) without mutating historical snapshots or lines; promotes `CurrentApprovedRevisionId` & sets `Status = APPROVED`.
4. **Controlled Amendment (`Rev N + 1`), Field/Line Diff & Close / Cancel:**
   - Creating an amendment revision on an `APPROVED` SO copies current approved lines into an editable `DRAFT` `Rev N + 1` while keeping `CurrentApprovedRevisionId` intact until independent approval of `Rev N + 1`.
   - Downward reduction below already-reserved/dispatched quantities (`ReservedBaseQuantity + DispatchedBaseQuantity`) or cancellation when `TotalReservedBaseQuantity > 0 || TotalDispatchedBaseQuantity > 0 || TotalBilledBaseQuantity > 0` fails closed (`409 so_has_downstream_fulfillment`).
   - Hard delete (`DELETE`) is blocked (`409 so_delete_forbidden` + `DELETE` revoked at DB role level).
   - Authorized short-close (`POST .../close`) closes an `APPROVED` SO cleanly when no open draft/pending revision exists.

### Explicit Non-Goals (Deferred to S13–S15 / OD-09)
- Zero inventory reservations/picks/dispatch postings (`S13`), zero AR invoices or GL revenue/COGS postings (`S14`), zero customer receipts (`S15`), zero unapproved discounts (`discountAmount > 0`) or credit-limit overrides (`ATH-CR-009`), and zero CRM/ecommerce/MTO planning.

---

## 2. Module Ownership & Bounded Contexts (`ATH-DOMAIN-BOUNDARIES-001`)

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `Sales` | `sales.sales_order_sequences`, `sales.sales_orders`, `sales.sales_order_revisions`, `sales.sales_order_lines`, `SalesOrderHash`, `SalesOrderDiffCalculator`, `SalesOrderCoordinator` | SO lifecycle, per-entity `SO-` numbering, immutable commercial/tax revision snapshots, diff calculator, short-close/cancel | Writing `inventory.stock_reservations` / `stock_positions` (`S13`), posting `gl.journal_entries` / `ar.*` (`S14`) |
| `Inventory` (contract read) | `InventoryAvailabilityService.GetItemAvailabilityMapAsync` | Read-only stock availability projection (`OnHand`, `Reserved`, `Quarantined`, `Blocked`, `Available`) | Sales order commercial lines or prices |
| `Approval` | `approval.document_approvals`, `approval.approval_policies` (`SALES_ORDER_COMMITMENT`) | Document four-eyes approval policy & decision checks | Sales header/line snapshots |
| `Partners` / `Catalog` / `Tax` | Read-only evaluators | `PartnerEligibilityEvaluator`, `ItemEligibilityEvaluator`, `UomConverter.CaptureSnapshot`, `TaxDeterminationEvaluator` | Direct writes from `Sales` |
| `Identity` (extension) | `PermissionCodes`, `RoleTemplates` | Adds `sales.manage`, `sales.approve`; updates `SalesOfficer` (`sales.read`, `sales.manage`, `inventory.read`, ...) & `EntityApprover` (`sales.read`, `sales.approve`); preserves `U1` without `sales.read` for `S02` approval test compatibility; adds demo `U5` (`dewi.lestari@demo.athera.local`) | Sales domain rows |
| `Audit` / `Integration` | `AuditEvent`, `OutboxMessage` | Append-only audit & outbox (`sales.order.submitted.v1`, `sales.order.revision.approved.v1`, `sales.order.rejected.v1`, `sales.order.revised.v1`, `sales.order.cancelled.v1`, `sales.order.closed.v1`) | Approval decisions |

---

## 3. State Machines & Concurrency Guarantees

- **`SalesOrder.Status`:** `DRAFT` -> `PENDING_APPROVAL` -> `APPROVED` -> `CLOSED` | `CANCELLED` (or `DRAFT` / `PENDING_APPROVAL` -> `CANCELLED`; rejecting Rev 1 returns `DRAFT`, rejecting Rev N+1 keeps `APPROVED` on `CurrentApprovedRevisionId`).
- **`SalesOrderRevision.Status`:** `DRAFT` -> `PENDING_APPROVAL` -> `APPROVED` | `REJECTED`; `APPROVED` -> `SUPERSEDED` (status transition only when successor revision approves; `IsFrozen` stays `true`).
- **`FulfillmentProgress` (derived view):** `NOT_APPLICABLE`, `NOT_FULFILLED`, `PARTIALLY_RESERVED`, `FULLY_RESERVED`, `PARTIALLY_DISPATCHED`, `FULLY_DISPATCHED`, `SHORT_CLOSED`.
- **Concurrency & Lock Order:**
  - `Submit`, `Approve/Reject`, `Revise`, `Close`, and `Cancel` acquire `SELECT ... FOR UPDATE` on `sales.sales_orders`, plus `sales_order_revisions` / `approval.document_approvals`.
  - `"sales.sales_order_sequences"` added to `InventoryValuationService.IncrementEntityYearSequenceAsync` table whitelist and executed on the transaction's `DbCommand`.
  - PostgreSQL `23505` / `DbUpdateConcurrencyException` caught outside transaction executor and mapped to `409`.

---

## 4. Database Schema, Forced RLS & SQL Freeze Triggers (`M12_CustomerSalesOrdersAndCommercialCommitments`)
1. **`sales.sales_order_sequences`**: PK `(tenant_id, legal_entity_id, fiscal_year)`, `last_number bigint`.
2. **`sales.sales_orders`**: `sales_order_id uuid PK`, `(tenant_id, legal_entity_id, sales_order_id)` AK, partial unique `ux_sales_orders_entity_so_number` (`WHERE so_number IS NOT NULL`), unique `(tenant_id, legal_entity_id, draft_code)`, checks on `status` and non-negative fulfillment quantities (`total_reserved_base_quantity`, `total_dispatched_base_quantity`, `total_billed_base_quantity`).
3. **`sales.sales_order_revisions`**: `revision_id uuid PK`, `(tenant_id, legal_entity_id, revision_id)` AK, unique `(tenant_id, legal_entity_id, sales_order_id, revision_number)`, partial unique `ux_so_revisions_approval_record` (`WHERE approval_record_id IS NOT NULL`), four-eyes CHECK (`decided_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id`), status & non-negative totals checks.
4. **`sales.sales_order_lines`**: `so_line_id uuid PK`, composite FKs to `sales_order_revisions`, `items`, `units_of_measure`, optional `tax_codes`; unique `(tenant_id, legal_entity_id, revision_id, line_number)` & `(..., lineage_line_key)`; CHECK `ordered_quantity > 0`, `ordered_base_quantity > 0`, `unit_price > 0`, **`discount_amount = 0` (`OD-09`)**, `reserved_base_quantity >= 0`, `dispatched_base_quantity >= 0`, `billed_base_quantity >= 0`.
- **RLS & Privileges:** `ENABLE` + `FORCE ROW LEVEL SECURITY` (`platform.current_tenant_id()`) on all 4 `sales` tables; `athera_app` gets `SELECT, INSERT, UPDATE` ONLY (`DELETE` revoked) on header/revision/sequence tables, and `SELECT, INSERT, UPDATE, DELETE` on `sales_order_lines` guarded by immutability triggers:
  - `sales.guard_frozen_so_revision_snapshot()` (`BEFORE UPDATE` -> `ERRCODE = '55000'` when `OLD.is_frozen = true` and any snapshot/total/hash column changes).
  - `sales.guard_frozen_so_revision_lines()` (`BEFORE INSERT OR UPDATE OR DELETE` -> `ERRCODE = '55000'` when parent revision `is_frozen = true` — except future S13/S14 fulfillment quantity counters? Wait: lines on `sales_order_lines` keep `ReservedBaseQuantity/DispatchedBaseQuantity/BilledBaseQuantity` either on `sales_order_lines` updated via controlled path OR trigger blocks commercial column edits while allowing downstream fulfillment quantity increments if needed — for S12 all columns are commercial snapshot columns, so `guard_frozen_so_revision_lines` blocks `INSERT`/`DELETE` and any mutation to commercial columns when `is_frozen = true`!).

---

## 5. Permissions, API Endpoints & Next.js UX Contracts
- **Permissions:** `sales.read`, `sales.manage`, `sales.approve`. (`U5` `dewi.lestari@demo.athera.local` receives `SalesOfficer` on `NUSA-JKT` only; `U2` receives `sales.read` + `sales.approve` on `NUSA-JKT` & `SMDR-BDG` and `sales.read` on `NUSA-SBY`; `U1` intentionally omits `sales.*` to preserve S02 permission-request test).
- **Endpoints (`/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders`):**
  - `GET /reference-data` (eligible customers with default payment terms, sales-eligible items with UoMs + live `InventoryAvailabilityService` summary, effective `OUTPUT` tax codes, policy `OD-09` summary)
  - `GET /`, `GET /{salesOrderId:guid}`
  - `POST /` (create DRAFT Rev 1)
  - `PUT /{salesOrderId:guid}/revisions/{revisionId:guid}` (update mutable DRAFT Rev)
  - `POST /{salesOrderId:guid}/revisions` (fork `DRAFT` `Rev N + 1` from approved revision)
  - `POST /{salesOrderId:guid}/revisions/{revisionId:guid}/submit`
  - `POST /{salesOrderId:guid}/revisions/{revisionId:guid}/decide` (`APPROVED` / `REJECTED` + reason + optional `soNumberOverride`)
  - `POST /{salesOrderId:guid}/cancel`, `POST /{salesOrderId:guid}/close`, `DELETE /{salesOrderId:guid}` -> `409 so_delete_forbidden`
- **Next.js UX (`/w/[tenantId]/[entityId]/sales-orders`):**
  - Following `PRODUCT.md`, `DESIGN.md`, and `craft-floor.md`: `#f6f5f0` archival paper shell, `1px` hairline KPI strip + collapsible `OD-09` & `OD-02` governance strip, 3 synchronous tabs (`Daftar & Rincian SO`, `Editor SO & Revisi`, `Antrean Persetujuan Four-Eyes`), empty placeholder-first inputs, live stock availability badges per line item (`Tersedia / On Hand`), `Perbandingan Perubahan Revisi` diff block in `SalesOrderDetailSheet.tsx`, single primary CTA per state, read-only NUSA-SBY view, full `id-ID` & `en-US` localization.

---

## 6. Mandatory Executable Acceptance Tests
- **Unit (`S12SalesOrdersUnitTests.cs`):** State transitions, `OD-09` discount `> 0` & credit-override block, positive price/qty checks, `OD-02` self-approval guard, deterministic `SnapshotHash`, `SalesOrderDiffCalculator`, downstream fulfillment cancel block, role templates separation (`SalesOfficer` vs `EntityApprover`).
- **PostgreSQL 18 Integration (`S12SalesOrdersPostgresTests.cs`):** `S12_PG_01` (RLS deny & cross-tenant isolation across all 4 `sales` tables), `S12_PG_02` (`M11 -> M12 -> M11 -> M12` round-trip), `S12_PG_03` (`DELETE` denied `42501`, SQL freeze triggers returning `55000`, `discount_amount = 0` check constraint, four-eyes check constraint), `S12_API_01` (`U5` drafts -> submits -> `U5` self-approve `403` -> `U2` approves `200` assigning `SO-2026-...` + duplicate SO number `409`), `S12_API_02` (`6x` concurrent approval -> single `200`), `S12_API_03` (`Rev 1` -> `Rev 2` amendment, `Rev 1` unchanged, diff verified), `S12_API_04` (inactive customer `409`, inactive/restricted item `409`, `INPUT` tax rejected `409`, `OD-09` discount blocked `422`, downstream fulfillment cancel/reduction `409` vs close `200`, `NUSA-SBY` read-only `403` & foreign `SMDR` `404`/`403`).
- **Frontend & E2E (`s12-sales-orders.test.tsx`, `s12-sales-orders.spec.ts`):** `U5` creates/submits SO with live stock availability & `PPN-OUT-11`, self-approval blocked, `U2` approves `Rev 1`, `U5` amends `Rev 2` & `U2` reviews diff and approves (`Rev 1` intact), `U5` short-closes or tests `OD-09` guard, `NUSA-SBY` read-only, `SMDR` cross-tenant denial, desktop (`1440x900`) & mobile (`390x844`) captures.
