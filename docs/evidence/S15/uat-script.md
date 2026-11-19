# ATH-S15 Manual UAT Script — Customer Receipts, Allocation & AR Aging

## Prerequisites
- PostgreSQL 18 seeded via `dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- At least one posted customer invoice (`INV-2026-00001` / `AR-2026-00001`) on `NUSA-JKT` (from S14 UAT).

## Steps
1. **Import Inbound Bank Statement (`budi.santoso@demo.athera.local`):**
   - Navigate to `NUSA-JKT` → **Penerimaan & Umur Piutang** (`/receivables`).
   - Confirm KPI strip displays `GL 1200 == Subledger AR`.
   - Click **Impor Mutasi Bank CSV**, enter statement reference `STMT-IN-UAT-01`, paste a `CREDIT` CSV row for `Rp 120.000`, and import. Re-importing the exact same CSV row must be rejected as duplicate (SHA-256 fingerprint).
2. **Confirm Customer Receipt & Partial Allocation:**
   - Click **Konfirmasi Penerimaan** on the unmatched `CREDIT` bank line.
   - Select customer `PT-CUST-001`, allocate `Rp 80.000` to `INV-2026-00001`, verify live preview shows `Diterima: Rp 120.000 · Dialokasikan: Rp 80.000 · Belum Dialokasikan: Rp 40.000`, and click **Konfirmasi & Posting Dr 1110 / Cr 1200**.
   - On tab **Penerimaan & Alokasi (RCT)**, confirm `RCT-2026-00001` is created with status `Sebagian Dialokasikan`, `JRN-*` link, and KPI badge remains `GL 1200 == Subledger AR`.
3. **AR Aging Schedule:**
   - Switch to **Umur Piutang (AR Aging)**, verify `PT-CUST-001` bucket totals, `Belum Alokasi` (`Rp 40.000`), and `Piutang Neto` match the KPI strip.
4. **Read-Only Multi-Entity Boundary (`NUSA-SBY`):**
   - Switch scope to `NUSA-SBY` → `/receivables`. Confirm `#ar-readonly-banner` is shown and zero import/confirm/allocate/reverse buttons appear.
