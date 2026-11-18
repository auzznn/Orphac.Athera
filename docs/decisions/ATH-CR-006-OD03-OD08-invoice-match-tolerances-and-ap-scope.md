# ATH-CR-006 — Open Decision Requests OD-03 & OD-08: Invoice Match Tolerances, Small-Difference Write-Off, and Non-PO / Withholding AP Scope

- **Decision IDs:** `OD-03`, `OD-08` (`docs/product/ATH-OPEN-DECISIONS-001.md`; companion to `OD-02` [`ATH-CR-001`], `OD-04` [`ATH-CR-004`], and `OD-07` [`ATH-CR-002`])
- **Affected Slices:** `S09` (Supplier Invoice Three-Way Match, AP and Price Difference), `S10` (Supplier Returns & Credits), `S11` (Payment Runs & Bank Clearing)
- **Status:** **OPEN — Awaiting Product Owner & Finance Controller Decision**

---

## 1. Decision Questions

1. **`OD-03` — Three-Way Match `PP` / `DQ` / `BD` Tolerances & Small-Difference Account:**
   - What numeric percentage and absolute functional-currency (`IDR`) limits authorize automatic posting (`WITHIN_TOLERANCE`) for per-unit price variance (`PP`), quantity discrepancy (`DQ`), and header rounding/small difference (`BD`) per legal entity?
   - Which dedicated GL expense/clearing account is authorized to absorb minor `BD` rounding differences, and what governance prevents small write-offs from masking genuine quantity or unit-price discrepancies?
   - When unit-price variance exceeds automatic tolerance under moving-average costing (`DEC-P2P-013`), should independent Finance Approver sign-off always be mandatory and optionally enforce a `PAYMENT_BLOCKED` hold prior to payment settlement (`S11`)?
2. **`OD-08` — Non-PO Expense Bills, Supplier Advances & Indonesian Withholding Lifecycle:**
   - Which approval thresholds, cost-center/GL mappings, and supporting document checks apply before enabling `NON_PO_EXPENSE` or `SUPPLIER_ADVANCE` AP bills?
   - What statutory evidence fields (`Faktur Pajak` tax-invoice number, `Bukti Potong` PPh 23 slip reference) are mandatory at AP draft, posting, or period-end reporting?

---

## 2. Options Considered

- **Option A (`EXACT_MATCH_OD03_SAFE` & `PO_THREE_WAY_ONLY_SAFE_DEFAULT` Active in S09):**
  - Default entity `InvoiceMatchPolicy` (`EXACT_MATCH_OD03_SAFE`) sets automatic `PP` tolerance = `0%` / `Rp 0`, `DQ` over-receipt/over-bill tolerance = `0%` / `0`, and `BD` write-off limit = `Rp 0` (`BdWriteOffAccountId = null`). Public API rejects unreviewed non-zero tolerance or write-off configurations with **`409 od03_unreviewed_tolerance_policy_blocked`**.
  - **Over-quantity (`DQ`):** Invoiced base quantity exceeding `ReceiptAccrualLine.RemainingUnbilledBaseQuantity` is unconditionally **`POSTING_BLOCKED`** (`dq_exceeds_unbilled_accepted_capacity`). Partial bills (`0 < qty <= remaining`) are permitted while conserving unbilled accrual capacity.
  - **Header balance difference (`BD`):** Header declared gross amount differing from computed `(LinesNetSubtotal + PpnTaxTotal)` is **`POSTING_BLOCKED`** unless governed by a `REVIEWED_INTERNAL` policy with configured write-off account and within limit.
  - **Unit-price variance (`PP != 0`):** Evaluates **`APPROVAL_REQUIRED`**; cannot be silently posted by the preparer. Posting requires an independent four-eyes approver (`submitter != approver`, `OD-02`) explicitly acknowledging price variance (`ApprovePriceVariance = true`, optionally applying `PAYMENT_BLOCKED`), applying `DEC-P2P-013` stock-coverage apportionment (`coveredQty / billedQty` to Inventory Asset `1300` guarded by `OD-04` valuation floor, residual uncovered difference to Purchase Price Variance `5200`).
  - **`OD-08` Non-PO / Advances:** Creating `NON_PO_EXPENSE` or `SUPPLIER_ADVANCE` bills returns **`409 od08_non_po_bill_not_supported`**. Line-level `PPN` (`INPUT`) requires explicit `TaxCode` selection (`OD-07`), while optional `PPh 23` (`WITHHOLDING`) splits `Cr 2220` + `Cr 2100` while retaining explicit `Faktur Pajak` / `Bukti Potong` metadata (`UNREVIEWED_CONFIGURABLE` disclaimer; zero statutory filing claim).
- **Option B (Owner-Approved Entity Match Tolerances & Non-PO Expense Policy):**
  - Product Owner and Finance Controller approve signed `PP`/`DQ`/`BD` tolerance policies (`REVIEWED_INTERNAL`), designate a `BD` write-off GL account, and approve non-PO / advance workflows before enabling automated tolerance posting.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A — strict exact-match safe defaults (`0%`/`0`/`Rp 0`), mandatory four-eyes variance approval (`OD-02`) for `PP` adjustments, strict blocking of `DQ` overbilling, unconfigured `BD` differences, `OD-04` valuation floor breaches, `OD-07` invalid/missing-NPWP tax determinations, and `OD-08` unsupported non-PO / advance bills.

---

## 4. Product Owner & Finance Controller Sign-Off (Owner Only)

- **Reviewer Name / Role (PO / Finance Controller):** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
