# ATH-S09 - Supplier Invoice Three-Way Match, AP and Price Difference

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S04,S07-S08  
**Authoritative module owners:** Accounts Payable coordinator / GL / Inventory Valuation / Tax  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Finance matches PO vs accepted receipt vs supplier invoice, handles discrepancies and posts GRNI clearance + AP + tax + moving-average price adjustment atomically.

### User-acceptance story
Invoice 100 units at a changed price after 60 were consumed: apply approved stock-coverage valuation adjustment to 40 remaining; difference on consumed portion goes to approved P&L; block unresolved mismatches.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** ap.SupplierBill, BillLine, SupplierBillReceiptMatch, InvoiceMatchEvaluation, AP payable; gl.ReceiptAccrualClearing; inventory.InvoicePriceAdjustment; tax.TaxDetermination, effective dated policy.

**Application / API contract.** Bill draft/match evaluation/submit/approve/post APIs; source line eligibility, match outcome, payment block, line discrepancy breakdown and bill history.

**Next.js experience (use Impeccable).** Impeccable: side-by-side PO/receipt/invoice comparison grid with highlighted actual mismatches, not meaningless colors; clear reasons for posting/payment blocks, visible source/journal links.

Before editing code, create `docs/design/slices/S09-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement line quantity-capacity locking, three-way PP/DQ/BD independent tolerance evaluation, exact-match safe mode if thresholds absent, tax determination validation, weighted-average invoice correction for on-hand coverage and residual P&L, atomic AP/GRNI/GL/inventory/outbox posting and immutability.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Never bill same accepted capacity twice; no bill posting before eligible acceptance; posted AP balances reconcile; all journals balance; price difference cannot blindly increase current stock for already consumed units; failed posting rolls back all.

## Mandatory executable acceptance tests
Boundary tests: exact vs tolerance, partial bills, duplicate source, race for same receipt capacity, price negative/positive, zero inventory coverage, rollback injected, closed period, tax code effective date and source reconciliation.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No automatic foreign currency, supplier advances or non-PO expense bill posting without policy approval.

## Owner-only decisions / blockers
OD-03 mismatch tolerances/write-off; OD-04 coverage chronology; OD-07 tax treatment; OD-08 non-PO/withholding. Block ambiguous financial postings and present clear decision requests.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S09/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
