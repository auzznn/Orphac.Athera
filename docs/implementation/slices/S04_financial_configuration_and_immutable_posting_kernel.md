# ATH-S04 - Financial Configuration and Immutable Posting Kernel

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S01-S03  
**Authoritative module owners:** General Ledger / Tax Configuration / Financial Governance  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Each legal entity has a controlled chart of accounts, fiscal periods, account mappings and tax-code configuration; posting kernel can commit balanced immutable source-linked journals.

### User-acceptance story
Finance creates accounting period and mappings; tries valid balanced journal then invalid unbalanced/cross-entity/closed-period journal and sees precise outcome.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** gl.Account, FinancialPeriod, AccountMapping, JournalEntry, JournalLine, PostingSource and reversal link; tax.TaxCode effective-dated records/configuration, optional TaxDetermination envelope.

**Application / API contract.** Configure CoA, period, mappings, tax rule review; post/reverse journal handler; read-only GL source and line detail endpoints. Protected by entity scope and posting permissions.

**Next.js experience (use Impeccable).** Impeccable: account tree and ledger table with readable debit/credit columns; visually distinguish posted/draft/reversal and period lock; no decorative finance dashboards.

Before editing code, create `docs/design/slices/S04-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement decimal-safe balanced posting engine and source-key idempotency, period locks, immutable posted records, posting/reversal contracts, account-type/category validation, audit/outbox in same transaction. Tax engine must be configurable but not imply statutory approval.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Exactly balanced per journal and legal entity; no write to closed period; posted journal cannot be edited/deleted; same source effect cannot post twice; tax status not inferred from supplier class alone.

## Mandatory executable acceptance tests
Real PG tests: balanced/unbalanced, double submit, concurrent duplicate source, period close, reversal, entity foreign scope, migration audit/rollback.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No multicurrency FX in MVP unless authorized; no auto-generated Indonesian statutory forms or unverified tax calculations.

## Owner-only decisions / blockers
OD-07 tax specifics, OD-11 opening balance cutover, OD-13 calendar, statutory edge cases must be PO/accountant reviewed before live use.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S04/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
