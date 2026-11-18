# ATH-CR-007 — Open Decision Requests OD-05 & OD-07: Supplier Returns, Replacement Entitlements, Credits, and Statutory Tax Reversals

- **Decision IDs:** `OD-05`, `OD-07` (`docs/product/ATH-OPEN-DECISIONS-001.md`; companion to `OD-02` [`ATH-CR-001`], `OD-04` [`ATH-CR-004`], and `OD-03`/`OD-08` [`ATH-CR-006`])
- **Affected Slices:** `S10` (Supplier Returns, Replacements and Financial Corrections), `S11` (Supplier Payment Runs & Settlement)
- **Status:** **OPEN — Awaiting Product Owner & Finance Controller Decision**

---

## 1. Decision Questions

1. **`OD-05` — Supplier Return, Replacement & Financial Correction Matrix:**
   - Across the orthogonal axes (**Unvalued Dock Rejection** vs **Valued Unbilled** vs **Valued Billed**; **Unpaid** vs **Paid**; **Open Period** vs **Closed Period**; **Replacement Only** vs **AP Credit Note** vs **Close Without Replacement**), which return resolutions are permitted, how should remaining inventory value vs replacement valuation be carried (`CommercialIncrementalUnitPrice = 0` vs `AuthorizedInventoryUnitCostBase > 0`), and when must an already-billed return issue a credit vs re-enable replacement?
   - When returning valued inventory (`VALUED_UNBILLED_RETURN` or `VALUED_BILLED_RETURN`) where moving-average pool cost differs from original receipt/billed unit price due to intermediate transactions, which GL account absorbs the valuation difference (`5200 PPV` vs dedicated return variance clearing)?
2. **`OD-07` — Statutory Indonesian `Nota Retur` PPN Reversal & PPh 23 Return Amendments:**
   - When returning goods from a posted taxable supplier bill (`PPN-IN-11` / `PPN-12-DPP1112`), what documentary fields (`Nota Retur` tax return note number, date, counterparty PKP confirmation) are mandatory before posting `PPN_INPUT_REVERSAL` (`Cr 1410 PPN Masukan`)?
   - How must `PPh 23` withholding amendments/refunds on partially/fully returned billed invoices be handled when `Bukti Potong` tax withholding has already been recognized (`Cr 2220`)?

---

## 2. Options Considered

- **Option A (`EXPLICIT_SUPPORTED_MATRIX_OD05_OD07_SAFE_DEFAULT` Active in S10):**
  - **`M1` (`UNVALUED_DOCK_REJECTION` + `CLOSE_WITHOUT_REPLACEMENT` or `REPLACEMENT_ONLY`):** Permitted. Returns unvalued `DockRejectedBaseQuantity` physically with zero GL, zero Inventory valuation, and zero GRNI impact; `REPLACEMENT_ONLY` issues an independent four-eyes `ReplacementAuthorization` (`CommercialIncrementalUnitPrice = 0`, `AuthorizedInventoryUnitCostBase = ApprovedPoUnitCostBase > 0`).
  - **`M2` (`VALUED_UNBILLED_RETURN` + `CLOSE_WITHOUT_REPLACEMENT` or `REPLACEMENT_ONLY` in `OPEN` period):** Permitted with four-eyes approval (`OD-02`). Atomically posts outbound `PURCHASE_RETURN_OUTBOUND` movement (`UNRESTRICTED` unreserved or `BLOCKED` stock, reducing `StockLocationBalance`, `StockPosition`, `ValuationPool` `Cr 1300`), reverses unbilled `ReceiptAccrualLine` (`Dr 2110` ± `5200 PPV` for MAC delta), reopens `PurchaseOrder.AcceptedBaseQuantityTotal`, and if `REPLACEMENT_ONLY`, issues a linked `ReplacementAuthorization` (`AuthorizedInventoryUnitCostBase = OriginalReceiptUnitCostBase > 0`).
  - **`M3` (`VALUED_BILLED_RETURN` + `AP_CREDIT_NOTE` on `UNPAID` `POSTED` bill with `WithholdingTaxAmount == 0` in `OPEN` period):** Permitted with independent `ap.approve` four-eyes sign-off (`OD-02`) and mandatory `TaxReturnNoteNumber` (`Nota Retur`) whenever `PpnTaxAmount > 0` (`OD-07`). Atomically posts outbound `PURCHASE_RETURN_OUTBOUND` (`Cr 1300`), append-only `ap.SupplierCredit` (`SCN-YYYY-NNNNN`), `PayableItem.ApplySupplierCredit` (`Dr 2100`), append-only `tax.TaxDetermination` (`PPN_INPUT_REVERSAL`, `Cr 1410` when `PPN > 0`), and `Dr/Cr 5200 PPV` when pool MAC differs from billed price.
  - **`M4` (`VALUED_BILLED_RETURN` + `REPLACEMENT_ONLY`):** Draft claim permitted for documentation; submit/execute blocked with **`409 od05_billed_replacement_policy_blocked`**.
  - **`M5` (`VALUED_BILLED_RETURN` where `PayableItem.PaidAmount > 0`):** Draft claim permitted; submit/execute blocked with **`409 od05_paid_bill_return_policy_blocked`**.
  - **`M6` (`VALUED_BILLED_RETURN` where `SupplierBillLine.WithholdingTaxAmount > 0`):** Draft claim permitted; submit/execute blocked with **`409 od07_withholding_return_reversal_blocked`**.
  - **`M7` (`CLOSED` / `LOCKED` fiscal period for `M2` or `M3`):** Execute blocked with **`409 period_closed`** (no silent prior-period mutation).
- **Option B (Expanded Product Owner & Indonesian Tax Policy Matrix):**
  - Owner and Indonesian tax advisor sign off custom policies permitting paid-bill vendor refunds/cash claims, billed no-credit replacement swaps, `PPh 23` `Bukti Potong` cancellation/amendment automation, and prior-period catch-up posting rules.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A — explicit matrix enforcement (`M1`–`M3` executable under four-eyes `OD-02`, `OD-04` floor, and `OD-07` `Nota Retur` evidence checks; `M4`–`M7` cleanly documented as blocked with deterministic `409` codes and zero speculative accounting).

---

## 4. Product Owner & Finance Controller Sign-Off (Owner Only)

- **Reviewer Name / Role (PO / Finance Controller):** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
