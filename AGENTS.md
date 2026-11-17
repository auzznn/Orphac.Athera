# Orphac Athera — Persistent Engineering and UX Rules (Greenfield)

## Product / authority
This is a **new codebase** for Athera; the Product Owner decides WHAT the product must do and whether each slice is accepted. The agent designs, implements, tests and documents within the preauthorized S00–S18 workstream. It **cannot sign its own acceptance**, alter material approved business policy or deploy live services without the Owner.

## Stack
- Next.js (security-patched stable), React, TypeScript, Tailwind/shadcn where useful.
- ASP.NET Core 10 API, modular monolith with owned domain handlers and controlled cross-module orchestration.
- PostgreSQL, EF Core 10 and compatible Npgsql provider, module-owned schemas, incremental migrations.
- ASP.NET Core Identity is the ONLY credential authority; secure same-origin session arrangement, CSRF defense, no separate Next.js authentication user store and no browser-stored auth tokens.
- OpenAPI + generated TypeScript client. Real PostgreSQL in integration/acceptance tests.

## Required skills
- **Ponytail (`full`)** for engineering minimalism, review and maintainability; read installed skill instructions; never trade away invariants or tests for fewer lines.
- **Impeccable** for UX strategy, layout, app shell, tables, forms, accessibility and design critique/polish. Read its actual skill commands before use; maintain PRODUCT.md and DESIGN.md with deliberate visual hierarchy. Do not invent commands or claim absent skill use.
- Visual test via screenshots/browser where environment supports it. Avoid generic admin-dashboard filler and flashy motion around critical financial actions.

## Invariants: no exceptions
1. No tenant cross-read or cross-write; legal-entity scope and actor permission checked per protected operation.
2. PostgreSQL RLS enforces visibility and write checks; missing transaction-local tenant context denies. Runtime role lacks ownership/bypass privileges; verified pooling behavior. Do not claim RLS alone resists arbitrary app-server compromise.
3. Client identifiers are selectors only; resolve membership/entitlements on server before SQL tenant context.
4. Monetary and UoM quantities use decimal-safe arithmetic, explicit precision/rounding; no floating point for postings.
5. Physical dock custody is unvalued. Accepted-valued stock, weighted-average valuation, Inventory Asset and GRNI entries post atomically with outbox.
6. Posted financial/valuation/stock records immutable. Correct with linked compensating postings, not edits.
7. GL postings balance, source-ID uniqueness/idempotency; concurrent retry cannot duplicate stock, invoice, payment or journal effects.
8. Approved PO/revisions/snapshots traceable; independent approvals where required.
9. AP invoice matching checks PO/accepted receipt/invoice; stock-price differences apportioned to remaining coverage/P&L per **approved** rules, else block.
10. Payment file preparation never settles AP. Authorized release and bank reconciliation are separate transactions.
11. Indonesian tax is effective-dated, transaction-specific and evidence-dependent. Do not invent rates/exemptions or claim filings completed.
12. No industry-specific foundry/manufacturing entity in the ERP core.

## Per-slice execution
1. Read PRD/BRS, domain/DB/tenancy/UX contracts, the next Sxx file and current state log. Check real env and Git.
2. Write short design at `docs/design/slices/Sxx-design.md`, with module ownership, business flow, state diagram if helpful, schema/APIs, permission rules, tests and Owner-only decisions.
3. Implement minimal real persistence/API/frontend and run standard tools: `dotnet test`, relevant PG integration tests with actual low-privileged role, Next typecheck/lint/build and E2E where possible. Never use EF InMemory or SQLite as substitute evidence for PostgreSQL constraints or RLS.
4. Apply Ponytail review and Impeccable UX audit; test failure cases and hostile selectors.
5. Save real logs and UAT script in `docs/evidence/Sxx/`, record SHA, commit locally; update `WORKSTREAM-STATE.md` to distinguish CODE_COMPLETE, ENGINEERING_PASS, BLOCKED and PO_ACCEPTANCE_PENDING.
6. Continue only to validated dependent slice or safely independent task. No fake success, unexecuted tests or undocumented architecture deviations.

## Stop and escalate only when necessary
Escalate security bypass, contradictory product policy, disallowed tax/financial automation, destructive action, paid cloud use, external data transfer, unplanned large architecture changes, production credentials or deployments. Where possible continue independent reversible work and mark the blocked part honestly.

## No presumptions
There is NO legacy application, no previous code to reuse or migrate, and no preexisting tests or database. Do not port anything from SQL Server/Blazor. The packaged PRD/BRS carry product business requirements; technology is determined solely by the current greenfield architecture ADR.
