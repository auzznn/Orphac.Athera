# ATH-S05 Manual UAT Script — Purchase Orders, Approved Revisions and Four-Eyes

## Preconditions
1. Ensure disposable PostgreSQL 18 is running on `127.0.0.1:55433`:
   ```bash
   ./scripts/dev/bootstrap-postgres.sh
   ```
2. Apply migrations (`M00`–`M05`) and seed S05 tolerance policy (`EXACT_QTY_OD01_SAFE`), `PURCHASE_ORDER_COMMITMENT` approval policy, separated SoD role grants, and initial `PO-2026-00001` fixtures:
   ```bash
   export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
   export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
   export ASPNETCORE_ENVIRONMENT=Development
   export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

   dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Run ASP.NET Core API (`127.0.0.1:5200` as low-privilege `athera_app`) and Next.js web app (`127.0.0.1:3200`).

---

## Case 1 — Multi-Line PO Draft, Submit & Four-Eyes Self-Approval Lock (`U1` -> `U2` on `NUSA-JKT`)
1. Sign in as `rina.utami@demo.athera.local` (`ProcurementOfficer` on `NUSA-JKT`).
2. Click **Pengadaan** in the left sidebar (`/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/procurement`).
3. Switch to tab **Buat & Edit Draf PO**:
   - Supplier: `PT-SUP-001 — PT Sumber Makmur Kemasan`
   - Delivery Destination: `Gudang E2E S05 Jakarta`
   - Line 1: `ITM-PKG-001` · `BOX` (`x24`) · Qty `3` (`72 PCS`) · Unit Price `480000` · Tax `PPN-12-DPP1112`
   - Line 2: `ITM-RES-002` · `BOX` (`x36`) · Qty `2` (`72 PCS`) · Unit Price `350000` · Tax `PPN-IN-11`
4. Verify live exact decimal preview displays Subtotal `Rp 2.140.000`, PPN `Rp 235.400`, Grand Total `Rp 2.375.400`, and `OD-01` notice locking tolerance at `0%`.
5. Click **Simpan & Ajukan Persetujuan**.
   - **Expected:** Assigns sequential PO number (`PO-2026-...`), freezes Rev 1 snapshot with `Hash Snapshot`, displays four-eyes warning banner blocking self-approval, and renders no **Setujui Revisi PO** button for `Rina`.

## Case 2 — Independent Approval, Controlled Revision Diff & Immutable Snapshot (`U2` + `U1`)
1. Sign out and sign in as `budi.santoso@demo.athera.local` (`procurement.approve` on `NUSA-JKT`).
2. Open the newly submitted PO via **Tinjau & Putuskan**, enter decision reason, and click **Setujui Revisi PO**.
   - **Expected:** PO status updates to **Disetujui**, `Ringkasan Komitmen Penerimaan` shows `144` base units open, `Belum Diterima`.
3. Sign in as `Rina`, open the approved PO, enter amendment reason `Kenaikan qty 4 box & harga 495rb`, click **Buat Draf Revisi Baru**.
4. In Editor (`Rev 2`), change Line 1 Qty to `4` and Unit Price to `495000`, and click **Simpan & Ajukan Persetujuan**.
5. Sign in as `Budi`, open the PO via **Tinjau & Putuskan**.
   - **Expected:** **Perbandingan Perubahan Revisi (`Rev 1 -> Rev 2`)** highlights `L001` `Diubah` (`3 BOX @ Rp 480.000` -> `4 BOX @ Rp 495.000`). Click **Setujui Revisi PO**, then select the **`Rev 1 · Digantikan`** pill and verify Rev 1 remains unchanged (`Rp 480.000`, original `SnapshotHash`).

## Case 3 — Authorized Short-Close & Downstream Dependency Cancellation Guard
1. On `NUSA-JKT` as `Rina`, open an approved PO with no pending/draft revisions, enter justification, and click **Tutup Sisa Komitmen (Short-Close)**.
   - **Expected:** PO transitions to **Ditutup**, Receiving Progress shows **Ditutup Cepat (Short-Close)**, and Remaining Open Commitment drops to `0`.
2. In PostgreSQL integration verification (`S05_API_04`), sending `DELETE /purchase-orders/{id}` fails `409 po_delete_forbidden`, and attempting **Cancel** after registering a downstream reference fails `409 po_has_downstream_references`.

## Case 4 — Read-Only (`NUSA-SBY`), Unauthorized (`SMDR-BDG`), and PostgreSQL Freeze Triggers
1. Sign in as `budi.santoso@demo.athera.local`, open `NUSA-SBY` `/procurement`, and click **Lihat Rincian & Riwayat** on `PO-2026-00001`.
   - **Expected:** Amber **Mode baca saja** banner, zero Editor tab, zero approve/amend/close/cancel controls, and immutable **Hash Snapshot** displayed.
2. Navigate as `Budi` to `SMDR-BDG` `/procurement` -> **Expected:** `403 Izin Entitas Tidak Mencukupi`.
3. Sign in as `Rina` and navigate to `SMDR-BDG` `/procurement` -> **Expected:** `404 Ruang Kerja Tidak Tersedia` with zero `SMDR` disclosure.
4. Under runtime role `athera_app`, `UPDATE procurement.purchase_order_revisions SET subtotal_amount = 1` on a frozen revision fails with SQLSTATE `55000` (`po_revision_frozen_immutable`), and `DELETE FROM procurement.purchase_orders` fails with SQLSTATE `42501`.
