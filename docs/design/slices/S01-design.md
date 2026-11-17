# ATH-S01 Design — Secure Tenant and Legal-Entity Workspace

**Slice:** S01 (`docs/implementation/slices/S01_secure_tenant_and_legal_entity_workspace.md`)  
**Status:** Workstream-authorized engineering scope; Product Owner acceptance pending  
**Prerequisites:** S00 (ASP.NET Core Identity + CSRF + PostgreSQL `athera_migrator` / `athera_app` roles)  
**Authoritative module owners:** `Platform`, `Identity`

---

## 1. Business Behavior & User Journey

An authenticated user discovers and enters only the tenants and legal entities they are authorized for:
- **U1 (`rina.utami@demo.athera.local`)**: signs in -> authorized only for Tenant A (`PT Nusantara Niaga Utama` / `NUSA`) and Entity A1 (`NUSA-JKT`). `NUSA-SBY` (A2) and Tenant B (`CV Samudra Teknik Mandiri` / `SMDR` / `SMDR-BDG`) never appear in workspace lists or scope switchers; direct navigation or API access to A2 or Tenant B returns `404` (`workspace_not_found` / `entity_not_found`) with zero foreign entity details leaked.
- **U2 (`budi.santoso@demo.athera.local`)**: multi-tenant controller authorized for `NUSA-JKT`, `NUSA-SBY`, and `SMDR-BDG`. Switches cleanly between entities and tenants using the top-bar Scope Switcher without stale cross-scope cache or React state bleed.
- **U3 (`hendra.wijaya@demo.athera.local`)**: authenticated identity with zero active memberships/grants -> sees a clear accessible no-access workspace directory explaining how an Organization Administrator provisions access.

---

## 2. Module Ownership, Aggregate Boundaries & State Transitions

| Module | Authoritative Tables | Owns | Must NOT own |
|---|---|---|---|
| `Platform` | `platform.tenants`, `platform.legal_entities`, `platform.current_tenant_id()`, `platform.current_actor_user_id()`, `TenantScopeExecutor` | Tenant registry, LegalEntity registry, trusted SQL scope setup | User credentials, stock, commercial orders, GL journals |
| `Identity` | `identity.asp_net_*`, `identity.user_accounts`, `identity.tenant_memberships`, `identity.entity_access_grants` | Global identity, tenant membership lifecycle, per-entity permission grants | Tenant/legal-entity master metadata, commercial/financial postings |

### Lifecycles & Invariants
- `Tenant.Status` and `LegalEntity.Status`: `ACTIVE`, `SUSPENDED`, `ARCHIVED`. Only `ACTIVE` tenants and `ACTIVE` legal entities permit workspace access.
- `TenantMembership.Status` and `EntityAccessGrant.Status`: `ACTIVE`, `SUSPENDED`, `REVOKED`.
- **No self-healing stale grants:** Suspending or revoking a `TenantMembership` takes effect on the very next protected operation even if `EntityAccessGrant` rows remain `ACTIVE`. Grant rows never reactivate themselves automatically on rejoin.
- **Validity windows:** `EntityAccessGrant` enforces `valid_until_utc IS NULL OR valid_until_utc > valid_from_utc` and evaluates `ValidFromUtc <= nowUtc && (ValidUntilUtc == null || ValidUntilUtc > nowUtc)`.
- **No cascading delete across business history:** All tenant/entity/membership/grant foreign keys use `ON DELETE RESTRICT`.

---

## 3. PostgreSQL Schema, Composite Scoped FKs & Forced RLS (`M01_TenantAndLegalEntityWorkspace`)

### Tables & Keys
1. `platform.tenants`
   - `tenant_id uuid PRIMARY KEY`
   - `code varchar(64) NOT NULL UNIQUE`
   - `name varchar(200) NOT NULL`
   - `status varchar(32) NOT NULL CHECK (status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED'))`
   - `created_at_utc timestamptz NOT NULL`, `updated_at_utc timestamptz NOT NULL`, `version bigint NOT NULL DEFAULT 1`
