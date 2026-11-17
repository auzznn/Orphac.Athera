# ATH-S04 Design — Financial Configuration and Immutable Posting Kernel

**Slice:** S04 (`docs/implementation/slices/S04_financial_configuration_and_immutable_posting_kernel.md`)  
**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**  
**Prerequisites:** S01 (`737971a`), S02 (`57618ad`), S03 (`287d7e5`)  
**Open Decision Escalations:** [ATH-CR-002 (OD-07 / OD-13)](../../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md)

---

## 1. Business Behavior & User Journey
1. **Entity Financial Configuration (`finance.config.manage`):** Authorized finance officers maintain each legal entity's Chart of Accounts (`gl.accounts`: `HEADER_GROUP` non-postable rollups vs. postable accounts across `ASSET`, `LIABILITY`, `EQUITY`, `REVENUE`, `EXPENSE`), accounting periods (`gl.financial_periods`: `OPEN -> CLOSED -> LOCKED`), standardized purpose mappings (`gl.account_mappings`: Inventory, GRNI, AP, AR, PPV, PPN Input/Output, PPh Payable, Bank Clearing/Main, Revenue, COGS), and effective-dated configurable tax rules (`tax.tax_codes` explicitly marked `UNREVIEWED_CONFIGURABLE` until PO/accountant review per `OD-07`).
2. **Balanced, Source-Linked, Immutable Journals (`finance.journal.post`):** Posting via `GlPostingKernel` guarantees every committed journal belongs to a single `(TenantId, LegalEntityId)`, matches functional currency (`IDR` MVP), has `>= 2` valid single-sided lines, is exactly balanced (`SUM(debit) == SUM(credit) > 0` in `decimal`), hits an `OPEN` fiscal period covering `PostingDate`, uses postable `ACTIVE` entity accounts, enforces `(sourceModule, sourceEventKey)` idempotency (`PayloadHash` check across sequential & concurrent retries), and atomically appends `AuditEvent` + `OutboxMessage`.
3. **Immutable Reversals & Read-Only Separation (`finance.read`):** Posted entries cannot be edited or deleted (database grants `SELECT, INSERT` only on `gl.journal_entries` & `gl.journal_lines`). Corrections create linked compensating `REVERSAL` journals (`ReversalOfJournalId`), rejecting double-reversal or reversal-of-reversal (`409`). Read-only actors inspect accounts, periods, mappings, tax rules/previews, and journal lines/sources without mutation controls.

### Explicit Non-Goals
- No multicurrency FX revaluation or foreign-currency ledger postings in S04 MVP (`CurrencyCode` must equal legal entity `FunctionalCurrencyCode`).
- No automated Indonesian statutory filings (`e-Faktur` / `e-Bupot`), inferred tax without explicit `taxCodeId`, or unapproved opening-balance cutover (`OD-11`).
- No decorative accounting dashboards or profit/loss KPI charts in this slice.

---

## 2. Module Ownership & Bounded Contexts (`ATH-DOMAIN-BOUNDARIES-001`)

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `GeneralLedger` | `gl.accounts`, `gl.financial_periods`, `gl.account_mappings`, `gl.journal_sequences`, `gl.journal_entries`, `gl.journal_lines`, `GlPostingKernel`, `AccountMappingValidator` | Entity CoA, period lifecycle, account purpose mappings, per-period journal sequence, balanced append-only journals & reversal links | POs, dock custody, stock lots/valuations, supplier bills, bank statements |
| `Tax` | `tax.tax_codes`, `TaxDeterminationEvaluator`, `TaxDeterminationEnvelope` | Effective-dated configurable PPN/PPh rules (`11/12` DPP ratio support), explicit evaluation & NPWP/evidence blocking | Statutory tax filing claims or AP/AR subledger balances |
| `Identity` (extension) | `PermissionCodes`, `RoleTemplates` | `finance.config.manage` (alongside existing `finance.read`, `finance.journal.post`) & updated `FINANCE_OFFICER` template | Financial ledger rows |
| `Audit` / `Integration` | `AuditEvent`, `OutboxMessage` | Append-only financial configuration/posting audit & outbox events (`finance.journal.posted.v1`, `finance.journal.reversed.v1`, etc.) | Accounting balance rules |

---

## 3. State Transitions & Concurrency Design

