# ATH-S01 Evidence Packet — Secure Tenant and Legal-Entity Workspace

- **Slice:** S01 — Secure Tenant and Legal-Entity Workspace (`docs/implementation/slices/S01_secure_tenant_and_legal_entity_workspace.md`)
- **Design spec:** [S01-design.md](../../design/slices/S01-design.md)
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S00 SHA:** `920bc78` (ledger `b863561`)
- **S01 Implementation SHA:** `737971a`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against live PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **U1 (`rina.utami@demo.athera.local`)**: Authorized exclusively for `NUSA` / `NUSA-JKT` (`workspace.view`, `procurement.read`). `NUSA-SBY` and `SMDR-BDG` are hidden in `/api/workspaces` and `/workspaces`; direct navigation to `NUSA-SBY` or `SMDR-BDG` returns non-leaking `404` (`Ruang Kerja Tidak Tersedia`) with zero tenant/entity metadata leakage, and spoofed `X-Tenant-Id` headers are ignored.
2. **U2 (`budi.santoso@demo.athera.local`)**: Authorized for `NUSA-JKT`, `NUSA-SBY`, and `SMDR-BDG`. Switches within-tenant and cross-tenant cleanly using `/workspaces` and the top-bar Scope Switcher with `key={`${tenantId}:${entityId}`}` state reset and `no-store` fetches.
3. **U3 (`hendra.wijaya@demo.athera.local`)**: Authenticated with `NO_ACTIVE_WORKSPACE` (`0` authorized entities) -> sees accessible default-deny guidance referencing Organization Administrator provisioning (`S02`).

### B. Module Ownership & Architecture
- `Platform` owns `platform.tenants`, `platform.legal_entities`, `platform.current_tenant_id()`, `platform.current_actor_user_id()`, and `TenantScopeExecutor`.
- `Identity` owns `identity.tenant_memberships`, `identity.entity_access_grants`, and permission constants alongside S00 Identity tables.
- All tenant/entity FKs enforce composite `(tenant_id, legal_entity_id)` and `(tenant_id, user_id)` integrity with `DeleteBehavior.Restrict`.
- Client routes (`{tenantId}`, `{entityId}`) act solely as selectors resolved server-side inside explicit transactions before setting `set_config('app.tenant_id', ..., true)`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (18 Unit + 14 Real PostgreSQL 18 integration tests: `M01` Up/Down/Up, `athera_app` privilege & `FORCE ROW LEVEL SECURITY` catalog checks, missing-context deny & `42501` write block, cross-tenant block, composite FK `23503`, `U1`/`U2`/`U3` + spoofed header API checks, immediate `TenantMembership` revocation denial, 60-task `MaxPoolSize=4` pooling/rollback/exception leak proof) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (5/5 tests), `next build`, Playwright E2E (`s00-auth.spec.ts` + `s01-workspace.spec.ts`: 4/4 tests on `127.0.0.1:3200` + `127.0.0.1:5200` + PostgreSQL `55433`), `npm audit --omit=dev --audit-level=high`, & `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`grep` across `src/`, `apps/web/`, `docs/`, `scripts/`, `tests/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`6ea971c8` Round 1 `fix` -> 7 fixes resolved -> Round 2 `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots | [screenshots/](./screenshots/) | PASS |

### D. Dependency Audit & Known Limitations
- **Production npm dependencies (`npm audit --omit=dev --audit-level=high`):** `0 vulnerabilities`.
- **Dev-only npm tooling:** Same dev-only transitive `picomatch <=4.0.7` advisory (`GHSA-fc5q-39xc-vjpf`) noted in S00; not shipped in runtime builds.
- **Deferred to S02:** Tenant provisioning UI, user invitations, role bundles, and four-eyes grant approval workflows (read-only membership/grant seed in S01).

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
