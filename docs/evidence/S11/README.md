# ATH-S11 Evidence Packet — Supplier Settlement, Outbound Clearing and Bank Reconciliation

- **Slice:** S11 — Supplier Settlement, Outbound Clearing and Bank Reconciliation (`docs/implementation/slices/S11_supplier_settlement_outbound_clearing_and_bank_reconciliation.md`)
- **Design Spec:** [S11-design.md](../../design/slices/S11-design.md)
- **Decision Request & Safe Default:** [ATH-CR-008-OD06-payment-release-and-bank-clearing.md](../../decisions/ATH-CR-008-OD06-payment-release-and-bank-clearing.md) (`OD-06` two-stage bank clearing & `OD-02` four-eyes release controls; file generation never settles AP or posts GL)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review in `WORKSTREAM-STATE.md` and `ATH-CR-008`)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S10 SHA:** `0d7a969` (`WORKSTREAM-STATE.md` HEAD `390f70a`)
- **S11 Implementation SHA:** Recorded in [WORKSTREAM-STATE.md](../../implementation/WORKSTREAM-STATE.md)

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Proposal, Simulated Payment File (`0 GL / 0 AP`) & Four-Eyes Notice (`S11-E2E-01` step 1):** AP Officer (`siti.rahayu@demo.athera.local`) opens `/payments` (`01`), inspects `PAY-DRF-2026-00001` (`SIMULASI · BUKAN TRANSFER LANGSUNG`, `pay-sod-notice` blocking self-release), generates an additional simulated CSV file (`PIN-`), and prepares/submits a new proposal (`PAY-DRF-2026-00004`) against `INV-JKT-S11-03` with zero GL or AP reduction.
2. **Independent Four-Eyes Release (`Dr 2100 AP / Cr 1120 Clearing`) (`S11-E2E-01` step 2):** Group Controller (`budi.santoso@demo.athera.local`) releases Siti's pending proposal with explicit bank portal reference `BCA-PORTAL-*` (`02`: assigns `PAY-2026-00005`, posts balanced `JRN-2026-11-00016`, settles `AP-2026-00002`, and moves obligation to open `1120` Clearing).
3. **Bank Statement CSV Import & Single-Match Clearing (`Dr 1120 Clearing / Cr 1110 Cash`) (`S11-E2E-01` step 3):** Controller matches seeded debit line `TRF-BCA-20261115-01` (`Rp 2.220.000`) to `PAY-2026-00003` creating `BMK-2026-00001` (`JRN-2026-11-00017`, `Diposting (1120 -> 1110)`), and imports a fresh CSV statement (`03`).
4. **Live `1120` GL-vs-Subledger Proof, Read-Only Entity Scope & `390x844` Mobile (`S11-E2E-01` steps 4–6):** Accounts tab (`04`) confirms `Selisih GL vs Subledger: Rp 0` (`Kliring 1120 Cocok`) across `BCA-IDR-001`; `NUSA-SBY` (`05`) enforces read-only view for `BCA-SBY-001`; `390x844` mobile layout (`06`) stacks tables with zero horizontal overflow.

### B. Module Ownership & Schema Artifacts
- **Accounts Payable (`AccountsPayable`):** Added `SupplierPayment`, `PaymentAllocation`, `PaymentSequence` (`SupplierPaymentModels.cs`) and `PayableItem.ApplyPaymentSettlement` / `ReversePaymentSettlement` (`AccountsPayableModels.cs`) enforcing `PAYMENT_BLOCKED` guards and `Net == Paid + Credited + Open`.
- **Cash (`Cash`):** Added `BankAccount`, `PaymentInstruction` (`SimulationOnly = true`, SHA-256 hash), `BankStatement`, `BankStatementLine` (SHA-256 fingerprint dedup), `BankPaymentMatch`, sequence entities (`CashModels.cs`), `CashCoordinatorHelpers.cs`, `SupplierSettlementCoordinator.cs` (proposal, simulated file, submit, four-eyes release, compensating bank rejection reversal, `FOR UPDATE` locking, savepoint rollback/fault injection), and `BankReconciliationCoordinator.cs` (CSV statement import, single/partial bank match, compensating match reversal). Note: `DemoDataSeeder.cs` also anchors demo seed timestamps 3 hours in the past (`DateTimeOffset.UtcNow.AddHours(-3)`) so seeded audit events remain chronologically behind live runtime events.
- **Persistence (`M11`):** Added `20261118183609_M11_SupplierSettlementOutboundClearingAndBankReconciliation` creating `cash` schema + 11 tables across `ap` and `cash`, `ENABLE` + `FORCE ROW LEVEL SECURITY`, immutability triggers (`cash.guard_payment_instruction_immutability`, `cash.guard_bank_statement_immutability`, `cash.guard_bank_payment_match_immutability`), and `athera_app` least-privilege grants.
- **API, Seed & OpenAPI:** Added `PaymentsAndBankingContracts.cs`, `PaymentsAndBankingEndpoints.cs`, updated `WorkspaceEndpoints.cs` (`"payments"`), `Program.cs`, `DemoDataSeeder.cs` + `DemoDataSeeder.S11.cs` (`BCA-IDR-001`, `BCA-SBY-001`, `ITM-S11-FILTER-01`, `PIN-2026-00001`, `PAY-2026-00003`, `TRF-BCA-20261115-01`), exported `docs/contracts/openapi.v1.json`, and verified TypeScript schema parity (`apps/web/src/lib/api/schema.d.ts`).
- **Frontend:** Updated `DESIGN.md`, `AppShell.tsx`, `client.ts`, `messages.ts`, `payables/page.tsx`, and added `SupplierPaymentDetailSheet.tsx` + `/payments` workbench (`payments/page.tsx`).

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln --logger "console;verbosity=normal"` (`62/62` UnitTests including `S11SupplierSettlementAndBankReconciliationUnitTests` + `72/72` Real PostgreSQL 18 IntegrationTests including `S11_PG_01`, `S11_PG_02`, `S11_API_01`..`03` = `134/134` total) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` skips) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`12/12` files, `33/33` tests), `next build`, Playwright E2E (`24/24` passed across `s00`–`s11`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Impeccable static anti-pattern check (`npx --yes impeccable detect --json apps/web/src`) & finish review (`08f70e5c`) | [impeccable-review.md](./impeccable-review.md) | PASS (`[]`, `disposition: ship`) |
| Secrets scan across `src/` & `apps/web/src/` | [secrets-scan.log](./secrets-scan.log) | PASS (`SECRETS_SCAN_CLEAN`) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS |
| Captured Desktop (`1440x900`) & Mobile (`390x844`) Screenshots (`01`–`06`) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" ConnectionStrings__App="<app-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S11 schema (`M11 -> M10`):** `dotnet ef database update 20261118135307_M10_SupplierReturnsReplacementsAndFinancialCorrections --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (verified via `S11_PG_02`).
- **Scope boundaries (`S12+` / `OD-06`):** Live bank host-to-host transmission, automated MT940/CAMT.053 parser formats, bank fee/FX differences, and post-reconciliation bank chargebacks remain disabled/deferred per `ATH-CR-008` and milestone sequencing; S12+ covers Customer Sales Orders, AR, Billing, and Financial Reporting.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