- **Financial Period (`FinancialPeriod`):** `OPEN` -> `CLOSED` -> `LOCKED` (`Closed` & `Locked` reject any posting into `[StartDate..EndDate]`; `CLOSED -> OPEN` reopening is out of S04 scope / irreversible without future governance workflow).
- **Journal Entry (`JournalEntry`):**
  - Draft composed strictly in client UI or calling module -> posted directly as immutable `STANDARD` entry.
  - Optional one-time compensating `REVERSAL` entry linked via `ReversalOfJournalId` (enforced single reversal via partial unique index `ux_journal_entries_single_reversal WHERE reversal_of_journal_id IS NOT NULL`).
- **`GlPostingKernel` Transaction & Lock Order (race-safe under `READ COMMITTED`):**
  1. Validate currency + exact `decimal` line/balance invariants + compute normalized SHA-256 `PayloadHash`.
  2. Acquire `FOR UPDATE` row lock on the matching `gl.financial_periods` row (`tenant_id`, `legal_entity_id`, `start_date <= posting_date AND end_date >= posting_date`). Reject `period_not_found_for_date` or `period_closed` immediately if invalid.
  3. **Inside the period lock**, check `(tenant_id, legal_entity_id, source_module, source_event_key)` idempotency in `gl.journal_entries`: same `PayloadHash` returns `idempotentReplay = true`; differing `PayloadHash` fails with `duplicate_posting_source_conflict` (`409`). If two different dates/periods ever race on the same `(source_module, source_event_key)`, unique index `ux_journal_entries_source_event` fails one writer and outer endpoint catch rolls back and resolves cleanly in a fresh read tx (`200` replay if hash matches, `409` if different).
  4. Validate all referenced `(TenantId, LegalEntityId, AccountId)` accounts (`ACTIVE` + `AllowPosting == true`) and optional partner/item/tax references (`TenantId` / `LegalEntityId` match).
  5. Upsert `gl.journal_sequences` (`ON CONFLICT DO UPDATE SET last_number = last_number + 1 RETURNING last_number`) -> `JRN-{PeriodCode}-{Seq:D5}`.
  6. Insert `JournalEntry`, `JournalLine`s, `AuditEvent`, `OutboxMessage`, and commit atomically.

---

## 4. Database Schema, Forced RLS & Deferred SQL Integrity (`M04_FinancialConfigAndPostingKernel`)
1. **`gl.accounts`**: `account_id uuid PK`, `(tenant_id, legal_entity_id, account_id)` AK, unique `(tenant_id, legal_entity_id, account_code)`, self-FK `(tenant_id, legal_entity_id, parent_account_id)` nullable (`parent_account_id <> account_id`), checks for valid type/category/normal balance & `account_category <> 'HEADER_GROUP' OR allow_posting = false`.
2. **`gl.financial_periods`**: `period_id uuid PK`, `(tenant_id, legal_entity_id, period_id)` AK, unique `(tenant_id, legal_entity_id, period_code)`, unique `(tenant_id, legal_entity_id, fiscal_year, period_number)`, check `end_date >= start_date`, check `status IN ('OPEN', 'CLOSED', 'LOCKED')`.
3. **`gl.account_mappings`**: `mapping_id uuid PK`, unique `(tenant_id, legal_entity_id, purpose_code)`, composite FK `(tenant_id, legal_entity_id, account_id) -> gl.accounts`.
4. **`gl.journal_sequences`**: PK `(tenant_id, legal_entity_id, period_id)`, `last_number bigint`, composite FK to `gl.financial_periods`.
5. **`gl.journal_entries`** (*append-only*): `journal_id uuid PK`, `(tenant_id, legal_entity_id, journal_id)` AK, unique `(tenant_id, legal_entity_id, journal_number)`, unique `(tenant_id, legal_entity_id, source_module, source_event_key)`, partial unique `reversal_of_journal_id` (`WHERE reversal_of_journal_id IS NOT NULL`), checks `total_debit = total_credit`, `total_debit > 0`, `line_count >= 2`, `entry_kind IN ('STANDARD', 'REVERSAL')`.
6. **`gl.journal_lines`** (*append-only*): `line_id uuid PK`, composite FKs to `gl.journal_entries` & `gl.accounts`, optional FKs `(tenant_id, partner_id)`, `(tenant_id, item_id)`, `(tenant_id, legal_entity_id, tax_code_id)`, unique `(tenant_id, legal_entity_id, journal_id, line_number)`, check one-sided strictly positive amount.
7. **`tax.tax_codes`**: `tax_code_id uuid PK`, `(tenant_id, legal_entity_id, tax_code_id)` AK, unique `(tenant_id, legal_entity_id, code, effective_from)`, check `nominal_rate_percent >= 0 AND nominal_rate_percent <= 100`, `taxable_base_ratio_numerator >= 1 AND taxable_base_ratio_denominator >= taxable_base_ratio_numerator`, optional composite FK `(tenant_id, legal_entity_id, tax_account_id) -> gl.accounts`.