2. `platform.legal_entities`
   - `legal_entity_id uuid PRIMARY KEY`
   - `tenant_id uuid NOT NULL REFERENCES platform.tenants(tenant_id) ON DELETE RESTRICT`
   - `code varchar(64) NOT NULL`
   - `name varchar(200) NOT NULL`
   - `tax_registration_npwp varchar(64) NULL`
   - `functional_currency_code char(3) NOT NULL DEFAULT 'IDR'`
   - `country_code char(2) NOT NULL DEFAULT 'ID'`
   - `time_zone_id varchar(64) NOT NULL DEFAULT 'Asia/Jakarta'`
   - `status varchar(32) NOT NULL CHECK (status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED'))`
   - `created_at_utc`, `updated_at_utc`, `version bigint NOT NULL DEFAULT 1`
   - Unique constraints: `UNIQUE (tenant_id, legal_entity_id)` (composite alternate key), `UNIQUE (tenant_id, code)`
   - Index: `(tenant_id, status)`
3. `identity.tenant_memberships`
   - `membership_id uuid PRIMARY KEY`
   - `tenant_id uuid NOT NULL REFERENCES platform.tenants(tenant_id) ON DELETE RESTRICT`
   - `user_id uuid NOT NULL REFERENCES identity.user_accounts(user_id) ON DELETE RESTRICT`
   - `status varchar(32) NOT NULL CHECK (status IN ('ACTIVE', 'SUSPENDED', 'REVOKED'))`
   - `created_at_utc`, `updated_at_utc`, `revoked_at_utc timestamptz NULL`, `version bigint NOT NULL DEFAULT 1`
   - Unique constraints: `UNIQUE (tenant_id, user_id)` (composite alternate key), `UNIQUE (tenant_id, user_id, membership_id)`
   - Index: `(user_id, status)`
4. `identity.entity_access_grants`
   - `grant_id uuid PRIMARY KEY`
   - `tenant_id uuid NOT NULL`
   - `legal_entity_id uuid NOT NULL`
   - `user_id uuid NOT NULL`
   - `permission_code varchar(100) NOT NULL`
   - `status varchar(32) NOT NULL CHECK (status IN ('ACTIVE', 'SUSPENDED', 'REVOKED'))`
   - `valid_from_utc timestamptz NOT NULL`, `valid_until_utc timestamptz NULL CHECK (valid_until_utc IS NULL OR valid_until_utc > valid_from_utc)`
   - `created_at_utc`, `updated_at_utc`, `revoked_at_utc timestamptz NULL`, `version bigint NOT NULL DEFAULT 1`
   - **Composite FKs:**
     - `FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entities(tenant_id, legal_entity_id) ON DELETE RESTRICT`
     - `FOREIGN KEY (tenant_id, user_id) REFERENCES identity.tenant_memberships(tenant_id, user_id) ON DELETE RESTRICT`
   - Unique constraint: `UNIQUE (tenant_id, legal_entity_id, user_id, permission_code)`
   - Index: `(tenant_id, user_id, legal_entity_id, status)`

### RLS Helper Functions & Policies
- `platform.current_tenant_id() RETURNS uuid` (`SECURITY INVOKER`, `STABLE`, `SET search_path = pg_catalog`): parses `NULLIF(current_setting('app.tenant_id', true), '')` safely (`EXCEPTION WHEN invalid_text_representation THEN RETURN NULL`).
- `platform.current_actor_user_id() RETURNS uuid` (`SECURITY INVOKER`, `STABLE`, `SET search_path = pg_catalog`): parses `NULLIF(current_setting('app.actor_user_id', true), '')` safely.
- **Role privileges for `athera_app`:**
  - `GRANT USAGE ON SCHEMA platform TO athera_app;`
  - `GRANT EXECUTE ON FUNCTION platform.current_tenant_id(), platform.current_actor_user_id() TO athera_app;`
  - `GRANT SELECT ON platform.tenants TO athera_app;` (read-only; runtime cannot insert/update/delete tenants)
  - `GRANT SELECT, INSERT, UPDATE, DELETE ON platform.legal_entities, identity.tenant_memberships, identity.entity_access_grants TO athera_app;`
