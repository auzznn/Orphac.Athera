# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

delegated: Next.js + React + TypeScript + Tailwind CSS (`apps/web`), ASP.NET Core 10 Web API modular monolith (`src/Orphac.Athera.Api` + `src/Orphac.Athera.Modules`), PostgreSQL 18 + EF Core 10 / Npgsql, ASP.NET Core Identity as sole credential authority, same-origin `/api/*` proxy, OpenAPI-generated TypeScript client (`docs/contracts/openapi.v1.json`). Selected from binding `docs/architecture/ATH-ADR-STACK-001.md`.

## Users

- **Owner / Executive:** Reviews cash, payables, receivables, inventory exposure, and operational exceptions with direct source drill-down.
- **Procurement Officer:** Prepares and revises supplier purchase orders, obtains independent approvals, and tracks receipts and matching exceptions.
- **Warehouse / Inventory Officer:** Records dock arrivals and physical custody (Gate 1), executes accepted stock receipt/quality disposition (Gate 2), manages locations, picks, and dispatches.
- **Finance / Accounting Officer:** Controls Chart of Accounts, fiscal periods, GRNI/AP/AR matching, Indonesian tax treatments, payment releases, bank reconciliation, and period close.
- **Organization Administrator:** Manages tenant/legal-entity membership, least-privilege entity grants, four-eyes approval policies, and audit trails.
- `[Inferred from ATH-UX-001.md]` Primary desktop workstation use in trading/distribution offices, with responsive tablet/mobile support for warehouse floor receiving and quick approvals.

## Product Purpose

Orphac Athera is an industry-neutral, multi-tenant B2B ERP for Indonesian SMEs (starting with trading and wholesale distribution) that unifies purchasing, warehouse inventory, sales, billing, and double-entry accounting into one transactionally coherent system—eliminating spreadsheet re-entry, unvalued-vs-valued receiving ambiguity, and disconnected operational and financial ledgers.

## Positioning

Unlike lightweight bookkeeping apps without dock-to-stock-to-ledger controls or complex manufacturing-heavy ERPs, Athera enforces an industry-neutral core where:
- Every critical fact has one authoritative module owner.
- Valued inventory acceptance posts stock, perpetual moving-average valuation, and Dr Inventory / Cr GRNI in a single PostgreSQL transaction.
- Tenant and legal-entity isolation are enforced both by server-verified control-plane authorization and PostgreSQL Row-Level Security (`FORCE ROW LEVEL SECURITY`).

## Operating Context

- **Primary workflow 1 (P2P):** Supplier -> Purchase Order -> Independent Approval -> Gate 1 Unvalued Dock Custody -> Gate 2 Valued Acceptance & GRNI -> 3-Way Bill Match & Moving-Average Coverage Adjustment -> AP -> Two-Stage Payment Release & Bank Statement Reconciliation.
- **Primary workflow 2 (O2C):** Customer -> Sales Order -> Stock Reservation, Pick & Dispatch -> Customer Billing & Indonesian Tax Determination -> AR -> Customer Receipt Allocation & Aging -> Period Close.
- **Localization:** Bahasa Indonesia (`id-ID`) default interface language with clean English (`en-US`) switchability; IDR (`Rp`) default transactional currency (`tabular-nums`, explicit decimal precision); Asia/Jakarta business date awareness alongside UTC audit instants.
- **Governance:** Workstream authorized under `ATH-MVP-SLICES-001.md` (S00–S18); Product Owner acceptance is never self-signed and unresolved policy gates (`OD-01`..`OD-13` in `docs/product/ATH-OPEN-DECISIONS-001.md`) remain fail-closed or blocked until signed.

## Capabilities and Constraints

- **Included MVP Scope (`P0`):** Tenancy & Legal Entities, ASP.NET Core Identity & Entity Access, Business Partners, Item Catalogue & UoM, Purchasing & Two-Gate Receiving, Moving-Average Inventory & Locations/Lots, Sales & Fulfillment, Billing/AP/AR/GL/Cash & Banking, Versioned Indonesian Tax Matrix, Documents & Outbox Integration, Operational/Financial Reporting.
- **Explicitly Excluded:** Manufacturing/BOM/Foundry extensions, MRP/APS, HR/Payroll, POS, intercompany consolidation, automated statutory tax e-filing, live bank API transfers, autonomous AI actions.
- **Open Policy Gates (`OD-01`..`OD-13`):** Overdelivery tolerances, four-eyes duty matrix thresholds, 3-way match tolerances, negative/zero valuation pool edge cases, supplier return/credit matrices, qualified payment release triggers, statutory Indonesian PPN/PPh 23 rates/facilities, credit limits, and fiscal calendar rules remain explicitly gated until Product Owner sign-off.
- **Derived under unattended `/goal` execution:** No interactive interview was conducted; all product scope statements above trace directly to `reference/ATH-PRD-001-v0.2.md`, `reference/ATH-BRS-P2P-001-v0.3.md`, `docs/design/ATH-UX-001.md`, and `docs/product/ATH-OPEN-DECISIONS-001.md`.

## Evidence on Hand

- Authoritative specifications in `reference/ATH-PRD-001-v0.2.md`, `reference/ATH-BRS-P2P-001-v0.3.md`, `docs/architecture/*`, `docs/design/ATH-UX-001.md`, `docs/testing/ATH-TESTING-001.md`, and `docs/implementation/slices/S00_*.md`..`S18_*.md`.
- **Absences (must never be fabricated):** No commercial customer testimonials, no live production metrics, no statutory Indonesian tax certification, and no Product Owner acceptance sign-offs exist. Demo identities/tenants in development are strictly synthetic `@demo.athera.local` fixtures.

## Product Principles

1. **Authoritative Ownership:** Every operational and financial fact belongs to one module; cross-module postings execute inside one coordinated PostgreSQL transaction.
2. **Fail-Closed Control & Tenancy:** Missing tenant context, revoked membership, absent entity grant, or unresolved accounting/tax policy denies rather than guesses.
3. **Immutable Posted History:** Posted stock movements, valuation entries, journals, and audit events are append-only and corrected via linked compensating records.
4. **Operational Clarity Over Chrome:** Interfaces prioritize scanability, tabular numeric precision, explicit document state, and clear recovery reasons without decorative SaaS filler or accidental one-click postings.

## Accessibility & Inclusion

- WCAG AA contrast, keyboard-first form and table operation, visible focus indicators, semantic HTML landmarks/tables, `aria-describedby` field error associations, reduced-motion respect (`prefers-reduced-motion`), and unambiguous non-color-only business status chips.