- **RLS & Privileges:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 7 tables (`tenant_id = platform.current_tenant_id()` for `athera_app`, `p_*_migrator_all` for `athera_migrator`). `athera_app` receives `SELECT, INSERT, UPDATE` on `gl.accounts`, `gl.financial_periods`, `gl.account_mappings`, `gl.journal_sequences`, `tax.tax_codes`, and **`SELECT, INSERT` ONLY** on `gl.journal_entries` and `gl.journal_lines`.
- **Deferred Trigger (`gl.enforce_journal_entry_integrity`):** `AFTER INSERT DEFERRABLE INITIALLY DEFERRED` verifies at transaction commit that the referenced `FinancialPeriod` is `OPEN` covering `posting_date`, `line_count >= 2` matches inserted `gl.journal_lines`, and `SUM(debit_amount) == SUM(credit_amount) == total_debit == total_credit`.

---

## 5. Permissions, API Endpoints & Next.js UX Contracts
- **Permissions:** `finance.read`, `finance.config.manage`, `finance.journal.post` (and updated `FINANCE_OFFICER` template).
- **API Endpoints (`/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance`):**
  - `GET /accounts`, `POST /accounts`, `PUT /accounts/{accountId:guid}`
  - `GET /periods`, `POST /periods`, `POST /periods/{periodId:guid}/close`
  - `GET /account-mappings`, `PUT /account-mappings`
  - `GET /tax-codes`, `POST /tax-codes`, `POST /tax-codes/evaluate-preview`
  - `GET /journals` (nullable `page`, `pageSize`, `periodId`, `sourceModule`, `entryKind`), `GET /journals/{journalId:guid}`, `POST /journals`, `POST /journals/{journalId:guid}/reverse`
- **Next.js UX (`Impeccable` `operate` mode):** `/w/[tenantId]/[entityId]/finance` with 3 ledger tabs (`Jurnal & Buku Besar`, `Bagan Akun & Pemetaan`, `Periode Fiskal & Pajak (OD-07)`), right-aligned `.font-code tabular-nums` Debit/Credit columns, live Draft balance delta indicator before submit, `DRAF` / `DIPOSTING` / `JURNAL PEMBALIK` / `TELAH DIBALIK` & `TERBUKA` / `DITUTUP` / `TERKUNCI` badges, explicit `OD-07` configurable tax disclosure + `12%` on `11/12` DPP preview, and strict read-only separation (`Lihat Baris & Sumber`).

---

## 6. Mandatory Acceptance Test Matrix
- **Unit (`S04FinancialPostingKernelTests.cs`):** Balance/single-sided/currency validation, header vs posting account rules, account-mapping type checks, period lifecycle, deterministic payload hash, reversal inversion, `TaxDeterminationEvaluator` explicit tax-code gate + `11/12` DPP rounding + NPWP guard, `FinanceOfficer` permissions.
- **PostgreSQL 18 Integration (`S04FinancialKernelPostgresTests.cs`):** `S04_PG_01` (missing `app.tenant_id` & cross-tenant RLS on all 7 tables), `S04_PG_02` (`M03 -> M04 -> M03 -> M04` round-trip), `S04_PG_03` (`UPDATE`/`DELETE` denied `42501` on journals/lines + deferred SQL trigger blocking unbalanced SQL inserts), `S04_API_01` (CoA/period/mapping + balanced `201` + unbalanced/header `400` + foreign `404`), `S04_API_02` (sequential replay `200`, conflict `409`, 6x concurrent `Task.WhenAll` duplicate posting -> 1 journal & 1 outbox row), `S04_API_03` (period close `409`, reversal `201`, double-reversal/reversal-of-reversal `409`), `S04_API_04` (tax preview + NPWP guard + read-only `NUSA-SBY` `200`/`403` + `U1` `403` + foreign `404`).
- **UI & E2E (`s04-finance-gl.test.tsx`, `s04-finance-gl.spec.ts`):** Balanced journal + unbalanced validation + closed-period attempt + reversal + tax `12%` (`11/12` DPP) preview + read-only `NUSA-SBY` + unauthorized `U1` (`403`) + foreign `SMDR` (`404`) + desktop (`1280x800`) & mobile (`390x844`) zero-overflow screenshots.
