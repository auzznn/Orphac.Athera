# ATH-S04 Manual UAT Script — Financial Configuration and Immutable Posting Kernel

## Preconditions
1. Ensure disposable PostgreSQL 18 is running on `127.0.0.1:55433`:
   ```bash
   ./scripts/dev/bootstrap-postgres.sh
   ```
2. Apply migrations (`M00`–`M04`) and seed S04 CoA, periods (`2026-10` CLOSED, `2026-11` OPEN), 12 account mappings, and OD-07 tax codes:
   ```bash
   export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
   export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
   export ASPNETCORE_ENVIRONMENT=Development
   export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

   dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Run ASP.NET Core API (`127.0.0.1:5200` as low-privilege `athera_app`) and Next.js web app (`127.0.0.1:3200`).

---

## Case 1 — Unbalanced & Closed-Period Rejections + Balanced Journal Posting (`U2` on `NUSA-JKT`)
1. Sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026`.
2. Open `NUSA-JKT` -> click **Keuangan & GL** in the left rail (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/finance`).
3. **Unbalanced journal check:** Enter Posting Date `2026-11-18`, unique Source Key, Description `Uji Tidak Seimbang`, Line 1 Debit `250000`, Line 2 Credit `200000`.
   - **Expected:** Live indicator shows crimson **Tidak Seimbang** with variance `Rp 50.000`. Clicking **Posting Jurnal Permanen** returns inline `role="alert"` error (`unbalanced_journal` localized) and creates no database row.
4. **Closed period check:** Fix Line 2 Credit to `250000` (green **Seimbang**), change Posting Date to `2026-10-15` (closed period `2026-10`), and click **Posting Jurnal Permanen**.
   - **Expected:** Inline `role="alert"` displays period-closed rejection (`period_closed` localized).
5. **Valid balanced posting:** Set Posting Date to `2026-11-18` (open period `2026-11`), enter Description `Biaya Kirim Jakarta`, and click **Posting Jurnal Permanen**.
   - **Expected:** Journal commits atomically (`JRN-2026-11-...`) with **Diposting** chip, immutable Debit/Credit breakdown, and `AuditEvent` + `OutboxMessage`.

## Case 2 — Compensating Reversal & CoA / Account Mappings (`U2` on `NUSA-JKT`)
1. On the newly posted journal detail sheet, enter written reason `Koreksi E2E S04` and click **Posting Jurnal Pembalik**.
   - **Expected:** Creates linked `Jurnal Pembalik` (`REVERSAL` inverting Debit/Credit per line), marks original journal **Telah Dibalik**, and blocks any second reversal (`409 journal_already_reversed`).
2. Switch to tab **Bagan Akun & Pemetaan**.
   - **Expected:** Displays hierarchical Chart of Accounts (`1000` **Grup Header** non-postable vs `1110`..`5300` postable accounts, **Kontrol** badges) and all 12 canonical subledger mapping purposes (`INVENTORY_ASSET`, `GRNI_CLEARING`, `AP_TRADE_CONTROL`, etc.).

## Case 3 — Fiscal Periods & Effective-Dated Tax Evaluation (`OD-07` / `OD-13`)
1. Switch to tab **Periode Fiskal & Pajak (OD-07)**.
2. Verify explicit `UNREVIEWED_CONFIGURABLE` disclaimer notice (`ATH-CR-002`).
3. Under **Uji Evaluasi Pajak Eksplisit**, select `PPN-12-DPP1112`, Gross Amount `1000000`, Date `2026-11-18`, click **Hitung Dasar & Pajak**.
   - **Expected:** Output shows DPP `Rp 916.667` (`11/12` base), Tax `Rp 110.000`, Effective Rate `11%`. Selecting `-- Select explicitly --` rejects without inferring from partner class; selecting `PPH23-SVC-2` with NPWP unchecked flags `counterparty_npwp_required_for_rate`.

## Case 4 — Read-Only (`NUSA-SBY`), Unauthorized (`U1`), Foreign (`SMDR`), and PG Append-Only Proof
1. Open `NUSA-SBY` Finance (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102/finance`) as `Budi`, and click **Lihat Baris & Sumber** on `JRN-2026-11-00001`.
   - **Expected:** Amber **Mode baca saja** banner; no composer, reversal, CoA-create, or period-close buttons; row button reads **Lihat Baris & Sumber**, and opening `JRN-2026-11-00001` displays the immutable **Hash Payload** and journal lines with zero reversal controls.
2. Sign out, sign in as `rina.utami@demo.athera.local`, navigate to `NUSA-JKT` `/finance` -> **Expected:** `403` **Izin Entitas Tidak Mencukupi**. Navigate to `SMDR-BDG` `/finance` -> **Expected:** `404` **Ruang Kerja Tidak Tersedia** with zero `SMDR` leak.
3. Under runtime PostgreSQL role `athera_app`:
   ```sql
   UPDATE gl.journal_entries SET description = 'tamper'; -- Expect ERROR: permission denied (42501)
   DELETE FROM gl.journal_lines;                         -- Expect ERROR: permission denied (42501)
   ```
