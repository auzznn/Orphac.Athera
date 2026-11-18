# S09 Product Owner UAT Script — Supplier Invoice Three-Way Match, AP & Price Difference

> **Acceptance Authority:** Executed for **Product Owner** review. Do not mark Slice S09 accepted without Product Owner sign-off in `docs/implementation/WORKSTREAM-STATE.md` and `docs/decisions/ATH-CR-006-OD03-OD08-invoice-match-tolerances-and-ap-scope.md`.

## Prerequisites (Local Demo Environment)
1. Start local PostgreSQL 18 container (`athera-greenfield-pg` on `127.0.0.1:55433`).
2. Apply migrations (`M00`–`M09`) & clean demo seed:
   ```bash
   ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="AtheraDemo!2026" \
     ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw" \
     dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo
   ```
3. Seeded verification fixtures on `NUSA-JKT`:
   - Supplier `PT-SUP-001` (`PT Mitra Baja Indonesia`, valid NPWP & PKP), tax codes `PPN-IN-11` (`11%`) and `PPH23-SVC-2` (`2%`),
   - Stocked item `ITM-S09-VALVE-01` (`Katup Industri PN16 50mm`): accepted `100 PCS @ Rp 50.000` (`Rp 5.000.000` open GRNI accrual) followed by operational stock issue `ISS-JKT-DEMO-S09-01` of `60 PCS`, leaving **`40 PCS` on-hand (`Rp 2.000.000`)** alongside **`100 PCS` unbilled GRNI (`Rp 5.000.000`)**.
   - Demo users: `U4` (`siti.rahayu@demo.athera.local`, `AP_CLERK` on `NUSA-JKT`), `U2` (`budi.santoso@demo.athera.local`, `Controller` on `NUSA-JKT` / `SMDR-BDG`, `ap.read`-only on `NUSA-SBY`), `U1` (`rina.utami@demo.athera.local`, zero AP/Finance grants).

## UAT Test Cases

### UAT-S09-01 — Open GRNI & DEC-P2P-013 Positive Price Variance Preview (`U4` AP Officer)
1. Sign in as `siti.rahayu@demo.athera.local` / `AtheraDemo!2026`.
2. Open **NUSA-JKT** -> **Accounts Payable (`Hutang Usaha (AP)`)** -> **Open GRNI Queue (`Antrean GRNI Terbuka`)**.
3. **Expected:**
   - Governance banner clearly states `OD-03` safe defaults (PP/DQ/BD tolerances = `0`) and `OD-08` PO-only invoice scope.
   - `ITM-S09-VALVE-01` shows `100 PCS` unbilled GRNI @ `Rp 50.000` (`Rp 5.000.000`) and `40 PCS` current stock pool on-hand (`Rp 2.000.000`).
4. Click **Bill This Receipt (`Tagih Baris Ini`)**, enter Vendor Invoice number, Tax Invoice / Faktur Pajak, `100 PCS` @ `Rp 55.000`, select `PPN-IN-11` + `PPH23-SVC-2`, enter Declared Gross `6105000`, and click **Run Live 3-Way Match Preview**.
5. **Expected:**
   - Status = **`APPROVAL_REQUIRED` (`Wajib Persetujuan Selisih`)**, `DQ = MATCHED`, `PP = VARIANCE_APPROVAL_REQUIRED`, `BD = MATCHED`.
   - `DEC-P2P-013` stock-coverage split shows **`Rp 200.000` to Inventory `1300`** (`40/100`), **`Rp 300.000` to PPV `5200`** (`60/100`), **`PPN = Rp 605.000`**, **`PPh 23 = Rp 110.000`**, **`Net AP = Rp 5.995.000`**, and **Projected Moving Avg = `Rp 55.000/PCS`**.
   - GL preview balances (`Total Debit = Rp 6.105.000 = Total Credit`).

### UAT-S09-02 — Draft, Submit, Independent Four-Eyes Approval (`OD-02`), Payment Block & Atomic Posting (`U4` -> `U2`)
1. As `U4`, click **Save Supplier Bill Draft**, then click **Submit for 4-Eyes Approval**.
2. **Expected:** Status transitions to `PENDING_APPROVAL`; `U4` sees neither Approve nor Post controls (`ap.approve` and `ap.post` segregated). Sign out.
3. Sign in as `budi.santoso@demo.athera.local` / `AtheraDemo!2026` (`U2`). Open the submitted bill on `NUSA-JKT` -> `/payables`.
4. Attempt approval before checking the price-variance acknowledgement checkbox -> **Expected:** Approve button remains disabled.
5. Check **Confirm Price Variance Approval** AND **Place Payment Hold (`PAYMENT_BLOCKED`)**, enter a decision reason, and approve.
6. Enter posting date `2026-11-10` and click **Post Bill to AP & GL**.
7. **Expected:**
   - Bill transitions atomically to `POSTED` (`BILL-2026-...`), linked balanced GL Journal (`JRN-2026-...`), `OPEN` AP subledger liability (`AP-2026-...`, Net `Rp 5.995.000`) in **`PAYMENT_BLOCKED`** state, and GRNI accrual becomes **`CLEARED`**.
8. Enter a verification reason and click **Release Payment Block** -> **Expected:** Bill & AP subledger item update to **`RELEASED`**.

### UAT-S09-03 — Inventory Moving-Average Revaluation Verification & Operational Ledger (`U2`)
1. Navigate to **NUSA-JKT** -> **Inventory & Stock Ledger (`/inventory`)** and select `ITM-S09-VALVE-01`.
2. **Expected:**
   - Stock position displays `40 PCS` on-hand with carrying value **`Rp 2.200.000`** (`Rp 2.000.000 + Rp 200.000`) and moving-average unit cost **`Rp 55.000/PCS`**.
   - Item Valuation Ledger displays sequential entries: `#1 Receipt Acceptance (+100)`, `#2 Operational Stock Issue (-60, ISS-JKT-DEMO-S09-01)`, and `#3 Invoice Price Adjustment (0 qty, +Rp 200.000, BILL-2026-...)`.

### UAT-S09-04 — Fail-Closed RBAC, Read-Only Scope (`NUSA-SBY`), & Responsive Mobile Layout
1. Resize browser to `390x844` on `/payables` -> **Expected:** Zero horizontal overflow; cards/tables stack vertically cleanly.
2. Sign in as `rina.utami@demo.athera.local` (`U1`) -> **Expected:** Accounts Payable sidebar item is locked/not linked and direct navigation to `/w/.../payables` shows access-denied (`403`) without leaking AP data.
3. Switch to `U2` on `NUSA-SBY` -> **Expected:** Read-only banner visible (`ap.read` only) with zero create/approve/post controls.
