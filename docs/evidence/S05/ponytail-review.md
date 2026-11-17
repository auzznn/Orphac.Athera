# ATH-S05 Ponytail (`full`) Engineering Review

**Reviewer Mode:** Ponytail (`full`)  
**Scope:** Full S05 vertical slice — `Identity` extensions (`procurement.manage`, `procurement.approve`, `ProcurementOfficer` vs `EntityApprover` SoD templates), `Approval` (`DocumentApproval`), `Procurement` (`DeliveryTolerancePolicy`, `PoSequence`, `PurchaseOrder`, `PurchaseOrderRevision`, `PurchaseOrderLine`, `PurchaseOrderHash`, `PurchaseOrderDiffCalculator`, `PurchaseOrderWorkflowService`), `M05_PurchaseOrdersRevisionsAndFourEyes` forced-RLS + SQL freeze triggers, `ProcurementContracts`, `ProcurementEndpoints` (including `ExecuteProcurementMutationAsync` inside `ExecuteInEntityAsync` + `AbortedWorkflowException` rollback), `DemoDataSeeder`, Next.js `/procurement` UI (`procurement/page.tsx`, `client.ts`, `AppShell.tsx`, `messages.ts`), and S05 Unit/PostgreSQL/Vitest/E2E suites.

## Review Checklist & Findings
1. **Minimalism & Bounded Context Discipline:** PASS
   - Only `procurement` (`delivery_tolerance_policies`, `po_sequences`, `purchase_orders`, `purchase_order_revisions`, `purchase_order_lines`) and `approval.document_approvals` were added; zero dock arrivals, physical stock movements, inventory valuations, GRNI journals, or AP invoices exist in S05.
   - Open Decision `OD-01` (overdelivery tolerances & unlimited delivery waiver governance) is escalated via [ATH-CR-003-OD01-po-delivery-tolerance-policy.md](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) with blank Product Owner sign-off lines; S05 enforces `EXACT_APPROVED_QTY_ONLY` (`0%`, `0` extra qty, `AllowUnlimitedOverdelivery = false`, `UNREVIEWED_SAFE_DEFAULT`).
2. **DRY, Transactional Rollback & Decimal/UoM/Tax Precision:** PASS
   - `PurchaseOrderWorkflowService` centralizes PO creation, draft editing, amendment branching (`Rev N + 1`), submission freeze, four-eyes approval/rejection, short-close, and cancellation inside scoped entity transactions (`SELECT ... FOR UPDATE` on `procurement.purchase_orders` and atomic `INSERT ... ON CONFLICT DO UPDATE RETURNING last_number` on `procurement.po_sequences`).
   - `ProcurementEndpoints.ExecuteProcurementMutationAsync` executes both the domain workflow and `LoadPurchaseOrderDetailDtoAsync` inside `ExecuteInEntityAsync` so all EF Core reads operate under active `app.tenant_id` RLS and throws `AbortedWorkflowException` to guarantee full DB rollback on domain validation failures (e.g., duplicate `PO` number or four-eyes violation).
   - UoM conversions (`UomConverter`), tax bases (`TaxDeterminationEvaluator`), and Next.js live editor calculations (`parseScaledBigInt`) use exact decimal / `BigInt` integer math without IEEE-754 floating-point drift.
3. **Database, RLS & Immutable Revision Snapshot Invariants:** PASS
   - `M05_PurchaseOrdersRevisionsAndFourEyes` enables `ENABLE` + `FORCE ROW LEVEL SECURITY` on all `6` tables (`athera_migrator` bypass + `athera_app` `tenant_id` checks).
   - `athera_app` holds `SELECT, INSERT, UPDATE` ONLY (`DELETE` absent) on `approval.document_approvals`, `procurement.delivery_tolerance_policies`, `procurement.po_sequences`, `procurement.purchase_orders`, and `procurement.purchase_order_revisions`.
   - PostgreSQL SQL triggers `procurement.guard_frozen_po_revision_lines()` and `procurement.guard_frozen_po_revision_snapshot()` raise SQLSTATE `55000` (`po_revision_frozen_immutable`) on any attempt by `athera_app` (or even `athera_migrator`) to alter lines or commercial/tax snapshot totals/hashes once `is_frozen = true` (`S05_PG_03`).
   - Four-eyes SoD is enforced across domain (`ApplyDecision`), PostgreSQL CHECK constraints (`ck_po_revisions_four_eyes`, `ck_document_approvals_four_eyes`), and UI (`po-self-approval-blocked`).
4. **What Was Intentionally Skipped & Known Residual Risk:**
   - Gate 1 physical dock custody (`S06`), Gate 2 valued stock/GRNI (`S07`), and three-way invoice matching (`S09`) are intentionally omitted; PO `ReceivingProgress` reads derived commitments cleanly with `AcceptedBaseQuantityTotal = 0`.
   - Residual `OD-01` policy lock: non-zero overdelivery tolerances and unlimited flags remain blocked (`409 od01_overdelivery_tolerance_blocked`) until Product Owner signs `ATH-CR-003`.

**Verdict:** **Ship** (immutable PO revision history, SoD enforcement, and PostgreSQL RLS/triggers verified end-to-end).