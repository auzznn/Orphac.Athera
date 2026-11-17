# ATH-S04 Evidence Packet — Financial Configuration and Immutable Posting Kernel

- **Slice:** S04 — Financial Configuration and Immutable Posting Kernel (`docs/implementation/slices/S04_financial_configuration_and_immutable_posting_kernel.md`)
- **Design spec:** [S04-design.md](../../design/slices/S04-design.md)
- **Open Policy Escalations:** [ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md) (`OD-07` Indonesian Tax Matrix & `OD-13` Fiscal Calendar / Numbering — `UNREVIEWED_CONFIGURABLE` safe defaults pending Product Owner & Indonesian tax advisor sign-off)
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S03 SHA:** `287d7e5` (ledger `b8eaba1`)
- **S04 Implementation SHA:** `<pending S04 commit>`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against live PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Unbalanced, Closed-Period, Balanced Posting & Compensating Reversal (`S04-E2E-01`):** `U2` (`budi.santoso@demo.athera.local`) verifies live decimal variance `Rp 50.000` (`Tidak Seimbang`), confirms closed-period (`2026-10`) server rejection (`period_closed`), posts a balanced journal into open period `2026-11`, inspects deterministic `payloadHash` + `MANUAL_GL` trace, posts an append-only compensating `Jurnal Pembalik`, and switches across CoA (`1000`–`5300` + `12` subledger mappings) and `OD-07` Tax (`PPN-12-DPP1112` on `1.000.000` -> `DPP Rp 916.667`, `Pajak Rp 110.000`, `11%`).
2. **Read-Only Scope, Cross-Entity 403 & Cross-Tenant 404 (`S04-E2E-02`):** `U2` on read-only `NUSA-SBY` inspects `JRN-2026-11-00001` (`Lihat Baris & Sumber` -> `Hash Payload` visible) with zero composer, reversal, or configuration mutation controls; `U1` (`rina.utami@demo.athera.local`) receives `403 Izin Entitas Tidak Mencukupi` on `NUSA-JKT` finance and `404 Ruang Kerja Tidak Tersedia` on foreign `SMDR-BDG`.

### B. Module Ownership & Architecture
- `GeneralLedger` owns `gl.accounts`, `gl.financial_periods`, `gl.account_mappings`, `gl.journal_sequences`, `gl.journal_entries`, `gl.journal_lines`, `AccountMappingValidator`, `GlPostingHash`, and `GlPostingKernel`.
- `Tax` owns `tax.tax_codes` (`UNREVIEWED_CONFIGURABLE` default status, explicit `taxCodeId` mandate, `11/12` `DPP` evaluation, and counterparty NPWP gating) and `TaxDeterminationEvaluator`.
- `Identity` adds `finance.config.manage` permission and `FinanceOfficer` least-privilege template.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`28/28` UnitTests + `33/33` Real PostgreSQL 18 integration tests: `S04_PG_01` missing `app.tenant_id` & cross-tenant RLS across all 7 tables, `S04_PG_02` `M03 -> M04 -> M03 -> M04` round-trip, `S04_PG_03` append-only `UPDATE`/`DELETE` `42501` block & deferred SQL balance/period trigger, `S04_API_01..04` balanced posting, 6x `Task.WhenAll` concurrent idempotency, closed period & single reversal, `11/12` DPP & NPWP/RBAC) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`5` files, `11/11` tests), `next build`, Playwright E2E (`s00`, `s01`, `s02`, `s03`, `s04`: `10` tests in `18.2s`), `npm audit --omit=dev --audit-level=high` (`0`), & `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg` across `src/`, `apps/web/src/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`cd04a8b5`: Round 1 `fix` -> 8 fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Limitations
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Database="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S04 schema (`M04 -> M03`):** `dotnet ef database update M03_PartnersCatalogueAndUom --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (drops trigger `gl.enforce_journal_entry_integrity()`, all `7` S04 tables, plus `gl` and `tax` schemas cleanly; verified via `S04_PG_02`).
- **Known limitation (`OD-07` / `OD-13`):** Statutory Indonesian tax rates and period/tax-invoice numbering schedules remain marked `UNREVIEWED_CONFIGURABLE` until [ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md) is signed by the Product Owner and tax advisor.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
