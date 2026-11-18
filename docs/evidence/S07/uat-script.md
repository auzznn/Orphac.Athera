# S07 Product Owner UAT Script — Valued Acceptance, Moving-Average Inventory and GRNI

> **Acceptance Authority:** Executed for **Product Owner** review. Do not mark Slice S07 accepted without PO sign-off in `docs/implementation/WORKSTREAM-STATE.md` and `docs/decisions/ATH-CR-004-OD04-moving-average-valuation-edge-cases.md`.

## Prerequisites (Local Demo Environment)
1. Start local PostgreSQL 18 container (`athera-greenfield-pg` on `127.0.0.1:55433`).
2. Apply migrations & clean demo seed:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
     ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
     dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Run API on `http://127.0.0.1:5200` and Next.js Web on `http://127.0.0.1:3200`.

## UAT Test Cases

### UAT-S07-01 — Closed-Period & OD-04 Block in Gate 2 Preview
1. Sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026` at `http://127.0.0.1:3200/login`.
2. Open **NUSA-JKT** -> **Dock Receiving** (`Penerimaan Gudang`).
3. Click **View Details** on seeded Gate 1 released receipt `SJ-JKT-DEMO-S07-02` (`10 PCS` `ITM-STL-PLATE-10`).
4. Set **Valued Posting Date** to `2026-10-15` (closed fiscal period `2026-10`) and click **Calculate Valuation & GL Preview**.
5. **Expected:** Preview displays policy block (`CLOSED` period `2026-10`); **Confirm Valued Acceptance** stays disabled; zero stock movements, GRNI accruals, or GL journals are posted.

### UAT-S07-02 — Preview-Before-Confirm Two-Batch Moving-Average Recalculation & Atomic GRNI/GL Posting
1. Change **Valued Posting Date** to `2026-11-10`, keep **Destination Stock Status** = `Unrestricted Available`, keep quantity = `10`, and click **Calculate Valuation & GL Preview**.
2. **Expected:** Preview table shows:
   - Before Pool: `10 PCS` @ `Rp 850.000` (`Rp 8.500.000` carrying value)
   - New Batch: `+10 PCS` @ `Rp 950.000` (`Rp 9.500.000`)
   - Projected Pool: `20 PCS` @ **`Rp 900.000 / PCS`** (`Rp 18.000.000` carrying value)
   - Balanced GL Journal Preview: `Debit 1300 (Inventory Asset)` `Rp 9.500.000` / `Credit 2110 (GRNI Clearing)` `Rp 9.500.000`.
3. Click **Confirm Valued Acceptance**.
4. **Expected:** Success banner displays atomic references (`ACC-2026-00002`, `MOV-2026-00002`, `GRNI-2026-00002`, `JRN-2026-11-00003`); Gate 1 GL counter badge remains `0`; Linked Gate 2 section records the posting.

### UAT-S07-03 — Inventory Balances, Sequential Item Valuation Ledger & Open GRNI
1. Click **Inventory** (`Persediaan`) in the left rail (or the banner link from step UAT-S07-02).
2. **Expected:**
   - KPI summary displays `1` stocked item with balance, `Rp 18.000.000` Total Carrying Value, `Rp 18.000.000` Open GRNI Liability.
   - `ITM-STL-PLATE-10` row displays `20 PCS`, `Rp 900.000 / PCS`, `Rp 18.000.000`, status `Pool Active`.
   - **Item Moving-Average Valuation Ledger** below displays sequential entries `#1` (`850.000`) and `#2` (`850.000 -> 900.000`) linked to `MOV-*` and `JRN-*`.
3. Switch to **Open GRNI Accruals** tab -> **Expected:** Two `OPEN` GRNI records (`GRNI-2026-00001` & `GRNI-2026-00002`) summing `Rp 18.000.000`, ready for S09 three-way matching.
4. Toggle `ID` / `EN` language switch and resize viewport to `390px` -> **Expected:** All copy translates cleanly with zero horizontal clipping.

### UAT-S07-04 — Cross-Entity Read-Only Isolation & Hostile Cross-Tenant Denial
1. Switch workspace scope to `NUSA-SBY` and open **Inventory** -> **Expected:** Empty state with zero `NUSA-JKT` stock or GRNI items visible.
2. Sign in as `rina.utami@demo.athera.local` and browse directly to `/w/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01/bbbbbbbb-bbbb-bbbb-bbbb-bbbbb101/inventory` -> **Expected:** Unavailable scope alert (`404`) with zero tenant leakage.
