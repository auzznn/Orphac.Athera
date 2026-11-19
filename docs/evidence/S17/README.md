# Evidence Packet — Slice S17: Documents, Evidence Trails & Operational UX

- **Branch:** `greenfield/mvp`
- **Status:** `CODE_COMPLETE` | `ENGINEERING_PASS` | **Product Owner Acceptance:** `PENDING`
- **Design Doc:** [S17-design.md](../../design/slices/S17-design.md)
- **Open Decision Request:** [ATH-CR-014-OD12-document-retention-and-sharing-policy.md](../../decisions/ATH-CR-014-OD12-document-retention-and-sharing-policy.md) (`OPEN`, Owner sign-off blank)

## Automated Verification Summary
| Check | Result | Evidence |
| --- | --- | --- |
| `.NET` Unit Tests | **79/79 PASS** (incl. 3 new S17 unit tests) | [dotnet-test.log](dotnet-test.log) |
| `.NET` Real PostgreSQL 18 Integration Tests (`athera_app` + forced RLS) | **95/95 PASS** (incl. 2 new S17 PG18/API + 50-doc load tests) | [dotnet-test.log](dotnet-test.log) |
| OpenAPI Export & TS Schema Sync (`api:check`) | **0 diff** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| TypeScript (`tsc --noEmit`) & ESLint | **0 errors** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Vitest Frontend Suite | **50/50 PASS** (18 files) | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Playwright E2E Full Regression (`S00`–`S17`) | **30/30 PASS** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Static Detect (`impeccable detect --json src`) | **`[]`** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Finish Reviewer (Pass 2) | **`disposition: ship`** | [impeccable-review.md](impeccable-review.md) |
| Secrets Scan | **`SECRETS_SCAN_CLEAN`** | [secrets-scan.log](secrets-scan.log) |

## Migration & Recovery
- **Migration:** `20261119015232_M17_DocumentsAndEvidenceTrails` (`documents.document_sequences`, `documents.business_documents`, `documents.document_links` with `ENABLE` + `FORCE ROW LEVEL SECURITY`, immutability triggers, and least-privilege `athera_app` grants).
- **Rollback:** `dotnet ef database update 20261119011903_M16_PeriodCloseAuditAndReconciliation --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api`.

## Known Risks / Owner-Pending Decisions
- `OD-12` (Statutory retention & external sharing policy) remains `OPEN` in `ATH-CR-014` awaiting Product Owner sign-off; safe append-only, authenticated signed-token defaults are enforced.
