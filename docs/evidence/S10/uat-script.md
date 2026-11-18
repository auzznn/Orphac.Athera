# S10 Product Owner UAT Script — Supplier Returns, Replacements & Financial Corrections

> **Acceptance Authority:** Executed for **Product Owner** review. Do not mark Slice S10 accepted without Product Owner sign-off in `docs/implementation/WORKSTREAM-STATE.md` and `docs/decisions/ATH-CR-007-OD05-OD07-supplier-returns-replacements-and-credits.md`.

## Prerequisites (Local Demo Environment)
1. Ensure local PostgreSQL 18 container (`athera-greenfield-pg`) is running on `127.0.0.1:55433`.
2. Apply migrations (`M01`–`M10`) and clean demo seed:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
     ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
     dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Seeded verification fixtures on `NUSA-JKT` (`PT Nusantara Distribusi Jakarta`, supplier `PT-SUP-001` — `PT Sumber Makmur Kemasan`, valid NPWP & PKP):
   - **`ITM-S10-SEAL-01` (`Seal Silikon Hidrolik Industrial`):**
     - **M1 Unvalued Dock Rejection Queue:** `GRN-2026-00005` (`SJ-JKT-DEMO-S10-01`) with `6 PCS` dock-rejected at Gate 1 (`eligibleQuantity = 6 PCS`, unit PO cost `Rp 75.000`).
     - **M3 Posted Bill Queue:** `BILL-2026-00001` (`INV-JKT-S10-M3-01`, `14 PCS @ Rp 75.000 + PPN-IN-11`, `WithholdingTaxAmount = 0`, unpaid `AP-2026-00001`).
   - **`ITM-S10-GASKET-02` (`Gasket PTFE Flange 4 Inch`):**
     - **M2 Valued Unbilled Queue:** `GRNI-2026-00005` (`GRN-2026-00006`, `SJ-JKT-DEMO-S10-02`) with `30 PCS @ Rp 120.000` accepted unbilled GRNI & `30 PCS` unrestricted stock in `LOC-MAIN`.
   - Demo users: `U1` (`rina.utami@demo.athera.local`), `U2` (`budi.santoso@demo.athera.local`, four-eyes approver & `ap.post` on `NUSA-JKT`, read-only on `NUSA-SBY`), `U4` (`siti.rahayu@demo.athera.local`, `AP_CLERK` on `NUSA-JKT`).

## UAT Test Cases

### UAT-S10-01 — M1 Unvalued Dock Rejection, Linked `RPA` Replacement & Gate 2 Valuation (`U1` -> `U2` -> `U1`)
1. Sign in as `rina.utami@demo.athera.local` / `AtheraDemo!2026` on **NUSA-JKT** and open **Returns & Credits (`/returns`)**.
2. Click **Lihat Matriks Kebijakan OD-05/OD-07 (`M1`–`M7`)**.
   - **Expected:** Displays all 7 matrix rules (`M1`–`M3` supported, `M4`–`M5` `Diblokir OD-05`, `M6` `Diblokir OD-07`, `M7` `Periode Ditutup`), localized with zero raw enum tokens.
3. Under **M1 · Penolakan Dermaga Tanpa Nilai**, click **Buat Retur** on `ITM-S10-SEAL-01` (`6 PCS` rejected). Select `Penggantian Barang (RPA)`, enter `2 PCS`, preview impact -> **Expected:** `M1 · Didukung`, Inventory/GRNI/Credit = `Rp 0`, zero GL entries.
4. Click **Simpan Draf Retur** -> open detail sheet `DRT-YYYY-NNNNN` -> click **Ajukan Persetujuan**.
   - **Expected:** Status becomes `Menunggu Persetujuan`; four-eyes `OD-02` notice (`ret-sod-notice`) forbids `U1` self-approval.
