# Evidence Packet — Slice S16: Accounting Reports, Control Reconciliations & Period Close

- **Branch:** `greenfield/mvp`
- **Status:** `CODE_COMPLETE` | `ENGINEERING_PASS` | **Product Owner Acceptance:** `PENDING`
- **Design Doc:** [S16-design.md](../../design/slices/S16-design.md)
- **Open Decision Request:** [ATH-CR-013-OD11-OD13-opening-balances-and-period-close-policy.md](../../decisions/ATH-CR-013-OD11-OD13-opening-balances-and-period-close-policy.md) (`OPEN`, Owner sign-off blank)

## Automated Verification Summary
| Check | Result | Evidence |
| --- | --- | --- |
| `.NET` Unit Tests | **76/76 PASS** (incl. 2 new S16 unit tests) | [dotnet-test.log](dotnet-test.log) |
| `.NET` Real PostgreSQL 18 Integration Tests (`athera_app` + forced RLS) | **93/93 PASS** (incl. 2 new S16 PG18/API tests) | [dotnet-test.log](dotnet-test.log) |
| OpenAPI Export & TS Schema Sync (`api:check`) | **0 diff** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| TypeScript (`tsc --noEmit`) & ESLint | **0 errors** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Vitest Frontend Suite | **48/48 PASS** (17 files) | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Playwright E2E Full Regression (`S00`–`S16`) | **29/29 PASS** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Static Detect (`impeccable detect --json src`) | **`[]`** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Finish Reviewer (Pass 2) | **`disposition: ship`** | [impeccable-review.md](impeccable-review.md) |
| Secrets Scan | **`SECRETS_SCAN_CLEAN`** | [secrets-scan.log](secrets-scan.log) |

## Migration & Recovery
- **Migration:** `20261119011903_M16_PeriodCloseAuditAndReconciliation` (`gl.period_close_reviews` with `ENABLE` + `FORCE ROW LEVEL SECURITY`, immutability trigger `gl.guard_period_close_review_immutability()`, least-privilege `SELECT, INSERT` for `athera_app`).
- **Rollback:** `dotnet ef database update 20261119005242_M15_CustomerReceiptsAllocationAndArAging --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api`.

## Known Risks / Owner-Pending Decisions
- `OD-11` (Opening balance cutover policy) and `OD-13` (Locked period override governance) remain `OPEN` in `ATH-CR-013` awaiting Product Owner sign-off.
