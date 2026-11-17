# ATH-S03 Evidence Packet — Partners, Catalogue and Unit of Measure

- **Slice:** S03 — Partners, Catalogue and Unit of Measure (`docs/implementation/slices/S03_partners_catalogue_and_unit_of_measure.md`)
- **Design spec:** [S03-design.md](../../design/slices/S03-design.md)
- **Related Open Policy (`OD-07`):** Partner `tax_registration_npwp` and `tax_pkp_status` (`UNKNOWN` / `PKP` / `NON_PKP`) are persisted strictly as master-data metadata pending statutory Indonesian tax rule configuration (`S04+`).
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S02 SHA:** `57618ad` (ledger `c2fdca7`)
- **S03 Implementation SHA:** `(populated on ledger commit)`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against live PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Tenant-Shared Dual Partners & Entity Eligibility (`S03-E2E-01`):** `U1` (`rina.utami@demo.athera.local`) creates a dual Supplier + Customer partner (`RESTRICTED_ENTITIES`), attaches a primary contact, enables AP/AR eligibility for `NUSA-JKT`, and sees immediate metadata-only `OD-07` disclosure alongside searchable partner table rows.
2. **Catalogue, Exact UoM Conversion & Discrete Guard (`S03-E2E-01`):** `U1` creates a `STOCKED_GOOD` item (`PCS` discrete base `scale = 0`), defines `1 BOX = 24 PCS`, verifies live server-computed exact conversion `3 BOX = 72 PCS` (`round-trip: 3 BOX`), and sets `NUSA-JKT` purchase/sales policy flags.
3. **Sibling Read-Only Restriction & Cross-Tenant Isolation (`S03-E2E-02`):** `U2` (`budi.santoso@demo.athera.local`, `partners.read`/`catalog.read` only on `NUSA-SBY`) sees read-only views (`Lihat Rincian`, `Lihat UoM & Kebijakan`), restricted `PT-DUAL-001` (`Tidak Layak di Entitas Ini`) and `ITM-RES-002` (`Diblokir di Entitas Ini`) without mutation forms; `U1` navigating directly to foreign `SMDR-BDG` fails closed with `Ruang Kerja Tidak Tersedia` (`404`) and zero leak.

### B. Module Ownership & Architecture
- `Partners` owns `partners.partners`, `partners.partner_contacts`, `partners.partner_entity_profiles`, and `PartnerEligibilityEvaluator`.
- `Catalog` owns `catalog.units_of_measure`, `catalog.items`, `catalog.item_uom_conversions`, `catalog.item_entity_policies`, `UomConverter`, `ItemTransactionSnapshot`, and `ItemEligibilityEvaluator`.
- `Identity` adds `partners.read`, `partners.manage`, `catalog.read`, `catalog.manage` and `SALES_OFFICER` template alongside updated `PROCUREMENT_OFFICER` and `INVENTORY_CLERK`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`24/24` UnitTests + `26/26` Real PostgreSQL 18 integration tests: `S03_PG_01` missing `app.tenant_id` & cross-tenant RLS across all 7 tables, `S03_PG_02` `M02 -> M03 -> M02 -> M03` round-trip, `S03_PG_03` `DELETE` blocked `42501` for `athera_app`, `S03_API_01..03` eligibility/UoM/conflict/isolation) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`4` files, `9/9` tests), `next build`, Playwright E2E (`s00`, `s01`, `s02`, `s03`: `8/8` tests in `15.2s`), `npm audit --omit=dev --audit-level=high` (`0`), & `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg` across `src/`, `apps/web/src/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`3087ae5e`: Round 1 `fix` -> 7 fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Limitations
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Database="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S03 schema (`M03 -> M02`):** `dotnet ef database update M02_AdminApprovalsAuditAndOutbox --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (drops S03 tables plus `partners` and `catalog` schemas cleanly; verified via `S03_PG_02`).
- **Known limitation (`OD-07`):** Partner `TaxPkpStatus` and `TaxRegistrationNpwp` are recorded as master-data metadata only; no statutory Indonesian PPN/PPh computation is performed in S03.

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
