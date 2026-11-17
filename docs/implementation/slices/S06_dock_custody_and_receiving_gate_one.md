# ATH-S06 - Dock Custody and Receiving Gate One

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S05  
**Authoritative module owners:** Procurement / Receiving  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Record physically delivered supplier goods before they qualify for inventory ownership/valuation, including rejection and outstanding custody quantities.

### User-acceptance story
Warehouse records arriving goods against approved revision; flags damaged/excess quantity; holds/rejects or releases eligible goods to acceptance; finance sees no GRNI yet.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** procurement.GoodsReceipt, GoodsReceiptLine, DockCheck, dock custody/disposition and source links; no valued Inventory movement until Gate Two.

**Application / API contract.** Receipt register, dock document entry and disposition API; PO eligible line selection and outstanding custody calculation.

**Next.js experience (use Impeccable).** Impeccable: receiving station focused on quantity/count, photos/docs (if infrastructure available), damage reason and correct action; large controls where shop-floor workflow requires.

Before editing code, create `docs/design/slices/S06-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement PO/revision snapshot check, source delivery document, partial receipt and line conservation, dock discrepancy and approval required for excess. Explicit unvalued custody statuses and idempotent dock events.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Physical quantity = dock rejected/returned unvalued + valued accepted + unresolved custody under consistent UoM; dock custody does not create Inventory/GRNI or available stock.

## Mandatory executable acceptance tests
PG tests for duplicate receipt source, overdelivery held, partial delivery, zero finance effects, dock reject vs pending custody and cross-tenant receipt/PO mismatch.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No valued acceptance, inventory/financial posting or automatic replacement stock.

## Owner-only decisions / blockers
OD-01 if overreceipt requires approval; capture exception but do not post excess.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S06/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
