# ATH-S09 Evidence Packet — Supplier Invoice Three-Way Match, AP and Price Difference

- **Slice:** S09 — Supplier Invoice Three-Way Match, AP and Price Difference (`docs/implementation/slices/S09_supplier_invoice_three_way_match_ap_and_price_difference.md`)
- **Design Spec:** [S09-design.md](../../design/slices/S09-design.md)
- **Decision Request & Safe Default:** [ATH-CR-006-OD03-OD08-invoice-match-tolerances-and-ap-scope.md](../../decisions/ATH-CR-006-OD03-OD08-invoice-match-tolerances-and-ap-scope.md) (`OD-03` `EXACT_MATCH_OD03_SAFE` zero automatic tolerances & `OD-08` PO Three-Way Match only until Product Owner sign-off)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review in `WORKSTREAM-STATE.md` and `ATH-CR-006`)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S08 SHA:** `606f138` (`WORKSTREAM-STATE.md` HEAD `b08cc28`)
- **S09 Implementation SHA:** Recorded in [WORKSTREAM-STATE.md](../../implementation/WORKSTREAM-STATE.md)

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Open GRNI & `DEC-P2P-013` Positive Price Variance Preview (`S09-E2E-01`):** `U4` (`siti.rahayu@demo.athera.local`) inspects `ITM-S09-VALVE-01` open GRNI (`100 PCS @ Rp 50.000`, `40 PCS` pool on-hand after operational issue `ISS-JKT-DEMO-S09-01`) on `/payables` (`01`), drafts a vendor bill @ `Rp 55.000` with `PPN-IN-11` (`11%`) and `PPH23-SVC-2` (`2%`), and previews balanced GL postings (`02`: `APPROVAL_REQUIRED`, `Rp 200.000` to `1300 Persediaan`, `Rp 300.000` to `5200 PPV`, `PPN = Rp 605.000`, `PPh 23 = Rp 110.000`, `Net AP = Rp 5.995.000`, projected MAC `Rp 55.000`).
2. **Four-Eyes Approval (`OD-02`), Atomic Posting & Payment Block (`S09-E2E-01`):** `U4` saves & submits `BILL-2026-00001` (`PENDING_APPROVAL`; approve/post hidden). `U2` (`budi.santoso@demo.athera.local`) opens `SupplierBillDetailSheet`, confirms variance acknowledgement + `PAYMENT_BLOCKED` (`06`), posts to `JRN-2026-11-00006` + `AP-2026-00001`, releases payment hold (`07`: `RELEASED`), inspects AP subledger & revaluation split (`03`), verifies `ITM-S09-VALVE-01` inventory carrying value `Rp 2.200.000` / MAC `Rp 55.000` (`04`), and validates `390x844` mobile layout (`05`) plus `U1` `403` / `NUSA-SBY` read-only isolation (`S09-E2E-02`).

### B. Module Ownership & Schema Artifacts
- **Identity (`Identity`):** Added `ap.read`, `ap.manage`, `ap.approve`, `ap.post` (`EntityAccessGrant.cs`) and `RoleTemplates.ApClerk` (`RoleTemplates.cs`); seeded `U4` (`siti.rahayu@demo.athera.local`, `AP_CLERK` on `NUSA-JKT`), granted `U2` full AP rights on `NUSA-JKT`/`SMDR-BDG` and `ap.read` on `NUSA-SBY`, keeping `U1` ungranted.
- **Accounts Payable (`AccountsPayable`):** Added `InvoiceMatchPolicy`, `SupplierBill`, `SupplierBillLine`, `InvoiceMatchEvaluation`, `SupplierBillReceiptMatch`, `InvoicePriceAdjustment`, `PayableItem`, `PayablePaymentBlockEvent`, `InvoiceThreeWayMatchCalculator` (`AccountsPayableModels.cs`), and `SupplierBillCoordinator.cs` (preview, draft create/update, submit, four-eyes decide, `sp_post_supplier_bill` atomic post, payment block toggle).
- **GL, Tax & Inventory Extensions:** Added `ReceiptAccrual.ApplyLineClearings` + append-only `ReceiptAccrualClearing` (`GeneralLedgerModels.cs`), append-only `TaxDetermination` (`TaxModels.cs`), and `OperationalIssue` + `InvoicePriceAdjustment` valuation adjustments (`InventoryModels.cs`, `InventoryValuationService.cs`).
- **Persistence (`M09`):** Added `20261118034334_M09_SupplierInvoiceThreeWayMatchApAndPriceDifference` creating `ap` schema & S09 tables with `ENABLE` + `FORCE ROW LEVEL SECURITY` (`platform.current_tenant_id()`), least-privilege `athera_app` grants (append-only on audit/clearing/tax/adjustment tables), and immutability triggers (`trg_ap_supplier_bills_immutability`, `trg_ap_supplier_bill_lines_immutability`).
- **API, Seed & OpenAPI:** Added `PayablesContracts.cs`, `PayablesEndpoints.cs`, updated `InventoryContracts.cs`/`InventoryEndpoints.cs`, `WorkspaceEndpoints.cs`, `Program.cs`, `DemoDataSeeder.cs` (`ITM-S09-VALVE-01`, `EXACT_MATCH_OD03_SAFE`, `SUPPLIER_BILL_POSTING`), `docs/contracts/openapi.v1.json`, and `apps/web/src/lib/api/schema.d.ts`.
- **Frontend:** Updated `DESIGN.md`, `globals.css`, `AppShell.tsx`, `client.ts`, `messages.ts`, `InventoryStockDetailSheet.tsx`, `inventory/page.tsx`, and added `SupplierBillDetailSheet.tsx` + `/payables` workbench (`payables/page.tsx`).

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet build Orphac.Athera.sln --no-restore && dotnet test Orphac.Athera.sln --no-build` (`52/52` UnitTests including `S09SupplierInvoiceMatchValuationTests` + `62/62` Real PostgreSQL 18 IntegrationTests including `S09_PG_01`, `S09_PG_02`, `S09_API_01`, `S09_API_02`, `S09_API_03` = `114/114` total) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` warnings/errors; zero `.cs` edits since run) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`10/10` files, `27/27` tests), `next build`, Playwright E2E (`19/19` passed in `40.8s` across `s00`–`s09`), `npm audit --omit=dev --audit-level=high` (`0`), `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg --pcre2` across `src/`, `apps/`, `tests/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`277c32f0`: Round 1 `fix` -> 7 material fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1440x900`) & Mobile (`390x844`) Screenshots (`01`–`07`) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S09 schema (`M09 -> M08`):** `dotnet ef database update 20261118020321_M08_StockAvailabilityLocationsAndQualityRestrictions --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (drops immutability triggers, clears `OPERATIONAL_ISSUE`/`INVOICE_PRICE_ADJUSTMENT` entries before restoring M08 constraints, and drops S09 tables & `ap` schema; verified via `S09_PG_02`).
- **Scope boundaries (`S10` / `OD-03` / `OD-08`):** AP payment file preparation, authorized release, bank reconciliation (`S10`), non-zero automatic match tolerances (`OD-03`), and Non-PO / supplier advance bills (`OD-08`, `ATH-CR-006`) remain blocked/separated pending Product Owner approval.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
