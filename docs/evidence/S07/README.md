# ATH-S07 Evidence Packet — Valued Acceptance, Moving-Average Inventory and GRNI

- **Slice:** S07 — Valued Acceptance, Moving-Average Inventory and GRNI (`docs/implementation/slices/S07_valued_acceptance_moving_average_inventory_and_grni.md`)
- **Design Spec:** [S07-design.md](../../design/slices/S07-design.md)
- **Decision Request & Safe Default:** [ATH-CR-004-OD04-moving-average-valuation-edge-cases.md](../../decisions/ATH-CR-004-OD04-moving-average-valuation-edge-cases.md) (`OD-04` `STRICT_POSITIVE_POOL_SAFE_DEFAULT` active until Product Owner sign-off)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review in `WORKSTREAM-STATE.md` and `ATH-CR-004`)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S06 SHA:** `876a916`
- **S07 Implementation SHA:** Recorded in [WORKSTREAM-STATE.md](../../implementation/WORKSTREAM-STATE.md)

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Closed-Period / OD-04 Block & Gate 2 Preview-Before-Confirm (`S07-E2E-01`):** After Gate 1 release on `NUSA-JKT` (`PO1`: `10 PCS @ Rp 850.000`, `PO2`: `10 PCS @ Rp 950.000`), posting preview on closed fiscal period `2026-10-15` blocks commit and keeps Confirm disabled. Switching to open period `2026-11-10` calculates exact two-batch moving-average recalculation (`Rp 850.000 -> Rp 900.000 / PCS`, `20 PCS`, `Rp 18.000.000` carrying value) and balanced `Dr 1300 Inventory Asset / Cr 2110 GRNI Clearing` (`Rp 9.500.000`) before unlocking confirmation, then atomically persists `ACC-*`, `MOV-*`, `GRNI-*`, and `JRN-*` while preserving Gate 1 GL count `0`.
2. **Inventory Balances, Sequential Valuation Ledger, Open GRNI & Isolation (`S07-E2E-01`):** Navigates to `/w/{tenantId}/{entityId}/inventory`, verifies perpetual position totals, sequential `#1 -> #2` moving-average ledger entries linked to movement/GL references, `OPEN` GRNI accruals ready for `S09`, bilingual `id-ID` / `en-US` rendering, `390x844` mobile stacking (`04` & `05`) with zero horizontal overflow, empty read-only `NUSA-SBY` view, and `404` default-deny on hostile `SMDR-BDG` tenant navigation.

### B. Module Ownership & Schema Artifacts
- **Identity (`Identity`):** Added `ReceivingAccept` (`receiving.accept`) permission (`EntityAccessGrant.cs`) and `InventoryClerk` template (`RoleTemplates.cs`).
- **Inventory (`Inventory`):** Added `InventoryModels.cs` (`MovementSequence`, `StockPosition`, `StockMovement`, `MovementLine`, `ValuationPool`, `ValuationEntry`) and `InventoryValuationService.cs`.
- **General Ledger (`GeneralLedger`):** Added `ReceiptAccrual`, `ReceiptAccrualLine` (`GeneralLedgerModels.cs`) and `PreviewPeriodAndAccrualMappingsAsync` / `PostReceiptAccrualAsync` (`GlPostingKernel.cs`).
- **Procurement (`Procurement`):** Added `AcceptanceSequence`, `ValuedAcceptance`, `ValuedAcceptanceLine` (`ReceivingModels.cs`), `PurchaseOrder` accepted/progress tracking (`ProcurementModels.cs`), and `ReceiptAcceptanceCoordinator.cs`.
- **Persistence (`M07`):** Added `20261118005753_M07_ValuedAcceptanceMovingAverageInventoryAndGrni` creating all 11 S07 tables, Gate 2 CHECK constraints, `ENABLE` + `FORCE ROW LEVEL SECURITY`, append-only `SELECT, INSERT` grants for `athera_app`, `athera_migrator` policies, and deferred trigger `procurement.enforce_valued_acceptance_backing()`.
- **API & Seed:** Added `InventoryContracts.cs`, `InventoryEndpoints.cs`, updated `ReceivingContracts.cs`, `ReceivingEndpoints.cs`, `WorkspaceEndpoints.cs` (`inventory = "AVAILABLE"`), `DemoDataSeeder.cs` (`ITM-STL-PLATE-10` Batch 1 accepted / Batch 2 released), `docs/contracts/openapi.v1.json`, and `apps/web/src/lib/api/schema.d.ts`.
- **Frontend:** Enabled `/inventory` route & `InventoryPage`, integrated Gate 2 preview/confirm panel & linked acceptances into `/receiving`, updated `AppShell.tsx`, `client.ts`, `messages.ts`, and `DESIGN.md`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`40/40` UnitTests including `S07ValuedAcceptanceValuationTests` + `52/52` Real PostgreSQL 18 IntegrationTests: `S07_PG_01` schema/RLS/append-only/deferred trigger, `S07_PG_02` `M07 -> M06 -> M07` round-trip, `S07_API_01` closed-period/OD-04 preview, multi-batch MAC & GL/GRNI fault savepoint rollback, `S07_API_02` concurrent `Task.WhenAll` acceptances & idempotency replay, `S07_API_03` `receiving.accept` separation, QI/Blocked zero-availability & hostile cross-tenant/entity denial) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` warnings/errors) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`8/8` files, `18/18` tests), `next build`, Playwright E2E (`15/15` passed in `29.6s` across `s00`–`s07` with zero horizontal clipping), `npm audit --omit=dev --audit-level=high` (`0`), `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg --pcre2` across `src/`, `apps/`, `tests/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`0e693034`: Round 1 `fix` -> 7 material fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots (`01`–`05`) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S07 schema (`M07 -> M06`):** `dotnet ef database update 20261117214046_M06_DockCustodyAndReceivingGateOne --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (cleanly drops `inventory` schema, `gl.receipt_accruals*`, S07 `procurement` tables/columns, and `enforce_valued_acceptance_backing()`; verified via `S07_PG_02`).
- **Scope boundaries (`S08` / `S09` / `OD-04`):** Per-warehouse/bin location transfers (`S08`), supplier invoice three-way match / GRNI clearing / price-difference apportionment (`S09`), and Owner sign-off on `OD-04` valuation edge-case policies (`ATH-CR-004`) remain cleanly separated and deferred.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
