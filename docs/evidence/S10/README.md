# ATH-S10 Evidence Packet — Supplier Returns, Replacements and Financial Corrections

- **Slice:** S10 — Supplier Returns, Replacements and Financial Corrections (`docs/implementation/slices/S10_supplier_returns_replacements_and_financial_corrections.md`)
- **Design Spec:** [S10-design.md](../../design/slices/S10-design.md)
- **Decision Request & Safe Default:** [ATH-CR-007-OD05-OD07-supplier-returns-replacements-and-credits.md](../../decisions/ATH-CR-007-OD05-OD07-supplier-returns-replacements-and-credits.md) (`OD-05` & `OD-07` M1–M7 matrix safe defaults; paid-invoice returns M5 and PPh-bearing return reversals M6 remain blocked fail-closed until Product Owner sign-off)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review in `WORKSTREAM-STATE.md` and `ATH-CR-007`)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S09 SHA:** `b42150e` (`WORKSTREAM-STATE.md` HEAD `a8cb64e`)
- **S10 Implementation SHA:** Recorded in [WORKSTREAM-STATE.md](../../implementation/WORKSTREAM-STATE.md)

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **M1 Unvalued Dock Rejection, Linked `RPA` Replacement & Gate 2 Valuation (`S10-E2E-01`):** `U1` (`rina.utami@demo.athera.local`) expands the bilingual `M1`–`M7` policy matrix (`01`), prepares & submits `M1` (`REPLACEMENT_ONLY`, `2 PCS`) on rejected `ITM-S10-SEAL-01` (`GRN-2026-00005`), sees self-approval lock (`ret-sod-notice`), `U2` (`budi.santoso@demo.athera.local`) approves four-eyes (`OD-02`), and `U1` executes (`SRT-2026-00002`, zero GL entries, issues `RPA-2026-00001`). Recording replacement arrival (`07`, `AP Delta: Rp 0`, `Authorized Cost: Rp 75.000`) renders `Pengganti · RPA-2026-00001` with `RotateCcw` icon on `/receiving`, where Gate 1 release + Gate 2 acceptance post **`Rp 150.000`** (`02`).
2. **M2 Valued Unbilled Return & Compensating Outbound Stock (`S10-E2E-02`):** `U1` prepares & submits `M2` (`CLOSE_WITHOUT_REPLACEMENT`, `5 PCS`) on unbilled `ITM-S10-GASKET-02` (`GRNI-2026-00005 @ Rp 120.000`), `U2` approves (`OD-02`), and `U1` executes atomically (`03`: `SRT-2026-00004`, `Dr 2110 GRNI Rp 600.000 / Cr 1300 Persediaan Rp 600.000`, `MOV-2026-00008`, `JRN-2026-11-00010`), reflected on `/inventory` as signed `-5` `PURCHASE_RETURN_OUTBOUND`.
3. **M4 Policy Block, OD-07 `Nota Retur` Gate & M3 Append-Only Credit Note (`S10-E2E-03`):** `U4` (`siti.rahayu@demo.athera.local`) verifies attempting `REPLACEMENT_ONLY` on billed `INV-JKT-S10-M3-01` fails preview under `OD-05 (M4)` (`04`: `Rp 0` summary tiles & suppressed Credit Note GL rows), saves an `AP_CREDIT_NOTE` draft without `Nota Retur` (`08`: fail-closed `409 od07_nota_retur_required` on submit with live GL preview), adds `NR-JKT-S10-...` and submits, and `U2` approves & executes (`05`: `SCN-2026-00001`, `Dr 2100 Rp 166.500 / Cr 1300 Rp 150.000 / Cr 1410 Rp 16.500`, `/payables` shows `Dikreditkan: -Rp 166.500`).
4. **Read-Only Scope & `390x844` Mobile Responsiveness (`S10-E2E-04`):** `U2` sees read-only notice on `NUSA-SBY` with zero mutation controls, and `390x844` mobile layout (`06`) reflows `M1`–`M7` policy cards and Live GL Preview rows with zero horizontal overflow.

