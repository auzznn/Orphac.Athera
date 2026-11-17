# ATH-S00 — Greenfield Full-Stack Foundation and Authenticated Path

**Status:** Engineering workstream authorized; Product Owner acceptance **not granted**.  
**Prerequisites:** Blank/new repository, reviewed PRD+BRS and greenfield ADR.  
**Owners:** Platform, Identity, Application Hosting.  
**Environment:** Next.js + TypeScript; ASP.NET Core 10; PostgreSQL + EF Core/Npgsql.

## Business outcome
An actual, independently runnable application exists from zero: a user can authenticate via ASP.NET Core Identity, the Next.js interface receives authenticated API data, and the PostgreSQL-backed foundation has a reproducible migration/test workflow. This is a functional foundation, not a pretend ERP dashboard.

## Design before writing code
Create `docs/design/slices/S00-design.md` documenting chosen repo layout, runtime/tool versions, trusted identity boundary, same-origin routing, cookies+CSRF, data model (minimum auth foundation only), migrations, API response/error contracts, initial UI states, threat model and test mapping. Record decisions within the approved stack; major deviations go to Owner.

## Implement (Ponytail full review)
1. Confirm directory is code-free aside from planning assets; initialize Git, `.gitignore`, basic lint/editor conventions and one local baseline commit.
2. Bootstrap minimal Next.js React/TypeScript web app, ASP.NET Core 10 Web API and test projects. Prefer simple modular folders; do not pre-create projects for 19 domains.
3. Set up disposable PostgreSQL and Npgsql with separate schema-migration/admin and constrained runtime roles. Implement just enough identity persistence for one authenticated vertical flow.
4. Establish ASP.NET Core Identity as the only credential store. Configure safe browser sign-in/sign-out via same-origin app/API arrangement, cookie security, CSRF defense, session handling and unauthorized responses.
5. Implement real `GET /api/health` and one authenticated API endpoint backed by actual PostgreSQL data; generate TypeScript API client from OpenAPI rather than hand-maintaining duplicate contracts.
6. Build a usable Next.js sign-in page and application shell with error/loading/empty states. Use Impeccable to shape, critique, refine and check responsiveness and accessibility; maintain PRODUCT.md and DESIGN.md.
7. Add basic health/build/test tooling and secure local developer runbook. No hardcoded credentials or tracked `.env` files.

## Invariants
Only .NET authenticates users; Next.js never opens the DB; unauthenticated requests cannot read protected identity data; no leaking auth/session data across SSR users; unsafe cookie-authenticated actions use CSRF controls; DB privileges separate and no admin runtime login. Do not add fake master data or other module tables.

## Acceptance evidence
- Empty-repo bootstrap recorded with exact Git SHAs and dependency versions.
- `dotnet restore/build/test`, Next lint/typecheck/build executed with output.
- Disposable PostgreSQL migration applied/rolled back/reapplied successfully.
- Real login success, invalid login, unauthorized API, logout, session expiry/invalidity scenarios.
- Typed Next.js->API contract demo, CSRF tests for unsafe actions, secrets scan and actual browser screenshot/a11y checks if supported.
- Reproducible local runbook with non-production database and credentials.

## Out of scope
Tenant/LegalEntity self-service and RLS verification belong to S01. No partner, item, PO, inventory, finance or manufacturing features. Do not claim all tenant security is complete in S00.

## Completion and governance
Implement, test, submit `docs/evidence/S00/` with truthful statuses and exact SHA, and commit. Engineering PASS is separate from the unsigned Product Owner acceptance record. Proceed to S01 only when the verified S00 authentication/migration prerequisites are sound.
