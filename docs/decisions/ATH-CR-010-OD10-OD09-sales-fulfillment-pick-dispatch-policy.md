# ATH-CR-010 — Open Decision Request OD-10 & OD-09: Sales Order Reservation Allocation, Partial Fulfillment Cancellation & Outbound COGS Dispatch Policy

- **Decision IDs:** `OD-10`, `OD-09` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slice:** `S13` (Sales Reservation, Pick and Dispatch)
- **Status:** **OPEN — Awaiting Product Owner Decision**

---

## 1. Decision Questions

1. **Sales Order Reservation Allocation & Priority (`OD-10`):** Should sales order reservations bind explicitly to warehouse operator-selected `(warehouse_id, location_id, stock_lot_id)` balances with `UNRESTRICTED` status and hard-cap at `AvailableBaseQuantity = OnHandBaseQuantity - ReservedBaseQuantity`, or should the system auto-allocate across bins/lots using configurable FEFO/FIFO priority rules and allow soft backorder reservations exceeding physical on-hand stock?
2. **Negative Stock & Restricted Lot Dispatch (`OD-10`):** Under any operational urgency, may outbound sales picking or shipment dispatch issue stock into negative location/position balances or consume `QUALITY_INSPECTION` / `BLOCKED` / expired lot balances?
3. **Order Cancellation & Amendment After Partial Reservation or Dispatch (`OD-09`):** When an approved sales order has active reservations (`TotalReservedBaseQuantity > 0`) or partial dispatches (`TotalDispatchedBaseQuantity > 0`), should order cancellation automatically force-release active reservations or block cancellation (`409 so_has_downstream_fulfillment`) until warehouse staff explicitly release unpicked reservations, and require short-close (`CLOSED`) rather than cancellation once any shipment has been dispatched?

---

## 2. Options Considered

- **Option A (`STRICT_EXPLICIT_RESERVATION_NO_NEGATIVE_DISPATCH_SAFE_DEFAULT` — Active in S13):**
  - Reservations against approved sales order lines bind explicitly to an active `UNRESTRICTED` `StockLocationBalance` in a `STORAGE` or `DISPATCH` zone and are capped by both `locBalance.AvailableBaseQuantity` and the sales order line's remaining unfulfilled base quantity (`OrderedBaseQuantity - DispatchedBaseQuantity - ReservedBaseQuantity`).
  - `QUALITY_INSPECTION` and `BLOCKED` stock balances, as well as expired lots (`ExpiryDate < DispatchDate`), are strictly rejected (`409 od10_restricted_stock_not_reservable` / `409 lot_expired_for_fulfillment`).
  - Competing reservations for the same available stock serialize via PostgreSQL `FOR UPDATE` row locks on `inventory.stock_positions`, `inventory.stock_location_balances`, and `sales.sales_order_lines`; any request exceeding remaining availability fails deterministically with **`409 od10_insufficient_available_stock`**.
  - Outbound shipment dispatch atomically consumes the active reservation (`ConsumedBaseQuantity += qty`, `RemainingReservedBaseQuantity -= qty`), deducts `OnHandBaseQuantity` and `ReservedBaseQuantity` in lockstep, deducts the moving-average `ValuationPool` (`OD-04`), records immutable `StockMovement` + `ValuationEntry` rows, and posts a balanced GL journal (`Dr 5100 COGS_DEFAULT / Cr 1300 INVENTORY_ASSET`).
  - Sales orders with active reservations cannot be cancelled until those reservations are explicitly released; once partial shipment dispatch has occurred (`TotalDispatchedBaseQuantity > 0`), cancellation and quantity reduction below dispatched quantity remain blocked (`409 so_has_downstream_fulfillment`).
- **Option B (Automated FEFO/FIFO Soft Allocation & Auto-Release on Cancel):**
  - Product Owner authorizes automatic cross-bin lot suggestion, soft backorder queues, or automatic reservation unwinding upon sales order cancellation.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A (`STRICT_EXPLICIT_RESERVATION_NO_NEGATIVE_DISPATCH_SAFE_DEFAULT`). Zero negative stock, explicit hard reservations on `UNRESTRICTED` location/lot balances, mandatory lot specification for `REQUIRED` lot-tracked items, atomic weighted-average COGS journal posting on dispatch, and explicit reservation release before order cancellation.

---

## 4. Product Owner & Operations Sign-Off (Owner Only)

- **Reviewer Name / Role:** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
