# ATH-S02 Manual UAT Script — Organization Administration, Approvals, Audit & Outbox

## Preconditions
1. Ensure disposable PostgreSQL 18 is running on `127.0.0.1:55433`:
   ```bash
   ./scripts/dev/bootstrap-postgres.sh
   ```
2. Apply migrations (`M00`–`M02`) and seed S02 demo users/policies:
   ```bash
   export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
   export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
   export ASPNETCORE_ENVIRONMENT=Development
   export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

   dotnet run --project src/Orphac.Athera.Api -- --migrate
   dotnet run --project src/Orphac.Athera.Api -- --seed-demo
   ```
3. Run .NET API (`127.0.0.1:5200` as `athera_app`) and Next.js (`127.0.0.1:3200`).

---

## Case 1 — Organization Invitation Lifecycle & Revocation (`U2` + `U3`)
1. Sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026`.
2. Open `NUSA-JKT` -> click **Administrasi** in the left rail (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/admin`).
3. Under **Undang Pengguna ke Entitas Ini**, invite `hendra.wijaya@demo.athera.local` with template `PROCUREMENT_OFFICER` -> click **Buat Undangan**.
4. **Expected:** One-time token banner appears (`INVITATION_CREATED` recorded in Audit Trail and `Outbox` shows pending/processed message).
5. Copy the token, sign out, and sign in as `hendra.wijaya@demo.athera.local`.
6. Open **Terima Undangan** (`/invitations`), paste token, click **Terima Undangan & Aktifkan Akses**.
7. **Expected:** Membership + `NUSA-JKT` grants activate; `NUSA-SBY` and `SMDR-BDG` remain hidden (`404`).
8. Sign out, sign back in as `budi.santoso@demo.athera.local`, open `NUSA-JKT` -> **Administrasi**, click **Proses Antrean Outbox**, then click **Cabut Akses** on `hendra.wijaya@demo.athera.local`.
9. **Expected:** Member transitions to `REVOKED`, audit trail records `MEMBERSHIP_REVOKED`, and subsequent access by `Hendra` returns `404`.

## Case 2 — Four-Eyes Self-Approval Block & Independent Decision (`U1` + `U2`)
1. Sign in as `rina.utami@demo.athera.local`, open `NUSA-JKT` -> **Persetujuan** (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/approvals`).
2. Select policy `Sensitive Entity Access Change`, permission `sales.read`, enter business justification, and click **Kirim Permintaan Persetujuan**.
3. **Expected:** Case renders with amber **Menunggu Keputusan** badge and **Aturan Empat Mata (OD-02)** warning — no Approve button appears for `Rina`. Attempting direct API call `POST .../decide` returns `403 four_eyes_self_approval_forbidden`.
4. Sign out and sign in as `budi.santoso@demo.athera.local`.
5. Open `NUSA-JKT` -> **Persetujuan**, enter decision note on `Rina`'s pending case, and click **Setujui**.
6. **Expected:** Case transitions to **Disetujui**; `Rina` receives `sales.read` on `NUSA-JKT`; Audit Trail logs `APPROVAL_APPROVED`.

## Case 3 — Append-Only PostgreSQL Proof
Connect as low-privileged runtime role `athera_app` inside a valid scoped transaction and run:
```sql
UPDATE audit.audit_events SET summary = 'tamper';       -- Expect ERROR: permission denied (42501)
DELETE FROM approval.approval_decisions;               -- Expect ERROR: permission denied (42501)
```
