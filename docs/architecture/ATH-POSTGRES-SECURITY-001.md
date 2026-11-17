# ATH-SEC-PG-001 - PostgreSQL tenancy, transaction and integrity contract

## Control and business plane
Global identities, tenants, memberships, legal entity authorizations and configuration are accessed via a protected control-plane path. Business operations resolve current authenticated user + active tenant membership + legal-entity permission **before** entering a tenant-scoped transaction. URL/headers and client-selected tenant IDs are selectors, not trusted authorization. `platform.Tenant` is not exposed to runtime cross-tenant enumeration.

## PostgreSQL RLS design
- Every tenant-owned business table has `TenantId NOT NULL`. Every entity-owned transaction also has `LegalEntityId NOT NULL`. All relevant child relationships have composite scoped FKs.
- Enable and where appropriate `FORCE ROW LEVEL SECURITY` on tenant-owned tables. Runtime role must not own tables and must not have `BYPASSRLS`, SUPERUSER or security-definer bypass privileges.
- For tenant-readable/writable tables define a scoped SELECT/UPDATE/DELETE `USING` filter and INSERT/UPDATE `WITH CHECK` predicate, as appropriate. Default deny on absent or invalid tenant setting; do not catch policy failure and retry unscoped.
- Resolve a trusted tenant identity in the application; inside an explicitly begun SQL transaction set `SELECT set_config('app.tenant_id', @validated_tenant_uuid, true)` and use that same transaction for the query/posting. Transaction-local scope avoids connection pool leakage if properly applied. Verify behavior on connection reuse, rollback, early exception and retry. Do not use session-level `SET` across pooling.
- TenantId custom GUC is not proof against application compromise because the shared runtime SQL role can itself set it. RLS is defense-in-depth against scoped query mistakes. Permissions and policy checks remain in application service; do not claim absolute isolation after RCE or credential compromise.
- Module schema ownership: runtime has only required schema/table privileges; migrator privileged separately; use least privilege, secure `search_path`, reviewed functions/triggers, and no arbitrary `SECURITY DEFINER` helpers.
- A table owner, elevated roles, SECURITY DEFINER code, views with owner privileges and testing as superuser can bypass RLS. Verify using actual unprivileged runtime principal and separate restricted writer role.

## Atomic postings
One coordinator controls a single PostgreSQL connection and DB transaction across critical Inventory+GL+source/outbox effects. Module ownership is respected through explicit handlers/contracts even if persistence contexts share that transaction. Posted journals, stock movements and valuation entries are append-only. Use source-key unique constraints and business idempotency keys; on concurrent receipts/bills lock or version the correct aggregate and valuation pool, reject over-acceptance and duplicate posting. A `READ COMMITTED` transaction alone does not prove race safety; add required locking/constraints and repeat tests under concurrent writers.

## Test obligations
1. Real PostgreSQL provider and migrations up/down/up on disposable DB; never `EnsureCreated` as substitute.
2. Tenant A may not read or write Tenant B row under runtime principal. Missing tenant context sees no rows and cannot write.
3. Unauthorized entity action denied even if tenant matches. Foreign FK combinations rejected.
4. Pool alternating tenant contexts, failed transaction, exceptions and retries without state leakage.
5. Verify role grant matrix, owner bypass (`FORCE`), no BYPASSRLS, view/functions and RLS policy behavior.
6. Concurrency stale updates and simultaneous receipt/billing duplicate prevention.
7. Test scripts use expected exceptions and assert actual absence of side effects, not just HTTP response code.

## Primary references
PostgreSQL Row Security Policies: https://www.postgresql.org/docs/current/ddl-rowsecurity.html
PostgreSQL set_config/current_setting: https://www.postgresql.org/docs/current/functions-admin.html
Npgsql EF provider: https://www.npgsql.org/efcore/
