# ATH-DB-FOUNDATION-001 — Greenfield PostgreSQL Logical Foundation

**Status:** Approved design direction; specific DDL to be derived and verified in S00–S02. Do not generate all future tables now.

## Schemas and foundational entities
- `platform`: Tenant (`TenantId`, code, name, lifecycle), LegalEntity (`LegalEntityId`, TenantId, entity code, currency, lifecycle), organization hierarchy later as needed.
- `identity`: global ASP.NET Core Identity store + UserAccount mapping, TenantMembership (TenantId, UserId, lifecycle), EntityAccessGrant (TenantId, LegalEntityId, UserId, permission, validity, status).
- `approval`: rule versions, approval request/decision and immutable actor evidence (S02).
- `partners`: Partner and supplier/customer roles (S03).
- `catalog`: Item, base and purchasing units, conversion rules, tracking/eligibility (S03).
- `procurement`: PurchaseOrder/Line, immutable approved revision and approval evidence (S05).
- `receiving`, `inventory`, `gl`, `ap`, `banking`, `sales`, `fulfillment`, `ar`, `tax`, `documents`, `audit`, `integration`: introduced incrementally when their slice begins.

## Scope and relational constraints
Every tenant-owned row has `TenantId uuid NOT NULL`; entity-specific commercial/financial rows also have `LegalEntityId uuid NOT NULL`. Unique keys and foreign keys express these scopes, e.g. `(TenantId, LegalEntityId, PurchaseOrderId)` as a referenced alternate key and same composite key on child relationships. Tenant-wide Partner/Item master rows are shared across authorized legal entities; their eligibility can be controlled per legal entity without duplicating their identity.

## Identity architecture
One global identity can belong to multiple tenants, but effective access requires active account, active tenant, active membership, permitted legal entity, valid permission and optionally independent approval. A grant must not restore itself when a user rejoins a tenant after suspension or revocation. Browser-provided identifiers are untrusted selectors.

## Data types and concurrency
`uuid` IDs; `timestamptz` UTC instants; `date` for commercial/business date; `numeric` for quantities and monetary amounts with approved scale, currency and UoM; `text`/`varchar` as appropriate; database check constraints for positive quantities, valid windows and line uniqueness. Use a deliberate optimistic-concurrency strategy (e.g. version bigint or tested Npgsql `xmin` mapping), not a naive timestamp.

## Row-level isolation
Use PostgreSQL RLS (`USING` + `WITH CHECK`) on tenant-owned rows, including SQL principal and owner-bypass controls. Resolve verified tenant membership in protected control plane before issuing SQL transaction-local `app.tenant_id` and never trust a raw browser-supplied tenant ID. Always run tests with actual low-privilege runtime role; verify connection reuse/rollback behavior. Entity access checks must be enforced independently of RLS.

## Posting tables (design ahead, build later)
Posted inventory movement/valuation and financial journal history are append-only, source linked, idempotent; mutable balances are authoritative projections reconciled to ledgers. Stock valuation pool key must include the legally meaningful owning entity and item, with scope extended where approved costing policy requires. Postings use one atomic transaction; partial failure rolls back source, stock, valuation, ledger and outbox together.

## Growth rule
Build migrations only when the owning slice needs them. No speculative 70-table scaffolding, no separate database per module, no uncontrolled cascading deletes across business history. Every created table has a named owner, scope, delete/update rule and an acceptance test.
