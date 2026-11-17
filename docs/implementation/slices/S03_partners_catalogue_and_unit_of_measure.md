# ATH-S03 - Partners, Catalogue and Unit of Measure

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S01-S02  
**Authoritative module owners:** Partners / Catalogue  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
A purchasing/sales user can maintain cross-entity tenant master data, with entity-specific eligibility and accurate UoM conversions.

### User-acceptance story
Create one Partner serving both supplier and customer roles; define a stock item and purchasing unit conversion; restrict the item/supplier to a legal entity if required.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** partners.Partner, PartnerContact/Address, PartnerEntityProfile; catalog.Item, UnitOfMeasure, ItemUom, ItemEntityPolicy, basic status/history. Avoid putting stock balance on Item.

**Application / API contract.** Paginated search/create/detail/update APIs, contacts and roles, item/UoM and per-entity eligibility. Public IDs treated as selectors; tenant/entity checks on every mutation.

**Next.js experience (use Impeccable).** Impeccable: design usable dense searchable partner and item tables, high-confidence forms, clear status and eligibility chips, detail drawers only if context warrants them. Preserve long names and regional formatting.

Before editing code, create `docs/design/slices/S03-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement validated normalization, composite tenant FKs, safe creation/editing/deactivation, UoM positive factors and base-unit conversions; capture immutable snapshots in downstream transactions rather than changing historical records.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Partner/item shared only within tenant; entity policies enforced at transactions; UoM conversions exact under approved rounding rules; no cross-tenant master record; referenced masters not hard-deleted.

## Mandatory executable acceptance tests
Create duplicate-code and foreign-tenant tests; UoM edge and round-trip tests; entity eligibility and deactivation tests; UI search/filter/permission E2E.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No current stock field, foundry material fields, unsupported unit conversion guessing or inventory costing on master item.

## Owner-only decisions / blockers
Initial partner-tax attributes only metadata; detailed statutory decision requires OD-07.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S03/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
