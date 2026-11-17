# ATH-S02 - Organization Administration, Approvals and Audit Core

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S01  
**Authoritative module owners:** Identity / Organization / Approval / Audit / Integration  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
An authorized administrator can invite and scope users; business approvals capture initiator, approver, policy and audit trail; reliable outbox can be used by later slices.

### User-acceptance story
Administrator invites user, assigns entity-scoped permissions, suspends access; user requests protected action and a distinct authorized approver decides it with evidence.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** identity.Invitation, membership/grant lifecycle, approval.Policy/Case/Decision/ActorParticipation, audit.AuditEvent, integration.OutboxMessage/InboxReceipt/CommandIdempotency; only entities needed by first workflows.

**Application / API contract.** Role/permission administration endpoints, invitation acceptance, approval submission/decision endpoints; basic audit read and outbox processing status behind admin authorization.

**Next.js experience (use Impeccable).** Impeccable: design people-and-permissions table, effective access preview, obvious high-risk approval states, helpful why-denied copy and minimal approval inbox. Keep high-stakes actions away from ambiguous icon-only buttons.

Before editing code, create `docs/design/slices/S02-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement secure invite/revoke/suspend, MFA support for elevated roles, role assignments, four-eyes approval service and immutable decision evidence; add transactional outbox/inbox + idempotency that later posting slices reuse; scoped worker and audit of privilege changes. Do not overbuild generic BPM/workflow designer.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
No self-approval where four-eyes applies; rejoining never reactivates stale grants; no role escalation via crafted request; failed outbox consumer cannot duplicate business effect; audit evidence append-only.

## Mandatory executable acceptance tests
Test invite expiry, revoked membership access, entity role restrictions, approve-your-own rejection, concurrent decisions/duplicates, outbox retry/idempotency and tenant-safe worker context.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No custom workflow designer, rules DSL, microservice broker or autonomous superadmin.

## Owner-only decisions / blockers
Four-eyes thresholds, allowed role combinations and SME exception policy remain OD-02. For sensitive actions requiring an undefined threshold, fail closed or require distinct approver.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S02/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
