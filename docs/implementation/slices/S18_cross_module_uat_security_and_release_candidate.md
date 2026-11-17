# ATH-S18 - Cross-Module UAT, Security and Release Candidate

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** All S00-S17  
**Authoritative module owners:** Product Engineering / Security / Finance / Product Owner (acceptance only)  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Produce a demonstrable, recoverable, auditable ERP MVP candidate whose business flows, tenant security, financial ledgers and UI are independently reviewable by the Product Owner.

### User-acceptance story
Run one end-to-end P2P story and one O2C story from real logged-in users; demonstrate cross-tenant attacks denied, posting rollback, exception resolution, period close and backup recovery.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** No major new tables; migration plan, seed/demonstration dataset, runbooks, backups and evidence/acceptance records. Only add defects fixes.

**Application / API contract.** Reproducible staging/local runbook, test matrix, evidence index, health checks and production rollout proposal. No production deploy.

**Next.js experience (use Impeccable).** Impeccable final critique/audit/polish of key daily journeys, including 1280px desktop, tablet width and accessible keyboard operation. Remove redundant actions and empty dashboard chrome.

Before editing code, create `docs/design/slices/S18-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Run normal full-stack CI, real PG migration/recovery drills, RLS abuse tests, API permission fuzzing, ledger reconciliation, concurrency soak for posting and reservation, observability/privacy review and dependency audit. Prepare unsigned owner signoff forms with exact SHA, business demonstration videos/screenshots if tools available.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
No blocker-class critical bugs; no unknown financial side effects; correct backup/restore; verified cross-tenant isolation; audited source-to-ledger results; only human may accept and authorize production.

## Mandatory executable acceptance tests
Full P2P and O2C scripted UAT; high-stakes negative tests; performance at representative data volumes; restore rehearsal; migration rollback/forward plan; standard runner/real PG logs; PO four-pillar acceptance packet.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No production deployment, no paid infrastructure provision and no retrospective fabricated sign-off.

## Owner-only decisions / blockers
Owner must approve exceptions, pilot scope, data opening balances, statutory tax position and production release separately. Passing engineering tests is not acceptance.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S18/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
