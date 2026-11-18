# ATH-S06 Evidence Packet — Dock Custody and Receiving Gate One (Unvalued)

- **Slice:** S06 — Dock Custody and Receiving Gate One (`docs/implementation/slices/S06_dock_custody_and_receiving_gate_one.md`)
- **Design Spec:** [S06-design.md](../../design/slices/S06-design.md)
- **Prerequisite Policy Links:** [ATH-CR-003-OD01-po-delivery-tolerance-policy.md](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) (`OD-01` strict `0%` overdelivery lock enforced at dock release)
- **Engineering Verification:** **PASS**
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S05 SHA:** `d2bf6b5` (ledger `2eed2ab`)
- **S06 Implementation SHA:** `876a916`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Gate 1 Partial Arrival, Unvalued Rejection & OD-01 Overdelivery Segregation (`S06-E2E-01`):** `U1` (`rina.utami@demo.athera.local`) records partial arrival `1 BOX = 24 PCS` against an approved `2 BOX = 48 PCS` stock PO, splits dock custody into `20` released (`DOCK_RELEASED`) + `4` rejected unvalued (`DOCK_REJECTED` under `DAMAGED_PACKAGING`) conserving `24 / 24 PCS` with zero GL/GRNI journals, then records second arrival `2 BOX = 48 PCS` (exceeding `28 PCS` remaining approved cap by `20 PCS`). Releasing `30 PCS` is blocked both in UI and by `409 od01_overdelivery_release_blocked`; applying `Lepas Sesuai Batas PO` cleanly releases `28 PCS` and holds `20 PCS` in `DOCK_HELD`. Verifies in `/finance` (`U2`) that zero GRN/GRNI postings were created.
2. **Read-Only SBY Monitor (`SJ-SBY-DEMO-001`), Cross-Entity `403` & Cross-Tenant `404` (`S06-E2E-02`):** `U2` (`budi.santoso@demo.athera.local`) views pre-seeded `SJ-SBY-DEMO-001` (`24 PCS`, `Menunggu Cek Fisik`, `0` GL journals) on `NUSA-SBY` in read-only mode (tabs and dock mutation buttons hidden), receives `403 Izin Entitas Tidak Mencukupi` on `SMDR-BDG`, and `U1` receives `404 Ruang Kerja Tidak Tersedia` on `SMDR-BDG` with zero cross-tenant leakage.

### B. Module Ownership & Schema Artifacts
- **Domain & Workflow (`Procurement`):** `ReceivingModels.cs` (`ReceiptSequence`, `GoodsReceipt`, `GoodsReceiptLine`, `DockCheck`, `ReceivingHash`), `GoodsReceiptWorkflowService.cs` (`RecordArrivalAsync`, `RecordDockCheckAsync`, `FOR UPDATE` PO lock, `OD-01` cap check, unvalued conservation).
- **Persistence (`M06`):** `20261117214046_M06_DockCustodyAndReceivingGateOne` creates `procurement.receipt_sequences`, `goods_receipts`, `goods_receipt_lines`, and append-only `dock_checks` (`SELECT, INSERT` only; composite tenant/entity FKs; `ENABLE` + `FORCE ROW LEVEL SECURITY`).
- **Identity & API:** Added `receiving.read` / `receiving.manage` permissions, `ReceivingContracts.cs`, `ReceivingEndpoints.cs`, updated `WorkspaceEndpoints.cs` (`receiving` = `AVAILABLE`, `inventory` = `PLANNED_S07_S08`), `DemoDataSeeder.cs` (`SJ-SBY-DEMO-001`), and `docs/contracts/openapi.v1.json`.
- **Frontend:** Added `/w/[tenantId]/[entityId]/receiving/page.tsx`, `apps/web/src/lib/receivingMath.ts`, `apps/web/src/components/AppShell.tsx`, `apps/web/src/lib/api/client.ts`, `apps/web/src/lib/i18n/messages.ts`, and `DESIGN.md`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`36/36` UnitTests + `47/47` Real PostgreSQL 18 IntegrationTests: `S06_PG_01` missing `app.tenant_id` & cross-tenant RLS across all 4 tables, `S06_PG_02` `M05 -> M06 -> M05 -> M06` round-trip, `S06_PG_03` append-only `dock_checks` `UPDATE`/`DELETE` privilege denial + zero `gl.journal_entries` & zero `HasFinancialPosting`, `S06_API_01..03` conservation, `OD-01` cap + amendment unblock, idempotency, duplicate delivery note, concurrent `Task.WhenAll` release serialization) | [dotnet-test.log](./dotnet-test.log) | PASS (`0` warnings/errors) |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`7` files, `16/16` tests), `next build`, Playwright E2E (`14/14` passed in `29.4s` across `s00`–`s06` with zero horizontal clipping), `npm audit --omit=dev --audit-level=high` (`0`), `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg --pcre2` across `src/`, `apps/`, `tests/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`3f7a16af`: Round 1 `fix` -> 6 fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Boundaries
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Migrator="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S06 schema (`M06 -> M05`):** `dotnet ef database update 20261117205100_M05_PurchaseOrdersRevisionsAndFourEyes --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (cleanly removes all 4 S06 `procurement` tables and policies; verified via `S06_PG_02`).
- **Scope boundary (`Gate 2` deferred to `S07`):** Gate 1 dock release never posts inventory quantities, moving average valuation, Inventory Asset, or GRNI accruals; Gate 2 acceptance and stock valuation begin in `S07`.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
