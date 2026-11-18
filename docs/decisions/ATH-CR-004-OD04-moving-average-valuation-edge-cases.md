# ATH-CR-004 — Open Decision Request OD-04: Moving-Average Valuation Edge Cases & Negative/Zero Pool Policy

- **Decision ID:** `OD-04` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slices:** `S07` (Valued Acceptance, Moving-Average Inventory and GRNI), `S09` (Supplier Invoice Three-Way Match & Price Difference)
- **Status:** **OPEN — Awaiting Product Owner Decision**

---

## 1. Decision Questions

1. **Zero / Negative / Orphan Carrying-Value Pool Handling:** When an inventory valuation pool encounters an anomalous state (negative on-hand quantity, negative carrying value, or `valued_base_quantity = 0` with non-zero residual carrying value), should inbound/invoice transactions fail cleanly until a Product Owner-authorized accounting adjustment clears the pool, or under what narrow automated clearing account may residual cents be absorbed?
2. **Moving-Average Cost & Sub-Rupiah Rounding Residual Policy:** When `acceptedLineAmount` (`numeric(18,4)`) divided by `newValuedBaseQuantity` (`numeric(18,6)`) yields non-terminating decimals (e.g., `100,000 / 3 = 33,333.333333`), should `CarryingValueAmount` (`numeric(18,4)`) always remain the authoritative sum (`previousCarrying + deltaAmount`, preserving `SUM(Dr Inventory Asset) == CarryingValueAmount`) while `MovingAverageUnitCost` (`numeric(18,6)`) serves as derived display/issue unit cost, and how should final full-depletion rounding residuals be routed?
3. **Zero-Cost / Free-of-Charge Valued Acceptance Policy:** May positive stock quantities ever enter a valued pool with `UnitPrice <= 0` (`acceptedLineAmount = 0`), or must free goods / supplier replacements follow an approved cost recognition rule (`OD-05`)?
4. **Valuation Pool Grouping Granularity:** Does legal-entity moving-average valuation pool strictly by `(tenant_id, legal_entity_id, item_id, currency_code, valuation_group_code)` (`DEFAULT` in S07), or require separate valuation pools per warehouse/location (`S08`)?

---

## 2. Options Considered

- **Option A (`STRICT_POSITIVE_POOL_SAFE_DEFAULT` Active in S07):**
  - Clean zero pool (`valued_base_quantity = 0` AND `carrying_value_amount = 0`) initializes directly to the accepted quantity and amount (`newAvg = Round(newCarrying / newQty, 6, MidpointRounding.AwayFromZero)`).
  - Anomalous pool (`Status != ACTIVE`, `valued_base_quantity < 0`, `carrying_value_amount < 0`, or `valued_base_quantity = 0 && carrying_value_amount != 0`) blocks posting with **`409 od04_anomalous_valuation_pool_blocked`** and SQL CHECK enforcement.
  - Zero or negative acceptance value (`UnitPrice <= 0` or `acceptedLineAmount <= 0`) blocks posting with **`409 od04_zero_or_negative_acceptance_value_blocked`**.
  - Carrying value (`numeric(18,4)`) stays strictly authoritative and tied 1:1 to `Dr Inventory Asset / Cr GRNI` postings; moving-average unit cost is rounded cleanly at scale `6` without altering GL totals.
- **Option B (Approved Automated Residual & Location-Scoped Pool Rules):**
  - Product Owner and Accounting Advisor approve exact residual rounding write-off account mapping, negative-stock allowance (`OD-10`), and per-warehouse valuation grouping before activating automated adjustments.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A — strict positive/clean-zero valuation pools, authoritative `numeric(18,4)` carrying value, scale-6 `AwayFromZero` moving-average unit cost, and explicit `409` blocking of anomalous pools or zero-cost acceptance.

---

## 4. Product Owner & Accounting Advisor Sign-Off (Owner Only)

- **Reviewer Name / Role:** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
