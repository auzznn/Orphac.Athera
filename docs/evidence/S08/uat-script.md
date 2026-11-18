# S08 Product Owner UAT Script — Stock Availability, Locations and Quality Restrictions

> **Acceptance Authority:** Executed for **Product Owner** review. Do not mark Slice S08 accepted without Product Owner sign-off in `docs/implementation/WORKSTREAM-STATE.md` and `docs/decisions/ATH-CR-005-OD10-negative-stock-and-reservation-policy.md`.

## Prerequisites (Local Demo Environment)
1. Start local PostgreSQL 18 container (`athera-greenfield-pg` on `127.0.0.1:55433`).
2. Apply migrations & clean demo seed:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
     ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
     dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Seeded verification fixtures on `NUSA-JKT` (`WH-MAIN` with default zones + storage location `LOC-RACK-B2`, item `ITM-CHEM-LOT-01` (`LotTrackingPolicy = REQUIRED`), lot `LOT-JKT-2611-01` `20 PCS` in `QUALITY_INSPECTION` at `LOC-QC`, open inspection `QCI-2026-00001` `PENDING`). `NUSA-SBY` starts with `0` stock positions.

## UAT Test Cases

### UAT-S08-01 — Lot Tracking Policy on Catalog & Lot-Tracked Quarantine Receipt in Gate 2
1. Sign in as `rina.utami@demo.athera.local` / `AtheraDemo!2026` (has `inventory.manage` without `quality.inspect`).
2. Open **NUSA-JKT** -> **Catalogue & UoM** (`Katalog & Satuan`). Create a stock item with **Lot Tracking Policy = Required Lot (`REQUIRED`)** (and verify non-stock items force `NONE`).
3. Create & approve a PO (`budi.santoso@demo.athera.local` four-eyes approval), record Gate 1 physical arrival + release `20 PCS` for the new item.
4. On **Dock Receiving** -> **Gate 2**, select Destination Stock Status = **Quality Inspection Hold (`QUALITY_INSPECTION`)**, enter lot number + expiry date, preview GL & moving-average impact, and confirm Valued Acceptance.
5. **Expected:** Gate 2 posts atomically (`LOC-QC`, `QUALITY_INSPECTION`, lot number saved, pending inspection `QCI-2026-00002` created). On **Inventory**, `OnHand = 20 PCS`, `Quality Inspection = 20 PCS`, **`Available = 0 PCS`**.

### UAT-S08-02 — U1 / U2 Segregation of Duties (Quality Inspection vs Warehouse Operations)
1. Still signed in as `rina.utami@demo.athera.local` (`U1`), open **Inventory** and inspect the new `REQUIRED`-lot item.
2. **Expected:**
   - **Quality Disposition** form is locked behind a visible read-only banner (`quality.inspect` required).
   - Empty transfer/reservation selects show explicit disabled explanations rather than blank dropdowns.
3. Sign out and sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026` (`U2`, Group Controller with `quality.inspect` + `inventory.manage`).

### UAT-S08-03 — Partial & Final Quality Disposition, Intra-Warehouse Transfer & OD-10 Explicit Reservation
1. Open the quarantined item's stock detail sheet as `U2` on **Inventory**.
2. Submit a split **Quality Disposition** on `QCI-2026-00002`: Release `12 PCS` to `LOC-MAIN` (`UNRESTRICTED`) and Block `3 PCS` to `LOC-BLK` (`BLOCKED`).
   - **Expected:** `OnHand = 20`, `Unrestricted = 12`, `QI = 5`, `Blocked = 3`, `Available = 12 PCS`, **`Reconciled`** badge holds, carrying value unchanged (`Rp 7.000.000`), zero extra GL entries.
3. Submit a second disposition releasing the remaining `5 PCS` QI to `LOC-MAIN` -> **Expected:** `Unrestricted = 17`, `QI = 0`, `Available = 17 PCS`, inspection `COMPLETED`.
4. Transfer `4 PCS` `UNRESTRICTED` from `LOC-MAIN` -> `LOC-RACK-B2` -> **Expected:** `LOC-MAIN = 13`, `LOC-RACK-B2 = 4`, carrying value unchanged.
5. Attempt an **Explicit Reservation (OD-10)** above `LOC-MAIN` available stock (`20 PCS`) -> **Expected:** Hard `409` OD-10 block; stock balances unchanged. Reserve `6 PCS` against `SALES_ORDER` (`SO-S08-...`) -> **Expected:** `Reserved = 6 PCS`, `Available = 11 PCS`.
6. Release `2 PCS` then remaining `4 PCS` (`Release All`) -> **Expected:** `Reserved = 0`, `Available = 17 PCS`. Switch **State & Movement Timeline** + toggle **English** (`EN`) -> **Expected:** All `-Q` and `+Q` movement deltas (`-15 PCS`, `-5 PCS`, `-4 PCS`, `+4 PCS`, `+20 PCS`), badges, and banners render cleanly in English; mobile `390x844` has zero clipping.

### UAT-S08-04 — Cross-Entity Empty Isolation & Hostile Cross-Tenant Denial
1. Switch workspace scope to `NUSA-SBY` and open **Inventory** -> **Expected:** `0` stocked items, zero `NUSA-JKT` locations/lots/inspections visible.
2. Sign in as `rina.utami@demo.athera.local` and navigate directly to `/w/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01/bbbbbbbb-bbbb-bbbb-bbbb-bbbbb101/inventory` -> **Expected:** Default-deny unavailable scope alert (`404`) with zero tenant leakage.
