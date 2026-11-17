# ATH-S14 - Customer Billing, Tax and Accounts Receivable

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S04,S12-S13  
**Authoritative module owners:** Billing / AR / GL / Tax  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Finance can invoice eligible delivered goods/services under controlled billing policy and create source-linked AR and revenue/tax journal entries exactly once.

### User-acceptance story
Ship an approved sales line, create invoice with tax determination, finalize, post Dr AR / Cr Revenue and applicable tax, view customer balance and document history.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** billing.CustomerInvoice, InvoiceLine, InvoiceRevision/CreditDocument; ar.CustomerReceivable; tax determination and evidence; gl journal sources.

**Application / API contract.** Invoice draft/preview/approve/finalize/post APIs; read delivery billing capacity, tax determination and AR source; PDFs later S17.

**Next.js experience (use Impeccable).** Impeccable: customer invoice detail with clear quantities invoiced vs eligible, totals, tax classification/evidence and irreversible posting confirmation.

Before editing code, create `docs/design/slices/S14-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Build invoice capacity and amount validation, legal-entity tax code selection, GL/revenue mapping, AR subledger, append-only posting with idempotency, credit/reversal framework for supported cases; no invoicing same fulfillment capacity twice.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
No AR without a valid posting decision, no double revenue on retries, taxes from effective and documented rules, posted invoice remains historically immutable, source/invoice/journal reconcile.

## Mandatory executable acceptance tests
Tests for partial billing, credit invoice, duplicate submit, closed period, tax status variants, permission, concurrent invoice on same shipment and rollback injection.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No automated tax filing, cross-border or intercompany invoice. Non-delivery independent invoices only if approved.

## Owner-only decisions / blockers
OD-07 Indonesian tax legal decision and OD-09 sales returns/credit/cancellation policy. Block uncertain tax or correction treatment.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S14/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
