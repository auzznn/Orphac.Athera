# Orphac Athera — Product Requirements Document

| Field | Value |
|---|---|
| Document ID | ATH-PRD-001 |
| Version | 0.2 — Stack-aligned product direction |
| Baseline date | 24 October 2026; technology update 17 November 2026 |
| Product owner | Orphac |
| Product | Orphac Athera |
| Category | B2B Enterprise Resource Planning (ERP) |
| Initial market | Indonesian small and medium enterprises (SMEs) |
| Delivery model | Multi-tenant SaaS |
| Technical direction | Next.js + React + TypeScript; ASP.NET Core 10 Web API; PostgreSQL + EF Core 10/Npgsql |
| Approval | Product-level review questions approved; granular business/financial rules governed by BRS and later decisions |

> **Version note:** v0.2 preserves the product scope and business direction of the reviewed PRD v0.1, while updating its technology statement to the Product Owner's November 2026 stack decision. Detailed P2P policies remain governed by the BRS and decision register. This document does not imply that each unresolved P0 policy has been individually approved.

## 1. Executive summary

Orphac Athera is a modular, cloud-based ERP platform for Indonesian SMEs. It aims to address **fragmented business information and disconnected workflows** that force employees to re-enter transactions and reconcile inconsistent operational and financial records.

Athera provides an **industry-neutral business core** — buying, selling, inventory, billing, and accounting — combined with optional capabilities and industry extensions. It must not impose metal-foundry-specific requirements on all customers. The eBST manufacturing system serves as a business-logic reference, not a database or architecture template to port directly.

### Product vision

Make integrated, reliable, and adaptable enterprise software accessible to growing Indonesian businesses, without the implementation complexity of large-enterprise ERP deployments.

### Product proposition

**One connected business platform, built around how your business operates.**

### Product principles

1. **Industry-neutral foundation.** Common operations do not require manufacturing/foundry entities.
2. **Single authoritative owner.** Every critical record is owned by one module.
3. **Reliable business effects.** Inventory and accounting postings are transactionally protected.
4. **Modularity.** Customers use relevant capabilities; modules share contracts, not arbitrary table writes.
5. **Configuration over customer forks.** Variation is handled by controlled policies and extensions.
6. **Traceability.** Source documents, approvals, movements, journals, and corrections remain linked.
7. **Operational clarity.** Workflows are designed for nontechnical SME staff.
8. **Explicit rules and tests.** Critical states, exceptions, and release criteria are documented.

## 2. Background

Athera evolves from eBST, a metal-foundry ERP/MES built for PT. Berkah Selaras Teknindo. Its functional reference covers authentication and authorization, partners, sales and invoicing, purchasing and receiving, raw-material and finished-goods inventory, manufacturing execution, QC and traceability, accounting, documents, collaboration, and a customer portal.

The new product must separate common business concepts from foundry-specific ones:

- A unified **Item Catalogue** replaces separate default identities for parts and materials, while inventory quantity remains separately owned.
- Generic **Sales Orders** are distinct from customers' purchase-order references.
- **Inventory** unifies physical stock movements, reservations, locations, and valuation across item categories.
- **Billing** is independent from the physical fulfillment method; **Accounts Receivable** and **General Ledger** own financial recognition.
- **Manufacturing**, **Quality**, **Traceability**, and the **Foundry extension** are optional capabilities.

## 3. Problem statements

| ID | Problem hypothesis | Intended product response |
|---|---|---|
| BRP-001 | Fragmented records across spreadsheets and applications | One integrated, authoritative transaction chain |
| BRP-002 | Duplicate departmental data entry | Reuse records through module contracts and business events |
| BRP-003 | Limited source-to-outcome traceability | Linked operational, inventory, and financial history |
| BRP-004 | Disconnected operational/financial records | Controlled postings, reconciliations, and audit evidence |
| BRP-005 | ERP adoption complexity | Incremental modules, sensible defaults, clear operational UI |

These are product hypotheses accepted as the initial business direction, **not yet independently proven market research**.

## 4. Initial customers and personas

### Initial target segments

**Primary:** Trading businesses, wholesale distributors, and inventory-led Indonesian SMEs with recurring purchases, sales, and financial reconciliation needs.

**Expansion:** Manufacturing SMEs, professional services, and businesses requiring specialized workflows. Retail POS and construction-specific workflows need their own future capabilities before being marketed as complete solutions.

### Business personas

| Persona | Key needs |
|---|---|
| Owner / Executive | Revenue, cash, profitability, working capital, purchasing and stock visibility |
| Sales Officer | Customer records, sales orders, fulfillment status, billing progress |
| Procurement Officer | Supplier records, purchase orders, deliveries, outstanding commitments |
| Warehouse / Inventory Officer | Receiving, stock availability, locations, adjustments, movement trace |
| Finance / Accounting Officer | AP/AR, journals, payments, close, reconciliation, financial reports |
| Organization Administrator | Users, roles, entity scope, configuration, controlled approvals |
| Orphac Platform Operator | Provisioning, uptime, diagnostics, restricted support access |

