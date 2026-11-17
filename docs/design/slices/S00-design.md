# ATH-S00 Design — Greenfield Full-Stack Foundation and Authenticated Path

**Slice:** S00 (`docs/implementation/slices/S00_greenfield_full_stack_foundation.md`)  
**Status:** Preauthorized greenfield engineering design (Product Owner acceptance not granted)  
**Baseline SHA:** `e25008f` (`greenfield/mvp`)  
**Assumptions note (unattended `/goal` mode):** Per `prompts/00_MASTER_GOAL.md`, `PRODUCT.md` and S00 UX assumptions were derived strictly from `reference/ATH-PRD-001-v0.2.md`, `reference/ATH-BRS-P2P-001-v0.3.md`, `docs/design/ATH-UX-001.md`, and `docs/product/ATH-OPEN-DECISIONS-001.md` without blocking for interactive prompts. No product or financial policy was altered or self-approved.

---

## 1. Repository Layout & Verified Tooling

Follows `docs/architecture/ATH-SOLUTION-STRUCTURE.md` without speculative domain assemblies:

- `Orphac.Athera.sln`
  - `src/Orphac.Athera.Modules` (class library: `Identity`, `Platform`, `Persistence`)
  - `src/Orphac.Athera.Api` (ASP.NET Core 10 Web API host, auth, CSRF, OpenAPI export, CLI `--migrate` / `--seed-demo` / `--export-openapi`)
  - `tests/Orphac.Athera.UnitTests` (xUnit deterministic rule & validation tests)
  - `tests/Orphac.Athera.IntegrationTests` (xUnit real PostgreSQL 18 + `WebApplicationFactory<Program>` tests)
- `apps/web` (Next.js App Router + React + TypeScript + Tailwind CSS + `openapi-typescript`/`openapi-fetch` + Vitest)
- `tests/e2e` (or `apps/web/tests/e2e` executed via `npm run e2e` with Playwright Chromium)
- `docs/contracts/openapi.v1.json` (generated from `Orphac.Athera.Api`)
- `scripts/dev/bootstrap-postgres.sh` (disposable local PostgreSQL 18 startup + role provisioning on `127.0.0.1:55433`)

Verified host environment:
- .NET SDK `10.0.401`
- Node.js `v22.18.0`, npm `10.9.3`
- Docker `28.5.2` with local `postgres:18` image
- Playwright Chromium cache `chromium-1248`
- Ponytail skill `v5.3.3` (`full`) and Impeccable skill `v4.9.1`

---

## 2. Module Ownership, Aggregate Boundaries & Persistence

| Module | Owns in S00 | Must NOT own |
|---|---|---|
| `Identity` | ASP.NET Core Identity credential store (`identity.asp_net_*`) and `identity.user_accounts` (`UserId`, `DisplayName`, `PreferredLocale`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `LastSignedInAtUtc`, `Version`) | Tenant/legal-entity registries (S01), approvals, stock, journals |
| `Platform` | Shared hosting primitives and DB context coordination (`AtheraDbContext`) | Operational business data or separate frontend auth DB |

### Minimal S00 Schema (`identity`)
- ASP.NET Core Identity `Guid`-keyed tables in schema `identity` (`asp_net_users`, `asp_net_roles`, `asp_net_user_claims`, `asp_net_user_roles`, `asp_net_user_logins`, `asp_net_user_tokens`, `asp_net_role_claims`).
- `identity.user_accounts`:
  - `user_id uuid PRIMARY KEY` FK -> `identity.asp_net_users(id) ON DELETE RESTRICT`
  - `display_name varchar(160) NOT NULL`
  - `preferred_locale varchar(16) NOT NULL DEFAULT 'id-ID'` (`CHECK (preferred_locale IN ('id-ID', 'en-US'))`)
  - `is_active boolean NOT NULL DEFAULT true`
  - `created_at_utc timestamptz NOT NULL`
  - `updated_at_utc timestamptz NOT NULL`
  - `last_signed_in_at_utc timestamptz NULL`
  - `version bigint NOT NULL DEFAULT 1` (optimistic concurrency token)
- **Append-only vs mutable:** `user_accounts` profile/lifecycle fields are mutable; no financial or audit ledger tables are created in S00.
- **No speculative schemas:** Tenant/entity tables belong strictly to S01; `partners`, `catalog`, `gl`, `procurement`, etc. are deferred to their assigned slices.

