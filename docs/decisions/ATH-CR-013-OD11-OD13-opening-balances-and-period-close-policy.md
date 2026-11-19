# ATH-CR-013 — Opening Balances, Period Close & Controlled Reopening Policy (OD-11 / OD-13)

- **Decision ID:** `ATH-CR-013`
- **Related Open Decisions:** `OD-11` (Real opening balances cutover), `OD-13` (Fiscal period calendar, close & reopen convention)
- **Slice:** `S16` — Accounting Reports, Reconciliations and Period Close
- **Status:** `OPEN` (Safe deterministic defaults enforced in engineering build; Product Owner sign-off required before production release)
- **Product Owner Sign-Off:** *(BLANK — PENDING HUMAN APPROVAL)*
- **Date Approved:** *(BLANK)*

## Safe Engineering Defaults Implemented Pending Owner Confirmation

1. **No Production Reconciliation Claims from Demo Data (`OD-11`):**
   - Demo seed data is strictly disposable; production opening balances must be cut over via audited opening journals once `OD-11` is signed off by the Product Owner.
2. **Period Close & Lock Controls (`OD-13`):**
   - Closing a period (`OPEN` → `CLOSED`) acquires a `FOR UPDATE` row lock on `gl.financial_periods`, verifies Trial Balance equilibrium (`Total Debits == Total Credits`) and 5-control GL-to-subledger reconciliation (`1120`, `1200`, `1300`, `2110 GRNI`, `2100 AP`), and appends an immutable `gl.period_close_reviews` audit record.
   - Locking a closed period (`CLOSED` → `LOCKED`) requires `finance.config.manage` and permanently blocks subsequent postings and reopening.
   - Controlled reopening (`CLOSED` → `OPEN`) requires `finance.config.manage` and a mandatory written justification; reopening a `LOCKED` period is deterministically blocked pending Owner override policy (`OD-13`).
   - All posted journals, period close reviews, and financial statements remain source-traceable with zero silent backdating.

## Owner Decisions Requested

- [ ] Confirm production opening balance cutover date and audited opening Trial Balance (`OD-11`).
- [ ] Confirm whether fiscal year follows standard Jan–Dec calendar year and whether closing with open unbilled GRNI (`2110`) should be a warning (default) or hard block (`OD-13`).
- [ ] Confirm statutory authority required to unlock a `LOCKED` period (`OD-13`).
