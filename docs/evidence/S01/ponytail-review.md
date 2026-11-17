# ATH-S01 Ponytail (`full`) Engineering Review

**Reviewer Mode:** Ponytail (`full`)  
**Scope:** Full S01 vertical slice — `Platform` (`Tenant`, `LegalEntity`, `TenantScopeExecutor`), `Identity` (`TenantMembership`, `EntityAccessGrant`), EF Core `M01_TenantAndLegalEntityWorkspace` forced-RLS migration, `WorkspaceEndpoints`, `DemoDataSeeder`, Next.js `AppShell` Scope Switcher, `/workspaces`, `/w/[tenantId]/[entityId]`, and S01 tests.

## Review Checklist & Findings
1. **Minimalism & No Speculative Infrastructure:** PASS
   - Only S01 tables (`platform.tenants`, `platform.legal_entities`, `identity.tenant_memberships`, `identity.entity_access_grants`) and read-only scope discovery/verification endpoints (`/api/workspaces`, `/api/workspaces/{tenantId}/entities`, `/api/workspaces/{tenantId}/entities/{entityId}`) were added.
   - Zero speculative role CRUD, invitation engines, or downstream ERP tables (`S02`–`S18`) were added.
2. **DRY & Single Responsibility:** PASS (with 1 deferred S02 refinement note)
   - Validation helpers (`GuardValidation`, status classes) prevent duplicated string/ISO/length checks across `Tenant` and `LegalEntity`.
   - Frontend `/workspaces` renders one single responsive semantic `<table>` (`block` on mobile, `md:table-row` on desktop) instead of duplicating markup across breakpoints; `AppShell.tsx` extracts `renderScopePanel` cleanly.
   - `TenantScopeExecutor.DiscoverWorkspacesForUserAsync` and `WorkspaceEndpoints.MapWorkspaceEndpoints` (`GET /api/workspaces/{tenantId}/entities`) share a small 14-line active-grant join query; kept explicit per endpoint for readability rather than premature query-builder abstraction, natural to unify when `S02` introduces role assignments.
3. **Database & Security Invariants:** PASS
   - All tenant-owned FKs use composite `(tenant_id, legal_entity_id)` and `(tenant_id, user_id)` keys with `DeleteBehavior.Restrict`.
   - `M01` enables `ENABLE ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY` on all 4 tenancy tables; runtime role `athera_app` lacks `SUPERUSER`, `BYPASSRLS`, and table ownership, and holds read-only `SELECT` on `platform.tenants`.
   - Explicit `BeginTransactionAsync()` + transaction-local `set_config(..., ..., true)` verified against 60 concurrent/rollback/exception tasks on `MaxPoolSize=4` (`S01_PG_06`).
   - React entity sheet uses `key={`${tenantId}:${entityId}`}` + `cache: "no-store"` so switching tenants/entities cannot leak stale React or fetch state.
4. **Verification Discipline:** PASS
   - Verified strictly against real PostgreSQL 18 (`18/18` Unit + `14/14` Integration tests) and real Next.js + ASP.NET Core 10 Chromium E2E (`4/4` suites). Zero InMemory/SQLite shortcuts.

**Verdict:** **Ship** (meets Ponytail `full` quality, invariant safety, and maintainability standards).
