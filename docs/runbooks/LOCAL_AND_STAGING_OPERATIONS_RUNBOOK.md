# Orphac Athera — Local & Staging Operations Runbook (MVP Release Candidate)

> [!IMPORTANT]
> This runbook governs local verification and staging rehearsal only. **Production deployment requires explicit Product Owner sign-off** on the Four-Pillar Acceptance Packet and resolution of open policy decisions (`OD-01`–`OD-13`).

## 1. Architecture & Runtime Topology
- **Web Frontend:** Next.js 16.4.6 (`apps/web`, port `3200`) proxying `/api/*` same-origin to the ASP.NET Core 10 API. Zero browser-stored auth tokens; `HttpOnly` session cookie + `X-CSRF-Token` on all mutations.
- **Backend API:** ASP.NET Core 10 modular monolith (`src/Orphac.Athera.Api`, port `5200`), ASP.NET Core Identity credential authority, OpenAPI v1 contract (`docs/contracts/openapi.v1.json`).
- **Database:** PostgreSQL 18 (`127.0.0.1:55433/athera_dev` locally), partitioned by 19 module-owned schemas (`platform`, `identity`, `approval`, `audit`, `integration`, `partners`, `catalog`, `gl`, `tax`, `procurement`, `inventory`, `quality`, `ap`, `cash`, `sales`, `fulfillment`, `billing`, `ar`, `documents`).
- **Database Principals:**
  - `athera_migrator`: DDL/migration owner used **only** during `--migrate` and `--seed-demo`; not for backup or restore.
  - `athera_app`: least-privilege runtime role (`NOSUPERUSER`, `NOBYPASSRLS`, owns zero tables), restricted by `ENABLE` + `FORCE ROW LEVEL SECURITY` (`tenant_id = platform.current_tenant_id()`).

## 2. Startup & Health Verification
```bash
# Disposable loopback instance and local-only bootstrap defaults; never use on staging.
./scripts/dev/bootstrap-postgres.sh
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Runtime='Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw'
ConnectionStrings__Migrator='Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw' \
ATHERA_DEMO_PASSWORD='AtheraDemo!2026' dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
dotnet run --project src/Orphac.Athera.Api --no-launch-profile
# New terminal: curl -fsS http://127.0.0.1:5200/api/health
# New terminal: npm --prefix apps/web ci && npm --prefix apps/web run build && npm --prefix apps/web run start
```

## 3. Backup & Restore Procedure (PostgreSQL 18)
> [!NOTE]
> Because every tenant-scoped table enforces `FORCE ROW LEVEL SECURITY`, even the table owner (`athera_migrator`) is subject to RLS during `COPY`/`pg_dump`. Backups and restores must therefore run under a dedicated `BYPASSRLS` backup principal (`athera_dev_admin` in local dev, or a dedicated read-only `BYPASSRLS` backup role in staging/production).

```bash
# Backup (custom compressed archive)
archive="athera_backup_$(date +%Y%m%d_%H%M%S).dump"
pg_dump -h 127.0.0.1 -p 55433 -U athera_dev_admin -Fc -f "$archive" athera_dev

# Restore into clean target database
createdb -h 127.0.0.1 -p 55433 -U athera_dev_admin -O athera_migrator athera_restore
pg_restore -h 127.0.0.1 -p 55433 -U athera_dev_admin -d athera_restore "$archive"

# Post-restore integrity verification
psql -h 127.0.0.1 -p 55433 -U athera_dev_admin -d athera_restore -c \
  "SELECT COUNT(*) AS forced_rls_tables FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped WHERE c.relkind='r' AND n.nspname NOT IN ('pg_catalog','information_schema','public') AND c.relrowsecurity AND c.relforcerowsecurity;"
```

## 4. Migration Rollback & Forward Plan
- Every EF Core migration (`M00`–`M17`) implements an explicit reversible `Down()` method that drops triggers, functions, and policies cleanly before dropping tables.

> [!WARNING]
> `M17` Down drops `documents`. Rehearse only on an explicitly disposable restored copy. `AtheraDbContextFactory` falls back to `athera_dev` when `ConnectionStrings__Migrator` is unset. For both commands below, supply `--connection "$RESTORED_DB_MIGRATOR"` with the exact restored database and local migrator credentials; never run on the source database.

- Example rollback of latest slice (`M17 -> M16`) and reapply (`M16 -> M17`):
```bash
: "${RESTORED_DB_MIGRATOR:?Set to the disposable restored database only}"

dotnet ef database update 20261119011903_M16_PeriodCloseAuditAndReconciliation \
  --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api \
  --connection "$RESTORED_DB_MIGRATOR"

dotnet ef database update \
  --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api \
  --connection "$RESTORED_DB_MIGRATOR"
```

## 5. Incident Recovery & Outbox Diagnostics
- **Failed Integration Outbox Messages:** Operators with `finance.config.manage` can inspect `/w/{tenantId}/{entityId}/documents` (`Pengecualian & Outbox` tab) and click `Coba Ulang` (`POST /documents/outbox/{messageId}/retry`) to requeue `FAILED` entries to `PENDING` with an immutable audit trail. Retry only requeues; it does not drain the queue. Due `PENDING` messages are actually processed by the authorized admin dispatch (`POST /admin/outbox/dispatch`) or the next tenant signal — there is no scheduled sweep.
- **Control Account Variance:** Open `/w/{tenantId}/{entityId}/reports` to inspect all 5 control accounts (`1120`, `1200`, `1300`, `2110 GRNI`, `2100 AP`), drill down to individual GL journals, and post compensating reversing entries rather than editing posted facts.

## 6. Staging-to-Production Rollout Proposal (Requires Owner Authorization)
1. Product Owner reviews & signs `docs/evidence/S18/PO-FOUR-PILLAR-ACCEPTANCE-PACKET.md` and resolves `OD-01`–`OD-13` (`docs/decisions/ATH-CR-001`–`ATH-CR-015`).
2. Provision dedicated managed PostgreSQL 18 instance with distinct `athera_migrator` / `athera_app` credentials stored in a secret manager, TLS (`sslmode=require`), and automated daily WAL/base backups.
3. Configure strong production `Security:DocumentDownloadSigningKey`, ASP.NET Core DataProtection key ring persistence, and strict HTTPS cookies (`SameSite=Lax; Secure; HttpOnly`).
4. Execute dry-run cutover of opening balances (`OD-11`) in staging, verify 5/5 control accounts reconcile with 0.00 variance, then promote signed build artifact.