---

## 3. PostgreSQL Role Separation & Migrations

- **Migrator role (`athera_migrator`):** `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS`. Owns `identity` schema and executes EF Core migrations (`--migrate`).
- **Runtime role (`athera_app`):** `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS`. Owns zero schemas or tables. Granted `USAGE ON SCHEMA identity` and explicit `SELECT, INSERT, UPDATE, DELETE` on `identity` tables only inside `M00_InitialIdentityFoundation`.
- **Verification:** `M00_InitialIdentityFoundation` must pass Up -> Down (`"0"`) -> Up on a fresh disposable PostgreSQL 18 database, and catalog inspection must confirm `athera_app` is non-superuser, lacks `BYPASSRLS`, and owns zero tables.

---

## 4. Trust Boundary, Same-Origin Routing, Cookies & CSRF

- **Same-origin browser topology:**
  - Browser interacts solely with Next.js (`http://127.0.0.1:3200`).
  - Next.js exposes a dynamic server route `apps/web/src/app/api/[...path]/route.ts` (`export const dynamic = 'force-dynamic'`, `fetch(..., { cache: 'no-store' })`) that proxies `/api/*` to ASP.NET Core API (`http://127.0.0.1:5200/api/*`), preserving multi-value `Set-Cookie` headers (`headers.getSetCookie()`), `Cookie`, `X-CSRF-Token`, `Idempotency-Key`, and `Origin`/`Host` safety without caching auth responses.
  - Next.js never opens PostgreSQL directly and never stores auth tokens in `localStorage`, `sessionStorage`, or client-readable cookies.
- **Cookie configuration (`athera.session`):**
  - `HttpOnly = true`, `SameSite = SameSiteMode.Lax`, `Path = "/"`
  - `SecurePolicy = CookieSecurePolicy.Always` in Production/Staging (`CookieSecurePolicy.SameAsRequest` in Development/Testing so HTTP loopback `127.0.0.1` E2E tests receive cookies while HTTPS environments enforce `Secure`)
  - Sliding expiration `8 hours`, `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` so disabled/revoked sessions fail immediately on next protected call
  - `OnRedirectToLogin` returns `401` JSON `ProblemDetails` (`errorCode: "unauthenticated"`); `OnRedirectToAccessDenied` returns `403` JSON `ProblemDetails` (`errorCode: "forbidden"`). No HTML login redirect on API routes.
- **CSRF defense (`athera.csrf` + `X-CSRF-Token`):**
  - ASP.NET Core `IAntiforgery` configured with `Cookie.Name = "athera.csrf"`, `HttpOnly = true`, `SameSite = SameSiteMode.Lax`, `HeaderName = "X-CSRF-Token"`.
  - Custom `CsrfProtectionMiddleware` enforces `antiforgery.ValidateRequestAsync(context)` plus allowed-origin verification (when `Origin` header is sent, it must match configured `AllowedBrowserOrigins` or request `Host`) on every unsafe HTTP method (`POST`, `PUT`, `PATCH`, `DELETE`) under `/api/*`. Returns `403` RFC 7807 `ProblemDetails` (`errorCode: "csrf_validation_failed"`).
  - **Principal-matched token rotation:** Inside `/api/auth/login` (after verifying credentials and signing in) and `/api/auth/logout` (after signing out), update `HttpContext.User` to the post-transition `ClaimsPrincipal` *before* generating the fresh `csrfToken` via `antiforgery.GetAndStoreTokens(httpContext)`, preventing principal-claim mismatch on subsequent mutations.

---

## 5. API Contracts & Error Taxonomy (S00)

All errors return `application/problem+json` (RFC 7807 `ProblemDetails`) with `extensions.errorCode` and `extensions.traceId`.

| Endpoint | Auth | Purpose | Success | Failure |
|---|---|---|---|---|
| `GET /api/health` | Public | API + live PostgreSQL `SELECT 1` readiness | `200 HealthResponse` | `503 ProblemDetails (database_unavailable)` |
| `GET /api/auth/csrf` | Public | Issues `athera.csrf` cookie + request token | `200 CsrfTokenResponse` | — |
| `POST /api/auth/login` | Public + CSRF | Verifies Identity password, lockout & `UserAccount.IsActive`; sets `athera.session`, rotates `csrfToken` | `200 AuthSessionResponse` | `400 validation_error`, `401 invalid_credentials`, `403 csrf_validation_failed`, `403 account_inactive`, `423 account_locked` |
| `POST /api/auth/logout` | Public/Session + CSRF | Signs out Identity cookie, issues fresh anonymous `csrfToken` | `200 LogoutResponse` | `403 csrf_validation_failed` |
| `GET /api/auth/me` | `[Authorize]` | Reads live PostgreSQL `UserAccount` + user status; returns `401` if inactive/missing | `200 CurrentUserResponse` | `401 unauthenticated` |