- **Row-Level Security (`ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` on all 4 tables):**
  - `platform.legal_entities`: `FOR ALL TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id())`
  - `identity.entity_access_grants`: `FOR ALL TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id())`
  - `identity.tenant_memberships`:
    - `FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id() OR (user_id = platform.current_actor_user_id() AND status = 'ACTIVE'))`
    - `FOR INSERT, UPDATE, DELETE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id())`
  - `platform.tenants`:
    - `FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id() OR EXISTS (SELECT 1 FROM identity.tenant_memberships tm WHERE tm.tenant_id = tenants.tenant_id AND tm.user_id = platform.current_actor_user_id() AND tm.status = 'ACTIVE' AND tenants.status = 'ACTIVE'))`
  - Migration/control-plane role `athera_migrator` receives explicit `FOR ALL TO athera_migrator USING (true) WITH CHECK (true)` (`p_*_migrator_all`) on all four tables because `FORCE ROW LEVEL SECURITY` also restricts non-superuser table owners lacking `BYPASSRLS`; runtime `athera_app` remains strictly isolated.

---

## 4. Trusted Control-Plane & Transaction-Local Scope Runner (`TenantScopeExecutor`)

1. Client route parameters `{tenantId}` and `{entityId}` and headers like `X-Tenant-Id` are **untrusted selectors only** (headers are ignored).
2. `ExecuteControlPlaneForUserAsync(Guid userId, ...)`:
   - Opens explicit DB transaction on `AtheraDbContext`.
   - Sets `SELECT set_config('app.actor_user_id', @uid, true), set_config('app.tenant_id', '', true);`.
   - Queries active memberships + active tenants, then for each active tenant enters an explicit tenant-scoped step (`set_config('app.tenant_id', @tid, true)`) to resolve authorized active legal entities for that user.
3. `ExecuteInTenantAsync(Guid userId, Guid requestedTenantId, ...)`:
   - Begins explicit DB transaction on `AtheraDbContext`.
   - Sets `set_config('app.actor_user_id', @uid, true), set_config('app.tenant_id', '', true);` to check active `UserAccount`, active `TenantMembership` for `(requestedTenantId, userId)`, and active `Tenant`.
   - If any check fails -> rolls back immediately and returns `ScopeDenial.NotFound` (`404 workspace_not_found`, hiding foreign existence).
   - If valid -> runs `SELECT set_config('app.tenant_id', @verifiedTenantId, true);` inside the **same open transaction** and executes the business delegate, then commits (or rolls back on exception, ensuring zero pooled connection leakage).
4. `ExecuteInEntityAsync(Guid userId, Guid requestedTenantId, Guid requestedEntityId, string? requiredPermission, ...)`:
   - Executes inside `ExecuteInTenantAsync`.
   - Verifies `platform.legal_entities` row `(requestedTenantId, requestedEntityId)` is `ACTIVE`.
   - Queries active grants (`status = 'ACTIVE'`, `valid_from_utc <= nowUtc`, `valid_until_utc IS NULL OR valid_until_utc > nowUtc`) for `(requestedTenantId, requestedEntityId, userId)`.
   - If entity does not exist OR user holds **zero** active grants (no `"workspace.view"` or any active grant on that entity) -> `404 entity_not_found` (so `U1` cannot probe `A2`).
   - If `requiredPermission` is supplied and user lacks it -> `403 insufficient_entity_permission`.

---

## 5. API Contracts

| Endpoint | Auth | Response | Failure Semantics |
|---|---|---|---|
| `GET /api/workspaces` | `[Authorize]` | `200 WorkspacesResponse` (list of authorized active tenants + authorized active entities + grant codes) | `401 unauthenticated` |
| `GET /api/workspaces/{tenantId:guid}/entities` | `[Authorize]` + Tenant membership | `200 TenantEntitiesResponse` (tenant metadata + authorized active entities) | `401`, `404 workspace_not_found` |
| `GET /api/workspaces/{tenantId:guid}/entities/{entityId:guid}` | `[Authorize]` + Tenant + Entity visibility | `200 EntityWorkspaceDetailResponse` (tenant, entity metadata, effective permissions, navigation capabilities, `serverTimestampUtc`) | `401`, `404 workspace_not_found` / `entity_not_found`, `403 insufficient_entity_permission` |
| `GET /api/auth/me` (updated) | `[Authorize]` | `CurrentUserResponse` with live `authorizedWorkspaceCount` and `"ACTIVE_WORKSPACES"` / `"NO_ACTIVE_WORKSPACE"` | `401 unauthenticated` |

