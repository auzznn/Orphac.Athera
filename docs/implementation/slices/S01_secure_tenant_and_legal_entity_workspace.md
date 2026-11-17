# ATH-S01 - Secure Tenant and Legal-Entity Workspace

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S00  
**Authoritative module owners:** Platform / Identity / Organization  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
An authenticated person can discover and operate only authorized tenants and legal entities with defense-in-depth PostgreSQL RLS.

### User-acceptance story
User U1 signs in; sees Tenant A and A1; cannot open A2 or Tenant B; another multi-tenant user can switch scopes without leaking previous records.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** platform.Tenant, platform.LegalEntity, identity.UserAccount, identity.TenantMembership, identity.EntityAccessGrant; use actual ASP.NET Core Identity tables. Composite-scoped FKs and active-status indexes.

**Application / API contract.** GET /api/workspaces, GET /api/workspaces/{tenantId}/entities, GET /api/workspaces/{tenantId}/entities/{entityId}. Deny unauthenticated (401); hide foreign/inaccessible resources (404); action-specific forbidden (403 where appropriate).

**Next.js experience (use Impeccable).** Impeccable: shape the scope switcher and entity workspace, no duplicate navigation. Visible current tenant/entity context, predictable return route, compact mobile adaptation and a clear no-access state.

Before editing code, create `docs/design/slices/S01-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement approved domain invariants from scratch; build control-plane membership lookup, distinct auth vs tenant vs entity permission checks, short-lived EF operations, trusted transaction-local set_config and RLS policies, runtime/migration principals, secure auth and CSRF; tests target real PG (no fake success). Implement native PostgreSQL acceptance tests for isolation, permissions and pooling using standard dotnet test.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Missing tenant context denies; cross-tenant read/write denied by runtime role + policy; membership revocation takes effect next protected operation; entity grant alone does not grant tenant membership; pool reuse never carries scope.

## Mandatory executable acceptance tests
PG integration: 2 tenants/3 entities/3 users, forged selector/header, RLS USING/WITH CHECK, default deny, cross-tenant FK, revoked grants, pooling rollback/exception, role bypass checks, CSRF and auth endpoints, concurrent requests.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No supplier/item CRUD, no production tenant provisioning billing, no automated subscription.

## Owner-only decisions / blockers
No Owner decision to bypass RLS, merge global identity with tenant membership, or weaken cross-entity authorization.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S01/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
