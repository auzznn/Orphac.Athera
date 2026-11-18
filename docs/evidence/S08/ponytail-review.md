# Ponytail (`full`) Code & Architecture Review — Slice S08

- **Scope:** `EntityAccessGrant.cs`, `RoleTemplates.cs`, `CatalogModels.cs`, `InventoryModels.cs`, `InventoryAvailabilityService.cs`, `InventoryValuationService.cs`, `QualityModels.cs`, `QualityInspectionService.cs`, `ReceivingModels.cs`, `ReceiptAcceptanceCoordinator.cs`, `AtheraDbContext.cs`, `M08_StockAvailabilityLocationsAndQualityRestrictions`, `MasterDataContracts.cs`, `MasterDataEndpoints.cs`, `WorkspaceEndpoints.cs`, `ReceivingContracts.cs`, `ReceivingEndpoints.cs`, `InventoryContracts.cs`, `InventoryEndpoints.cs`, `DemoDataSeeder.cs`, `DESIGN.md`, `receivingMath.ts`, `AppShell.tsx`, `client.ts`, `messages.ts`, `catalog/page.tsx`, `receiving/page.tsx`, `inventory/page.tsx`, `InventoryStockDetailSheet.tsx`, and S08 unit/PostgreSQL/Vitest/Playwright tests.
- **Verdict:** **PASS (`Ship`)**

## Assessment

1. **Minimal Scope & Authoritative Module Boundaries:**
   - Kept lot/location/quality/reservation persistence cleanly separated across module schemas (`inventory.warehouses`, `inventory.storage_locations`, `inventory.stock_lots`, `inventory.stock_location_balances`, `inventory.stock_reservations` vs `quality.receipt_inspections`, `quality.receipt_inspection_dispositions`), preserving append-only `inventory.stock_movements` / `inventory.movement_lines` without touching general ledger balances on intra-warehouse transfers or quality dispositions.
   - Deferred AP invoice 3-way matching, price variance apportionment, and payment batch settlement strictly to `S09`/`S10`, and kept foundry/manufacturing entities out of ERP core.
   - Reused shared `parseScaledBigInt` scaling primitives cleanly by extracting `formatSignedQuantityValue` in `receivingMath.ts` rather than duplicating formatting pipelines across components.

2. **Availability, Valuation & Concurrency Invariants (`OD-10` / `OD-04`):**
   - **Pre-Mutation Guard & FK Ordering:** `StockLot.AssignSourceAcceptanceIfMissing` aligns EF Core Graph save ordering on Gate 2 valued acceptance into `stock_lots`; `StockPosition.ApplyStatusTransition` validates unrestricted availability before bucket mutation so concurrent/hostile transitions cannot transiently go negative or corrupt position vs location reconciliation.
   - **Strict Negative-Stock & Over-Reservation Block (`OD-10`):** Deterministic `FOR UPDATE` row locking on `StockPosition` and `StockLocationBalance` ensures concurrent reservations (`S08_API_02`) cannot over-allocate `Available = Unrestricted - ActiveReserved`, returning `409` on exhaustion without altering `valuation_pools`.
   - **Location & Zone Compatibility:** `LocationZone` / stock status rules block unrestricted stock from staging into `BLOCKED`/`QUARANTINE` zones and lock quarantine stock to the `QualityInspectionService` disposition workflow (`pending_receipt_inspection_location_locked`).

3. **PostgreSQL RLS, SoD & UI Resilience:**
   - All S08 `inventory.*` and `quality.*` tables enforce `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `app.tenant_id` / `app.legal_entity_id` under non-owner/no-bypass `athera_app`, with `S08_PG_02` verifying `M08 -> M07 -> M08` schema rollback cleanly.
   - Role separation enforces `inventory.manage` vs `quality.inspect` at both API authorization and UI boundaries (`U1` can transfer/reserve unrestricted stock but cannot dispose quarantine holds).
   - Fixed signed-delta display (`formatSignedQuantityValue`), locale-reactive banner state, disabled empty `<select>` states, flush nested-border removal on Receiving/Gate 2, `36px` touch targets, and uniform `1px` metric hairlines without adding unnecessary abstractions.
