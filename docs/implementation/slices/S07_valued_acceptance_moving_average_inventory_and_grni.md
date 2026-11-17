# ATH-S07 - Valued Acceptance, Moving-Average Inventory and GRNI

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S04,S06  
**Authoritative module owners:** Procurement coordinator / Inventory / General Ledger  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Eligible accepted goods create one atomic physical-and-financial receipt: stock movement, weighted-average valuation, Dr Inventory Asset / Cr GRNI and linked outbox message.

### User-acceptance story
Warehouse accepts 100 eligible units; stock and GRNI both appear; retry is idempotent; simulated GL failure leaves neither valued stock nor GRNI.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** procurement.ValuedAcceptance; inventory.StockPosition, StockMovement, MovementLine, ValuationPool, ValuationEntry; gl.ReceiptAccrual and JournalEntry/Line, source linkage; integration.OutboxMessage.

**Application / API contract.** POST /api/receipts/{id}/accept with idempotency key; read acceptance and stock/GRNI source links. GL posting invoked through a module contract in the same DB transaction.

**Next.js experience (use Impeccable).** Impeccable: explicit acceptance preview showing quantity, stock status, valued amount, financial postings and blocking reasons; short completion confirmation with links to stock and journal.

Before editing code, create `docs/design/slices/S07-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Build transaction coordinator, valuation pool identity per tenant/entity/item/group, concurrent-pool control, decimal cost calculation and zero/negative pool handling, exact account mapping/period checks and atomic source+stock+valuation+GL+outbox commit.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
Unvalued dock goods remain unposted; one accepted source has one financial effect; positive eligible quantity and capped acceptance; stock ledger and journal cannot partially commit; balanced journal; valuation pool belongs to same entity.

## Mandatory executable acceptance tests
Real PG concurrent accepts, duplicate retries, rollback failure injection, negative/zero pool, precise decimal rounding, cross-tenant FK, closed-period rejection and outbox failure recovery.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No asynchronous GRNI generation, no direct inventory ledger mutation from Procurement without owner contract, no automatic negative inventory.

## Owner-only decisions / blockers
OD-04 unusual valuation pool/rounding/negative cases; block unsupported scenarios instead of inventing cost assumptions.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S07/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
