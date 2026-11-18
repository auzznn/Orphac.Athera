# ATH-S12 — Manual UAT & Demo Script (Customer Sales Orders and Commercial Commitments)

**Slice:** S12 — Customer Sales Orders and Commercial Commitments  
**Branch:** `greenfield/mvp`  
**Product Owner Acceptance:** `PENDING` (awaiting human four-pillar sign-off)

---

## Prerequisites & Local Startup

1. Ensure PostgreSQL 18 container (`athera-greenfield-pg`) is running on `127.0.0.1:55433`:
   ```bash
   docker start athera-greenfield-pg
   ```
2. Apply migrations through `M12_CustomerSalesOrdersAndCommercialCommitments` and seed demo data:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
   ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
   ConnectionStrings__App="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw" \
   dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Run the ASP.NET Core 10 API (`http://127.0.0.1:5100`) and Next.js 16.4.6 web application (`http://127.0.0.1:3000`).

---

## Demo Personas (`Password: AtheraDemo!2026`)

| Persona | Email | Role on `NUSA-JKT` (`aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101`) | Role on `NUSA-SBY` (`aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102`) |
|---|---|---|---|
| **U5 — Dewi Lestari** | `dewi.lestari@demo.athera.local` | `SalesOfficer` (`sales.read`, `sales.manage`, `inventory.read`) | None (`403 Forbidden`) |
| **U2 — Budi Santoso** | `budi.santoso@demo.athera.local` | `EntityApprover` (`sales.read`, `sales.approve`) | Read-only (`sales.read`) |

---

## Step-by-Step Verification Flow

### 1. Inspect `OD-09` Governance Rules & Self-Approval SoD Lock (`OD-02`)
1. Sign in as **`dewi.lestari@demo.athera.local`** and navigate to `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/sales-orders`.
2. Click **`Lihat Aturan OD-09`** (`#so-policy-toggle`) to expand the 4 governance cards covering customer/item eligibility, `OD-09` safe commercial defaults (`discount = 0`, no automated credit-limit override), live read-only stock availability + `OUTPUT` VAT (`OD-07`), and immutable revision snapshots (`OD-02`).
3. Verify the seeded committed order **`SO-2026-00001`** (`APPROVED`, `PT-CUST-001`, `10 PCS ITM-S09-VALVE-01 @ Rp 95.000` + `PPN-OUT-11` = `Rp 1.054.500`) and the seeded pending draft (`SO-DRF-...`, `PENDING_APPROVAL`, `PT-DUAL-001`, `Rp 999.000`).
4. Click **`Detail & Revisi`** on the seeded `SO-DRF-...` prepared by Dewi.
5. Confirm that `#so-detail-sheet` displays the **`[data-testid="so-sod-notice"]`** banner and omits `#so-approve-btn` / `#so-reject-btn` because Dewi submitted the revision.

### 2. Draft & Submit a New Sales Order with Live Stock Availability & Output VAT
1. Click **`+ Buat Pesanan Penjualan Baru`** (`#so-new-draft-btn`) to open the **Editor SO & Revisi** tab (`#so-panel-editor`).
2. Select customer **`PT-CUST-001 · Graha Ritel Nusantara`**, set **`Target Tanggal Kirim`** (`#so-ed-req-date`) to `2026-11-26`, enter **`Referensi PO Pelanggan`** (`#so-ed-cust-ref`), and enter **`Alamat Pengiriman Pelanggan`** (`#so-ed-address`).
3. On Line 1, select **`ITM-S09-VALVE-01 · Katup Industri PN16 50mm`**. Confirm the live read-only availability caption shows `Fisik: 40 · Bebas: 40 PCS` without mutating or reserving inventory.
4. Enter quantity **`4`** (`PCS`), unit price **`95000`**, and Output VAT **`PPN-OUT-11 (11%)`**.
5. Confirm the live totals strip shows:
   - **Total Kuantitas Basis:** `4`
   - **Subtotal DPP:** `Rp 380.000`
   - **Total PPN Keluaran:** `Rp 41.800`
   - **Total Komitmen SO:** `Rp 421.800`
6. Click **`Simpan Draf SO`** (`#so-save-draft-btn`). Verify the draft receives a `SO-DRF-...` draft code (`SoNumber` is still unassigned).
7. Click **`Ajukan Persetujuan Empat Mata`** (`#so-submit-btn`). Verify the revision freezes (`isFrozen = true`, status `PENDING_APPROVAL`) and displays the `OD-02` self-approval lock notice.

### 3. Independent Four-Eyes Approval & Official `SO-2026-00002` Commitment
1. Sign out and sign in as **`budi.santoso@demo.athera.local`** (Group Controller with `sales.approve` on `NUSA-JKT`).
2. Open `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/sales-orders` and switch to **`Antrean Persetujuan Empat Mata`** (`#so-tab-queue`).
3. Open Dewi's submitted draft, enter an approval reason in `#so-decision-reason`, and click **`Setujui & Sahkan SO`** (`#so-approve-btn`).
4. Confirm the order transitions to **`APPROVED`** and is assigned the gap-free entity-year commitment number **`SO-2026-00002`**.

### 4. Amendment Revision (`Rev 2`), Pending Queue Total Separation & Baseline Diff
1. Sign back in as **`dewi.lestari@demo.athera.local`**, open **`SO-2026-00002`**, enter an amendment reason in `#so-amend-reason`, and click **`Buat Draf Amandemen`** (`#so-amend-btn`).
2. Update Line 1 quantity from `4` to **`6`** (`PCS`) and click **`Simpan Draf SO`** (`#so-save-draft-btn`).
3. Verify `#so-revision-diff` shows `Rev 1 → Rev 2`, **`Selisih Nilai Total: Rp 210.900`**, **`Selisih Kuantitas Basis: 2`**, and `4 PCS → 6 PCS (Rp 570.000)`.
4. Click **`Ajukan Persetujuan Empat Mata`** (`#so-submit-btn`).
5. Confirm that while `Rev 2` is `PENDING_APPROVAL`:
   - The main Orders Ledger row and KPI strip still show the active approved **`Rev 1`** commitment (`Rp 421.800`, `Rev 1 · Basis: 4`) alongside the amber **`Rev 2 · Revisi Terbuka`** badge.
   - The **Four-Eyes Approval Queue** (`#so-queue-table`) shows the pending **`Rev 2`** total (`Rp 632.700`, `Rev 2`).
6. Sign in as **`budi.santoso@demo.athera.local`**, open `SO-2026-00002` from `#so-tab-queue`, use `#so-rev-select` to verify **`Rev 1 (Disetujui)`** remains immutable at `Rp 421.800` with its original SHA-256 snapshot hash, switch back to **`Rev 2`**, enter a decision reason, and click **`Setujui & Sahkan SO`**.
7. Confirm `SO-2026-00002` advances its active commitment to **`Rev 2`** (`Rp 632.700`, `6 PCS`), updating `Komitmen Disetujui Aktif` to **`Rp 1.687.200`** (`16` base units).

### 5. Multi-Entity Read-Only Boundary (`NUSA-SBY`) & Mobile Layout
1. While signed in as **`budi.santoso@demo.athera.local`**, switch to **`NUSA-SBY`** (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102/sales-orders`).
2. Confirm the read-only banner (`#so-readonly-banner`) is displayed, `#so-tab-editor` and `#so-new-draft-btn` are absent, and `SO-2026-00001` (`Rp 1.332.000`) can be inspected in read-only mode.
3. Resize the viewport to `390×844` on `NUSA-JKT` and verify stacked card rows render with zero horizontal overflow.
