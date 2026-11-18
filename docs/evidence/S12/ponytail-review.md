# ATH-S12 — Ponytail Engineering Minimalism & Invariant Review (`full`)

**Slice:** S12 — Customer Sales Orders and Commercial Commitments  
**Skill Level:** `ponytail` (`full`)  
**Verdict:** `PASS`

---

## 1. Scope & Minimalism Assessment

- **Direct Domain & Coordinator Ownership (`Orphac.Athera.Modules.Sales`)**:
  - Implemented in two focused files (`SalesOrderModels.cs` and `SalesOrderCoordinator.cs`) without speculative abstraction layers, generic rules engines, or premature S13/S14 fulfillment/billing tables.
  - Reuses existing authoritative module evaluators directly:
    - `PartnerEligibilityEvaluator.EvaluateForSales` (`Partners`)
    - `ItemEligibilityEvaluator.EvaluateForSales` + `UomConverter.ToBaseQuantity` (`Catalog`)
    - `TaxDeterminationEvaluator.Calculate` enforcing `TaxDirections.Output` (`Tax`, `OD-07`)
    - `InventoryAvailabilityService.GetItemAvailabilityMapAsync` read-only query (`Inventory`)
    - `ApprovalPolicy` / `DocumentApproval` enforcing `OD-02` four-eyes separation (`Approval`)
    - `InventoryValuationService.IncrementEntityYearSequenceAsync("sales.sales_order_sequences", ...)` for gap-free `SO-{Year:D4}-{Seq:D5}` commitment numbering on first approval.

---

## 2. Invariant Verification

| Invariant | Implementation & Test Proof |
|---|---|
| **1 & 2. Tenant/Entity Isolation & PostgreSQL RLS** | All 4 `sales.*` tables (`sales_order_sequences`, `sales_orders`, `sales_order_revisions`, `sales_order_lines`) have composite `(tenant_id, legal_entity_id)` foreign keys and `ENABLE` + `FORCE ROW LEVEL SECURITY` using `platform.current_tenant_id()`. Verified in `S12_PG_01` and `S12_API_04`. |
| **3. Server-Resolved Permissions** | `sales.read`, `sales.manage`, and `sales.approve` are checked against database-backed `EntityAccessGrant` rows before setting SQL tenant context. `rina.utami` (`u1Id`) remains without `sales.*` grants so `S02-E2E-02` access-request workflow stays green; `dewi.lestari` (`u5Id`, `SalesOfficer`) and `budi.santoso` (`u2Id`, `EntityApprover`) exercise multi-actor SoD. |
| **4. Decimal-Safe Arithmetic & UoM Conversion** | All quantities (`numeric(18,6)`), conversion factors (`numeric(18,8)`), and IDR currency amounts (`numeric(18,2)`) use `decimal` with `MidpointRounding.AwayFromZero`; frontend preview uses integer arithmetic. |
| **6 & 8. Immutable Frozen Revisions & Traceability** | Submitting or deciding a revision sets `is_frozen = true` and records a canonical SHA-256 `snapshot_hash`. PostgreSQL triggers `sales.guard_frozen_so_revision_snapshot()` and `sales.guard_frozen_so_revision_lines()` reject any SQL `UPDATE`/`DELETE` on frozen commercial columns or lines (`S12_PG_02`). |
| **OD-02 Four-Eyes Approval & Concurrent Idempotency** | Submitter/creator cannot approve or reject their own revision (`four_eyes_self_approval_forbidden`, `409 Conflict`). Concurrent approval race test (`S12_PG_03`) proves `FOR UPDATE` locking assigns exactly one `SO-YYYY-NNNNN` without duplicate outbox/audit side effects. |
| **OD-09 Safe Commercial Defaults (`ATH-CR-009`)** | Any line discount `> 0` (`od09_sales_discount_policy_unresolved`) or credit-limit override request (`od09_credit_limit_policy_unresolved`) is deterministically rejected (`409 Conflict`), and cancel/amendment reduction below already reserved/dispatched/billed base quantities is blocked (`so_has_downstream_fulfillment`, `S12_API_03`). |
| **12. No Industry-Specific Foundry/Manufacturing Entities** | Zero foundry/casting/manufacturing fields exist in `sales.*` schema or contracts. |
