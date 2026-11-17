# ATH-S13 - Sales Reservation, Pick and Dispatch

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S08,S12  
**Authoritative module owners:** Fulfillment / Inventory  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Warehouse can reserve eligible available stock against an approved sales order, pick and dispatch it without overselling or using blocked stock.

### User-acceptance story
Approved order shows available quantity; warehouse reserves, picks and dispatches partial shipment; source-linked stock issue reduces inventory and posts approved COGS/stock journal when applicable.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** fulfillment.PickOrder, PickLine, Shipment/DeliveryEvidence; inventory.StockReservation, StockMovement and ValuationEntry; GL inventory/COGS posting source if configured.

**Application / API contract.** Reserve, release, pick, dispatch, confirm delivery endpoints; stock availability read + shipment progress in Sales.

**Next.js experience (use Impeccable).** Impeccable: quick fulfillment queue with location, pick quantity and availability, clear partial status, compact mobile/readable warehouse layout.

Before editing code, create `docs/design/slices/S13-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement concurrency-safe reserve under pool/location availability, lot policy, pick/ship state machine, deduped dispatch, weighted-average inventory issue and authorized transactional GL COGS posting; ship source/ledger links together.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Cannot reserve or ship blocked/quarantined stock; total reservations <= available; dispatch cannot exceed authorized unfulfilled quantity; inventory issue & COGS are atomic, no negative stock default.

## Mandatory executable acceptance tests
Test competing sales orders reserve same stock, partial ship, restricted lot, cancellation/release, retry dispatch, journal balance and stock conservation.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No complex routing, cross-warehouse optimized picking, carrier integrations or high-volume WMS.

## Owner-only decisions / blockers
OD-10 reserve policy; OD-09 customer/order cancellation after partial fulfillment. Safe no-negative default.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S13/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
