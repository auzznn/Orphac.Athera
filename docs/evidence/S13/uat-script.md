# ATH-S13 — Manual UAT & Demo Script (Sales Reservation, Pick and Dispatch)

**Slice:** S13 — Sales Reservation, Pick and Dispatch  
**Branch:** `greenfield/mvp`  
**Product Owner Acceptance:** `PENDING` (awaiting human four-pillar sign-off)

---

## Prerequisites & Local Startup

1. Ensure PostgreSQL 18 container (`athera-greenfield-pg`) is running on `127.0.0.1:55433`:
   ```bash
   docker start athera-greenfield-pg
   ```
2. Apply migrations through `M13_SalesReservationPickAndDispatch` and seed demo data:
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
| **U1 — Rina Utami** | `rina.utami@demo.athera.local` | Warehouse & Inventory Officer (`inventory.read`, `inventory.manage`) | None (`404 NotFound`) |
| **U2 — Budi Santoso** | `budi.santoso@demo.athera.local` | Group Controller (`inventory.manage`, `sales.read`, `sales.approve`) | Read-only (`inventory.read`, `sales.read`) |

---

## Step-by-Step Verification Flow

### 1. Inspect `OD-10` & `OD-09` Governance Rules & Reserve Unrestricted Stock
1. Sign in as **`rina.utami@demo.athera.local`** and navigate to `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/fulfillment`.
2. Click **`Lihat Aturan OD-10`** (`#ful-policy-toggle`) to expand the 4 governance cards covering `OD-10` strict no-negative/unrestricted stock rules, `FOR UPDATE` reservation concurrency serialization, atomic weighted-average COGS (`Dr 5100 / Cr 1300`) on partial dispatch, and `OD-09` downstream cancellation locks + POD delivery confirmation.
3. In **Antrean Pemenuhan SO** (`#ful-orders-table`), inspect **`SO-2026-00001`** (`PT-CUST-001 · Graha Ritel Nusantara`, `10 PCS ITM-S09-VALVE-01 · Katup Industri PN16 50mm`, `LOC-MAIN: 40 PCS`).
4. Click **`Reservasi Stok`** (`[data-testid="open-reserve-SO-2026-00001-SO-L001"]`), enter **`6`** (`PCS`) in `#ful-rsv-qty`, add a note in `#ful-rsv-notes`, and click **`Simpan Reservasi Stok (RSV)`** (`#ful-rsv-submit-btn`).
5. Confirm `RSV-2026-...` is created in **`Reservasi & Pick Gudang`** (`#ful-reservations-table`) with `6 PCS` reserved (`Siap Pick: 6`).

### 2. Create & Confirm Warehouse Pick Order (`PCK-YYYY-NNNNN`)
1. On the active `RSV-2026-...` row in `#ful-reservations-table`, click **`Buat & Konfirmasi Pick (PCK)`** (`[data-testid^="pick-rsv-RSV-2026-"]`).
2. Confirm **`PCK-2026-00001`** appears in `#ful-picks-table` with status **`Sudah Diambil (Siap Kirim)`** (`PICKED`, `Req: 6 · Picked: 6 · Rem: 6`).

### 3. Partial Shipment Dispatch (`SHP-YYYY-NNNNN`) & Atomic Weighted-Average COGS (`Dr 5100 / Cr 1300`)
1. On `PCK-2026-00001`, click **`Kirim Surat Jalan (SHP)`** (`[data-testid^="open-dispatch-PCK-2026-"]`).
2. Enter a physical delivery note reference in `#ful-disp-sj` (e.g., `SJ-JKT-2026-001`), carrier name in `#ful-disp-carrier`, vehicle number in `#ful-disp-vehicle`, and change the dispatch quantity `#ful-disp-qty-0` from `6` to **`4`** (`PCS`).
3. Confirm the live **`Estimasi Posting HPP (Dr 5100 / Cr 1300)`** updates to match `4 PCS × moving-average unit cost`.
4. Click **`Sahkan Pengiriman & Posting HPP`** (`#ful-disp-submit-btn`).
5. Confirm **`SHP-2026-00001`** appears in **Pengiriman, HPP & Bukti Kirim** (`#ful-shipments-table`) with status **`Terkirim (Dalam Perjalanan)`** (`DISPATCHED`), linked stock movement **`MOV-2026-...`**, and balanced GL journal **`JRN-2026-...`**.

### 4. Confirm Customer Delivery Evidence (`POD`) & Verify Partial Progress
1. On `SHP-2026-00001`, click **`Konfirmasi Serah Terima (POD)`** (`[data-testid^="open-deliver-SHP-2026-"]`).
2. Enter receiver name **`Tari Wibowo`** (`#ful-pod-receiver`) and POD reference **`POD-GRN-2026-001`** (`#ful-pod-ref`), then click **`Simpan Bukti Serah Terima (DELIVERED)`** (`#ful-pod-submit-btn`).
3. Confirm `SHP-2026-00001` transitions to **`Terkirim & Diterima (POD)`** (`DELIVERED`).
4. Switch back to **Antrean Pemenuhan SO** (`#ful-tab-orders`) and confirm `SO-2026-00001` shows **`Terkirim Sebagian`** (`PARTIALLY_DISPATCHED`, `Dipesan: 10 PCS · Reservasi: 2 · Terkirim: 4 · Sisa Alokasi: 4`).

### 5. Multi-Entity Read-Only Boundary (`NUSA-SBY`) & Mobile Layout
1. Sign out and sign in as **`budi.santoso@demo.athera.local`**, then navigate to `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102/fulfillment` (`NUSA-SBY`).
2. Confirm `#ful-readonly-banner` is visible, `SO-2026-00001` (`48 PCS`) is readable, and all reserve/release/pick/dispatch/POD mutation buttons are hidden.
3. Resize the viewport to `390×844` on `NUSA-JKT` and confirm stacked responsive rows render with zero horizontal overflow.
