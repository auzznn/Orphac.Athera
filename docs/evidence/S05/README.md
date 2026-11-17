# ATH-S05 Evidence Packet — Purchase Orders, Approved Revisions and Four-Eyes

- **Slice:** S05 — Purchase Orders, Approved Revisions and Four-Eyes (`docs/implementation/slices/S05_purchase_orders_approved_revisions_and_four_eyes.md`)
- **Design spec:** [S05-design.md](../../design/slices/S05-design.md)
- **Open Policy Escalations:** [ATH-CR-003-OD01-po-delivery-tolerance-policy.md](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) (`OD-01` Overdelivery Tolerance & Unlimited Flag — `EXACT_APPROVED_QTY_ONLY` safe default active pending Product Owner sign-off); [ATH-CR-001-OD02-four-eyes-and-role-matrix.md](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md) (`OD-02` Four-Eyes)
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S04 SHA:** `0fcb7b5` (ledger `40ebc49`)
- **S05 Implementation SHA:** `PENDING_COMMIT`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against live PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Multi-Line PO Draft, Exact Decimal UoM/PPN 11/12 & Four-Eyes SoD (`S05-E2E-01`):** `U1` (`rina.utami@demo.athera.local`) drafts and submits a 2-line PO (`ITM-PKG-001` `3 BOX = 72 PCS` with `PPN-12-DPP1112`, `ITM-RES-002` `2 BOX = 72 PCS` with `PPN-IN-11`, `Grand Total Rp 2.375.400`), confirms self-approval buttons are locked in UI (`po-self-approval-blocked`), `U2` (`budi.santoso@demo.athera.local`) independently approves `Rev 1`, `U1` forks amendment `Rev 2` (`4 BOX @ Rp 495.000`), `U2` inspects `Perbandingan Perubahan Revisi` (`Rev 1 -> Rev 2`, `+Rp 599.400`, `+24`) and approves `Rev 2`, verifies historical `Rev 1` stays `SUPERSEDED` at `Rp 480.000`, and `U1` short-closes remaining commitment cleanly (`Ditutup Cepat (Short-Close)`).
2. **Read-Only Inspection, Cross-Entity 403 & Cross-Tenant 404 (`S05-E2E-02`):** `U2` on read-only `NUSA-SBY` inspects `PO-2026-00001` (`Hash Snapshot` visible, zero editor/approval/short-close controls); receives `403 Izin Entitas Tidak Mencukupi` on `SMDR-BDG` `/procurement`; and `U1` receives `404 Ruang Kerja Tidak Tersedia` on `SMDR-BDG` with zero foreign leakage.

### B. Module Ownership & Architecture
- `Procurement` owns `procurement.delivery_tolerance_policies`, `procurement.po_sequences`, `procurement.purchase_orders`, `procurement.purchase_order_revisions`, `procurement.purchase_order_lines`, `PurchaseOrderHash`, `PurchaseOrderDiffCalculator`, and `PurchaseOrderWorkflowService`.
- `Approval` owns `approval.document_approvals` linked to `PURCHASE_ORDER_COMMITMENT` four-eyes policy.
- `Identity` adds `procurement.manage` and `procurement.approve` (`ProcurementOfficer` vs `EntityApprover` least-privilege SoD templates).
- Zero inventory movement, GRNI accrual, GL journal, or AP liability is posted in S05.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`33/33` UnitTests + `40/40` Real PostgreSQL 18 integration tests: `S05_PG_01` forced RLS & cross-tenant isolation across all 6 tables, `S05_PG_02` `M04 -> M05 -> M04 -> M05` round-trip, `S05_PG_03` `42501` `DELETE` block + SQLSTATE `55000` freeze triggers + four-eyes constraint, `S05_API_01..04` draft/submit/SoD self-approve `403`/independent approve `200`, `6x` concurrent approval `200`x1 + `409`x5, `Rev 1 -> Rev 2` immutable diff, `OD-01` tolerance `409`, downstream cancel block vs short-close, `403`/`404`) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`6` files, `13/13` tests), `next build`, Playwright E2E (`s00`, `s01`, `s02`, `s03`, `s04`, `s05`: `12` tests in `24.4s`), `npm audit --omit=dev --audit-level=high` (`0`), & `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg` across `src/`, `apps/web/src/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`43934d5a-0a70-40d6-965d-214af219fa68`: Round 1 `fix` -> 7 fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots (`6` PNGs) | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Limitations
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Database="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S05 schema (`M05 -> M04`):** `dotnet ef database update M04_FinancialConfigAndPostingKernel --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (drops SQL freeze triggers/functions `guard_frozen_po_revision_lines()` / `guard_frozen_po_revision_snapshot()`, all `6` S05 tables, and the `procurement` schema cleanly; verified via `S05_PG_02`).
- **Known limitation (`OD-01`):** Overdelivery tolerance remains locked at `0%` / `0` extra qty / `AllowUnlimitedOverdelivery = false` until Product Owner signs [ATH-CR-003-OD01-po-delivery-tolerance-policy.md](../../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md).

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`