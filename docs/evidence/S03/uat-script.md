# ATH-S03 Manual UAT Script — Partners, Catalogue and Unit of Measure

## Preconditions
1. Ensure disposable PostgreSQL 18 is running on `127.0.0.1:55433`:
   ```bash
   ./scripts/dev/bootstrap-postgres.sh
   ```
2. Apply migrations (`M00`–`M03`) and seed S03 demo users, partners, UoMs, and items:
   ```bash
   export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
   export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
   export ASPNETCORE_ENVIRONMENT=Development
   export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

   dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Run ASP.NET Core API (`127.0.0.1:5200` as low-privilege `athera_app`) and Next.js web app (`127.0.0.1:3200`).

---

## Case 1 — Dual Partner Creation, Contacts & Entity Eligibility (`U1` on `NUSA-JKT`)
1. Sign in as `rina.utami@demo.athera.local` / `AtheraDemo!2026`.
2. Open `NUSA-JKT` -> click **Mitra Bisnis** in the left rail (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/partners`).
3. Verify the **Catatan kebijakan OD-07** notice clearly states NPWP and PKP status are stored as master-data metadata only (tax rate calculation lives in tax module S04+).
4. Under **Daftarkan Mitra Bisnis Baru**, create a dual supplier + customer partner (`PT-TEST-01`, Legal Name `PT Karya Niaga Test`, check **Pemasok** & **Pelanggan**, Cakupan Entitas `RESTRICTED_ENTITIES`), click **Simpan Mitra Baru**.
5. In the detail pane, add primary contact `Dian Permata` (`dian@karyaniaga.demo.local`) and save `NUSA-JKT` eligibility (`Izinkan sebagai Pemasok` + `Izinkan sebagai Pelanggan`, payment terms `0..365` days) via **Simpan Profil Entitas**.
6. **Expected:** Partner row shows **Pemasok & Pelanggan**, **Bisa Beli (AP)**, and **Bisa Jual (AR)** on `NUSA-JKT`. Inline validation triggers cleanly per field (`aria-invalid` + crimson border) if required fields or invalid payment terms are submitted.

## Case 2 — Item Catalogue, Exact UoM Conversion `3 BOX = 72 PCS` & Discrete Guard (`U1` on `NUSA-JKT`)
1. Click **Katalog & Satuan** in the left rail (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/catalog`).
2. Verify seeded UoMs (`PCS` scale `0`, `BOX` scale `0`, `KG` scale `3`).
3. Under **Daftarkan Barang / Jasa Baru**, create a `STOCKED_GOOD` item (`ITM-TEST-01`) with Base UoM `PCS`, Purchase UoM `BOX`, Sales UoM `PCS`, and `RESTRICTED_ENTITIES`.
4. Add explicit conversion `BOX -> PCS` factor `24` via **Simpan Konversi**.
5. Under **Uji Konversi Desimal Eksak**, select `BOX`, enter quantity `3`, and click **Hitung ke Satuan Dasar**.
6. **Expected:** Preview displays `Hasil Dasar Eksak: 3 BOX = 72 PCS` and `Balik ke Satuan Asal: 3 BOX`. Trying to convert an unconfigured UoM or a fractional discrete quantity returns an inline error (`unsupported_uom_conversion` / `fractional_discrete_base_quantity`).
7. Save `NUSA-JKT` item policy (`Izinkan Pengadaan` + `Izinkan Penjualan`).

## Case 3 — Sibling Entity Read-Only Restriction & Foreign-Tenant Denial (`U2` on `NUSA-SBY`, `U1` on `SMDR-BDG`)
1. Sign out and sign in as `budi.santoso@demo.athera.local`.
2. Navigate to `NUSA-SBY` Partners (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102/partners`).
3. **Expected:** Amber **Mode baca saja** banner appears, no Create Partner form renders, row action reads **Lihat Rincian**, restricted partner `PT-DUAL-001` shows **Tidak Layak di Entitas Ini**, and clicking **Lihat Rincian** opens read-only contacts & **Kelayakan Entitas** without mutation controls.
4. Navigate to `NUSA-SBY` Catalogue (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102/catalog`).
5. **Expected:** Read-only mode hides mutations, row action reads **Lihat UoM & Kebijakan**, restricted item `ITM-RES-002` shows **Diblokir di Entitas Ini**, and opening **Lihat UoM & Kebijakan** displays read-only scope/eligibility status.
6. Sign out, sign back in as `rina.utami@demo.athera.local`, and open foreign workspace URL `/w/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101/partners`.
7. **Expected:** Fails safely with **Ruang Kerja Tidak Tersedia** (`404`) and zero leak of `SMDR` or foreign partner names.

## Case 4 — Non-Hard-Delete PostgreSQL Proof (`athera_app`)
With an active low-privilege `athera_app` transaction (`SET LOCAL app.tenant_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'`):
```sql
DELETE FROM partners.partners;       -- Expect ERROR: permission denied (42501)
DELETE FROM catalog.items;           -- Expect ERROR: permission denied (42501)
```
