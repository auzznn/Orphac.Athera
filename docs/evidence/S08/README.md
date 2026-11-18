# ATH-S08 Evidence Packet — Stock Availability, Warehouses, Lots and Quality Restrictions

- **Slice:** S08 — Stock Availability, Locations and Quality Restrictions (`docs/implementation/slices/S08_stock_availability_locations_and_quality_restrictions.md`)
- **Design Spec:** [S08-design.md](../../design/slices/S08-design.md)
- **Decision Request & Safe Default:** [ATH-CR-005-OD10-negative-stock-and-reservation-policy.md](../../decisions/ATH-CR-005-OD10-negative-stock-and-reservation-policy.md) (`OD-10` `STRICT_NO_NEGATIVE_AND_EXPLICIT_RESERVATION_SAFE_DEFAULT` active until Product Owner sign-off)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review in `WORKSTREAM-STATE.md` and `ATH-CR-005`)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S07 SHA:** `e64f46b`
- **S08 Implementation SHA:** Recorded in [WORKSTREAM-STATE.md](../../implementation/WORKSTREAM-STATE.md)

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Lot Policy & Lot-Tracked Gate 2 Quarantine Acceptance (`S08-E2E-01`):** Configures `REQUIRED` lot tracking on `/catalog`, clears Gate 1 arrival + four-eyes PO release on `NUSA-JKT`, and confirms Gate 2 acceptance into `QUALITY_INSPECTION` (`LOC-QC`, lot number & expiry) creating pending `QCI-2026-00002` with `20 PCS` valued carrying cost (`Rp 7.000.000`) and **`Available = 0 PCS`**.
2. **U1/U2 SoD, Partial & Final Disposition, Intra-Warehouse Putaway & OD-10 Reservations (`S08-E2E-01`):** Verifies `U1` (`inventory.manage` without `quality.inspect`) sees a read-only quality lock (`02`), while `U2` executes split (`12 PCS -> LOC-MAIN` + `3 PCS -> LOC-BLK`) and final (`5 PCS -> LOC-MAIN`) quality dispositions, transfers `4 PCS` `LOC-MAIN -> LOC-RACK-B2` without new GL journals, enforces strict `409` OD-10 rejection on over-reservation, commits & releases (`2` partial + `4` full) reservation `RSV-2026-00001`, inspects signed `-Q`/`+Q` timeline deltas in `en-US` (`04`), and verifies `NUSA-SBY` empty isolation + `SMDR` cross-tenant denial (`S08-E2E-02`).

### B. Module Ownership & Schema Artifacts
- **Identity (`Identity`):** Added `InventoryManage` (`inventory.manage`) and `QualityInspect` (`quality.inspect`) permissions (`EntityAccessGrant.cs`); split `InventoryClerk` vs `QualityInspector` templates (`RoleTemplates.cs`).
- **Catalog (`Catalog`):** Added `LotTrackingPolicies` (`NONE`, `OPTIONAL`, `REQUIRED`) with non-stock enforcement (`CatalogModels.cs`).
- **Inventory (`Inventory`):** Added `Warehouse`, `StorageLocation` (`RECEIVING`, `STORAGE`, `QUARANTINE`, `BLOCKED`), `StockLot`, `StockLocationBalance`, `StockReservation` (`InventoryModels.cs`), `InventoryAvailabilityService.cs`, and extended `StockPosition` / `InventoryValuationService.cs` with location/lot movements and status transitions.
- **Quality (`Quality`):** Added `ReceiptInspection`, `ReceiptInspectionDisposition` (`QualityModels.cs`), and `QualityInspectionService.cs`.
- **Procurement (`Procurement`):** Extended Gate 2 (`ReceivingModels.cs`, `ReceiptAcceptanceCoordinator.cs`) with destination location routing, lot policy validation, and quarantine inspection creation.
- **Persistence (`M08`):** Added `20261118020321_M08_StockAvailabilityLocationsAndQualityRestrictions` creating S08 warehouse/location/lot/reservation/quality tables, `ENABLE` + `FORCE ROW LEVEL SECURITY`, `athera_app` least-privilege grants, and `athera_migrator` policies.
- **API, Seed & OpenAPI:** Updated `MasterDataContracts.cs`/`MasterDataEndpoints.cs`, `WorkspaceEndpoints.cs`, `ReceivingContracts.cs`/`ReceivingEndpoints.cs`, `InventoryContracts.cs`/`InventoryEndpoints.cs`, `DemoDataSeeder.cs`, `docs/contracts/openapi.v1.json`, and `apps/web/src/lib/api/schema.d.ts`.
- **Frontend:** Updated `DESIGN.md`, `AppShell.tsx`, `client.ts`, `messages.ts`, `receivingMath.ts`, `catalog/page.tsx`, `receiving/page.tsx`, `inventory/page.tsx`, and created `InventoryStockDetailSheet.tsx`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`45/45` UnitTests including `S08StockAvailabilityQualityTests` + `57/57` Real PostgreSQL 18 IntegrationTests: `S08_PG_01` M08 schema/RLS/constraints, `S08_PG_02` `M08 -> M07 -> M08` round-trip, `S08_API_01` disposition/transfer/hold/reserve/release flows, `S08_API_02` concurrent `Task.WhenAll` over-reservation race, `S08_API_03` `quality.inspect` vs `inventory.manage` SoD & hostile cross-tenant/entity denial) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` warnings/errors; zero `.NET` code changes since run) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`9/9` files, `22/22` tests), `next build`, Playwright E2E (`17/17` passed in `35.8s` across `s00`–`s08` with signed-delta, bilingual & zero-clipping assertions), `npm audit --omit=dev --audit-level=high` (`0`), `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg --pcre2` across `src/`, `apps/`, `tests/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`d8284df8`: Round 1 `fix` -> 6 material fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots (`01`–`05`) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S08 schema (`M08 -> M07`):** `dotnet ef database update 20261118005753_M07_ValuedAcceptanceMovingAverageInventoryAndGrni --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (cleanly drops `quality` schema, S08 `inventory` location/lot/reservation tables, and Gate 2 lot/inspection columns; verified via `S08_PG_02`).
- **Scope boundaries (`S09` / `OD-10`):** Supplier AP invoice three-way match, GRNI clearing & purchase-price variance apportionment (`S09`), and Owner sign-off on `OD-10` negative-stock / reservation policy (`ATH-CR-005`) remain cleanly separated.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
