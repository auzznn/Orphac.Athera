# ATH-S08 Design — Stock Availability, Locations and Quality Restrictions

- **Slice:** `S08` ([S08_stock_availability_locations_and_quality_restrictions.md](../../implementation/slices/S08_stock_availability_locations_and_quality_restrictions.md))
- **Requirements:** `BRS-ITM-004`, `BRS-RCV-008`, `INV-001..010`, `ATH-DOMAIN-BOUNDARIES-001`, `OD-04` (`ATH-CR-004`), `OD-10` (`ATH-CR-005`)
- **Status:** Design complete; awaiting implementation & verification (`Product Owner Acceptance: PENDING`)

---

## 1. Authoritative Module Ownership & Schema Boundaries

| Domain Module | Authoritative Tables Owned | Boundary Invariants |
|---|---|---|
| `Catalog` | `catalog.items` (+ `lot_tracking_policy`: `NONE`, `OPTIONAL`, `REQUIRED`) | Only stocked goods may enable `OPTIONAL` or `REQUIRED` lot policies (`NON_STOCK_SERVICE` locked to `NONE`); cannot tighten `NONE/OPTIONAL -> REQUIRED` while unlotted stock balances > 0 exist. |
| `Inventory` | `inventory.warehouses`, `inventory.locations`, `inventory.stock_lots`, `inventory.stock_location_balances`, `inventory.reservation_sequences`, `inventory.stock_reservations`, `inventory.stock_positions`, `inventory.stock_movements`, `inventory.movement_lines` (+ `warehouse_id`, `location_id`, `stock_lot_id`) | Owns locations, lot instances, location/lot status balances, reservations, and append-only balanced `-Q / +Q` location/status movements. **Zero** writes to `gl.*` or `ValuationPool` carrying value on internal S08 operations. |
| `Quality` (`quality` schema) | `quality.inspection_sequences`, `quality.receipt_inspections`, `quality.inspection_dispositions` (append-only `SELECT, INSERT` only) | Owns post-acceptance inspection queues created when Gate 2 receives stock into `QUALITY_INSPECTION`, plus authorized inspector release/block dispositions delegating stock bucket moves to `InventoryAvailabilityService`. |
| `Procurement` & `Identity` | `procurement.ReceiptAcceptanceCoordinator` Gate 2 location/lot hook; `identity.PermissionCodes` (`inventory.manage`, `quality.inspect`) & `RoleTemplates.QualityInspector` | `InventoryClerk` grants `inventory.manage` **without** `quality.inspect`; unauthorized clerks cannot release quarantined stock. |

---

## 2. State Machines, Locks & Invariants

1. **Gate 2 Location/Lot Intake (`ReceiptAcceptanceCoordinator` & `InventoryValuationService`):**
   - `EnsureDefaultWarehouseAndLocationsAsync` guarantees `WH-MAIN` (`LOC-MAIN` Storage/default receiving, `LOC-QC` Quarantine, `LOC-BLK` Blocked Hold, `LOC-DSP` Dispatch) per entity via `INSERT ... ON CONFLICT DO NOTHING`.
   - `StockLot` created/resolved cleanly by `(tenant_id, legal_entity_id, item_id, lot_number)` when `LotNumber` supplied. `REQUIRED` without lot returns `409 lot_required_by_item_policy`; `NONE` with lot returns `409 lot_not_enabled_for_item`.
   - If `StockStatus == QUALITY_INSPECTION`, creates `ReceiptInspection` (`QCI-YYYY-NNNNN`, `Status = PENDING`, `PendingBaseQuantity = acceptedQty`) atomically inside `TX-RECEIPT`.
2. **Quality Inspection Disposition (`QualityInspectionService.RecordDispositionAsync`):**
   - Requires `quality.inspect`. Idempotent by `(tenant_id, legal_entity_id, receipt_inspection_id, idempotency_key)` + payload hash.
   - `PENDING -> PARTIALLY_DECIDED -> COMPLETED` conserving `Pending + Released + Blocked == InspectedTotalBaseQuantity`.
   - Requires non-`PASSED_SPECIFICATION` / concrete reason when `BlockedBaseQuantity > 0`.
   - Atomically delegates `-Q / +Q` `QUALITY_STATUS_CHANGE` movement (`MovementLine.CreateInternalLeg`, `LineValuationAmount = 0`) moving `releasedQty` (`QUALITY_INSPECTION -> UNRESTRICTED` at `targetReleaseLocationId ?? LOC-MAIN`) and `blockedQty` (`QUALITY_INSPECTION -> BLOCKED` at `targetBlockLocationId ?? LOC-BLK`), updating `StockLocationBalance` and `StockPosition` while preserving total valued quantity and `ValuationPool.CarryingValueAmount`.
3. **Location Transfer, Direct Status Transition & Reservations (`InventoryAvailabilityService`):**
   - Global deadlock-safe lock order inside `ExecutionStrategy`: `StockPosition` (`FOR UPDATE`) -> ordered `(location_id, stock_lot_id NULLS FIRST, stock_status)` `StockLocationBalance` rows (`FOR UPDATE`) -> `StockReservation` (when releasing).
   - **Location Transfer (`LOCATION_TRANSFER`, `inventory.manage`):** Balanced `-Q / +Q` across active locations inside same warehouse/entity, preserving `StockStatus` and `StockLotId`; blocks moving `QUALITY_INSPECTION` quantities governed by unfinished `ReceiptInspection` (`409 pending_receipt_inspection_location_locked`) and blocks moving `UNRESTRICTED` stock above unreserved `AvailableBaseQuantity` (`409 od10_insufficient_available_stock`).
   - **Direct Status Transition (`QUALITY_STATUS_CHANGE`, `quality.inspect`):** Supports `UNRESTRICTED -> BLOCKED`, `UNRESTRICTED -> QUALITY_INSPECTION`, and `BLOCKED -> UNRESTRICTED`, refusing manual bypass of open `ReceiptInspection` queues and refusing quarantine/block of reserved quantities above unreserved `AvailableBaseQuantity`.
   - **Stock Reservation & Release (`inventory.manage`, `OD-10`):** Only `UNRESTRICTED` `StockLocationBalance` may be reserved (`ReservedBaseQuantity <= OnHandBaseQuantity`). Overdraw returns `409 od10_insufficient_available_stock`. Release conserves `Reserved == Consumed + Released + RemainingReserved`.
   - **Reconciliation (`VerifyItemReconciliationAsync`):** Asserts `StockPosition` buckets == `SUM(StockLocationBalances)` == `SUM(MovementLines.BaseQuantityDelta)` and `StockPosition.Reserved == SUM(active StockReservations.RemainingReserved)`.

