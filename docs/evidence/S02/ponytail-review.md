# ATH-S02 Ponytail (`full`) Engineering Review

**Reviewer Mode:** Ponytail (`full`)  
**Scope:** Full S02 vertical slice — `Identity` (`RoleTemplates`, `UserInvitation`, partial unique index on `EntityAccessGrant`), `Approval` (`ApprovalPolicy`, `ApprovalCase`, `ApprovalDecision`), `Audit` (`AuditEvent`), `Integration` (`OutboxMessage`, `InboxReceipt`, `CommandIdempotency`, `TenantOutboxProcessor`, `OutboxBackgroundWorker`), EF Core `M02_AdminApprovalsAuditAndOutbox`, `AdminAndApprovalEndpoints`, MFA TOTP endpoints, `DemoDataSeeder`, Next.js UI (`/`, `/invitations`, `/w/[tenantId]/[entityId]/admin`, `/w/[tenantId]/[entityId]/approvals`, `AppShell.tsx`), and S02 unit/integration/E2E suites.

## Review Checklist & Findings
1. **Minimalism & No Speculative Infrastructure:** PASS
   - Only S02 tenancy/governance schemas (`approval`, `audit`, `integration` + `identity.user_invitations`) were introduced; no downstream commercial/financial tables (`partners`, `catalog`, `gl`, `tax`, `procurement`, etc.) exist yet.
   - Open policy decision (`OD-02` four-eyes threshold and role matrix) uses fail-closed defaults (`requester != approver`, high-risk MFA enforcement) and is explicitly escalated in [ATH-CR-001-OD02-four-eyes-and-role-matrix.md](../../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md).
2. **DRY & Single Responsibility:** PASS
   - `AdminAndApprovalEndpoints` centralizes scoped execution (`ExecuteInEntityAsync`), permission checks, and audit+outbox writes atomically within the entity transaction; `TenantOutboxProcessor` isolates PostgreSQL `FOR UPDATE SKIP LOCKED` claims, savepoint failure isolation, `ChangeTracker` cleanup, and `InboxReceipt` deduplication.
   - Frontend tables in `/admin` and `/approvals` use single responsive `<table />` DOM structures (`block` on mobile with inline `md:hidden` labels, `md:table-row` on desktop) instead of duplicating markup across breakpoints.
3. **Database & Security Invariants:** PASS
   - `M02` enables `ENABLE ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY` on all 8 S02 tables with `athera_migrator` explicit policy support and `platform.current_invitation_token_hash()` scoped lookup during token acceptance.
   - Runtime role `athera_app` holds `SELECT, INSERT` ONLY on `audit.audit_events` and `approval.approval_decisions` (`REVOKE UPDATE, DELETE`); SQL check constraint `decided_by_user_id <> requested_by_user_id` enforces four-eyes at the PostgreSQL layer as well as domain/API layers.
   - Invitation tokens store SHA-256 hashes only; rejoining a tenant after revocation (`EntityAccessGrant` partial unique index `status <> 'REVOKED'`) never reactivates prior revoked grants (`S02_API_01`).
   - Catching `PostgresException` (`23505`) and `DbUpdateConcurrencyException` occurs outside `ExecuteInEntityAsync` so aborted PostgreSQL transactions roll back cleanly before mapping to `409 Conflict`.
4. **Verification Discipline:** PASS
   - Real PostgreSQL 18 (`21/21` Unit + `20/20` Integration tests), real Chromium Playwright E2E (`6/6`), Vitest (`7/7`), TypeScript, ESLint, Next.js build, and `impeccable detect` (`[]`) all pass with zero SQLite/InMemory shortcuts.

**Verdict:** **Ship** (cleanly scoped, invariant-hardened, maintainable foundation for S03+).
