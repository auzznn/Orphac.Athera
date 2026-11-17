# ATH-CR-003 — Open Decision Request OD-01: Purchase Order Overdelivery Tolerances & Unlimited Flag Policy

- **Decision ID:** `OD-01` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slices:** `S05` (Purchase Orders, Revisions & Four-Eyes), `S06` (Dock Custody), `S07` (Valued Acceptance & GRNI)
- **Status:** **OPEN — Awaiting Product Owner Decision**

---

## 1. Decision Questions
1. **Line-Level Overdelivery Tolerance:** What default percentage tolerance (e.g., `0%`, `2%`, `5%`) and/or absolute value/base-quantity cap may be configured per legal entity, item category, or supplier before excess delivery requires independent approval?
2. **Unlimited Overdelivery Flag Governance:** Which role(s) may set or approve an unlimited overdelivery allowance on a Purchase Order line, under what monetary threshold, and with what mandatory expiry condition?
3. **Excess Quantity Disposition:** Until approved (or when rejected), must excess physical quantity remain strictly in unvalued dock custody (`S06`) with valued acceptance/GRNI posting (`S07`) blocked?

## 2. Options Considered
- **Option A (`EXACT_APPROVED_QTY_ONLY` Safe Default Active in S05):**
  - Default and only active `procurement.delivery_tolerance_policies` mode in S05 (`OverdeliveryTolerancePercent = 0.0000`, `OverdeliveryToleranceBaseQty = 0.0000`, `AllowUnlimitedOverdelivery = false`, flagged `UNREVIEWED_SAFE_DEFAULT`).
  - Attempting to save or approve a non-zero tolerance percentage, positive extra base quantity, or `AllowUnlimitedOverdelivery = true` raises domain/API conflict **`od01_overdelivery_tolerance_blocked`** (`409`) and is additionally barred at PostgreSQL CHECK constraint level on `procurement.purchase_order_lines`.
  - Excess delivery above approved PO quantity cannot silently receive valuation in S06/S07; only exact approved quantities are eligible until Product Owner sign-off.
- **Option B (Approved Tiered Tolerance & Waiver Policy):**
  - Activate Product Owner-signed percentage/value tolerances, explicit four-eyes waiver workflow for `AllowUnlimitedOverdelivery`, and expiration dates under `DEC-P2P-004`.

## 3. Safe Default Behavior Until Signed
- **Active Now:** Option A — exact approved quantity only (`0%`, `0` extra base units, `AllowUnlimitedOverdelivery = false`) enforced across `DeliveryTolerancePolicy`, `PurchaseOrderWorkflowService`, and PostgreSQL CHECK constraints.

## 4. Product Owner Decision Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
