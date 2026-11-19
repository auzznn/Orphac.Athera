# ATH-S14 Manual UAT Script — Customer Billing, Output Tax & Accounts Receivable

## Prerequisites
- PostgreSQL 18 seeded via `dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- At least one dispatched shipment (`SHP-2026-00001`, 4 PCS of `ITM-S09-VALVE-01` for `SO-2026-00001`) on `NUSA-JKT` (either from running S13 UAT or dispatching 4 PCS on `/fulfillment`).

## Steps
1. **Sales Draft & Submit (`dewi.lestari@demo.athera.local`):**
   - Navigate to `NUSA-JKT` → **Penagihan & Piutang (AR)** (`/billing`).
   - On tab **Siap Ditagih (SHP)**, verify `SHP-2026-00001` shows `Terkirim: 4 PCS`, `Tertagih: 0`, `Siap Tagih: 4 PCS`.
   - Click **Buat Draf Faktur**, set quantity to `2`, verify live preview shows `PPN-OUT-11 (11%)` (`DPP Rp 190.000`, `Pajak Rp 20.900`, `Total Tagihan Rp 210.900`), and click **Simpan Draf Faktur**.
   - On tab **Faktur & Nota Kredit**, click **Ajukan ke Keuangan** (`SUBMITTED`). Verify Sales cannot post to AR/GL.
2. **Finance Irreversible Posting (`budi.santoso@demo.athera.local`):**
   - Sign in as `budi.santoso@demo.athera.local` on `NUSA-JKT` → `/billing` → **Faktur & Nota Kredit**.
   - Click **Posting AR & GL** on `INV-2026-00001`. Verify the irreversible warning banner and balanced GL preview (`Dr 1200 Rp 210.900 / Cr 4100 Rp 190.000 / Cr 2210 Rp 20.900`).
   - Verify **Sahkan & Posting Permanen** stays disabled until a Faktur Pajak reference (e.g., `FP-010.001-26.00000001`) is entered (`OD-07`).
   - Enter `FP-010.001-26.00000001` and post. Verify status becomes **Terposting** with a `JRN-2026-11-*` link.
3. **Linked Credit Note & AR Subledger Verification:**
   - Click **Nota Kredit** on `INV-2026-00001`, enter `Nota Retur` reference `NR-2026-0001`, set quantity `1`, and post.
   - Verify `CN-2026-00001` posts with `Kredit atas: INV-2026-00001` and its own `JRN-2026-11-*` compensating journal.
   - Switch to **Buku Pembantu Piutang (AR)** and confirm `AR-2026-00001` shows `Awal: Rp 210.900`, `Kredit: Rp 105.450`, `Saldo Piutang Terbuka: Rp 105.450`.
4. **Read-Only Multi-Entity Boundary (`NUSA-SBY`):**
   - Switch scope to `NUSA-SBY` → `/billing`. Confirm the read-only banner is displayed and zero draft/post/credit action buttons appear.