---

## 6. Next.js Workspace Experience (`apps/web`)

- `/` redirects authenticated users seamlessly to `/workspaces` (which lists available tenants/entities or automatically opens the sole authorized entity workspace when count = 1, or displays the clear no-access empty state for `U3`).
- `/w/[tenantId]/[entityId]` renders inside a single unified `WorkspaceShell` (no duplicated sidebars):
  - Sticky top bar shows explicit **Active Scope Switcher** (`NUSA · PT Nusantara Distribusi Jakarta · IDR`) dropdown allowing authorized multi-tenant users (`U2`) to jump cleanly between `NUSA-JKT`, `NUSA-SBY`, and `SMDR-BDG`.
  - Switching scope uses a keyed React subtree (`key={`${tenantId}:${entityId}`}`) and `cache: "no-store"` API calls so previous tenant/entity state never leaks visually.
  - Permission-driven sidebar renders permitted modules cleanly and marks unbuilt downstream slice modules honestly (`Segera di S03–S16`).
  - Navigating to `/w/{tenantA}/{entityA2}` or `/w/{tenantB}/{entityB1}` renders an explicit, non-leaking `Scope Unavailable / Ruang Kerja Tidak Tersedia` boundary with a safe link back to `/workspaces`.
  - Mobile (`390px`) collapses navigation cleanly behind a slide-over drawer with zero horizontal scroll.

---

## 7. S01 Mandatory Executable Tests

| Test ID | Verifies |
|---|---|
| `S01-PG-01` | `M01` Up -> Down (`M00`) -> Down (`0`) -> Up (`M01`) cleanly |
| `S01-PG-02` | `athera_app` has `rolsuper=false`, `rolbypassrls=false`, owns 0 tables, and `relrowsecurity=true` & `relforcerowsecurity=true` on all 4 tenancy tables |
| `S01-PG-03` | Raw `athera_app` connection with missing `app.tenant_id` reads 0 rows and fails `INSERT` with SQLSTATE `42501` |
| `S01-PG-04` | Raw `athera_app` scoped to Tenant A cannot `SELECT`/`UPDATE`/`DELETE`/`INSERT` Tenant B rows (`42501` / 0 rows affected) |
| `S01-PG-05` | Composite FK rejects mismatched `(tenantA, entityB1)` or `(tenantA, userWithoutTenantAMembership)` with SQLSTATE `23503` even under `athera_migrator` |
| `S01-API-01..03` | `U1` sees `A1` only (`A2` & `TenantB` return `404`), spoofed `X-Tenant-Id` header ignored; `U2` switches `NUSA-JKT` -> `NUSA-SBY` -> `SMDR-BDG`; `U3` gets empty workspace list |
| `S01-API-04` | Suspending/revoking `U1`'s `TenantMembership` while `EntityAccessGrant` remains `ACTIVE` causes immediate `404` on next `GET /api/workspaces/{tenantA}/entities/{A1}` |
| `S01-PG-06` | 60 concurrent/interleaved operations on `NpgsqlDataSource` (`MaxPoolSize=4` as `athera_app`) mixing Tenant A, Tenant B, rolled-back transactions, and mid-tx exceptions produce 0 cross-tenant leaks and empty `current_setting('app.tenant_id', true)` on idle pooled connections |
| `S01-E2E-01..03` | Playwright full-stack flows for `U1` (authorized + hostile URL probes), `U2` (multi-tenant scope switching), `U3` (no-access state), desktop + mobile screenshots |

**Owner-only decisions:** No Product Owner sign-off requested to weaken RLS or bypass entity checks; S02 will govern invitation/four-eyes administration.
