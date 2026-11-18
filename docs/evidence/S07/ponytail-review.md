# Ponytail (`full`) Code & Architecture Review — Slice S07

- **Scope:** `InventoryModels.cs`, `InventoryValuationService.cs`, `GeneralLedgerModels.cs`, `GlPostingKernel.cs`, `ProcurementModels.cs`, `ReceivingModels.cs`, `ReceiptAcceptanceCoordinator.cs`, `EntityAccessGrant.cs`, `RoleTemplates.cs`, `AtheraDbContext.cs`, `M07_ValuedAcceptanceMovingAverageInventoryAndGrni`, `InventoryContracts.cs`, `InventoryEndpoints.cs`, `ReceivingContracts.cs`, `ReceivingEndpoints.cs`, `WorkspaceEndpoints.cs`, `Program.cs`, `DemoDataSeeder.cs`, `receiving/page.tsx`, `inventory/page.tsx`, `AppShell.tsx`, `client.ts`, `messages.ts`, and S07 unit/PostgreSQL/Vitest/Playwright tests.
- **Verdict:** **PASS (`Ship`)**

## Assessment

1. **Minimal Scope & Authoritative Module Boundaries:**
   - Gate 2 orchestration (`ReceiptAcceptanceCoordinator`) coordinates across `Procurement` (`valued_acceptances`), `Inventory` (`InventoryValuationService`: `stock_positions`, `stock_movements`, `valuation_pools`, `valuation_entries`), and `GeneralLedger` (`GlPostingKernel.PostReceiptAccrualAsync`: `receipt_accruals`, `journal_entries`) inside a single EF `ExecutionStrategy` transaction with savepoint (`sp_accept_receipt`) rollback testing.
   - Kept warehouse bin/location allocations (`S08`), invoice 3-way matching & price variance (`S09`), and foundry/manufacturing entities strictly out of S07 scope.
   - Separated read-only Gate 2 preview (`PreviewReceiptAcceptanceAsync`) from atomic posting while sharing the same OD-04 / PO / period / released-custody validation rules, preventing duplicated business logic.

2. **Financial, Valuation & Concurrency Invariants:**
   - **Decimal-Safe Moving Average (`OD-04` safe default `ATH-CR-004`):** Both C# domain (`decimal(18,4)` / `decimal(18,6)` `MidpointRounding.AwayFromZero`) and TypeScript UI helpers avoid binary floating-point math. `CarryingValueAmount` (`numeric(18,4)`) remains authoritative (`prevCarrying + acceptedLineAmount`), preserving `SUM(Dr Inventory Asset) == CarryingValueAmount`.
   - **Anomalous Pool & Zero-Cost Blocking:** `ValuationPool` blocks non-positive acceptance values (`409 od04_zero_or_negative_acceptance_value_blocked`), negative/inactive pools, and zero-qty non-zero carrying pools (`409 od04_anomalous_valuation_pool_blocked`) pre-commit inside locked database rows.
   - **Quality Hold vs Availability Separation:** `QUALITY_INSPECTION` and `BLOCKED` receipts add carrying value and valued stock while leaving `available_base_quantity = 0` unchanged (verified by `S07_API_03`).
   - **Concurrency & Idempotency:** Deterministic `FOR UPDATE` row locking (`PurchaseOrder` -> `GoodsReceipt` -> ordered `ValuationPool`/`StockPosition` -> `FinancialPeriod`), `INSERT ... ON CONFLICT DO NOTHING` pool/position bootstrap, receipt-scoped `<=80`-char GL idempotency key, and savepoint rollback prevent duplicate stock/GRNI/GL postings on parallel requests (`S07_API_02`) or fault injection (`S07_API_01`).

3. **PostgreSQL Security, RLS & Least Privilege:**
   - All 11 S07 tables enforce `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `current_setting('app.tenant_id', true)` and `app.legal_entity_id`.
   - Runtime role `athera_app` (`NOSUPERUSER NOBYPASSRLS`) receives append-only `SELECT, INSERT` on immutable audit/ledger tables (`valued_acceptances`, `valued_acceptance_lines`, `stock_movements`, `movement_lines`, `valuation_entries`) and controlled `SELECT, INSERT, UPDATE` on mutable balances/accruals.
   - Deferred trigger `procurement.enforce_valued_acceptance_backing()` guards Gate 1 vs Gate 2 boundary integrity at commit time, and `Down()` cleanly reverses M07 schema/triggers (`S07_PG_02`).
   - `receiving.accept` permission sits independently from `receiving.manage`, and `S07_API_03` verifies hostile tenant/entity selector denial (`403`/`404`) and zero cross-entity leakage.
