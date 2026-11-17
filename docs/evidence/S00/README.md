# ATH-S00 Evidence Packet & Developer Runbook

- **Slice:** S00 — Greenfield Full-Stack Foundation and Authenticated Path (`docs/implementation/slices/S00_greenfield_full_stack_foundation.md`)
- **Design spec:** [S00-design.md](../../design/slices/S00-design.md)
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Baseline SHA:** `e25008f`
- **S00 Implementation SHA:** `920bc78`

---

## 1. Local Developer Runbook (Disposable Non-Production Environment)

### Prerequisites Verified
- `.NET SDK`: `10.0.401`
- `Node.js`: `v22.18.0`, `npm`: `10.9.3`
- `Docker`: `28.5.2` (`postgres:18` image)
- `Playwright Chromium`: installed (`v1255`)

### Start Disposable PostgreSQL 18 & Provision Roles
```bash
./scripts/dev/bootstrap-postgres.sh
```
This starts container `athera-greenfield-pg` on `127.0.0.1:55433` (`athera_dev`) and provisions non-superuser, `NOBYPASSRLS` roles `athera_migrator` and `athera_app`.

### Apply Migrations & Seed Disposable Demo Identity
```bash
export ConnectionStrings__Migrator="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw"
export ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw"
export ASPNETCORE_ENVIRONMENT=Development
export ATHERA_DEMO_PASSWORD="AtheraDemo!2026"

dotnet run --project src/Orphac.Athera.Api -- --migrate
dotnet run --project src/Orphac.Athera.Api -- --seed-demo
```

### Export OpenAPI & Verify Typed Client
```bash
dotnet run --project src/Orphac.Athera.Api -- --export-openapi "$PWD/docs/contracts/openapi.v1.json"
cd apps/web && npm run api:generate && npm run api:check
```

### Run Backend & Frontend Behind Same-Origin Proxy
Terminal 1 (.NET 10 API on `127.0.0.1:5200` using `athera_app`):
```bash
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Runtime="Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw" \
dotnet run --project src/Orphac.Athera.Api --no-launch-profile
```

Terminal 2 (Next.js App Router on `127.0.0.1:3200` proxying `/api/*` to `127.0.0.1:5200`):
```bash
cd apps/web
ATHERA_API_UPSTREAM=http://127.0.0.1:5200 npm run dev
```

### Run Full Verification Suite
```bash
dotnet test Orphac.Athera.sln
cd apps/web && npm run api:check && npm run typecheck && npm run lint && npm test && npm run build && npm run e2e
```

### Stop Services & Reset Disposable Data
```bash
./scripts/dev/reset-postgres.sh
```

---

## 2. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
1. Open `http://127.0.0.1:3200` while unauthenticated -> automatically redirects to `/login`.
2. Attempt sign-in with wrong password -> inline accessible `role="alert"` error banner displays (`invalid_credentials`); session remains unauthenticated.
3. Attempt unsafe `POST /api/auth/logout` without `X-CSRF-Token` -> API returns `403 csrf_validation_failed`.
4. Sign in with `rina.utami@demo.athera.local` / `AtheraDemo!2026` -> redirects to `/`, displays `Rina Utami (Demo Procurement)` fetched live from PostgreSQL `identity.user_accounts` under `athera_app`, and displays `S00_DEFAULT_DENY` workspace protection banner.
5. Click `Keluar` (`Sign out`) -> clears `athera.session` cookie and redirects to `/login`; subsequent `GET /api/auth/me` returns `401`.

### B. Module Ownership & Architecture
- `Identity` owns `identity.asp_net_*` and `identity.user_accounts` exclusively; ASP.NET Core Identity is the sole credential store.
- Next.js contains zero database connections and zero local user stores; `/api/[...path]/route.ts` forwards cookies/CSRF headers via `cache: "no-store"`.
- No speculative schemas or business modules (`partners`, `catalog`, `gl`, `procurement`) were created.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (12 Unit + 6 Real PostgreSQL 18 tests: `S00_PG_01` Up/Down/Up, `S00_PG_02` role privileges, `S00_API_01..05b` health/CSRF/login/lockout/deactivation/logout) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend `api:check`, `tsc --noEmit`, `eslint`, `vitest` (2 tests), `next build`, `npm audit --omit=dev --audit-level=high`, & Playwright E2E (`s00-auth.spec.ts` on `127.0.0.1:3200` + `127.0.0.1:5200` + PostgreSQL `55433`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`git grep`) | [secrets-scan.log](./secrets-scan.log) | PASS (0 leaked secrets) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable UX & Finish Review (`detect`, `impeccable-finish-reviewer`, `impeccable-documenter`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Screenshots (Desktop `1280x800` & Mobile `390x844`) | [screenshots/](./screenshots/) | PASS |

### D. Dependency Audit & Known Limitations
- **Production npm dependencies (`npm audit --omit=dev --audit-level=high`):** `0 vulnerabilities`.
- **Dev-only npm dependencies:** `npm audit` reports 16 transitive high-severity findings strictly under `devDependencies` via `picomatch <=4.0.7` (`GHSA-fc5q-39xc-vjpf`, ReDoS in `eslint-config-next` / `vitest` build/lint tooling; `picomatch 4.0.7` is currently the latest published upstream version). Production bundle/runtime does not ship `picomatch`.
- **Deferred to S01:** Tenant/legal-entity discovery, RLS enforcement, and scope switcher (explicitly default-denied in S00).
