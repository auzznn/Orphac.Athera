# ATH-ADR-STACK-001 — Greenfield Technology and Architecture Decision

**Recorded decision:** 2026-11-17 | **Decision:** Product Owner selected this stack | **Implementation status:** Not yet built or verified.

## Context
Orphac Athera is a new, industry-neutral, multi-tenant ERP for Indonesian SMEs. The implementation starts from an empty repository. Prior experiments or products are not source code for this program.

## Binding decision
- Frontend: **Next.js + React + TypeScript** (security-patched stable versions selected by the engineer at bootstrap).
- API/backend: **ASP.NET Core 10 Web API** within one **modular monolith**; .NET hosted workers when needed.
- Persistence: **PostgreSQL + EF Core 10/Npgsql**; module-owned SQL schemas and shared tenant-owned tables for the MVP.
- Identity authority: **ASP.NET Core Identity** with server-side credential checks; secure same-origin browser session design and CSRF mitigation.
- API boundary: OpenAPI contract and generated TypeScript client; the frontend never opens direct database connections.
- Financial business effects: one transactionally coordinated PostgreSQL posting across relevant domain modules, with outbox persisted atomically and asynchronous delivery only after commit.
- Architecture is industry-neutral; manufacturing/foundry and advanced APS are later extensions, not required core tables.

## Domain preservation, not code migration
The product decisions carried by the PRD and BRS (GRNI, weighted-average inventory, two receiving gates, audit and approval rules, invoice matching, two-stage payments, Indonesian tax configuration) are functional requirements. There is **no approved old app to reuse**. Implement fresh code and migrations from the current canonical contracts.

## Identity and transport design
Use one authoritative ASP.NET Core Identity account store and a documented same-origin deployment/proxy arrangement for Next.js and the API. Select cookie/session attributes intentionally (Secure, HttpOnly, SameSite, path, domain, expiration, renewal); design login, logout, revocation and CSRF protections for unsafe requests. Do not create a second auth database in the frontend. Always verify tenant membership and action permission in the API. Avoid user-specific cache sharing across requests/tenants and prevent server-side rendering leaks.

## PostgreSQL data and tenancy
Use `uuid` for stable primary identities, `timestamptz` for instants, `numeric` for precise money/quantity; explicit unit, decimal scale and rounding policies. Scope unique keys and relevant foreign keys by tenant/legal entity. Use PostgreSQL RLS with `USING` and `WITH CHECK`, runtime roles without bypass, separate migration/control-plane roles, and trusted transaction-local tenant configuration. RLS is defense in depth, not an absolute guarantee if application server credentials/process are compromised.

## Deployment and out-of-scope
Local disposable PostgreSQL must support real integration tests. No automatic cloud spend, production infrastructure, live money movement, statutory filing, microservices, event-bus platform or per-tenant database routing. The Product Owner separately approves live deployment and material scope changes.

## Verification gates
S00 proves the stack actually boots, authenticates and uses real PostgreSQL migrations. S01 proves SQL and API tenant isolation with actual runtime credentials, without connection pool scope leakage. Each slice must pass its engineering gate before relying on it for downstream critical behaviors. Final acceptance is a separate human decision tied to the immutable Git SHA.
