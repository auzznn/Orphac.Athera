# ATH-S16 - Accounting Reports, Reconciliations and Period Close

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S04,S07,S09,S11,S14-S15  
**Authoritative module owners:** General Ledger / AP / AR / Inventory / Reporting  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Finance can trust trial balance, source-linked GL reports, AP/AR aging, GRNI reconciliation and period closing controls backed by immutable postings.

### User-acceptance story
Controller opens month, sees balanced trial balance and source drilldown; finds unmatched GRNI and subledger variances; resolves/blockers; locks period; later postings are denied.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** gl.AccountBalance projection, closing review records and period lock; reporting projections derived from authoritative GL/AP/AR/Inventory, not independent ledger owners.

**Application / API contract.** Trial balance, GL detail, P&L/balance sheet basic reports, GRNI/AP/AR reconciliation, aging exports, close/reopen workflow with authorization.

**Next.js experience (use Impeccable).** Impeccable: finance cockpit with exceptions first, narrow readable amount columns, variance explanation and click-to-source. No fabricated CFO metrics or ambiguous charts.

Before editing code, create `docs/design/slices/S16-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement deterministic balances and report periods, cost/account mapping validation, reconciliation jobs and variance evidence, independent period close approval and immutable closure audit; controlled reopening only under approved policy.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Trial balance debits = credits; GL controls tie to subledgers; closed period rejects new postings; financial reports remain source-traceable; no silent backdating.

## Mandatory executable acceptance tests
Test multiple entity/currency-safe isolation, sums under reversals, incomplete GRNI, source-to-journal reconciliation, period close race, auth and export consistency.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No full consolidation/intercompany or regulator-certified statutory filing.

## Owner-only decisions / blockers
OD-11 real opening balances and OD-13 fiscal period convention; do not declare production books reconciled from demo data.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S16/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
