# ATH-S11 - Supplier Settlement, Outbound Clearing and Bank Reconciliation

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S04,S09-S10  
**Authoritative module owners:** Accounts Payable / Cash & Banking / General Ledger  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Generate a supplier payment instruction, release under separate authorization, clear AP to outbound clearing, then clear bank only after verified statement match.

### User-acceptance story
Prepare payment and file -> no GL impact; independently release -> Dr AP/Cr Outbound Clearing; bank statement confirms -> Dr Outbound Clearing/Cr Bank; rejected/duplicate matches handled.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** ap.SupplierPayment, PaymentAllocation, cash.BankAccount, PaymentInstruction, BankStatement/Line, BankPaymentMatch; GL source posting links.

**Application / API contract.** Payment proposal/approve/release API; file preview/export simulation (no live transfer); statement CSV import/reconcile API; exception management.

**Next.js experience (use Impeccable).** Impeccable: unmistakable payment lifecycle (draft vs file generated vs released vs bank-confirmed); before-release confirmation shows accounts, allocations and financial effect.

Before editing code, create `docs/design/slices/S11-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement payable eligibility and double-alloc prevention; four-eyes release, outbound clearing journal, bank statement staging, matching, reversal/reject rules, idempotent settlement proof and audit references. Restrict bank credential handling.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
File generation alone never clears AP or bank; payment allocations <= open AP; clearing account reconciles to unreconciled releases; bank statement line cannot settle twice; payment must be entity-scoped.

## Mandatory executable acceptance tests
Integration tests for file-only no posting, release posting once, duplicate bank statement, partial match, reject/reversal, unauthorized release, race in allocation and ledger reconciliation.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No live bank API, no signed real payment instructions, no unapproved supplier bank account changes.

## Owner-only decisions / blockers
OD-06 exact financial release moment/evidence and OD-02 roles; until decision only prepare/test simulated payment and block real posting.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S11/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
