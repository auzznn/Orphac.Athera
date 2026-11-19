# Ponytail Review — Slice S16 (Accounting Reports, Reconciliations & Period Close)

- **Level:** `full`
- **Scope:** `GeneralLedgerModels.cs` (`FinancialPeriod.ReopenFromClosed`, `PeriodCloseReview`), `AccountingReportsService.cs`, `M16_PeriodCloseAuditAndReconciliation.cs`, `ReportsEndpoints.cs`, `ReportsContracts.cs`, `apps/web/src/app/(shell)/w/[tenantId]/[entityId]/reports/page.tsx`, OpenAPI client, tests.

## Findings & Fixes Applied

1. **Outbox `source_key` collision on Close -> Reopen -> Re-Close cycle (FIXED):**
   - *Bug:* `ClosePeriodAsync` originally used `period.PeriodId.ToString("D")` as the outbox `source_key`, which triggered PostgreSQL `23505` unique violation on `IX_outbox_messages_tenant_id_topic_source_key` when a reopened period was closed a second time.
   - *Fix:* Keyed `gl.period.closed` outbox messages by `review.CloseReviewId.ToString("D")` so every close event is uniquely idempotent.
2. **Least-privilege immutability on `gl.period_close_reviews` (VERIFIED):**
   - `athera_app` has `SELECT, INSERT` only (no `UPDATE` or `DELETE`) plus BEFORE UPDATE/DELETE immutability trigger `gl.guard_period_close_review_immutability()` and `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `tenant_id = platform.current_tenant_id()`.
3. **Minimalism check:**
   - Reused existing `gl.financial_periods`, `gl.accounts`, `gl.journal_entries`, `gl.journal_lines`, `gl.receipt_accruals`, `ap.*`, `ar.*`, `cash.*`, and `inventory.*` tables with a single new append-only audit table (`gl.period_close_reviews`). Zero duplicated ledger tables or background cache tables.
4. **No silent swallows or fake fallbacks:**
   - `ClosePeriodAsync` rejects unbalanced TB (`409`) and unresolved control variances (`409` when `blockOnVariance = true`).
   - `ReopenPeriodAsync` mandates a non-empty written justification (`400`) and blocks `LOCKED` periods (`409 period_locked_reopen_blocked`).
   - Read-only entities (`NUSA-SBY`) hide mutation buttons and enforce `403` on the server.
