# ATH-S02 Design — Organization Administration, Approvals and Audit Core

**Slice:** S02 (`docs/implementation/slices/S02_organization_administration_approvals_and_audit_core.md`)  
**Status:** Workstream-authorized engineering scope; Product Owner acceptance pending  
**Prerequisites:** S01 (`737971a`)  
**Related Open Decision:** `OD-02` ([ATH-CR-001-OD02-four-eyes-and-role-matrix.md](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md))

---

## 1. Business Behavior & User Journey
1. **Organization & Entity Administration (`admin.manage`):** An authorized entity administrator invites users by email for a specific legal entity & role template (`PROCUREMENT_OFFICER`, `INVENTORY_CLERK`, `FINANCE_OFFICER`, `ENTITY_APPROVER`, `ENTITY_ADMIN`), views members & effective grants, assigns whitelisted grants, suspends/revokes access, and revokes pending invitations.
2. **Invitation Acceptance (`POST /api/invitations/accept`):** An invited user holding an active Identity account with matching email presents the one-time invite token. The server validates SHA-256 `token_hash`, expiry, email match, and `PENDING` status before activating/creating tenant membership and inserting **only** the newly invited entity grants. Rejoining never reactivates old `REVOKED` grants.
3. **Four-Eyes Approval Core:** An authorized user (`approval.request`) submits an access/action request (`Idempotency-Key` safe). A **distinct** authorized approver (`approval.decide`, and MFA-verified if `requires_approver_mfa = true`) reviews pending cases and approves or rejects with a written justification. Self-approval is blocked at both API (`403 four_eyes_self_approval_forbidden`) and DB (`CHECK` constraint) levels; concurrent decisions yield `200` + `409` cleanly.
4. **Append-Only Audit & Transactional Outbox:** Every privilege, invitation, and approval state change atomically writes `audit.audit_events` and `integration.outbox_messages` in the same DB transaction. `TenantOutboxProcessor` processes pending outbox messages inside tenant-scoped transactions (`FOR UPDATE SKIP LOCKED`), recording idempotent `integration.inbox_receipts`.

---

## 2. Module Ownership & Bounded Contexts

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `Identity` | `identity.user_invitations`, `identity.tenant_memberships`, `identity.entity_access_grants`, TOTP MFA via `UserManager` | Invitations, membership/grant lifecycle, TOTP enrollment, permission catalog & `RoleTemplates` | Business approvals, financial postings |
| `Approval` | `approval.approval_policies`, `approval.approval_cases`, `approval.approval_decisions` | Policy rules, request/case lifecycle, immutable four-eyes decision evidence | Custom workflow/BPM DSL, ERP source docs |
| `Audit` | `audit.audit_events` | Append-only governance & privilege audit log | Business transactional balances |
| `Integration` | `integration.outbox_messages`, `integration.inbox_receipts`, `integration.command_idempotency`, `TenantOutboxProcessor` | Outbox relay, inbox deduplication, command idempotency | Financial accounting rules |

---

## 3. Database Schema & Forced RLS (`M02_AdminApprovalsAuditAndOutbox`)

1. **`identity.user_invitations`**: `invitation_id uuid PK`, `tenant_id uuid`, `legal_entity_id uuid`, `invited_email varchar(256)`, `role_template_code varchar(64)`, `token_hash varchar(64) UNIQUE`, `status varchar(32) CHECK (IN ('PENDING','ACCEPTED','REVOKED','EXPIRED'))`, `invited_by_user_id uuid`, `accepted_by_user_id uuid NULL`, `expires_at_utc`, `created_at_utc`, `updated_at_utc`, `accepted_at_utc NULL`, `revoked_at_utc NULL`, `version bigint DEFAULT 1`. Composite FKs to `(tenant_id, legal_entity_id)` and `(tenant_id, invited_by_user_id)` (`RESTRICT`).
2. **`identity.entity_access_grants` (Index update)**: Replace unconditional unique index with partial unique index `WHERE status <> 'REVOKED'` so revoked grant rows stay immutable history while re-grants insert fresh `GrantId` rows.
3. **`approval.approval_policies`**: `policy_id uuid PK`, `tenant_id`, `legal_entity_id`, `policy_code varchar(64)`, `name varchar(160)`, `requires_four_eyes bool`, `requires_approver_mfa bool`, `is_active bool`, `version bigint DEFAULT 1`. Composite AK `(tenant_id, legal_entity_id, policy_id)` and unique `(tenant_id, legal_entity_id, policy_code)`.
4. **`approval.approval_cases`**: `case_id uuid PK`, `tenant_id`, `legal_entity_id`, `policy_id`, `action_type varchar(64)`, `target_user_id`, `target_permission_code varchar(100)`, `request_reason varchar(500)`, `status varchar(32) CHECK (IN ('PENDING','APPROVED','REJECTED','CANCELLED'))`, `requested_by_user_id`, `requested_at_utc`, `decided_by_user_id NULL`, `decided_at_utc NULL`, `version bigint DEFAULT 1`. `CHECK (decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id)`. Composite AK `(tenant_id, legal_entity_id, case_id)`.
5. **`approval.approval_decisions`** (*append-only*): `decision_id uuid PK`, `tenant_id`, `legal_entity_id`, `case_id`, `requested_by_user_id`, `decided_by_user_id`, `decision varchar(16) CHECK (IN ('APPROVED','REJECTED'))`, `reason varchar(500)`, `approver_mfa_verified bool`, `decided_at_utc`. `UNIQUE (tenant_id, legal_entity_id, case_id)` & `CHECK (decided_by_user_id <> requested_by_user_id)`.
6. **`audit.audit_events`** (*append-only*): `event_id uuid PK`, `tenant_id`, `legal_entity_id`, `actor_user_id`, `event_type varchar(80)`, `subject_type varchar(64)`, `subject_id varchar(128)`, `summary varchar(300)`, `payload_json jsonb`, `occurred_at_utc`.
7. **`integration.outbox_messages`**: `message_id uuid PK`, `tenant_id`, `legal_entity_id`, `topic varchar(120)`, `source_key varchar(160)`, `payload_json jsonb`, `status varchar(24) CHECK (IN ('PENDING','PROCESSED','FAILED'))`, `attempt_count int`, `last_error varchar(500) NULL`, `created_at_utc`, `next_attempt_at_utc`, `processed_at_utc NULL`, `version bigint DEFAULT 1`. AK `(tenant_id, message_id)`, unique `(tenant_id, topic, source_key)`.
8. **`integration.inbox_receipts`**: `receipt_id uuid PK`, `tenant_id`, `consumer_name varchar(100)`, `message_id uuid`, `processed_at_utc`. Unique `(tenant_id, consumer_name, message_id)`, FK `(tenant_id, message_id) -> outbox_messages`.
9. **`integration.command_idempotency`**: `id uuid PK`, `tenant_id`, `legal_entity_id`, `operation_code varchar(80)`, `idempotency_key varchar(128)`, `actor_user_id`, `resource_id varchar(128)`, `status_code int`, `created_at_utc`. Unique `(tenant_id, legal_entity_id, operation_code, idempotency_key)`.