---

## 3. Database & Security Design (`M08_StockAvailabilityLocationsAndQualityRestrictions`)

- **New Schema:** `quality` (`USAGE` granted to `athera_app`, `athera_migrator`).
- **Composite Tenant & Legal-Entity Isolation:** All 9 new tables carry composite `(tenant_id, legal_entity_id, ...)` foreign keys, enforce `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `tenant_id = platform.current_tenant_id()`, and rely on `TenantScopeExecutor` for legal-entity permission enforcement (`M01`–`M07` pattern).
- **Least Privilege (`athera_app` `NOSUPERUSER NOBYPASSRLS`):**
  - Append-only (`SELECT, INSERT` ONLY): `quality.inspection_dispositions` (+ existing M07 append-only tables).
  - Mutable (`SELECT, INSERT, UPDATE`, zero `DELETE`): `warehouses`, `locations`, `stock_lots`, `stock_location_balances`, `reservation_sequences`, `stock_reservations`, `inspection_sequences`, `receipt_inspections`.
- **Constraint Upgrades & Migration Backfill:** Updated `ck_stock_movements_type`, `ck_movement_lines_positive`, `ck_items_lot_tracking_policy`, and idempotent backfill of `WH-MAIN` / default locations + `stock_location_balances` for existing `stock_positions`. `Down()` cleanly drops `quality` schema and S08 objects.

---

## 4. API & UI Contracts

- **API Routes (`InventoryEndpoints`, `MasterDataEndpoints`, `WorkspaceEndpoints`):**
  - `GET /api/.../inventory/warehouses` (`inventory.read | inventory.manage | quality.inspect | receiving.read | receiving.accept`)
  - `POST /api/.../inventory/warehouses` & `.../locations` (`inventory.manage`)
  - `GET /api/.../inventory/items/{itemId}/detail` (`inventory.read | inventory.manage | quality.inspect`)
  - `POST /api/.../inventory/transfers` (`inventory.manage`)
  - `POST /api/.../inventory/inspections/{inspectionId}/dispositions` (`quality.inspect`)
  - `POST /api/.../inventory/status-transitions` (`quality.inspect`)
  - `POST /api/.../inventory/reservations` & `.../reservations/{reservationId}/release` (`inventory.manage`)
- **UI Experience (`/inventory`, `/receiving`, `/catalog` — Impeccable Operate Mode):**
  - `/catalog`: Displays and configures `LotTrackingPolicy` (`NONE`, `OPTIONAL`, `REQUIRED`) per stocked item.
  - `/receiving`: Gate 2 panel exposes destination `WarehouseLocation` selector + optional/required `LotNumber` & `ExpiryDate`.
  - `/inventory`: Single Stock Detail Context (`#inv-stock-detail-sheet`) placed cleanly above `#inv-item-ledger-sheet` with 6-metric breakdown (`Total Valued On-Hand`, `Unrestricted`, `Quarantined (QI)`, `Blocked`, `Reserved`, `Available`), `Reconciled` & `Lot Policy` status badges, inline tabs for **Locations, Lots & Operations** (Disposition / Transfer / Quality Hold-Unblock / Reserve-Release) and **State & Movement Timeline**, plus top-level **Quality Inspections** tab (`#inv-tab-inspections`). `U1` (`inventory.manage` without `quality.inspect`) sees `Eye` read-only banners on quality disposition/hold controls; `U2` (`quality.inspect`) sees active disposition controls.

---

## 5. Verification Plan & Owner Decisions

- **UnitTests (`S08StockAvailabilityQualityTests`):** Stock bucket transitions, `OD-10` reservation/release & overdraw errors, lot policy validation, and `InventoryClerk` vs `QualityInspector` permission segregation.
- **PostgreSQL 18 IntegrationTests (`S08StockAvailabilityPostgresTests`):** `S08_PG_01` (forced RLS, missing context, append-only `inspection_dispositions`, CHECK constraints), `S08_PG_02` (`M07 <- M08 -> M08`), `S08_API_01` (partial QI release + block conserving stock with zero valuation/GL mutation), `S08_API_02` (concurrent reservation race preventing overdraw + release + reconciliation verification), `S08_API_03` (`U1` `403` on inspection & hostile cross-tenant `404` selectors).
- **Frontend (`Vitest` + `Playwright E2E` + `Impeccable Finish Reviewer`):** Full regression across `s00`–`s08`, bilingual `id-ID`/`en-US`, `1280x800` & `390x844` zero-clipping captures, `impeccable detect --json apps/web/src` = `[]`, and `disposition: ship`.
- **Owner-Only Decisions:** `OD-10` ([ATH-CR-005](../decisions/ATH-CR-005-OD10-negative-stock-and-reservation-policy.md)) remains `OPEN`; S08 deliverable packet (`docs/evidence/S08/README.md`) stays `Product Owner Acceptance: PENDING`.
