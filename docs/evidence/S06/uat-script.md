# UAT Script — Slice S06: Dock Custody and Receiving Gate One (Unvalued)

**Slice Status:** ENGINEERING_PASS (Pending Product Owner Sign-Off)  
**Primary BRS Requirements:** `BRS-RCV-001`, `BRS-RCV-002`, `BRS-RCV-003`, `BRS-RCV-004`, `BRS-RCV-006`, `OD-01`, `UAT-P2P-006`, `UAT-P2P-007`, `UAT-P2P-008`, `UAT-P2P-009`

---

## Preconditions

- PostgreSQL 18 migrated through `20261117214046_M06_DockCustodyAndReceivingGateOne` and seeded with demo fixtures (`--migrate --seed-demo`).
- Demo accounts (`AtheraDemo!2026`):
  - **U1 (`rina.utami@demo.athera.local`)**: holds `receiving.read` + `receiving.manage` on `NUSA-JKT` only (zero access on `NUSA-SBY` and `SMDR-BDG`).
  - **U2 (`budi.santoso@demo.athera.local`)**: holds `receiving.read` + `receiving.manage` on `NUSA-JKT`, `receiving.read` only on `NUSA-SBY` (where `SJ-SBY-DEMO-001` is pre-seeded in `DOCK_CHECK_PENDING`), and zero receiving access on `SMDR-BDG`.

---

## Test Cases

### UAT-S06-01 — Gate 1 Physical Arrival & Unvalued Rejection Conservation (`BRS-RCV-001..004`)
1. Sign in as **U1 (`rina.utami@demo.athera.local`)** and open `/w/.../NUSA-JKT/receiving` (**Penerimaan Dermaga & Gate 1**).
2. Confirm the KPI strip displays **`Jurnal GL / GRNI Terbentuk: 0` / `Tanpa Posting GL / GRNI`**.
3. Switch to **Catat Kedatangan (Gate 1)**, choose an approved PO with `2 BOX = 48 PCS` of `ITM-PKG-001`, enter unique *Nomor Surat Jalan* (`SJ-JKT-...-A`), station (`Dermaga Utama Jakarta · Pintu 2`), and deliver `1 BOX` (`= 24 PCS`).
4. Click **Catat Kedatangan Gate 1 (Tanpa Valuasi)** and verify:
   - Receipt `GRN-YYYY-NNNNN` is created in `ARRIVED` custody status.
   - `Total Valued Accepted = 0`, `GL / GRNI Journals = 0`.
5. On the line dock inspection card, enter `Pending = 0`, `Held = 0`, `Released = 20`, `Rejected = 4`, choose `DAMAGED_PACKAGING`, enter inspection notes & evidence ref, verify **Conservation Law** shows `24 / 24 PCS · Seimbang`, and submit.
6. Confirm append-only `dockChecks` history appends the immutable check record, `DOCK_REJECTED` stays unvalued, and `GL / GRNI Journals = 0`.

### UAT-S06-02 — OD-01 Strict Overdelivery Hold & Release Block (`OD-01`, `BRS-RCV-006`)
1. On the same PO (already 20 PCS released/active, 4 PCS rejected unvalued -> `28 PCS` remaining cap), record a second delivery `2 BOX = 48 PCS` (`SJ-JKT-...-B`).
2. Confirm the Arrival composer warns `20 PCS` overdelivery excess before submission.
3. Submit Arrival #2 and confirm line card displays `OD-01 +20 PCS` and `maxApprovedReleaseCapBaseQty = 28 PCS`.
4. Attempt to release `30 PCS` (leaving `18 PCS` held): confirm client cap alert appears AND server blocks release with `409 od01_overdelivery_release_blocked`.
5. Click **Lepas Sesuai Batas PO** (`28 PCS` released, `20 PCS` held in `DOCK_HELD` under `OVERDELIVERY_OD01_HOLD`), submit, and verify zero GL/GRNI journals remain in `/finance`.

### UAT-S06-03 — Read-Only View (`NUSA-SBY`) & Hostile Cross-Tenant Isolation (`SMDR-BDG`)
1. Sign in as **U2 (`budi.santoso@demo.athera.local`)** and open `NUSA-SBY` `/receiving`.
2. Confirm the read-only banner is displayed, the Arrival tab and Dock Check submit controls are absent, and seeded receipt `SJ-SBY-DEMO-001` (`24 PCS`, `DOCK_CHECK_PENDING`) is visible with `0` GL journals.
3. Navigate as **U2** to `SMDR-BDG` `/receiving` and confirm `403` insufficient permission state renders cleanly with zero receipt data.
4. Sign in as **U1** and navigate directly to `SMDR-BDG` `/receiving`; confirm `404` scope unavailable renders with zero `SMDR`, `Samudra`, or `SJ-SBY` data leakage.

---

## Owner Acceptance Block

- **Product Owner Decision:** PENDING (awaiting PO acceptance review — DO NOT SELF-APPROVE)
- **Reviewer Name:** ____________________
- **Signature / Date:** ____________________
