# ATH-S01 Manual UAT Script — Tenant, Legal Entity & Workspace Switcher

## Preconditions
1. Start disposable PostgreSQL 18 on `127.0.0.1:55433`:
   ```bash
   ./scripts/dev/bootstrap-postgres.sh
   ```
2. Apply migrations (`M00` + `M01`) and seed demo accounts & tenants:
   ```bash
   export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
   export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
   export ASPNETCORE_ENVIRONMENT=Development
   export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

   dotnet run --project src/Orphac.Athera.Api -- --migrate
   dotnet run --project src/Orphac.Athera.Api -- --seed-demo
   ```
3. Launch the API (`127.0.0.1:5200` as `athera_app`) and Next.js proxy UI (`127.0.0.1:3200`).

---

## Case 1 — U1 (`rina.utami@demo.athera.local`) Scoped Access & Hostile URL Denial
1. Open `http://127.0.0.1:3200/login` and sign in as `rina.utami@demo.athera.local` / `AtheraDemo!2026`.
2. Verify `/` shows `Workspace Status = ACTIVE_WORKSPACES` and `Authorized Entities = 1`.
3. Click **Buka Direktori Ruang Kerja** (`Open Workspace Directory`) -> `/workspaces`.
4. **Expected:** Only `NUSA — PT Nusantara Niaga Utama` and `NUSA-JKT` (`PT Nusantara Distribusi Jakarta`) appear. `NUSA-SBY` and `SMDR-BDG` are absent.
5. Click **Masuk Ruang Kerja** on `NUSA-JKT` -> opens `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101`.
6. **Expected:** Displays NPWP `01.234.567.8-011.000`, `IDR · Asia/Jakarta`, active grants (`procurement.read`, `workspace.view`), and module readiness rows (`Procurement = Diizinkan`, `Finance = Tidak Diizinkan`).
7. Paste sibling entity URL `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102` (`NUSA-SBY`) into the browser address bar.
8. **Expected:** Displays non-leaking **Ruang Kerja Tidak Tersedia** (`entity_not_found`) with zero mention of `"NUSA-SBY"` or `"Surabaya"`.
9. Paste foreign tenant URL `/w/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101` (`SMDR-BDG`).
10. **Expected:** Displays non-leaking **Ruang Kerja Tidak Tersedia** (`workspace_not_found`) with zero mention of `"SMDR"`, `"Samudra"`, or `"Bandung"`.

## Case 2 — U2 (`budi.santoso@demo.athera.local`) Multi-Tenant Scope Switcher
1. Sign out, then sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026`.
2. Open `/workspaces`.
3. **Expected:** Both `NUSA` (`NUSA-JKT`, `NUSA-SBY`) and `SMDR` (`SMDR-BDG`) appear (`3` authorized entities).
4. Enter `NUSA-JKT`, then use the top-bar **Scope Switcher** dropdown to switch to `NUSA-SBY`, then across tenants to `SMDR-BDG`.
5. **Expected:** Heading (`CV Samudra Teknik Bandung`), top-bar scope indicator (`SMDR · SMDR-BDG · IDR`), NPWP (`02.345.678.9-421.000`), and module readiness (`Keuangan & GL`, `Administrasi`) update immediately with zero leftover `NUSA` data.
6. Resize viewport to `390x844` mobile and confirm zero horizontal scroll and zero badge clipping.

## Case 3 — U3 (`hendra.wijaya@demo.athera.local`) Zero-Workspace Empty State
1. Sign out, then sign in as `hendra.wijaya@demo.athera.local` / `AtheraDemo!2026`.
2. Open `/workspaces`.
3. **Expected:** Displays **Tanpa Ruang Kerja Aktif (Default-Deny)** badge in header and the accessible empty guidance sheet referencing Organization Administrator provisioning (`S02`).

## Case 4 — Direct PostgreSQL Forced-RLS Spot Check
Connect as low-privileged `athera_app`:
```sql
SELECT count(*) FROM platform.tenants;         -- Expect 0
SELECT count(*) FROM platform.legal_entities;  -- Expect 0
```
Scope a transaction to `NUSA`:
```sql
BEGIN;
SELECT set_config('app.actor_user_id', '11111111-1111-1111-1111-111111111102', true);
SELECT set_config('app.tenant_id', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', true);
SELECT code FROM platform.legal_entities ORDER BY code; -- Expect NUSA-JKT, NUSA-SBY ONLY
COMMIT;
```
