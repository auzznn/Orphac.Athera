# ATH-SOL-001 - Suggested minimal repository layout

Greenfield target layout; adjust modestly only when a concrete engineering reason is documented. Avoid generating an assembly for every table or a repository layer on every entity.

```text
Orphac.Athera/
  AGENTS.md
  README.md
  apps/
    web/                         # Next.js / React / TypeScript
      app/                       # URL and layouts by domain
      components/                # reusable UI primitives
      features/                  # procurement, inventory, finance, etc.
      lib/api/                   # generated OpenAPI types/client
  src/
    Orphac.Athera.Api/          # ASP.NET Core 10 API host, auth, middleware
    Orphac.Athera.Modules/      # module folders; domain/use cases/EF mappings
      Platform/ Identity/ Approval/ Partners/ Catalogue/
      Procurement/ Inventory/ Quality/ GeneralLedger/
      AccountsPayable/ Cash/ Sales/ Fulfillment/ Billing/
      AccountsReceivable/ Tax/ Documents/ Integration/ Audit/
  tests/
    Orphac.Athera.UnitTests/
    Orphac.Athera.IntegrationTests/    # real PostgreSQL
    e2e/                               # browser E2E
  docs/
    architecture/ design/ governance/ implementation/ evidence/
  reference/                           # historical approved source documents
```

Module-owned schemas in Postgres do not mandate individual assemblies. Split projects only when independent boundaries or compilation/maintenance benefits are demonstrated. Feature handlers should own their write logic, and cross-module operations should be explicit orchestrations, not ad hoc table writes. Use a shared transactional EF context or explicitly joined DbContexts/connection as appropriate; do not cause distributed transactions by introducing separate databases prematurely.

## API conventions
- Request DTOs validate at trust boundaries; domain invariants independently validated in application code and database.
- `tenantId`/`entityId` supplied by browser are selectors, never source of authorization; server validates current membership and permission.
- Use idempotency tokens for irreversible operations. Return stable actionable error codes for field errors, 401/403/404 and posting blockers without exposing foreign data.
- Use RFC 7807-style structured errors where practical, OpenAPI generated TS client; do not hand-maintain duplicate DTO models when generator works.

## Background jobs
Keep one simple hosted worker for outbox initially. Work is tenant-scoped and idempotent. Use durable retries, dead-letter/alerts, no side effect before required financial posting transaction commits.
