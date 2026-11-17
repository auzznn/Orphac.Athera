# ATH-S15 - Customer Receipts, Allocation, AR Aging and Credits

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S11,S14  
**Authoritative module owners:** AR / Cash & Banking / GL  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Finance can record a bank-confirmed customer receipt, allocate it to one or several invoices, and reconcile open AR balances without double collection.

### User-acceptance story
Import bank statement line -> identify customer -> allocate partial amount across two invoices -> leave unapplied remainder explicitly accounted; view aged AR.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** ar.CustomerReceipt, ReceiptAllocation, AR aging projection, customer CreditNote linkage; cash.BankStatementLine/Match; gl posting source.

**Application / API contract.** Bank receipt review/confirm, allocate/unallocate/reverse APIs, open-invoice suggestions and aging report endpoint.

**Next.js experience (use Impeccable).** Impeccable: payment allocation table with totals, remaining unapplied amount and source statements; distinguish payment received, allocated and reconciled.

Before editing code, create `docs/design/slices/S15-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement amount conservation, duplicate bank statement guard, transactionally balance AR/bank/clearing effects, partial allocation, unapplied balance status, authorized correction/reversal and tenant-scoped aging.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Sum allocations <= actual receipt, invoice open amount cannot drop below zero without valid credit policy, one bank line not recognized twice; GL AR control reconciles.

## Mandatory executable acceptance tests
Test partial receipt, multiple invoices, unapplied funds, duplicate bank import, reversal, closed period, out-of-order requests and cross-tenant allocations.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No payment gateway, live bank sync, factoring or complex credit netting.

## Owner-only decisions / blockers
OD-09 customer return/credit/refund and OD-07 tax implications of credit notes. Hold unsupported correction scenarios.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S15/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
