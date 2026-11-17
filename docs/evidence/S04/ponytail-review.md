# ATH-S04 Ponytail (`full`) Engineering Review

**Reviewer Mode:** Ponytail (`full`)  
**Scope:** Full S04 vertical slice — `GeneralLedger` (`GlAccount`, `FinancialPeriod`, `AccountMapping`, `AccountMappingValidator`, `JournalSequence`, `JournalEntry`, `JournalLine`, `GlPostingHash`, `GlPostingKernel`), `Tax` (`TaxCode`, `TaxDeterminationEvaluator`), `Identity` extensions (`finance.config.manage`, `FinanceOfficer`), `M04_FinancialConfigAndPostingKernel` forced-RLS + append-only + deferred SQL trigger migration, `FinanceContracts`, `FinanceEndpoints`, `WorkspaceEndpoints`, `DemoDataSeeder`, Next.js `/finance` UI (`finance/page.tsx`, `client.ts`, `AppShell.tsx`, `messages.ts`), and S04 unit/PostgreSQL/Vitest/E2E suites.

## Review Checklist & Findings
1. **Minimalism & Bounded Context Discipline:** PASS
   - Only `gl` and `tax` schemas (`7` tables: accounts, financial periods, account mappings, journal sequences, journal entries, journal lines, and tax codes) were added; zero AP invoices, POs, warehouse stock balances, payment runs, or tax filing submissions exist in S04.
   - Open Decisions `OD-07` and `OD-13` are escalated via [ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md) with blank Product Owner / Tax Advisor sign-off lines; S04 defaults to `UNREVIEWED_CONFIGURABLE` templates and explicit manual period calendars.
2. **DRY, Single Responsibility & Decimal/Idempotency Safety:** PASS
   - `GlPostingKernel` centralizes `PostJournalAsync` and `ReverseJournalAsync` inside serializable/atomic entity execution, using `SELECT ... FOR UPDATE` period locking (`gl.financial_periods`), atomic `INSERT ... ON CONFLICT DO UPDATE RETURNING last_number` per-period sequencing on a shared transaction `DbCommand`, deterministic `GlPostingHash`, and same-transaction `AuditEvent` + `OutboxMessage`.
   - Sequential and true concurrent duplicate postings (`6` parallel requests via `Task.WhenAll` in `S04_API_02`) share deterministic source idempotency (`SourceModule + SourceId`), returning the existing posted journal when payload hash matches (`200`) and failing with `409 duplicate_posting_source_conflict` on payload divergence.
   - `TaxDeterminationEvaluator` and `JournalEntry` enforce exact `decimal` arithmetic (explicit `11/12` `TaxableBaseRatioNumerator / TaxableBaseRatioDenominator`, single-sided positive debit/credit lines, `parseMinorTenThousandths` exact integer arithmetic in the Next.js composer), zero floating-point money math, and explicit `taxCodeId` selection (`explicit_tax_code_required`) rather than inferring rates from supplier flags.
3. **Database, RLS & Append-Only Ledger Invariants:** PASS
   - `M04_FinancialConfigAndPostingKernel` enables `ENABLE ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY` on all `7` tables (`athera_migrator` owner policies + `tenant_id = platform.current_tenant_id()`).
   - Runtime role `athera_app` holds `SELECT, INSERT` ONLY on `gl.journal_entries` and `gl.journal_lines` (verified in `S04_PG_03`: `UPDATE`/`DELETE` return SQLSTATE `42501`), while compensating reversals append a new `REVERSAL` entry linked back to the origin entry without mutating posted historical rows.
   - Deferred SQL constraint trigger `gl.enforce_journal_entry_integrity()` (`AFTER INSERT DEFERRABLE INITIALLY DEFERRED`) independently rejects unbalanced raw SQL journals or postings into closed/locked periods at commit time (`S04_PG_03`), and `Down()` drops triggers, functions, tables, and schemas cleanly (`S04_PG_02`).
4. **Verification Discipline:** PASS
   - Real PostgreSQL 18 (`28/28` UnitTests + `33/33` IntegrationTests), real Chromium Playwright E2E (`10/10`), Vitest (`5` files, `11/11`), TypeScript, ESLint, Next.js build, `npm audit`, secrets scan, and `impeccable detect` (`[]`) all pass with zero SQLite/InMemory shortcuts.

**Verdict:** **Ship** (invariant-complete financial kernel ready for S05+ operational posting consumers).
