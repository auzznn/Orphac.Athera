# ATH-S05 Design — Purchase Orders, Approved Revisions and Four-Eyes

**Slice:** S05 (`docs/implementation/slices/S05_purchase_orders_approved_revisions_and_four_eyes.md`)  
**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**  
**Prerequisites:** S02 (`57618ad`), S03 (`287d7e5`), S04 (`0fcb7b5`)  
**Open Decision Escalations:** [ATH-CR-001 (OD-02)](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md), [ATH-CR-002 (OD-07 / OD-13)](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md), [ATH-CR-003 (OD-01)](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md)

---

## 1. Business Behavior & User Journey
1. **Commercial PO Drafting (`procurement.manage`):** An authorized procurement officer creates a `DRAFT` Purchase Order inside one legal entity (`NUSA-JKT`, currency matching legal entity functional currency `IDR`) selecting an active eligible supplier (`PartnerEligibilityEvaluator`), `DeliveryDestination`, `PaymentTermsDays`, and `>= 1` line items referencing active purchase-eligible catalogue items (`ItemEligibilityEvaluator.PurchaseAllowed`), active item-scoped UoMs converted deterministically via `UomConverter.CaptureSnapshot`, decimal unit price `> 0`, optional explicit `TaxCodeId` (`TaxDeterminationEvaluator.Evaluate`), and exact-quantity `OD-01` tolerance.
2. **Submit, Safe Numbering & Snapshot Freeze:** Submitting a draft revision validates master eligibility, assigns a per-entity fiscal-year sequential number (`PO-{Year:D4}-{Seq:D5}` from `procurement.po_sequences` or caller-provided unique `poNumberOverride` for duplicate-number tests), freezes the revision snapshot (`IsFrozen = true`, immutable SHA-256 `SnapshotHash`), creates a `PENDING` `approval.document_approvals` record tied to policy `PURCHASE_ORDER_COMMITMENT` (`RequiresFourEyes = true`), and writes `AuditEvent` + `OutboxMessage` atomically.
3. **Independent Four-Eyes Approval (`procurement.approve`):** Self-approval by the submitter (or creator on single-actor flow) is blocked (`403 four_eyes_self_approval_forbidden` + PostgreSQL CHECK constraint `decided_by_user_id <> requested_by_user_id`). Only a distinct holder of `procurement.approve` may approve or reject. Approving revision `N` re-validates supplier/item eligibility, verifies `TotalOrderedBaseQuantity >= AcceptedBaseQuantityTotal`, supersedes revision `N - 1` (`SUPERSEDED`) without mutating any historical snapshot column or line, promotes `CurrentApprovedRevisionId` & `CurrentApprovedRevisionNumber`, and sets `PurchaseOrder.Status = APPROVED`.
4. **Controlled Revision (`Rev N + 1`) & Visual Diff:** Modifying an approved PO creates a new editable `DRAFT` revision (`SupersedesRevisionId = CurrentApprovedRevisionId`), leaving the approved revision & snapshot completely untouched until `Rev N + 1` is submitted and independently approved. Approvers inspect field- and line-level changes (`PurchaseOrderDiffCalculator`) before deciding.
5. **Cancel vs Short-Close:** Deleting POs is prohibited (`DELETE` revoked at DB level; API `DELETE` returns `409 po_delete_forbidden`). Cancellation is blocked (`409 po_has_downstream_references`) whenever `DownstreamReferenceCount > 0` or `AcceptedBaseQuantityTotal > 0`. Authorized short-close (`POST .../short-close`) on an `APPROVED` PO with no open `DRAFT`/`PENDING_APPROVAL` revision closes remaining commitment (`Status = CLOSED`, `RemainingOpenBaseQuantity = 0`).

### Explicit Non-Goals
- Zero dock arrival/custody records, zero physical stock movement, zero moving-average inventory valuation entries, zero GRNI or GL journals, and zero supplier bills in S05 (deferred to S06–S09).

---

## 2. Module Ownership & Bounded Contexts (`ATH-DOMAIN-BOUNDARIES-001`)

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `Procurement` | `procurement.po_sequences`, `procurement.delivery_tolerance_policies`, `procurement.purchase_orders`, `procurement.purchase_order_revisions`, `procurement.purchase_order_lines`, `PurchaseOrderDiffCalculator`, `PurchaseOrderWorkflowService` | PO aggregate, per-entity sequence numbers, immutable frozen commercial/item/tax snapshots, diff calculation, close/cancel lifecycle | Dock custody/acceptance rows (`S06`), stock/valuation ledgers (`S07`), GL journals (`S04`/`S07`), AP invoices (`S09`) |
| `Approval` (extension) | `approval.document_approvals`, `DocumentApproval` | Document-scoped four-eyes approval requests/decisions linked to `approval.approval_policies` | Procurement header/line snapshots (reads policy rules only) |
| `Partners` / `Catalog` / `Tax` | Read-only evaluators | `PartnerEligibilityEvaluator`, `ItemEligibilityEvaluator`, `UomConverter.CaptureSnapshot`, `TaxDeterminationEvaluator.Evaluate` | Direct SQL writes from `Procurement` into master tables |
| `Identity` (extension) | `PermissionCodes`, `RoleTemplates` | `procurement.manage`, `procurement.approve` (`ProcurementOfficer` gets `manage` without `approve`; `EntityApprover` gets `procurement.read` + `procurement.approve`) | Procurement documents |
| `Audit` / `Integration` | `AuditEvent`, `OutboxMessage` | Append-only audit & transactional outbox (`procurement.po.submitted.v1`, `procurement.po.approved.v1`, `procurement.po.rejected.v1`, `procurement.po.revised.v1`, `procurement.po.cancelled.v1`, `procurement.po.short_closed.v1`) | Document approval decisions |

