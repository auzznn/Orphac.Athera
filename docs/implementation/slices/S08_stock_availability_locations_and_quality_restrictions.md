# ATH-S08 - Stock Availability, Locations and Quality Restrictions

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S07  
**Authoritative module owners:** Inventory / Quality  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Operators can distinguish physical, valued, quarantined, blocked, reserved and available quantities; quality disposition changes availability without silently modifying valuation.

### User-acceptance story
Valued stock arrives quality-restricted; authorized inspector releases some, blocks some; inventory availability excludes blocked stock; location transfer preserves total quantity.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** inventory.Warehouse, Location, StockLot per item policy, StockPosition, Movement/Status history, reservation skeleton; quality.ReceiptInspection and InspectionDisposition minimal.

**Application / API contract.** Location/status transfer, lot trace, stock position and availability API; authorized quality release/hold actions.

**Next.js experience (use Impeccable).** Impeccable: a single stock detail context with physical vs available breakdown, lot/location tabs only if necessary, traceable state timeline and precise status semantics.

Before editing code, create `docs/design/slices/S08-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Add location/lot policies, restricted/available state model, status transition log and qualified role checks; reconcile quantity projections to append-only movements. Implement reservation only to necessary extent for S13, avoiding speculative WMS workflow.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Available <= eligible unrestricted valued quantity minus reservations; quality hold alone is not inventory write-off; no untracked stock disappearance; movements balanced across locations and status buckets.

## Mandatory executable acceptance tests
Test release/block and cross-status stock conservation, lot policy, reservation overdraw, unauthorized inspection, concurrent availability mutation and reconciliation.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No advanced inspection sampling plans, recall graph, RFID/WMS automation.

## Owner-only decisions / blockers
OD-10 negative stock/reservation policy; default no negative/overcommit.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S08/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
