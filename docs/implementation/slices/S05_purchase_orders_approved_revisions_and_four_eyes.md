# ATH-S05 - Purchase Orders, Approved Revisions and Four-Eyes

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S02-S04  
**Authoritative module owners:** Procurement / Approval  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
A procurement officer can draft, approve, revise and close a commercial PO without losing immutable approved terms or bypassing segregation-of-duties.

### User-acceptance story
Create multi-line PO, submit, independent approve; revise an approved term by new version; authorized cancel/short-close; view outstanding receipt commitment.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** procurement.PurchaseOrder, PurchaseOrderRevision, PurchaseOrderLine snapshot, DeliveryTolerancePolicy and approval linkage. Preserve TenantId/LegalEntityId scoped constraints.

**Application / API contract.** PO draft/save/submit/approve/revise/close/cancel APIs; typed summary/detail; status and approval history; safe numbering after controlled lifecycle event.

**Next.js experience (use Impeccable).** Impeccable: focused PO editor, item picker, UoM/quantity/price line grid, totals, approval comparison diff, revision badge, sticky action area with precise status explanation.

Before editing code, create `docs/design/slices/S05-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement finite state transitions, revision freezing and immutable approved snapshots, independent approval, quantity and value summary, per-entity numbering, outbox; make receipt/invoice progress derived later.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Cannot approve own action under required policy, change approved price silently, reuse a source approval or delete downstream-referenced PO; no overreceipt silently allowed.

## Mandatory executable acceptance tests
Test draft -> approval, actor separation, concurrent approval, duplicate PO number, revision freeze and unchanged old snapshot, wrong supplier/item scope, cancellation with dependencies.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No dock posting, inventory movement, GRNI or supplier invoice in this slice.

## Owner-only decisions / blockers
OD-01 tolerance values and OD-02 approval thresholds unresolved; use exact PO quantity and require independent approval rather than granting unlimited overdelivery by default.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S05/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
