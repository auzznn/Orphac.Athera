# ATH-CR-009 — Open Decision Request OD-09: Sales Credit Limits, Commercial Discounts, Cancellation and Customer Return Policy

- **Decision ID:** `OD-09` (`docs/product/ATH-OPEN-DECISIONS-001.md`; companion to `OD-02` [`ATH-CR-001`], `OD-07` / `OD-13` [`ATH-CR-002`], and `OD-10` [`ATH-CR-005`])
- **Affected Slices:** `S12` (Customer Sales Orders and Commercial Commitments), `S13` (Sales Reservation, Pick and Dispatch), `S14` (Customer Billing, Tax and Accounts Receivable), `S15` (Customer Receipts, Allocation, AR Aging and Credits)
- **Status:** **OPEN — Awaiting Product Owner & Finance Controller Decision**

---

## 1. Decision Questions

1. **Customer Credit Limits & Credit Hold Overrides (`OD-09`):**
   - Which credit-limit exposure formula applies before confirming a Sales Order (`SO`) or dispatching goods (open AR + unbilled dispatches + open approved SO commitment vs. fixed entity-level credit cap)?
   - Should credit-limit breaches hard-block Sales Order approval or allow a specific elevated role (`ENTITY_APPROVER` / Controller) to grant a tracked exception?
2. **Commercial Discounts, Promo Rebates & Price Overrides (`OD-09`):**
   - Are line-level commercial discounts, header invoice discounts, or customer price-list tiers permitted on Sales Orders, and which GL accounts and tax base (`DPP`) adjustments apply under `OD-07`?
3. **Amendment & Cancellation After Downstream Fulfillment (`OD-09`):**
   - When an approved Sales Order already has active Inventory reservations, dispatches (`S13`), or customer billing (`S14`), is post-fulfillment quantity reduction or order cancellation strictly prohibited (requiring short-close / credit note / RMA flows), and who may approve post-fulfillment amendments?

---

## 2. Options Considered

- **Option A (`POSITIVE_PRICE_ZERO_DISCOUNT_OD09_SAFE` Active in S12):**
  - Every `sales.sales_order_lines` row MUST have strictly positive `OrderedQuantity > 0`, strictly positive `UnitPrice > 0`, and **`DiscountAmount == 0`** (enforced in both domain logic -> `422 od09_sales_discount_policy_unresolved` and PostgreSQL `CHECK (discount_amount = 0)`).
  - Unapproved credit-limit bypass requests (`creditLimitOverrideRequested == true`) fail closed (`422 od09_credit_limit_policy_unresolved`); no automatic credit-limit override or unapproved commercial pricing tiers are invented.
  - Every Sales Order requires explicit active Customer eligibility (`PartnerEligibilityEvaluator.CustomerEligible`), active Sales-allowed Item & UoM conversion snapshot (`ItemEligibilityEvaluator.SalesAllowed`), explicit effective-dated `OUTPUT` tax determination (`TaxDeterminationEvaluator`), and independent four-eyes approval (`OD-02`, `submitted_by_user_id != decided_by_user_id`) before committing an immutable revision snapshot (`SO-{Year}-{Seq}`).
  - Once any stock reservation, dispatch, or billing exists (`TotalReservedBaseQuantity > 0 || TotalDispatchedBaseQuantity > 0 || TotalBilledBaseQuantity > 0`, or line `OrderedBaseQuantity < ReservedBaseQuantity + DispatchedBaseQuantity`), downward revision or cancellation fails closed (`409 so_has_downstream_fulfillment`).
- **Option B (Owner-Approved Credit Limit, Discount Tier & Post-Dispatch Amendment Policy):**
  - Product Owner and Finance Controller sign binding credit exposure thresholds, discount GL/tax rules, and RMA / post-dispatch amendment authority before enabling discounts or credit overrides in production.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A (`POSITIVE_PRICE_ZERO_DISCOUNT_OD09_SAFE`). Positive unit price and quantity required; `DiscountAmount` must equal `0`; no inferred credit-limit overrides or automated price/tax discounts; downward amendments below reserved/dispatched quantities and cancellations with downstream fulfillment remain blocked until Product Owner sign-off.

---

## 4. Product Owner & Finance Controller Sign-Off (Owner Only)

- **Reviewer Name / Role (PO / Finance Controller):** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
