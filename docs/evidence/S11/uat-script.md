# UAT-S11 — Supplier Settlement, Outbound Clearing and Bank Reconciliation (`ENGINEERING_PASS`, PO Sign-Off Pending)

## Prerequisites
1. Local PostgreSQL 18 container (`athera-greenfield-pg`) running on `127.0.0.1:55433`.
2. Cleanly migrated & seeded database:
   ```bash
   docker exec athera-greenfield-pg env PGPASSWORD=athera_dev_local_migrator_pw psql -U athera_migrator -d athera_dev \
     -c "DROP SCHEMA IF EXISTS ap, approval, audit, cash, catalog, gl, identity, integration, inventory, partners, platform, procurement, quality, tax CASCADE;"
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
     ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
     ConnectionStrings__App="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw" \
     dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Start API & Web (`http://127.0.0.1:3200`).

---

## Verification Scenarios (`UAT-P2P-018`..`020`, `UAT-P2P-023`..`025`)

1. **Stage 1 Simulation Non-Posting (`UAT-P2P-018`, `OD-06`):**
   - Sign in as `siti.rahayu@demo.athera.local` -> open `NUSA-JKT` -> **Pembayaran & Bank (`/payments`)**.
   - Open `PAY-DRF-2026-00001` -> inspect `"SIMULASI · BUKAN TRANSFER LANGSUNG"` (`PIN-2026-00001`), click **Hasilkan Berkas Simulasi (0 GL)**, confirm AP balances (`INV-JKT-S11-01 = Rp 1.110.000` still Open) and GL journals are unchanged, and confirm Four-Eyes `OD-02` lock prevents Siti from releasing her own submitted proposal.
   - Create a fresh draft proposal for open payable `INV-JKT-S11-03` (`Rp 1.665.000`) and submit for four-eyes release.
2. **Stage 2 Independent Four-Eyes Release (`UAT-P2P-019`, `OD-02` & `OD-06`):**
   - Sign out and sign in as `budi.santoso@demo.athera.local` -> open `PAY-DRF-2026-00001`.
   - Enter portal release reference (e.g., `BCA-PORTAL-20261115`) and release -> confirms balanced journal `Dr 2100 AP Trade Control / Cr 1120 Bank Outbound Clearing` posts once, assigns `PAY-2026-...`, marks `AP-2026-00002` Paid, and keeps **Live Outbound Clearing 1120 Proof** balanced (`Discrepancy = Rp 0`).
3. **Stage 3 Bank Statement CSV Import & Matching (`UAT-P2P-020`):**
   - Switch to **Mutasi Bank & Rekonsiliasi (BST / BMK)**.
   - Match seeded debit line `TRF-BCA-20261115-01` (`Rp 2.220.000`) to `PAY-2026-00003` -> confirms balanced journal `Dr 1120 Bank Outbound Clearing / Cr 1110 Cash & Bank Operating`, line becomes emerald `Sudah Padan`, `BMK-2026-00001` is recorded, and `ClearingProof` updates seamlessly (`Discrepancy = Rp 0`).
   - Import CSV bank statement (`BCA-STMT-...`) via UI and verify duplicate statement reference / duplicate transaction fingerprint fails closed (`409`).
4. **Bank Match Reversal & Bank Rejection Compensating Journal:**
   - Reverse a posted `BMK` match (`Dr 1110 / Cr 1120`), then reject an unreconciled `RELEASED` payment (`Dr 1120 / Cr 2100`) and confirm the supplier payable opens again cleanly (`RemainingOpenAmount` restored).
5. **Entity Isolation & Read-Only Scope (`UAT-P2P-023`..`025`):**
   - Switch to `NUSA-SBY` as Budi -> `/payments` shows `#pay-readonly-banner`, zero mutating buttons, and `BCA-SBY-001` only.
   - Closed period release/reconciliation rejected (`409 period_closed`); responsive check at `390x844` has zero clipping.
