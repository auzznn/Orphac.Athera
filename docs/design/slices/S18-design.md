# Slice S18 Design — Cross-Module UAT, Security & Release Candidate

- **Slice:** `ATH-S18`
- **Prerequisites:** `S00`–`S17` (all `CODE_COMPLETE` & `ENGINEERING_PASS`, PO Acceptance `PENDING`)
- **Authoritative Owners:** Engineering / Security / Finance / Product Owner (acceptance only)

## 1. Scope & Non-Goals
- **Zero new business tables or migrations** (`M00`–`M17` schema frozen for Release Candidate).
- **Objectives:**
  1. Exhaustive PostgreSQL 18 RLS & least-privilege verification across all 18 module schemas (`athera_app` non-superuser, `NOBYPASSRLS`, zero table ownership, `ENABLE` + `FORCE ROW LEVEL SECURITY` on every tenant-scoped table).
  2. Cross-module API permission & hostile cross-tenant fuzz matrix covering all 14 operational endpoints (`/partners`, `/catalog`, `/procurement`, `/receiving`, `/returns`, `/inventory`, `/payables`, `/payments`, `/sales`, `/fulfillment`, `/billing`, `/receivables`, `/reports`, `/documents`).
  3. Real PostgreSQL 18 concurrency soak for GL journal posting (idempotency deduplication + balanced TB) and stock reservation (zero over-reservation / negative available stock).
  4. Real PostgreSQL 18 `pg_dump` / `pg_restore` backup & recovery rehearsal + EF Core migration rollback (`M17 -> M16`) and forward (`M16 -> M17`) drill.
  5. Dependency vulnerability audit (`.NET` & `npm`), secrets scan, and Impeccable final responsive UX audit (`1280x800` desktop, `768x1024` tablet, `390x844` mobile) + keyboard accessibility check.
  6. Production-ready local/staging operations runbook and unsigned **Product Owner Four-Pillar Acceptance Packet** indexing all slices `S00`–`S18` and all open decisions `ATH-CR-001`–`ATH-CR-014` (`OD-01`–`OD-13`).
- **Explicit Non-Goals:** No live production deployment, no paid cloud provisioning, no self-signed PO acceptance.

## 2. Verification Matrix
| Test ID | Target | Execution Mode |
| --- | --- | --- |
| `S18_SEC_01` | Forced RLS on every tenant-scoped table + least-privilege `athera_app` + 14-endpoint hostile cross-tenant/read-only API matrix | Real PG18 (`dotnet test`) |
| `S18_CONCURRENCY_01` | 12-way concurrent GL journal posting + duplicate idempotency soak + concurrent stock over-reservation prevention | Real PG18 (`dotnet test`) |
| `S18_RC_01` | Cross-module 5-control reconciliation + TB/BS balance + period close/lock/reopen boundary proof | Real PG18 (`dotnet test`) |
| `S18_DRILL_01` | `pg_dump` -> `pg_restore` into clean PG18 DB + row/RLS parity + `M17 -> M16 -> M17` migration drill | Real PG18 CLI |
| `S18_AUDIT_01` | `dotnet list package --vulnerable`, `npm audit --omit=dev`, secrets regex scan | CLI |
| `S18_E2E_01` | Full cross-module UI walkthrough + `1280px` desktop, `768px` tablet, `390px` mobile & keyboard focus audit | Playwright + Impeccable Reviewer |
