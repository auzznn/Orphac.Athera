# ATH-S17 - Documents, Evidence Trails and Operational UX

**Status:** Workstream-authorized engineering scope; **Product Owner acceptance pending**.  
**Prerequisites:** S02,S05-S16  
**Authoritative module owners:** Documents / Audit / Reporting / All UI domain owners  
**Requirements:** Reviewed PRD/BRS (where applicable), greenfield stack ADR, current domain/DB/tenancy ownership contracts.  

## Business outcome
Users can locate business documents and linked evidence, understand exceptions, export authorized data and perform core daily workflows with a polished coherent interface.

### User-acceptance story
Open PO/receipt/invoice/payment details; see source-linked attachments and audit timeline; export operational report; recover from error states without data loss.

## Design contract (complete before implementing)
**Domain and authoritative data ownership.** documents.BusinessDocument, DocumentLink, secure file metadata; audit query projection; integration outbox diagnostics; report read models only.

**Application / API contract.** Authorized attachment upload/download with signed/time-limited retrieval, audit endpoints, exception queues, source-linked PDF render where needed, CSV/report export endpoints.

**Next.js experience (use Impeccable).** Impeccable intensive pass: shape information architecture across modules; critique layout, labels, density, empty/error/loading and mobile; harden/audit accessibility/performance; avoid introducing new nav or UI libraries without need.

Before editing code, create `docs/design/slices/S17-design.md` recording actual source requirements, aggregate boundaries, entity scope, state transitions, migration design, endpoint request/response/errors, UI states, negative cases and focused tests. Keep it concise; no speculative framework.

## Implementation plan (use Ponytail, then implement)
Implement file metadata scoped to tenant/entity, virus/type/size validation strategy, no public bucket URLs, durable document generation and audit refs, query performance, consistent pagination/filtering, outbox diagnostics and operational error handling.

Build incrementally with the smallest vertical flow that includes real persistence + business API + usable Next.js surface. Conform to approved domain contracts. Favor existing libraries/native platform capabilities, but do not simplify away business validation, referential integrity, audit or security. Write only migrations required by this slice; maintain tenant/entity composite scope and module-owned schemas.

## Critical invariants
File authorization checks every access; export respects entity scope; no document exposes another tenant via direct URL; audit not editable; no attachment can change posted financial fact.

## Mandatory executable acceptance tests
End-to-end attachment auth with forged IDs, document PDF content checks, export scope, outbox retry, UX screenshot/a11y review, query-load tests on realistic datasets.

All relevant tests must show actual `PASS`, `FAIL` or `BLOCKED` with a real command/log. Financial/posting and RLS tests require real PostgreSQL, not SQLite/InMemory mocks. Include negative-path evidence and a manual demo script.

## Explicit non-goals
No open public file index, social collaboration/chatbot or generic BI platform.

## Owner-only decisions / blockers
Retention and file sharing policy requiring OD-12 if deletion/public sharing is considered.

If a material policy is unresolved, implement reversible non-posting prerequisites where safe, create a decision request, mark the affected path BLOCKED, and continue independent work. Never substitute guesswork for an approved accounting/authorization policy.

## Completion deliverables and sign-off boundary
1. Design review document with ownership, state machine, DB constraints, API/UI contract and traceability.
2. Code + focused PostgreSQL migration(s), typed API client, real UI and correct auth.
3. Backend/frontend builds and unit/API/real PostgreSQL/E2E tests with reproducible evidence.
4. Ponytail simplification review and Impeccable UX critique/audit screenshots (if supported), findings addressed or tracked.
5. Commit SHA, migration/recovery instructions, known defects and risk register in `docs/evidence/S17/`.
6. Updated `WORKSTREAM-STATE.md` with **engineering** verification result only. Product Owner acceptance must remain blank/PENDING until a human signs the standard Orphac four-pillar acceptance record.