### B. Module Ownership & Schema Artifacts
- **Procurement (`Procurement`):** Added `SupplierReturn`, `SupplierReturnLine`, `ReplacementAuthorization`, `ReturnSequence`, `ReplacementSequence`, `SupplierReturnMatrixEvaluator` (`SupplierReturnModels.cs`), `SupplierReturnCoordinator.cs` (preview, draft create/update, submit, four-eyes decide `OD-02`, atomic savepoint `sp_execute_supplier_return` for M1–M3 & fail-closed M4–M7 + `FaultAfterSubledgerBeforeCommit`, and `ReceiveReplacementAsync`), plus PO accepted-qty reversal (`ProcurementModels.cs`), Gate 1 capacity accounting & Gate 2 replacement valuation (`ReceivingModels.cs`, `GoodsReceiptWorkflowService.cs`, `ReceiptAcceptanceCoordinator.cs`).
- **Inventory, GL, Tax & AP Extensions:** Added `PURCHASE_RETURN_OUTBOUND` (`Cr 1300` MAC guarded by `OD-04` floor in `InventoryModels.cs`, `InventoryValuationService.cs`), `ReceiptAccrual.ApplyUnbilledReturn` (`GeneralLedgerModels.cs`), `TaxRole.PpnInputReversal` (`TaxModels.cs`), `ap.SupplierCredit` + `ap.SupplierCreditLine` + `CreditSequence`, and `PayableItem.ApplySupplierCredit` (`AccountsPayableModels.cs`).
- **Persistence (`M10`):** Added `20261118135307_M10_SupplierReturnsReplacementsAndFinancialCorrections` creating `procurement.supplier_returns`, `procurement.supplier_return_lines`, `procurement.replacement_authorizations`, `procurement.return_sequences`, `procurement.replacement_sequences`, `ap.supplier_credits`, `ap.supplier_credit_lines`, `ap.credit_sequences`, non-circular `GoodsReceipt.ReplacementAuthorizationId`, updated check constraints, `ENABLE` + `FORCE ROW LEVEL SECURITY` (`platform.current_tenant_id()`), least-privilege `athera_app` grants (append-only `SELECT, INSERT` on `ap.supplier_credits*`), `athera_migrator` policies, and immutability triggers (`trg_procurement_supplier_returns_immutability`, `trg_procurement_supplier_return_lines_immutability`, `trg_ap_supplier_credits_immutability`, `trg_ap_supplier_credit_lines_immutability`).
- **API, Seed & OpenAPI:** Added `ReturnsContracts.cs`, `ReturnsEndpoints.cs`, updated `ReceivingContracts.cs`/`ReceivingEndpoints.cs`, `PayablesContracts.cs`/`PayablesEndpoints.cs`, `WorkspaceEndpoints.cs` (`"returns"`), `Program.cs`, `DemoDataSeeder.cs` + `DemoDataSeeder.S10.cs` (`SUPPLIER_RETURN_AUTHORIZATION`, `ITM-S10-SEAL-01`, `ITM-S10-GASKET-02`), `docs/contracts/openapi.v1.json`, and `apps/web/src/lib/api/schema.d.ts`.
- **Frontend:** Updated `DESIGN.md`, `globals.css`, `AppShell.tsx`, `client.ts`, `messages.ts`, `inventory/page.tsx`, `payables/page.tsx`, `receiving/page.tsx`, and added `SupplierReturnDetailSheet.tsx` + `/returns` workbench (`returns/page.tsx`).

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet build Orphac.Athera.sln --no-restore && dotnet test Orphac.Athera.sln --no-build` (`59/59` UnitTests including `S10SupplierReturnMatrixUnitTests` `7/7` + `67/67` Real PostgreSQL 18 IntegrationTests including `S10_PG_01`, `S10_PG_02`, `S10_API_01`, `S10_API_02`, `S10_API_03` = `126/126` total) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` warnings/errors, `0` skips; zero `.cs` edits since run) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`11/11` files, `30/30` tests), `next build`, `npm audit --omit=dev` (`0` vulnerabilities), Playwright E2E (`23/23` passed in `54.9s` across `s00`–`s10`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Impeccable static anti-pattern check (`impeccable detect --json apps/web/src`) & finish review (`8eeb3c3b`) | [impeccable-review.md](./impeccable-review.md) | PASS (`[]`, `disposition: ship`) |
| Secrets scan (`rg --pcre2` across `src/`, `apps/`, `tests/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Captured Desktop (`1440x900`) & Mobile (`390x844`) Screenshots (`01`–`08`) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S10 schema (`M10 -> M09`):** `dotnet ef database update 20261118034334_M09_SupplierInvoiceThreeWayMatchApAndPriceDifference --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (verified via `S10_PG_02`).
- **Scope boundaries (`S11+` / `OD-05` / `OD-07`):** Paid-invoice automatic cash-refund returns (`M5`), PPh-bearing return reversals (`M6`), AP payment preparation / release / bank reconciliation (`S11`), and multi-currency / sales flows remain blocked/deferred per `ATH-CR-007` and milestone sequencing.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
