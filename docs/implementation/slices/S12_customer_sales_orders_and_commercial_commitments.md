# ATH-S12 - Customer Sales Orders and Commercial Commitments

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S03-S04  
**Authoritative module owners:** Sales / Approval  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Sales users can create and approve customer orders using shared Items/Partners, preserving approved commercial revisions and line quantities.

### User-acceptance story
Sales quotes/order draft -> customer selection -> lines/prices -> approval -> reservation-ready order -> approved amendment or cancellation.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** sales.SalesOrder, SalesOrderRevision, SalesOrderLine, customer commercial terms snapshots and approval references.

**Application / API contract.** Sales order list/detail/draft/submit/approve/revise/close/cancel APIs; stock availability is read via Inventory contract, not direct table modifications.

**Next.js experience (use Impeccable).** Impeccable: accessible sales order grid and editor; delivery promise, price and tax context, revision diff and status trail; one primary action per state.

Before editing code, create `docs/design/slices/S12-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement state machine and tenant/entity constraints, customer eligibility, controlled numbering, immutable approved price/UoM snapshots, independent approval where required, outbox and realistic order status.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Cannot silently change approved price/quantity or delete referenced order; inactive customer not eligible for new order; approved source and entity preserved; no negative quantity.

## Mandatory executable acceptance tests
Test orders/line totals, cross-tenant customer/item, approval separation, revision snapshots, duplicate approve and canceled fulfillment restrictions.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No CRM campaigns, advanced pricing engine, omnichannel ecommerce, manufacturing-to-order planning.

## Owner-only decisions / blockers
OD-09 sales credit limits/discount/cancel policy unresolved; implement safe positive-price default and flag unsupported cases.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S12/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
