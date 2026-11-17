# ATH-DEC-OPEN-001 - Unresolved product, financial and statutory decisions

Status: Live decision register. The coding agent may propose but **cannot unilaterally approve** a material business policy.

| ID | Decision needing Owner / accountant / operational sign-off | Affected slices | Safe behavior until decided |
|---|---|---|---|
| OD-01 | Purchase overreceipt percentage/value tolerance, unlimited flag approval rights and expiry | S05-S07 | Allow exact approved quantities; record excess as unvalued custody with posting blocked. |
| OD-02 | Four-eyes threshold, actor role matrix, exceptional small-team separation-of-duties policy | S02, S05, S09, S10, S14 | Submitter != approver for sensitive posting; block any scenario lacking an authorized independent decision. |
| OD-03 | Three-way matching PP/DQ/BD tolerances and the small rounding write-off account | S09 | Exact-match posting only; discrepancies visible and blocked. |
| OD-04 | Detailed weighted-average negative/zero pool, partial invoice, valuation floor, chronology and coverage edge behavior | S07, S09 | Never allow unapproved negative inventory or guessing a valuation adjustment; block ambiguous posting. |
| OD-05 | Replacement/return credit and accounting treatment across valued/unvalued, billed/unbilled, paid/unpaid, periods | S10 | Document claims/custody, defer irreversible financial correction unless defined. |
| OD-06 | Qualified point of payment release and evidence required for Dr AP Cr Outbound Clearing | S11 | Draft/file simulation only until release evidence & approval policy specified. Never treat file generation as settlement. |
| OD-07 | Legally accurate Indonesian tax matrix (PPN, 12%/11/12, exempt/non-collected/0-rated, Batam facilities, PPh withholding, recognition moment) | S04, S09, S14 | Effective-dated configurable model and explicit review; no unverified statutory claim or automated filing. |
| OD-08 | Non-PO expense bills, withholding proof/document lifecycle and special invoice cases | S09 | Implement only approved core path; hold excluded cases as labeled not supported. |
| OD-09 | Credit limits, sales discounts, cancellation/revision after fulfillment and customer return policy | S12-S15 | Basic approved-price/positive-quantity workflow; block uncertain accounting/fiscal corrections. |
| OD-10 | Negative stock allowance and reservation priority/allocation policy | S08, S13 | Forbid negative stock and over-reservation by default until explicitly changed. |
| OD-11 | Opening inventory and accounting balance import/reconciliation for production cutover | S16-S18 | Use disposable demo fixtures only; no real migration without signed reconciliation. |
| OD-12 | Data retention, tenant offboarding and tenant-specific recovery operating procedures | S18 | No destructive tenant deletion; require backup/restore and recovery proof. |
| OD-13 | Client-specific numbering and fiscal calendar defaults | S04-S05, S12-S14 | Explicit configured sequences; no implied gapless numbers, no postings into closed periods. |

The BRS is authoritative for **approved directions**: perpetual weighted average; GRNI at valued acceptance; dock custody before valuation; three-way invoice match; immutable posted ledgers; price difference split by remaining coverage; controlled supplier replacement; two-stage payment bank clearing; effective-dated Indonesian tax logic. Numbers, rare exceptions and precise posting conditions explicitly left open in the BRS remain open here.

If a decision blocks a critical transaction, produce a decision request and continue independent UI/schema/test preparation or an unrelated slice, but do not auto-post or create fictional financial evidence.