All `/api/auth/*` responses emit `Cache-Control: no-store, no-cache, private` and `Pragma: no-cache` to prevent SSR or proxy session leaks.

---

## 6. UI States & Information Architecture (Impeccable Operate Mode)

- **Default language:** Bahasa Indonesia (`id-ID`) with instant non-destructive UI toggle to English (`en-US`).
- **Route `/login`:**
  - Single authoritative operational sign-in surface with clear brand/environment badge, labeled disposable demo helper hint (only shown when non-production demo mode is active), email + password inputs with visible labels, focus rings, password visibility button (`aria-label`), inline actionable RFC 7807 error banner, loading submit state preventing double-submission.
- **Authenticated shell `/` (`(shell)/page.tsx`):**
  - Server/client authenticated check redirecting unauthenticated visitors cleanly to `/login`.
  - Coherent top bar (brand wordmark, active scope indicator showing `No Active Scope (S00 Default-Deny)`, locale switcher, user display name, Sign Out button) + single primary sidebar navigation (`Ringkasan`, `Pengadaan`, `Persediaan`, `Penjualan`, `Keuangan`, `Administrasi`) showing operational modules explicitly scoped/locked until S01 tenant/legal-entity selection is enabled.
  - Main canvas displays verified Identity + PostgreSQL session verification card and explicit security boundary notice ("Tenant & legal-entity workspaces are denied by default until S01 authorization and RLS verification").

---

## 7. Threat Model & Failure Behavior

| Threat | Control |
|---|---|
| Unauthenticated data probe | Cookie auth middleware rejects with `401`; `/api/auth/me` queries DB under `athera_app` |
| Deactivated user with valid cookie | `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` + explicit `user_accounts.is_active` check on `/api/auth/me` signs user out and returns `401` |
| CSRF on login, logout, or mutations | `IAntiforgery` double-submit (`athera.csrf` cookie + `X-CSRF-Token` header) + `Origin` check |
| Brute-force password guessing | Identity lockout (`5` failures -> `15 min` lockout -> `423 account_locked`) |
| Cross-user SSR cache leak | `force-dynamic`, `no-store` proxy, `Cache-Control: no-store, private` |
| Runtime privilege escalation in DB | `athera_app` has `NOSUPERUSER NOBYPASSRLS`, owns zero tables, no DDL rights |

---

## 8. S00 Test Mapping

| Test ID | Layer | Verifies |
|---|---|---|
| `S00-UT-01..03` | Unit | Password/locale/email normalization & ProblemDetails error mapping |
| `S00-PG-01` | Real PG | `M00` migration Up -> Down (`"0"`) -> Up cleanly |
| `S00-PG-02` | Real PG | `athera_app` role has `rolsuper=false`, `rolbypassrls=false`, owns 0 tables |
| `S00-API-01` | Real PG + API | `GET /api/health` connects to PostgreSQL as `athera_app` |
| `S00-API-02` | Real PG + API | `POST /api/auth/login` valid -> `200` + `GET /api/auth/me` reads DB `UserAccount` |
| `S00-API-03` | Real PG + API | Invalid password -> `401 invalid_credentials`; 5 failures -> `423 account_locked` |
| `S00-API-04` | Real PG + API | Missing/forged `X-CSRF-Token` on `POST /api/auth/login` or `logout` -> `403 csrf_validation_failed` |
| `S00-API-05` | Real PG + API | Deactivated `UserAccount` rejected `401` on `GET /api/auth/me`; logout invalidates session |
| `S00-FE-01..02` | Vitest + TSC/Lint | OpenAPI schema check (`api:check`), typed client & form validation unit tests |
| `S00-E2E-01..04` | Playwright | Unauthenticated redirect -> invalid login -> valid login + PG profile -> CSRF fail + logout |

**Owner-only decisions:** None blocking S00. Tenant/entity RLS isolation begins in S01.
