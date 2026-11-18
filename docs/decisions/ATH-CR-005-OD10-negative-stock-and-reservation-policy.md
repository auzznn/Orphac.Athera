# ATH-CR-005 — Open Decision Request OD-10: Negative Stock Allowance & Reservation Allocation Policy

- **Decision ID:** `OD-10` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slices:** `S08` (Stock Availability, Locations and Quality Restrictions), `S13` (Sales Reservation, Pick and Dispatch)
- **Status:** **OPEN — Awaiting Product Owner Decision**

---

## 1. Decision Questions

1. **Negative Stock Allowance:** Should any legal entity, item category, or operational workflow ever be permitted to issue, transfer, or commit stock into negative `unrestricted_base_quantity`, `on_hand_base_quantity`, or `available_base_quantity` (for example under delayed physical receipt entry), or is negative stock strictly prohibited system-wide?
2. **Hard vs Soft Stock Reservation & Priority Rules:** Should reservations strictly bind against specific `(warehouse_id, location_id, stock_lot_id)` balances capped by `Unrestricted - Reserved` (`AvailableBaseQuantity`), or allow soft pool-wide backorder allocations, FIFO/FEFO automatic lot suggestions, or manual manager overrides?
3. **Reservation Expiry & Automatic Release:** When a reservation carries an optional `expires_at_utc` (`MANUAL_HOLD` or `SALES_ORDER`), should expiry require explicit release/extension by an authorized clerk (`inventory.manage`) or trigger scheduled auto-release?

---

## 2. Options Considered

- **Option A (`STRICT_NO_NEGATIVE_AND_EXPLICIT_RESERVATION_SAFE_DEFAULT` Active in S08):**
  - Negative on-hand, restricted, reserved, or available quantities are strictly forbidden at both domain validation and PostgreSQL `CHECK` constraints (`od10_negative_stock_forbidden` / `ck_stock_positions_nonnegative` / `ck_loc_bal_availability`).
  - Reservations bind explicitly to a single `UNRESTRICTED` `StockLocationBalance` (`on_hand - reserved >= requested`); `QUALITY_INSPECTION` and `BLOCKED` balances carry `0` availability and cannot be reserved or transferred as available stock.
  - Competing concurrent or over-capacity reservation requests block with **`409 od10_insufficient_available_stock`**.
  - Quality inspection holds (`QUALITY_INSPECTION`) and disposition moves (`UNRESTRICTED` / `BLOCKED`) update location/status availability without silently mutating moving-average `ValuationPool` carrying value or `GL` journals (`BRS-RCV-008`).
- **Option B (Approved Controlled Negative Override / Priority Allocation Rules):**
  - Product Owner approves named items/entities permitting bounded temporary negative dispatch (`S13`), costing catch-up (`OD-04`), and priority reservation queues or automated expiry jobs.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A (`STRICT_NO_NEGATIVE_AND_EXPLICIT_RESERVATION_SAFE_DEFAULT`) — zero negative stock, strict `Available = Unrestricted - Reserved`, explicit hard reservations on `UNRESTRICTED` location/lot balances, and `409 od10_insufficient_available_stock` on any over-reservation or restricted stock reservation attempt.

---

## 4. Product Owner & Operations Sign-Off (Owner Only)

- **Reviewer Name / Role:** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