### Forced RLS & Append-Only Least Privilege
- Helper `platform.current_invitation_token_hash()` (`STABLE`, `SECURITY INVOKER`, `SET search_path = pg_catalog`).
- `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` on all 8 tables.
- `athera_app` gets `SELECT, INSERT` **only** (never `UPDATE`, never `DELETE`) on `audit.audit_events` and `approval.approval_decisions`.
- `athera_app` gets `tenant_id = platform.current_tenant_id()` policies across all 8 tables; `identity.user_invitations` `SELECT` also allows `token_hash = platform.current_invitation_token_hash()` for token-verified acceptance before binding `app.tenant_id`.
- `athera_migrator` receives `p_*_migrator_all` policies so seeding/migrations operate cleanly under `FORCE ROW LEVEL SECURITY` without `BYPASSRLS`.

---

## 4. API & Next.js UX Contracts
- **API:** MFA setup/verify (`/api/auth/mfa/setup`, `/api/auth/mfa/verify`), Admin overview & invitations/grants/suspend/revoke (`/api/workspaces/{tenantId}/entities/{entityId}/admin/...`), Invitation acceptance (`/api/invitations/accept`), Approval cases & decisions (`/api/workspaces/{tenantId}/entities/{entityId}/approvals/cases/...`), and Admin outbox dispatch (`/admin/outbox/dispatch`).
- **UI (`DESIGN.md`):** Unlocks `Persetujuan` (`/w/[tenantId]/[entityId]/approvals`), `Administrasi` (`/w/[tenantId]/[entityId]/admin`), `/invitations`, and MFA enrollment card on `/` with clear four-eyes self-approval warnings, explicit text-labeled `Setujui` / `Tolak` / `Cabut` buttons, append-only audit ledger, and `<768px` responsive stacked rows.

---

## 5. S02 Mandatory Test Matrix
- `S02-PG-01`: `M02` Up -> Down (`M01`) -> Up (`M02`) & forced RLS catalog verification on all 8 tables.
- `S02-PG-02`: `athera_app` `UPDATE` / `DELETE` on `audit.audit_events` & `approval.approval_decisions` fail with `42501`.
- `S02-API-01`: Invitation creation, expiry (`410`), revocation (`409`), email mismatch (`403`), acceptance (`200`), and stale `REVOKED` grant non-reactivation after membership revoke + selective rejoin.
- `S02-API-02`: Unknown permission craft (`400`), missing `admin.manage` (`403`), and cross-entity grant rejection (`404`).
- `S02-API-03`: Approval `Idempotency-Key` deduplication, self-approval rejection (`403` + DB `23514` check), concurrent dual-approver race (`200` + `409`), and MFA-required approval enforcement (`403 mfa_required` -> `200`).
- `S02-PG-04`: `TenantOutboxProcessor` failure/retry, `InboxReceipt` deduplication, and empty idle pool GUCs.
- `S02-E2E-01..02`: Playwright full-stack browser flows (invitation creation/acceptance + revocation, four-eyes approval request & distinct approval + audit/outbox verification, desktop + `390px` mobile screenshots).
