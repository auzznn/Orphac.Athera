# Ponytail (`full`) Code & Architecture Review — Slice S06

- **Scope:** `ReceivingModels.cs`, `GoodsReceiptWorkflowService.cs`, `AtheraDbContext.cs`, `M06_DockCustodyAndReceivingGateOne`, `ReceivingContracts.cs`, `ReceivingEndpoints.cs`, `WorkspaceEndpoints.cs`, `DemoDataSeeder.cs`, `receivingMath.ts`, `receiving/page.tsx`, `AppShell.tsx`, `client.ts`, `messages.ts`, and S06 unit/PostgreSQL/Vitest/Playwright tests.
- **Verdict:** **PASS (`Ship`)**

## Assessment

1. **Minimal Scope & Correct Boundary Placement:**
   - Gate 1 unvalued custody lives inside the `Procurement` module (`procurement.receipt_sequences`, `goods_receipts`, `goods_receipt_lines`, append-only `dock_checks`), keeping physical arrival, dock hold, and unvalued rejection strictly separate from valued Gate 2 acceptance and GL/GRNI postings (zero `Inventory`, `GlPostingKernel`, or `GRNI` coupling).
   - No foundry/manufacturing-specific entities, speculative bulk batch orchestrators, or warehouse bin/location subsystems were added.

2. **Invariants Without Duplication:**
   - `GoodsReceiptLine.ApplyCustodySplit` is the single domain chokepoint enforcing decimal conservation (`Pending + Held + Released + Rejected == PhysicallyDeliveredBaseQuantity`), Gate 1 zero valuation (`ValuedAcceptedBaseQuantity = 0`, `HasFinancialPosting = false`), and `OD-01` cumulative approved-PO release cap (`409 od01_overdelivery_release_blocked`).
   - `receivingMath.ts` shares one `10^6` scaled-BigInt implementation (`parseScaledBigInt`, `expandScientificDecimal`, `convertArrivalQtyToBase`, `evaluateDockCheckPreview`) across both the `/receiving` UI and Vitest unit tests (`S06-UI`), matching backend decimal semantics without floating-point arithmetic.
   - `FOR UPDATE` row locking on the parent `PurchaseOrder` serializes concurrent multi-truck arrivals, cumulative `OD-01` capacity calculations, and dock-check releases cleanly in PostgreSQL.

3. **Security & Least Privilege:**
   - All 4 new tables enable and force PostgreSQL RLS (`ENABLE` + `FORCE ROW LEVEL SECURITY`) with composite `(tenant_id, legal_entity_id, ...)` foreign keys, runtime role grants limited cleanly (`SELECT, INSERT` only on append-only `dock_checks`), server-resolved `[receiving.read]` / `[receiving.manage]` checks, and CSRF-enforced mutating routes.
   - Concurrent retries with identical `(tenant_id, legal_entity_id, idempotency_key)` and payload hash replay idempotently without duplicating receipts or dock checks; divergent payload hashes conflict (`409`).