---

## 3. State Machines & Concurrency Guarantees

- **`PurchaseOrder.Status`:** `DRAFT` -> `PENDING_APPROVAL` -> `APPROVED` -> `CLOSED` | `CANCELLED` (or `DRAFT`/`PENDING_APPROVAL` -> `CANCELLED`; `REJECTED` first revision returns PO to `DRAFT`; `REJECTED` amendment revision leaves PO `APPROVED` on its prior `CurrentApprovedRevisionId`).
- **`PurchaseOrderRevision.Status`:** `DRAFT` -> `PENDING_APPROVAL` -> `APPROVED` | `REJECTED`; `APPROVED` -> `SUPERSEDED` (status transition only when successor revision approves; `IsFrozen` remains `true` forever).
- **`ReceivingProgress` (derived view):** `NOT_APPLICABLE` (before first approval or cancelled), `SHORT_CLOSED` (`CLOSED` with unfulfilled balance), `NOT_RECEIVED` (`AcceptedBaseQuantityTotal == 0`), `PARTIALLY_RECEIVED` (`0 < Accepted < Ordered`), `FULLY_RECEIVED` (`Accepted >= Ordered`).
- **Concurrency & Lock Order:**
  - `Submit` & `Decide` acquire `SELECT ... FOR UPDATE` on `procurement.purchase_orders` (`.FromSqlInterpolated(...).AsNoTracking().ToListAsync(ct)`), plus `purchase_order_revisions` and `approval.document_approvals` on `Decide`.
  - `IncrementPoSequenceAsync` executes `INSERT INTO procurement.po_sequences ... ON CONFLICT DO UPDATE SET last_number = last_number + 1 RETURNING last_number` on the ambient transaction's `DbCommand`.
  - Unique constraints & PostgreSQL `23505` / `DbUpdateConcurrencyException` are caught OUTSIDE `ExecuteInEntityAsync` and mapped cleanly to `409` (`duplicate_po_number`, `concurrency_conflict`, `approval_already_decided`).

---

## 4. Database Schema, Forced RLS & SQL Freeze Triggers (`M05_PurchaseOrdersRevisionsAndFourEyes`)
1. **`approval.document_approvals`**: `approval_id uuid PK`, `(tenant_id, legal_entity_id, approval_id)` AK, partial unique `(tenant_id, legal_entity_id, source_module, source_document_type, source_revision_id) WHERE status IN ('PENDING','APPROVED')`, `ck_document_approvals_status`, `ck_document_approvals_four_eyes` (`decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id`), composite FKs to `legal_entities`, `approval_policies`, `tenant_memberships`.
2. **`procurement.delivery_tolerance_policies`**: `policy_id uuid PK`, `(tenant_id, legal_entity_id, policy_id)` AK, unique `(tenant_id, legal_entity_id, code)`, CHECK `overdelivery_tolerance_percent >= 0 AND overdelivery_tolerance_base_qty >= 0`.
3. **`procurement.po_sequences`**: PK `(tenant_id, legal_entity_id, fiscal_year)`, `last_number bigint`.
4. **`procurement.purchase_orders`**: `purchase_order_id uuid PK`, `(tenant_id, legal_entity_id, purchase_order_id)` AK, partial unique `ux_purchase_orders_entity_po_number` (`WHERE po_number IS NOT NULL`), unique `(tenant_id, legal_entity_id, draft_code)`, checks on `status` and non-negative `downstream_reference_count` / `accepted_base_quantity_total`.
5. **`procurement.purchase_order_revisions`**: `revision_id uuid PK`, `(tenant_id, legal_entity_id, revision_id)` AK, unique `(tenant_id, legal_entity_id, purchase_order_id, revision_number)`, partial unique `ux_po_revisions_approval_record` (`WHERE approval_record_id IS NOT NULL`), checks on `status`, four-eyes (`decided_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id`), non-negative totals.
6. **`procurement.purchase_order_lines`**: `po_line_id uuid PK`, composite FKs to `purchase_order_revisions`, `items`, `units_of_measure`, optional `tax_codes`; unique `(tenant_id, legal_entity_id, revision_id, line_number)` & `(..., lineage_line_key)`; CHECK `ordered_quantity > 0`, `unit_price > 0`, `overdelivery_tolerance_percent = 0 AND overdelivery_tolerance_base_qty = 0 AND allow_unlimited_overdelivery = false`.
- **RLS & Privileges:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 6 tables; `athera_app` receives `SELECT, INSERT, UPDATE` ONLY (`DELETE` absent) on the 5 header/approval/sequence/tolerance tables, and `SELECT, INSERT, UPDATE, DELETE` on `purchase_order_lines` guarded by **SQL freeze triggers**:
  - `procurement.guard_frozen_po_revision_lines()` (`BEFORE INSERT OR UPDATE OR DELETE` -> `RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'po_revision_frozen_immutable: ...'` when `is_frozen = true`).
  - `procurement.guard_frozen_po_revision_snapshot()` (`BEFORE UPDATE` -> `ERRCODE = '55000'` when `OLD.is_frozen = true` and any snapshot/total/hash column changes).

