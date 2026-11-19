# ATH-CR-012 — Customer Receipts, Unapplied Funds & AR Credit Allocation Policy (OD-09 / OD-07)

- **Decision ID:** `ATH-CR-012`
- **Related Open Decisions:** `OD-09` (Customer returns, unapplied credit/refunds, and over-collection policy), `OD-07` (Tax implications of post-collection credit notes)
- **Slice:** `S15` — Customer Receipts, Allocation, AR Aging and Credits
- **Status:** `OPEN` (Safe deterministic defaults enforced in engineering build; Product Owner sign-off required before production release)
- **Product Owner Sign-Off:** *(BLANK — PENDING HUMAN APPROVAL)*
- **Date Approved:** *(BLANK)*

## Safe Engineering Defaults Implemented Pending Owner Confirmation

1. **Bank-Confirmed Receipt Recognition (`Dr 1110 Bank / Cr 1200 AR Control`):**
   - Customer receipts may only be recognized against an imported, unmatched inbound `CREDIT` bank statement line (`cash.bank_statement_lines`), protected by SHA-256 fingerprint deduplication so one bank line cannot be imported or recognized twice.
   - Recognizing the receipt immediately posts `Dr 1110 Bank / Cr 1200 AR Control` in the open financial period. Any unallocated portion remains explicitly tracked in `ar.customer_receipts.unapplied_amount` so `GL 1200 == Sum(Open Receivables) - Sum(Unapplied Receipts)`.
2. **Strict Amount Conservation & Anti-Negative Open AR:**
   - Sum of allocations for any receipt can never exceed `receipt_amount`.
   - Allocation against any receivable can never exceed its `open_amount` (open balance cannot drop below zero).
   - Cross-customer and cross-tenant allocations are rejected deterministically.
3. **Compensating Reversal Instead of Edits:**
   - Reversing a confirmed receipt requires an explicit reason and an open accounting period, posts a compensating `Dr 1200 AR Control / Cr 1110 Bank` journal, restores all allocated invoices' open amounts, and unmatches the source bank statement line.
   - Direct cash refunds of unapplied customer balances remain blocked pending `OD-09` approval.

## Owner Decisions Requested

- [ ] Confirm whether unapplied customer receipts should be carried on `1200 AR Control` (default) or reclassified to a separate liability account `2150 Uang Muka Pelanggan (Customer Advances)`.
- [ ] Confirm policy for refunding unapplied customer overpayments (`OD-09`).
- [ ] Confirm standard AR aging bucket boundaries (`Current`, `1-30`, `31-60`, `61-90`, `>90` days past due).
