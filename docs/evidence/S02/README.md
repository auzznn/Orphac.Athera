# ATH-S02 Evidence Packet — Organization Administration, Approvals, Audit & Outbox

- **Slice:** S02 — Organization Administration, Approvals, Audit & Outbox (`docs/implementation/slices/S02_organization_administration_approvals_and_audit_core.md`)
- **Design spec:** [S02-design.md](../../design/slices/S02-design.md)
- **Open Policy Decision Request:** [ATH-CR-001-OD02-four-eyes-and-role-matrix.md](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md) (`OD-02` fail-closed default applied; PO decision pending)
- **Engineering Verification:** PASS
- **Product Owner Acceptance:** **PENDING** (Agent never self-approves; awaiting human four-pillar review)
- **Git Branch:** `greenfield/mvp`
- **Prerequisite S01 SHA:** `737971a` (ledger `68bc60d`)
- **S02 Implementation SHA:** `57618ad`

---

## 1. Four-Pillar Evidence Manifest

### A. Business Behavior & Manual Demo Script
Executed end-to-end against live PostgreSQL 18 (`athera_app`) + ASP.NET Core 10 + Next.js 16 (see [uat-script.md](./uat-script.md)):
1. **Scoped Invitations & Non-Reactivation (`S02-E2E-01`):** `U2` (`budi.santoso@demo.athera.local`) issues a single-use `PROCUREMENT_OFFICER` invitation for `U3` (`hendra.wijaya@demo.athera.local`) on `NUSA-JKT` (server persists SHA-256 hash only). `U3` redeems the token on `/invitations`, immediately unlocking `NUSA-JKT` (`Pilih Entitas (1)` live in top bar), and `U2` later revokes `U3` cleanly back to default-deny without resurrecting stale revoked grants on rejoin.
2. **Four-Eyes Approval Inbox (`S02-E2E-02`):** `U1` (`rina.utami@demo.athera.local`) submits a scoped `sales.read` approval request under `SENSITIVE_ACCESS_CHANGE`; UI suppresses self-approval controls (`Aturan Empat Mata (OD-02)`) and API enforces `403 four_eyes_self_approval_forbidden`. `U2` independently reviews the pending row with written justification and approves (`Disetujui`), granting `sales.read` and emitting append-only audit + outbox events atomically.
3. **MFA TOTP & Outbox Dispatch:** TOTP enrollment (`/api/auth/mfa/setup`, `/api/auth/mfa/verify`) gates `HIGH_RISK_PRIVILEGE_ESCALATION` decisions (`mfa_required` if un-enrolled); transactional outbox dispatch (`TenantOutboxProcessor`) processes queued messages with `FOR UPDATE SKIP LOCKED` and `InboxReceipt` deduplication.

### B. Module Ownership & Architecture
- `Identity` extends `PermissionCodes`, adds pre-approved `RoleTemplates`, owns `identity.user_invitations`, and enforces partial unique index `status <> 'REVOKED'` on `identity.entity_access_grants`.
- `Approval` owns `approval.approval_policies`, `approval.approval_cases`, and append-only `approval.approval_decisions` (`decided_by_user_id <> requested_by_user_id`).
- `Audit` owns append-only `audit.audit_events`.
- `Integration` owns `integration.outbox_messages`, `integration.inbox_receipts`, `integration.command_idempotency`, `TenantOutboxProcessor`, and `OutboxBackgroundWorker`.

### C. Critical Invariants & Verification Results
| Test / Check | Evidence File | Result |
|---|---|---|
| `dotnet test Orphac.Athera.sln` (`21/21` UnitTests + `20/20` Real PostgreSQL 18 integration tests: `M02` Up/Down/Up, `FORCE ROW LEVEL SECURITY` on all 8 S02 tables, append-only `SELECT, INSERT` grants on `audit_events` & `approval_decisions`, SHA-256 invitation expiry/revocation/rejoin non-reactivation, SQL four-eyes constraint, RFC 6238 TOTP MFA gate, outbox idempotency/inbox dedup) | [dotnet-test.log](./dotnet-test.log) | PASS |
| Frontend OpenAPI parity (`api:check`), `tsc --noEmit`, `eslint`, `vitest run` (`3` files, `7/7` tests), `next build`, Playwright E2E (`s00`, `s01`, `s02`: `6/6` tests on `127.0.0.1:3200` + `127.0.0.1:5200` + PostgreSQL `55433`), `npm audit --omit=dev --audit-level=high` (`0`), & `impeccable detect --json apps/web/src` (`[]`) | [frontend-and-e2e.log](./frontend-and-e2e.log) | PASS |
| Secrets scan (`rg` across `src/`, `apps/web/src/`, `docs/`) | [secrets-scan.log](./secrets-scan.log) | PASS (`0` leaks) |
| Ponytail (`full`) code review | [ponytail-review.md](./ponytail-review.md) | PASS (`Ship`) |
| Impeccable finish review (`36f3a365`: Round 1 `fix` -> 8 fixes resolved -> Round 2 `recapture` -> `ship`) | [impeccable-review.md](./impeccable-review.md) | PASS (`ship`) |
| Captured Desktop (`1280x800`) & Mobile (`390x844`) Screenshots | [screenshots/](./screenshots/) | PASS |

### D. Migration, Rollback & Known Limitations
- **Apply migration & demo seed:** `ASPNETCORE_ENVIRONMENT=Development ATHERA_DEMO_PASSWORD="<dev-pw>" ConnectionStrings__Database="<migrator-conn>" dotnet run --project src/Orphac.Athera.Api -- --migrate --seed-demo`
- **Rollback S02 schema (`M02 -> M01`):** `dotnet ef database update M01_TenantAndLegalEntityWorkspace --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (drops S02 tables and `approval`, `audit`, `integration` schemas cleanly; S02 integration test `S02_PG_01` verifies `M01 -> M02 -> M01 -> M02` round-trip).
- **Known dev-only dependency note:** Dev-only transitive `picomatch <=4.0.7` advisory (`GHSA-fc5q-39xc-vjpf`) in toolchain; `npm audit --omit=dev --audit-level=high` reports `0` runtime vulnerabilities.
- **Open Product Policy (`OD-02`):** Custom tenant-configurable approval thresholds and small-team break-glass exception rules await Product Owner decision on [ATH-CR-001-OD02-four-eyes-and-role-matrix.md](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md); system remains strictly fail-closed (`requester != approver`).

---

## 2. Product Owner Acceptance Sign-Off (Owner Only)
- **Reviewer Name / Role:** `____________________`
- **Decision (`APPROVED` / `CHANGES_REQUESTED`):** `____________________`
- **Date:** `____________________`
- **Notes:** `____________________`