---

## 5. Permissions, API Endpoints & Next.js UX Contracts
- **Permissions:** `procurement.read`, `procurement.manage`, `procurement.approve` (`ProcurementOfficer` vs `EntityApprover` separated).
- **Endpoints (`/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement`):**
  - `GET /reference-data` (eligible suppliers, purchase-eligible stocked/service items with active UoMs, active tax codes, tolerance policies)
  - `GET /purchase-orders`, `GET /purchase-orders/{poId:guid}`
  - `POST /purchase-orders` (create DRAFT Rev 1)
  - `PUT /purchase-orders/{poId:guid}/revisions/{revisionId:guid}` (save editable DRAFT)
  - `POST /purchase-orders/{poId:guid}/revisions` (fork `DRAFT` `Rev N + 1` from `CurrentApprovedRevision`)
  - `POST /purchase-orders/{poId:guid}/revisions/{revisionId:guid}/submit`
  - `POST /purchase-orders/{poId:guid}/revisions/{revisionId:guid}/decide` (`APPROVED` / `REJECTED` + reason)
  - `POST /purchase-orders/{poId:guid}/cancel`, `POST /purchase-orders/{poId:guid}/short-close`, `DELETE /purchase-orders/{poId:guid}` -> `409 po_delete_forbidden`.
- **Next.js UX (`/w/[tenantId]/[entityId]/procurement`):**
  - Focused commercial PO ledger & editor following `DESIGN.md` & `craft-floor.md`: tab switcher (`Daftar & Rincian PO`, `Editor PO & Revisi`, `Antrean Persetujuan Four-Eyes`), item/UoM/tax/price line grid with exact integer preview, `Perbandingan Perubahan Revisi` (`Approval Comparison Diff`), `Ringkasan Komitmen Penerimaan`, immutable `Hash Snapshot`, sticky action footer with explicit status & four-eyes guidance, and read-only mode (`Lihat Rincian & Riwayat`).

---

## 6. Mandatory Executable Acceptance Tests
- **Unit (`S05PurchaseOrdersTests.cs`):** State transitions, `DeliveryTolerancePolicy` `OD-01` block, four-eyes `DocumentApproval` self-approval guard, deterministic `SnapshotHash`, `PurchaseOrderDiffCalculator`, downstream-dependency cancel block, role templates separation (`ProcurementOfficer` vs `EntityApprover`).
- **PostgreSQL 18 Integration (`S05ProcurementPostgresTests.cs`):** `S05_PG_01` (RLS deny missing context & cross-tenant isolation across all 6 tables), `S05_PG_02` (`M04 -> M05 -> M04 -> M05` round-trip), `S05_PG_03` (`DELETE` denied `42501` + SQL freeze triggers returning `55000` on frozen revision snapshot/lines + four-eyes SQL check constraint), `S05_API_01` (draft -> submit -> `U1` self-approve `403` -> `U2` approve `200` + duplicate `PO` number `409`), `S05_API_02` (`6x` concurrent `Task.WhenAll` approval -> single `200` + single approval/outbox), `S05_API_03` (`Rev 1` -> `Rev 2` amendment, `Rev 1` snapshot unchanged, diff verified), `S05_API_04` (ineligible supplier `409`, ineligible item `409`, `OD-01` tolerance `409`, downstream dependency cancel `409` vs short-close `200`, cross-tenant `404` & `403` `SMDR-BDG`).
- **Frontend & E2E (`s05-procurement-po.test.tsx`, `s05-procurement-po.spec.ts`):** `U1`/`U2` commercial PO flow, self-approval blocked, `Rev 2` diff & approval, `Rev 1` preserved, short-close, `NUSA-SBY` read-only, `SMDR-BDG` `403` (`U2`) & `404` (`U1`), desktop (`1280x800`) + mobile (`390x844`) captures.
