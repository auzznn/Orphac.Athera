# ATH-S10 - Supplier Returns, Replacements and Financial Corrections

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S06-S09  
**Authoritative module owners:** Procurement / Inventory / AP / GL  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Preserve supplier claims and original transaction traceability while reversing or correcting operational/financial effects, without double stock or double bills.

### User-acceptance story
Reject unvalued dock goods; receive linked replacement; alternatively return valued goods with linked claim and authorized financial correction; follow billed vs unbilled cases.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** procurement.SupplierReturn/ReturnLine/ReplacementAuthorization, source claim links; ap.SupplierCredit; inventory compensating movements; gl reversal/correction source links.

**Application / API contract.** Return request/approve/dispatch, replacement authorization/receipt, credit and correction evidence endpoints; source-to-correction drilldown.

**Next.js experience (use Impeccable).** Impeccable: clear transaction lineage and case-specific actions; show original PO/receipt/lot, valuation status, billed state, credit requirement and unresolved items.

Before editing code, create `docs/design/slices/S10-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Model explicit matrix of unvalued vs valued, billed vs unbilled, paid vs unpaid, period open vs closed. Implement safe supported cases; create append-only reversal/compensation, idempotent claims/credits and linked replacement entitlement; stop undefined cases.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
No free extra quantity, zero incremental commercial charge does NOT imply zero inventory value, no silent posted-ledger edits; original + correction chain reconciles.

## Mandatory executable acceptance tests
Tests: unvalued reject no GL; linked replacement no duplicate billing; valued return compensation; duplicate return prevention; concurrent replacements; closed-period correction block; audit trail completeness.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No universal one-click reversal for every combination; no forced zero-value accounting when it creates incorrect valuation.

## Owner-only decisions / blockers
OD-05 full correction matrix and OD-07 statutory credit note/PPN specifics; material scenarios require Owner/accountant policy.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S10/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