5. Sign in as `budi.santoso@demo.athera.local` (`U2`), open `DRT-...` under tab **Otorisasi Retur (`SRN`)**, and click **Setujui Retur**.
6. Sign back in as `U1`, execute `DRT-...` -> **Expected:** Status becomes `Dieksekusi` (`SRT-...`), zero GL journals posted (`GL = null`), and **`RPA-2026-00001`** (`2 / 2 PCS`, `AP Delta: Rp 0`, `Authorized Cost: Rp 75.000`) is issued.
7. Under tab **Penggantian Barang (`RPA`)**, click **Terima Pengganti**, record `2 PCS` with replacement delivery note `SJ-RPA-...`, navigate to **Penerimaan Gudang (`/receiving`)**, confirm amber `Pengganti · RPA-2026-00001` badge with `RotateCcw` icon on both queue and detail header, release `2 PCS` at Gate 1, and complete Gate 2 acceptance -> **Expected:** Posts **`Rp 150.000`** positive valuation (`ACC-... · MOV-... · GRNI-... · JRN-...`).

### UAT-S10-02 — M2 Valued Unbilled Return, Compensating GRNI Reversal & Outbound Stock (`U1` -> `U2` -> `U1`)
1. As `U1` on `/returns`, click **Buat Retur** under **M2 · Penerimaan Bernilai Belum Ditagih** for `ITM-S10-GASKET-02`, choose `Tutup Tanpa Pengganti`, enter `5 PCS`, and run preview.
   - **Expected:** Previews `Dr 2110 GRNI Rp 600.000` / `Cr 1300 Persediaan Rp 600.000` (`5 * Rp 120.000`), open period.
2. Save draft, submit as `U1`, approve as `U2` (`OD-02`), then execute on `2026-11-11` as `U1`.
   - **Expected:** `SRT-...` executes atomically (`MOV-...`, `GL JRN-...`), reverses `Rp 600.000` unbilled GRNI, and open unbilled capacity reduces by `5 PCS`.
3. Open **Persediaan (`/inventory`) -> Pergerakan Stok**:
   - **Expected:** Displays signed **`-5`** outbound movement `PURCHASE_RETURN_OUTBOUND` (`Pengeluaran Retur ke Pemasok (S10)`).

### UAT-S10-03 — M4 Policy Block, OD-07 Nota Retur Requirement & M3 Credit Note (`U4` -> `U2`)
1. Sign in as `siti.rahayu@demo.athera.local` (`U4`), open `/returns`, and click **Buat Retur** on **M3 · Faktur AP Terposting** (`BILL-2026-00001`, `INV-JKT-S10-M3-01`).
2. Select `Penggantian Barang (RPA)` and click **Pratinjau Dampak GL & Kebijakan**.
   - **Expected:** `M4 · Diblokir OD-05` alert explains replacement is rejected after AP posting; preview amounts are `Rp 0`, Credit Note journal lines are suppressed, and `Simpan Draf Retur` is demoted.
3. Switch to `Nota Kredit Pemasok (SCN)`, enter `2 PCS`, leave `Nomor Nota Retur Pajak` blank, save draft, and click **Ajukan Persetujuan**.
   - **Expected:** Submission fails closed with `OD-07` `Nota Retur` required error (`409 od07_nota_retur_required`).
4. Click **Perbarui Draf**, enter `NR-JKT-S10-...`, save and submit (`PENDING_APPROVAL`; `U4` sees self-approval SoD notice).
5. Sign in as `U2`, approve and execute `M3` return on `2026-11-11`.
   - **Expected:** Append-only **`SCN-2026-00001`** is recorded (`DPP Rp 150.000`, `PPN 1410 Rp 16.500`, `Dr 2100 Rp 166.500`), `TaxDetermination` records `PPN_INPUT_REVERSAL`, and **Hutang Usaha (`/payables`) -> Sub-Buku Besar AP** shows **`Dikreditkan: -Rp 166.500`** on `AP-2026-00001`.

### UAT-S10-04 — Fail-Closed Entity Read-Only Boundary (`NUSA-SBY`) & Mobile Responsive Layout (`390x844`)
1. Switch to `NUSA-SBY` as `budi.santoso@demo.athera.local` -> navigate to `/returns`.
   - **Expected:** Read-only banner visible; all `Buat Retur` and `Terima Pengganti` controls absent/hidden.
2. Open `NUSA-JKT` `/returns` at `390x844` mobile width, expand policy matrix and open live preview -> **Expected:** Zero horizontal overflow; matrix & GL preview rows stack cleanly with inline field labels.
