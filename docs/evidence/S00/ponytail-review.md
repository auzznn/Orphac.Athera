# Ponytail (`full`) Review — Slice S00

**Scope reviewed:** All S00 backend (`src/Orphac.Athera.Modules`, `src/Orphac.Athera.Api`), tests (`tests/Orphac.Athera.*`), and frontend (`apps/web/src`, `apps/web/e2e`) changes committed on `greenfield/mvp`.
**Assumed load:** Single-tenant-scale initial pilot (~10–50 concurrent SME users on one ASP.NET Core 10 Web API instance + PostgreSQL 18 pool).

## What this change does
Creates the greenfield ASP.NET Core 10 + EF Core 10 / Npgsql + Next.js 16 same-origin foundation with ASP.NET Core Identity authentication, `HttpOnly` cookie sessions (`athera.session`), CSRF double-submit + `Origin` protection (`athera.csrf` / `X-CSRF-Token`), separate `athera_migrator` and non-privileged `athera_app` PostgreSQL roles, OpenAPI-generated TypeScript API client, and an Indonesian-default authenticated operational shell (`/` and `/login`).

## Findings

### Should fix (addressed or tracked)
1. **Unnecessary array buffer allocation on empty POST requests** (`apps/web/src/app/api/[...path]/route.ts:L32`)
   - **What this is:** The Next.js `/api/[...path]` proxy calls `await request.arrayBuffer()` whenever `method !== "GET" && method !== "HEAD"`, even for bodyless `POST /api/auth/logout`.
   - **Problem:** Allocates an empty `ArrayBuffer` and forwards empty content-body semantics when `request.body === null`.
   - **Fix:** Check `hasBody && request.body ? await request.arrayBuffer() : undefined;` in S01 touch or keep minimal as-is (safe at pilot scale; 1-line cleanup).
   - **If we skip it:** Negligible extra 0-byte buffer allocation per logout call; zero correctness or security impact.

### Nice to have
2. **Typed 403 OpenAPI response metadata on `POST /api/auth/logout`** (`src/Orphac.Athera.Api/Endpoints/HealthAndAuthEndpoints.cs:L135`)
   - **What this is:** `CsrfProtectionMiddleware` returns `403 ProblemDetails` when CSRF validation fails on `/api/auth/logout`, while Minimal API metadata only declared `.Produces<LogoutResponse>(200)`.
   - **Fix:** Client already normalizes `(response as Response).status`; adding `.Produces<ProblemDetails>(403)` in S01 keeps OpenAPI schema 100% aligned without client type casts.

## Verdict
- **Bug / Security / Scale / Tests check:**
  - Auth & CSRF: `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` + live PostgreSQL `user_accounts.is_active` check on `/api/auth/me` immediately invalidates deactivated accounts; CSRF principal updates `HttpContext.User` before rotating tokens on login and logout; `athera_app` owns 0 tables and lacks `SUPERUSER`/`BYPASSRLS`.
  - Tests: 12 Unit Tests (`IdentityFoundationTests`), 6 Real PostgreSQL 18 integration tests (`S00_PG_01` Up/Down/Up, `S00_PG_02` role isolation, `S00_API_01`..`05b`), 2 Vitest frontend tests, and 1 full-stack Playwright E2E scenario all pass against live servers.
- **Verdict:** Ship (with minor cleanups eligible for S01).
- **Not checked:** Production multi-instance distributed DataProtection key ring (intentionally single-node local/disposable scope in S00; documented in `S00-design.md`).