One person may perform several business duties, but the separation-of-duties and four-eyes policies must be enforced for applicable sensitive actions.

## 5. Product scope and release priorities

`P0` means required in the commercial MVP scope; it does **not** mean all features are required for the first coding slice. `P1` is planned expansion; `P2` is deferred.

| Module | MVP responsibility | Priority |
|---|---|---|
| Tenancy & Organization | Provision tenant, legal entity, initial setup, entitlement enforcement | P0 |
| Identity & Access | Secure sign-in, membership, roles, entity permissions, MFA, audit | P0 |
| Business Partners | Suppliers/customers, contacts, addresses, commercial terms | P0 |
| Item Catalogue | Stock/non-stock goods and services, UoM, item policies | P0 |
| Procurement | Supplier purchase orders, receiving, approval, partial receipts | P0 |
| Inventory | Movements, stock balances, reservations, lots as required, valuation | P0 |
| Warehouse & Fulfillment | Locations, basic dispatch/picking, delivery evidence | P0 |
| Sales | Sales orders, commercial lines, changes, progress | P0 |
| Billing | Invoice drafts, finalization, tax data, PDFs, credits | P0 |
| General Ledger | CoA, periods, journals, opening balances, reports | P0 |
| Accounts Payable | Supplier bills, matching, payable balances, settlements | P0 |
| Accounts Receivable | Customer invoices posted to AR, payments, allocations, aging | P0 |
| Cash & Banking | Accounts and supplier/customer payment records | P0 |
| Documents | Attachments and business-document PDFs | P0 |
| Reporting | Operational summaries, financial reports and export | P0 |
| Business Events | Durable inter-module events, replay/idempotency/diagnostics | P0 |
| Quality Management | Advanced inspection plans and nonconformance | P1 |
| Manufacturing | BOM/routing, work orders, operation execution | P1 |
| Traceability | Advanced lot/serial genealogy and recall investigation | P1 |
| Planning / MRP | Demand and material replenishment planning | P2 |
| Project & Service Delivery | Timesheets, milestones, service delivery | P2 |
| Foundry Extension | Heats, pours, casting batches, remelt | P2 |
| AI Assistant / Collaboration | Controlled assistant and messaging | P2 |

### Explicit MVP exclusions

Advanced MES/APS, full HR/payroll, POS, native commerce storefront, universal user-designed workflows, sophisticated warehouse automation, autonomous AI operations, automated statutory tax filing, multi-country compliance, intercompany transactions, consolidation, native bank integration, and advanced supplier advance payments.

## 6. Core business workflows

### WF-P2P-001 — Procure-to-Pay

Supplier master → Item master → Purchase order → Approval → Physical dock receipt → Receiving acceptance / quarantine → Inventory and GRNI posting → Supplier bill and three-way match → AP/GL posting → Payment release → Bank reconciliation.

This workflow is the **first fully specified business process**. Detailed accepted behavior lives in `ATH-BRS-P2P-001-v0.3.md`.

### WF-O2C-001 — Order-to-Cash

Sales order → permitted stock/service fulfillment path → dispatch or milestone → invoice → AR/GL posting → customer payment and allocation. Partial delivery/payment, valid standalone invoices, and reversals must be supported under approved policies.

### WF-PLAN-001 — Plan-to-Produce (future)

Demand → approved BOM/routing → work order → material readiness/reservation → operations → QC disposition → finished stock receipt → traceability and costing. Foundry-specific operations remain optional.

## 7. Functional requirements overview

### Platform (P0)

- `TEN-001..007`: Tenant provisioning, settings, memberships, entitlements, isolation, future dedicated deployment.
- `IAM-001..007`: Authentication, invite/disable users, MFA, roles, server authorization, session revocation, security audit.
- `BPT-001..006`: Partner directory supporting customer and supplier roles with durable historical references.
- `CAT-001..008`: Shared items/services, UoM conversions, purchasing/selling eligibility, stock/lot/serial policy.

### Commercial operations (P0)

- `PUR-001..008`: Create/approve purchasing commitments; track receipts, partials, exceptions, cancellation and closure.
- `INV-001..010`: Movement ledger, on-hand/available quantities, locations, reservations, adjustments, lots where configured, reconciliation.
- `SAL-001..007`: Sales orders and authorized modification/cancellation; track fulfillment and invoicing quantities.
- `FUL-001..007`: Eligible fulfillment, picking/dispatch, partials, delivery evidence; Inventory owns movements.
- `BIL-001..009`: Invoice preparation, tax determination, numbering, PDFs, financial posting handoff, credit corrections.

### Financial operations (P0)

- `GL-001..007`: Chart of accounts, balanced journals, period controls, opening balances, trial balance and statements.
- `AR-001..007`: Customer receivables, payments/allocations and aging, reversals.
- `AP-001..007`: Supplier bills, 3-way matching where applicable, payable settlement and credits.
- `COST-001..005`: Perpetual weighted-average inventory valuation, movement value, applicable COGS, corrections, GL reconciliation.

