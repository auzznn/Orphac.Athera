# UAT Script — Slice S16: Accounting Reports, Control Reconciliations & Period Close

**Environment:** Local PostgreSQL 18 (`127.0.0.1:55433/athera_dev`) + ASP.NET Core 10 API (`http://127.0.0.1:5200`) + Next.js 16 (`http://127.0.0.1:3000`).  
**Primary Actor:** `budi.santoso@demo.athera.local` (Group Controller — write on `NUSA-JKT`, read-only on `NUSA-SBY`).

## Scenarios

1. **5-Control Account Reconciliations (`NUSA-JKT`):**
   - Open `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/reports`.
   - Confirm 4 KPI cards (`Total Debit / Kredit Neraca Saldo`, `Laba / (Rugi) Bersih`, `Total Aset`, `Status 5 Akun Kontrol GL`) render on one baseline with neutral static ink.
   - Verify all 5 control accounts (`1120 Kas/Bank`, `1200 Piutang AR`, `1300 Persediaan`, `2050 GRNI`, `2100 Hutang AP`) display GL Balance, Subledger Balance, Variance, Open Items, and Status.
2. **Trial Balance, Source-Linked GL Drilldown & CSV Export:**
   - Switch to `Neraca Saldo (TB)` tab; verify Opening, Period, and Ending Debit/Credit totals balance.
   - Click any account code to open `Penelusuran Buku Besar Akun` and confirm posted journals display `JournalNumber`, `PostingDate`, `SourceModule / SourceDocumentType`, `Debit`, `Credit`, and `RunningBalance`.
   - Click `Ekspor CSV Neraca Saldo` and verify valid CSV download with `AccountCode,AccountName,...` header and `TOTAL` row.
3. **Profit & Loss and Balance Sheet:**
   - Switch to `Laba Rugi & Neraca` tab; verify Revenue, Expense, Net Income, Assets, Liabilities, Equity, Current Period Earnings, and `Neraca Seimbang` badge.
4. **AP & AR Aging Summary:**
   - Switch to `Ringkasan Umur AP & AR` tab; verify bucket totals (`Belum Jatuh Tempo`, `1–30`, `31–60`, `61–90`, `> 90`) and open item counts.
5. **Audited Period Close, Blocked Posting, Reopen & Permanent Lock:**
   - On `Rekonsiliasi & Tutup Buku`, click `Tutup Periode` on an `OPEN` period -> confirm `CLOSED` status and new row in `Riwayat Audit Tutup Buku Periode`.
   - Confirm posting a journal into the `CLOSED` period is blocked (`409`).
   - Click `Buka Kembali`, enter written justification -> confirm status returns to `OPEN` and `REOPENED` audit entry is recorded.
   - Close again and click `Kunci Permanen` -> confirm `LOCKED` status and subsequent reopen attempt returns `409 period_locked_reopen_blocked`.
6. **Read-Only Boundary (`NUSA-SBY`) & Mobile (`390x844`):**
   - Switch to `NUSA-SBY` -> confirm read-only notice is shown and Close/Lock/Reopen buttons are absent (server enforces `403`).
   - Resize to `390x844` -> confirm segmented tabs wrap cleanly and zero horizontal page overflow occurs.

## Product Owner Acceptance
- **Status:** `PENDING` (Owner review required; open policy decision `ATH-CR-013` for `OD-11`/`OD-13`).