### Supporting experience (P0)

- `RPT-001..007`: Authorized reports across operational and financial domains; distinguish delayed projections from authoritative ledgers.
- `INT-001..009`: Synchronous contracts, transactionally durable events, retries, idempotency, tenant scope and observability.

Exact business rules and their approvals belong in BRS/domain/lifecycle artifacts rather than being inferred from this overview.

## 8. Industry and module boundaries

- **Catalogue** owns item definitions; **Inventory** owns stock quantities/valuation.
- **Procurement** owns purchase commitments and goods-receipt evidence; it may coordinate but never arbitrarily mutate stock ledger rows.
- **Inventory** owns stock movements, custody statuses of valued stock, reservations and valuation pools.
- **General Ledger** owns journals and GRNI financial accrual positions.
- **Accounts Payable** owns supplier bills, matching and settlements; **Cash & Banking** owns payment and bank evidence.
- **Sales** owns customer commitments; **Billing** owns commercial invoice documents; **AR** owns customer financial obligations.
- **Manufacturing** consumes/produces through Inventory contracts. **Foundry** extends Manufacturing only.

## 9. UX and localization

- Bahasa Indonesia is the initial default UI language. English-ready architecture is preferred.
- IDR is the initial transactional currency scope; future multi-currency fields can exist before FX transactions are enabled.
- Differentiate transaction dates, local business dates and UTC event timestamps.
- Desktop-first ERP with responsive core warehouse workflows.
- Use concise business terminology, consistent state labels, linked record histories, actionable validation messages and role-appropriate menus.
- Regulatory tax behavior must be effective-dated and configurable, with applicable Indonesian compliance review before production claims.

## 10. Nonfunctional acceptance objectives

Targets below are **proposed pilot targets, not proven measurements or service guarantees**:

| Dimension | Objective |
|---|---|
| Tenant isolation | No successful cross-tenant read/write via tested API, worker, files, DB runtime paths |
| Financial correctness | All posted journals balanced and source-traceable |
| Posting idempotency | Retries cannot duplicate stock or financial postings |
| Performance | Target p95 normal read < 2 seconds, common posting < 3 seconds on specified workload |
| Event latency | Target p95 internal event processing < 60 seconds under nominal load |
| Recovery | Proposed RPO <= 1 hour, RTO <= 4 hours, subject to demonstrated backups/restores |
| Availability | Proposed initial monthly service objective 99.5%; not a signed SLA |
| Security | Verified permission, tenant/entity boundaries, MFA for privileged actions |
| Maintainability | Traceable requirements, feature-slice tests and module ownership |
| Observability | Correlation IDs, structured logs, event failure and reconciliation diagnostics |

## 11. Commercial and release success criteria

**Functional:** P2P and O2C can execute end-to-end; inventory quantities/valuation, GRNI, AR/AP, and GL reconcile; source-to-posting traceability and isolation tests pass.

**Product validation:** Demonstrate with at least two organizations from meaningfully different business contexts, including a non-foundry operation. Measure manual workarounds, reconciliation incidents, setup effort, user friction and willingness to pay.

**Commercial readiness:** Supportable onboarding, permissions, backup/recovery, monitoring, training, change control and realistic hosting economics. Product-owner confidence does not substitute for market and operating evidence.

### Release stages

0. Product requirements, BRS, domain decisions and acceptance criteria.
1. Tenant/security and shared master-data foundations.
2. First complete slice: PO → dock receipt → valued acceptance → Inventory/GRNI.
3. Complete P2P and O2C transactional business core.
4. Controlled pilot, UAT, recovery/security verification and support procedures.
5. Add Manufacturing, Foundry, Projects, integrations and other validated capabilities.

## 12. Product governance

- Requirement → Business rule → Domain owner → Vertical slice → Test → Release evidence.
- AI coding agents must read the approved specification, propose a narrow implementation plan, and stop before coding until the design gate is approved.
- A slice is done only after permission/tenant checks, business invariants, transactional behavior, failure paths, tests, traceability and human acceptance.
- Missing specifications must be reported; agents must not invent accounting, tax, or inventory policy.

## 13. Open or later decisions

Detailed P2P policies and their current dispositions are tracked in `ATH-BRS-P2P-001-v0.3.md`. Of particular importance: tolerance thresholds, four-eyes actor matrix, special-zone tax evidence and bank-release definitions. These remain implementation gates where not yet specified.

## 14. Review result

On 24 October 2026, the Product Owner approved the five high-level PRD review criteria: clear fragmented-workflow problem, MVP direction, correctness of proposed commercial/inventory/finance workflows, industry-neutral extensibility, and intended commercial viability. **That was a product-level decision, not blanket approval of every detailed requirement or unverified numeric NFR target.**

**Next references:** `ATH-BRS-P2P-001-v0.3.md`, `docs/architecture/ATH-ADR-STACK-001.md`, `docs/architecture/ATH-DOMAIN-BOUNDARIES-001.md`, and `docs/product/ATH-MVP-SLICES-001.md`.
