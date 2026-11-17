# Orphac Athera — Business Requirements Specification: Procure-to-Pay

**v0.3 technical alignment:** The business rules and unresolved decision gates carry forward from v0.2. Only the database transaction reference has been aligned to PostgreSQL; this is not new approval of open policy choices.

| Field | Value |
|---|---|
| Document ID | ATH-BRS-P2P-001 |
| Version | 0.3 — Consolidated policy baseline |
| Original BRS baseline | v0.1, 24 October 2026 |
| Consolidation date | 25 October 2026; technical update 17 November 2026 |
| Parent | ATH-PRD-001-v0.2 |
| Business process | Procure-to-Pay (P2P) |
| Status | Owner-confirmed directions consolidated; unapproved thresholds, edge-case accounting and statutory compliance remain review gates |

> **Implementation warning:** This consolidates the original P2P BRS draft with the Product Owner's subsequent decisions. Do not treat references to SAP as requirements to replicate SAP transaction codes. Do not infer unspecified thresholds, tax eligibility, bank release criteria, or supplier-return journals. The BRS defines business behavior, not permission to deploy financial software without accounting/tax acceptance.

## 1. Purpose and scope

Define actors, business workflows, rules, quantities, money flows, exceptional paths, audit evidence, and acceptance conditions for:

**Supplier → Purchase order → Dock receipt → Acceptance / restriction → Inventory and GRNI → Supplier invoice matching → AP / GL → Payment release → Bank reconciliation.**

Industry-neutral purchases may be stock-tracked goods, non-stock goods, or services. Manufacturing-related purchase demand does not change the meaning of these common ERP operations.

### Included

- Supplier and Item master-data eligibility.
- Purchase order drafting, approval, revision, close/cancel.
- Two-gate receiving: unvaluated physical custody and valued accepted stock, possibly restricted.
- Partial delivery, overdelivery and rejected quantity control.
- Perpetual weighted-average inventory valuation and receipt accrual (GRNI).
- Three-way supplier invoice matching with tolerance decisions.
- Supplier liabilities, withholding/tax determination, payments, bank-outbound clearing and reconciliation.
- Four-eyes approvals, immutable posted ledgers and compensation.
- Tenant/entity isolation, events, reconciliation and acceptance testing.

### Excluded from initial complete P2P implementation unless separately planned

Automated supplier advances, foreign-currency procurement, intercompany transfers, group consolidation, full supplier-return automation, advanced manufacturing, comprehensive bank API integration and automatic Indonesian tax submission. The physical schema can be extensible without implementing these capabilities immediately.

## 2. Business objectives

| ID | Objective |
|---|---|
| BO-001 | Avoid duplicate entry across Procurement, Warehouse, Inventory and Finance |
| BO-002 | Maintain a consistent purchase commitment, receiving and payable story |
| BO-003 | Trace every accepted quantity and financial effect to its source and actor |
| BO-004 | Reconcile physical stock, financial inventory valuation, GRNI, AP and GL |
| BO-005 | Remain independent of any one manufacturing industry |
| BO-006 | Prevent tenant/entity disclosure and unauthorized posting |
| BO-007 | Support ordinary SME workflows without compromising mandatory controls |

## 3. Roles and separation of duties

| Duty | Typical actor | Responsible activity |
|---|---|---|
| INITIATE | Procurement requester | Initiates purchasing need |
| APPROVE | Independent approver | Authorizes financial commitment |
| PROCURE | Procurement officer | Issues/manages commercial order |
| RECEIVE | Receiving/warehouse officer | Records dock arrival, custody, decisions |
| RECORD | Finance officer | Records bills, journals, payments |
| RELEASE | Authorized finance/banking releaser | Releases payment for bank execution |

**DEC-P2P-007 approved principle:** At least two distinct authorized persons participate in significant financial actions (four-eyes). Each duty is separately permissioned and recorded. The precise actor-distinctness matrix, amounts and small-SME exception process are not yet approved; the implementation must not quietly assume all five duties always require five separate employees or that two users are universally enough.

## 4. Authoritative domain ownership

| Module | Owns |
|---|---|
| Tenancy / Organization | Tenant, legal entity, status, location and company scope |
| Identity & Approval | User membership, entity permissions, approval policies and actor evidence |
| Business Partners | Supplier identity and commercial eligibility |
| Item Catalogue | Item definition, UoM and stock/lot policies |
| Procurement | PO/revisions, receipts, dock checks, dispositions, returns/replacement commercial references |
| Inventory | Stock movement, valued stock statuses, balances, lots, valuation pools and entries |
| General Ledger | Balanced journals, GRNI positions, periods and control accounts |
| Accounts Payable | Supplier bill, source matching, payable subledger and allocations |
| Cash & Banking | Bank/payment execution evidence, statements and reconciliation |
| Tax | Versioned determination, source classifications, withholding evidence |
| Integration | Transactional outbox, inbox, delivery/retry status |
| Reporting | Read-only operational/financial projections; not authoritative ledger |

A module may coordinate multi-module postings under a shared SQL transaction, but must call the owning module's operation rather than directly update unrelated module tables.

## 5. Confirmed and proposed decision register

| Decision ID | Decision / outcome | Status |
|---|---|---|
| DEC-P2P-001 | Perpetual **weighted-average** inventory valuation | **Approved** |
| DEC-P2P-002 | Accepted owned inventory recognized via Inventory Asset Dr / GRNI Cr | **Approved** |
| DEC-P2P-003 | Unvaluated dock custody vs valued restricted inventory; two receiving gates | **Approved direction** |
| DEC-P2P-004 | 3-tier overdelivery rules: configured tolerance, unlimited flag, additional approvals | **Approved framework; thresholds pending** |
| DEC-P2P-005 | Linked supplier replacement reference and neutral settlement without duplicate billing | **Approved framework; scenario-specific journals pending** |
| DEC-P2P-006 | PP/DQ/BD absolute + percentage tolerance engine; explicit posting/payment block | **Approved framework; numerical limits pending** |
| DEC-P2P-007 | Four-eyes and separate initiation, approval, procurement, receiving, recording | **Approved principle; precise duty matrix pending** |
| DEC-P2P-008 | Supplier advance payments outside initial scope | **Proposed exclusion** |
| DEC-P2P-009 | Foreign-currency procurement outside initial scope; future-ready schema | **Proposed exclusion** |
| DEC-P2P-010 | Composite tenant/entity integrity and future transaction/functional currencies | **Approved architecture direction** |
| DEC-P2P-011 | Controlled non-PO expense bills | **Proposed inclusion** |
| DEC-P2P-012 | Posted stock/financial history immutable; corrections via append-only compensation | **Approved** |
| DEC-P2P-013 | Invoice price difference under moving average: stock-covered part to inventory, uncovered to difference P&L | **Approved resolution**, not universal standard-cost PPV |
| DEC-P2P-014 | Lot tracking configured by Item policy | **Proposed default** |
| DEC-P2P-015 | AP / Bank Outbound Clearing at qualified release; Main Bank on statement reconciliation | **Approved framework; posting trigger pending** |
| DEC-P2P-016 | Effective-dated Indonesian line-level PPN and transaction-specific PPh 23, including special zones | **Approved framework; compliance review required** |

## 6. Supporting master data requirements

### BRS-SUP: Business Partners

- `BRS-SUP-001`: Register a tenant-scoped partner and eligible Supplier role.
- `BRS-SUP-002`: One partner may have both customer and supplier roles.
- `BRS-SUP-003`: Maintain contacts, addresses, terms and legally relevant tax classifications.
- `BRS-SUP-004`: Only eligible active suppliers can be used on new approved POs.
- `BRS-SUP-005`: Deactivation leaves purchase/billing/payment history intact.
- `BRS-SUP-006`: Approved transactions preserve relevant supplier snapshots.

### BRS-ITM: Item Catalogue

- `BRS-ITM-001`: One tenant-scoped Item identity for goods/services.
- `BRS-ITM-002`: Base unit, optional purchase unit, validated conversion and purchase eligibility.
- `BRS-ITM-003`: Distinguish stock-tracked goods from non-stock goods and services.
- `BRS-ITM-004`: Lot tracking requirements are item-configured, not universal.
- `BRS-ITM-005`: Historical transaction line descriptions, conversions and price snapshots are preserved.
- `BRS-ITM-006`: Item master does not own on-hand, reservations, GRNI, or inventory cost pools.

## 7. Purchase order workflow

### Requirements (`BRS-PO`)

1. Create and edit DRAFT supplier PO with valid supplier, legal entity, delivery destination, item, quantity, units, price, terms and tax estimate.
2. Submit for independent approval. Only valid approval authorizes new receiving commitment.
3. Preserve the approved revision, relevant line snapshots and financial exposure.
4. Track accepted quantities, remaining commitment and authorized short closure separately from billing progress.
5. Modifying an approved material term requires a new controlled revision and approval.
6. Cannot delete an approved order with downstream references.
7. Cannot approve the same logical action twice, even after HTTP retry.

### States

`DRAFT → PENDING_APPROVAL → APPROVED → CLOSED` or permitted `CANCELLED`. Receiving progress (`NOT_RECEIVED`, `PARTIALLY_RECEIVED`, `FULLY_RECEIVED`) and invoicing progress are derived separately.

### Conservations

Purchase obligation is fulfilled by accepted quantities plus authorized short closures under the current approved revision/tolerance policy. Rejected goods do not automatically count toward accepted fulfillment unless an explicit replacement/closure policy dictates otherwise.

## 8. Two-gate receiving and custody

### Gate 1: Unvaluated dock custody (SAP 103 analogy)

Goods may be physically on-site but not recognized as company inventory. Record supplier delivery, counts, dock damage, disputed custody, inspection evidence and disposition.

Candidate states: `ARRIVED`, `DOCK_CHECK_PENDING`, `DOCK_HELD`, `DOCK_REJECTED`, `DOCK_RELEASED`.

**Rule:** Unvaluated dock goods create **no Inventory Asset/GRNI journal** and no available inventory. They remain visible in a custody ledger/receiving history. Legal title/recognition eligibility is verified according to contractual terms before valued posting.

### Gate 2: Valued accepted inventory with possible restrictions (SAP 101 + Q-stock analogy)

Once an eligible quantity is accepted and recognized, atomically create its inventory quantity, valuation and GRNI. The initial stock status may be `QUALITY_INSPECTION`, `BLOCKED`, or `UNRESTRICTED` according to item/receiving policy. Blocked and inspection stock have **zero fulfillment availability** but can remain financially valued until return, scrap, impairment or correction.

### Quantity invariant

For every receipt line, expressed consistently in the relevant base unit:

`PhysicallyDelivered = DockRejected/ReturnedUnvalued + ValuedAccepted + OtherUnresolvedCustody`

Avoid double-counting dock decisions and later Gate 2 holds: a post-acceptance quality hold changes the **status of already accepted goods**, not the original delivered/accepted conservation total.

### Receiving requirements (`BRS-RCV`)

- `BRS-RCV-001`: Only an eligible approved PO (or authorized replacement reference) permits a P2P physical receipt.
- `BRS-RCV-002`: Allow partial deliveries and capture quantity by PO line and delivery.
- `BRS-RCV-003`: Record dock decisions and accountable actors.
- `BRS-RCV-004`: Prohibit acceptance beyond unresolved eligible quantity.
- `BRS-RCV-005`: Apply overdelivery policy and required approval before valuing excess.
- `BRS-RCV-006`: One logical accepted quantity has at most one inventory/GRNI effect.
- `BRS-RCV-007`: Corrections preserve prior custody/valuation history and references.
- `BRS-RCV-008`: Status restrictions immediately affect availability, but do not automatically write off valued inventory.

## 9. Overdelivery policy

The engine evaluates three stages:

1. **PO tolerance:** line-level configured percentage/absolute quantity allowance.
2. **Unlimited overdelivery flag:** explicitly authorized, not a blanket approval bypass.
3. **Approval and authorization:** independent decision required when configured thresholds/financial exposure are exceeded.

Previously accepted quantity, pending receiving, replacement authorizations and the current approved PO revision must be considered. Excess goods can be recorded as unvaluated custody pending approval; they cannot silently become valued, available stock or GRNI.

**Open configuration:** tolerances, value thresholds, allowed approving actors, unlimited-delivery eligibility and exception expiration.

## 10. Atomic receipt valuation and GRNI

### Critical transaction TX-RECEIPT

**Coordinator:** Procurement receiving workflow, calling Inventory and GL contracts; single atomic PostgreSQL transaction coordinated inside the modular monolith.

Prerequisites: tenant/entity permission, current eligible PO/revision and quantities, valid item/UoM, accepted ownership/recognition conditions, costing pool, open period, inventory/GRNI account mappings and unique source posting ID.

Within **one commit**:

1. Record accepted receiving disposition and source identity.
2. Post Inventory stock movement and update authoritative balance/valuation.
3. Recalculate the perpetual weighted-average valuation for its pool.
4. Post balanced **Dr Inventory Asset / Cr GRNI** journal.
5. Persist source-to-movement/journal links and outbox messages.

If a mandatory substep fails, the full valued-acceptance operation rolls back. A physically arrived dock record may remain unvaluated, with retry possible.

**The outbox is not the mechanism that makes the GRNI entry later.** It only informs asynchronous consumers of effects already committed.

### Weighted-average initial formula

For a compatible, open and positively valued pool:

`NewAverageCost = (PreviousCarryingValue + AcceptedReceiptValue) / (PreviousValuedQty + AcceptedReceiptQty)`

Define pool scope by tenant, legal entity, item, functional currency and any approved valuation location/group policy. Use decimal precision and explicit rounding. A closed/zero/negative pool requires an explicit edge-case rule rather than blind division.

## 11. Later supplier invoice price differences (DEC-P2P-013)

**Approved direction:** No fixed-standard-cost PPV for the initial moving-average pool. Invoice differences attributable to valued inventory still in the appropriate pool adjust carrying value and average cost; the unsupported/uncovered portion is recognized through an approved P&L price-difference treatment.

Simplified illustration:

- Receive 100 units @ Rp50,000: GRNI Rp5,000,000.
- Issue/consume 60 units; 40 remain.
- Invoice 100 units @ Rp52,000: total matched difference Rp200,000.
- Assuming no other pool movements, returns, rounding or taxes: Dr Inventory Rp80,000; Dr price-difference expense Rp120,000; Dr GRNI Rp5,000,000; Cr AP Rp5,200,000.

Actual posting needs a transactional stock-coverage calculation and explicit handling of partial invoices, concurrent movements, consumption chronology, negative differences, residuals and valuation floors. **Do not implement the simplified illustrative formula as a complete costing engine.**

## 12. Three-way supplier bill matching and payment blocks

### Business requirements (`BRS-AP`)

- Draft bills identify supplier, source PO/receipts, eligible line quantities/prices/taxes, invoice date and due date.
- Match **PO approved terms vs eligible accepted quantity vs supplier invoice**.
- Never bill/clear the same receipt capacity twice.
- Assess price (`PP`), quantity (`DQ`) and small header difference (`BD`) independently using policy-specific absolute/percentage thresholds, direction, effective dates and legal-entity scope.
- Policy outcomes may be `MATCHED`, `WITHIN_TOLERANCE`, `APPROVAL_REQUIRED`, `POSTING_BLOCKED`, `POSTED_PAYMENT_BLOCKED`, `RELEASED`.
- A BD write-off requires an explicitly configured small-difference threshold/account and cannot conceal substantive quantity or price discrepancies.
- Successful posting clears eligible GRNI, posts AP liability, taxes and any authorized stock-coverage adjustment **atomically**. A blocked payment remains blocked if policy permits financial posting while delaying settlement.
- Posted bills and journal history remain immutable; corrections use credit/reversal workflows.

### Supplier bill posting TX-BILL

Verify tenant/entity, source eligibility, unmatched quantities, tolerance/approval result, tax determination, valuation coverage, financial period and mappings; then commit bill state, GRNI clearing, inventory/expense difference, tax effects, AP journal, match references and outbox together.

Drafting an invoice does not itself create a posted liability.

## 13. Supplier replacement and return

**Approved direction:** A linked replacement inbound reference may have zero **incremental commercial price**, but must not force zero physical inventory valuation.

- **Originally unvalued dock rejection:** no inventory/GRNI to reverse; replacement satisfies eligible original commitment; prevent duplicate billing.
- **Previously valued/possibly billed goods:** post controlled stock return, GRNI/AP/claim/credit correction appropriate to original financial status; recognize replacement at an authorized cost; reconcile before closing the claim.
- Replacement quantity cannot exceed authorized remaining entitlement.
- Retain original PO/receipt/lot/return/replacement references and approval history.

**Open:** Exact automatic net-zero credit/invoice postings for combinations of valued vs unvalued, billed vs unbilled, paid vs unpaid and open vs closed period. Do not generate zero-value accounting entries merely to make documents net to zero.

## 14. Append-only corrections and balances

**DEC-P2P-012 approved:** Posted inventory movements, valuation entries, journal entries and matching/clearing effects are append-only historical facts. Corrections create reversal/delta records with source, actor, reason, approval, date and related journal effects.

Balances and workflow status can be updated transactionally as controlled **projections** of this history. A change of status from unrestricted to blocked does not automatically change carrying value. Return, scrap and impairment accounting are distinct cases; there is no universal 'variance expense' account for all corrections.

## 15. Supplier payment and bank clearing (DEC-P2P-015)

### States (candidate)

`DRAFT → PENDING_APPROVAL → APPROVED → FILE_GENERATED → RELEASED → IN_TRANSIT → BANK_CONFIRMED → RECONCILED` plus appropriate `FAILED`, `REJECTED` or controlled `REVERSED` paths.

File generation alone must not clear AP or Main Bank.

At the **qualified payment-release posting point** (to be operationally specified):

- Dr AP
- Cr Bank Outbound Clearing

Upon matching valid bank statement evidence:

- Dr Bank Outbound Clearing
- Cr Main Bank

Support idempotent payment instructions, duplicate-statement matching protection, bank rejection, reconciliation reversal, outstanding clearing inspection, and supplier payable recovery if a released payment fails.

**Open:** Exact banking evidence that constitutes qualified release; behavior for partial bank payments, delays and failed payment files.

## 16. Indonesian tax module (DEC-P2P-016)

Tax is determined by an effective-dated, documented **Tax Code matrix** and statutory context, not one hardcoded rate. Inputs include legal entity, supplier PKP/NPWP/recipient status, item/service tax category, supply origin/destination, relevant Batam/free-trade-zone facility and required evidence, invoice date and tax reference.

- **PPN:** Calculate per eligible invoice line using the legally applicable rate, taxable base or special DPP method. A nominal 12% with an 11/12 base may produce an effective 11% for qualifying non-luxury transactions. Zero-rated, exempt and PPN-not-collected statuses must be distinct.
- **PPh 23:** Determine whether the specific transaction attracts withholding; supplier/expense flags are useful inputs but are not sufficient alone. Relevant services and some rent classes may attract 2%, or a different statutory rate (including 4% in applicable NPWP circumstances). Do not withhold on all raw-material purchases.
- Preserve gross supplier invoice/AP, calculate eligible withholding and recognize withholding liabilities at the correct statutory trigger. Maintain evidence for **Bukti Potong** and reporting/export.
- Version tax determination, jurisdiction/rule reference, effective dates and accounting postings; changing rules must not rewrite posted transactions.

**Open compliance gate:** Qualified Indonesian accounting/tax review of current laws, exclusions, Batam facilities, withholding timing and statutory document/filing behavior before production claims. The business design approves configurability, not an unverified promise of automatic legal compliance.

## 17. Tenant and legal-entity scope

- Every tenant business row carries TenantId; company-owned transactions carry LegalEntityId.
- Composite scoped foreign keys and explicit server authorization prohibit mixing valid IDs from different tenants/entities.
- Tenant-level Partner and Item may be shared, subject to legal-entity eligibility; authoritative financial records belong to one entity.
- Maintain transaction/functional currency snapshots on monetary records where relevant. IDR-only procurement can be an initial entitlement; intercompany and consolidation are deferred.
- Authoritative identity resolves from verified authentication + active membership + active entity grant, never a client-supplied header alone.

For the greenfield implementation, consult `docs/architecture/ATH-POSTGRES-SECURITY-001.md` and `docs/architecture/ATH-ADR-STACK-001.md` for PostgreSQL-specific tenancy and isolation requirements.

## 18. Events and failure behavior

### Business events (proposed contracts)

`procurement.order.approved.v1`, `procurement.dockreceipt.recorded.v1`, `procurement.receipt.accepted.v1`, `inventory.stock.received.v1`, `inventory.stock.held.v1`, `finance.grni.posted.v1`, `finance.supplierbill.posted.v1`, `inventory.valuation.adjusted.v1`, `finance.payment.released.v1`, `bank.payment.reconciled.v1`.

Each event includes EventId, tenant/entity scope, version, source action, occurrence time and correlation metadata. Use transactional outbox for committed facts, at-least-once delivery, idempotent consumers, bounded retry and operator-visible failures.

### Failure requirements

- Unapproved PO: no normal valued receipt.
- Unauthorized overdelivery: physical custody allowed; no automatic valued excess.
- Missing GRNI account: no partial accepted-inventory posting.
- Duplicate receipt/retry: no duplicated stock or journal effect.
- Concurrent receipt acceptance: accepted quantity cannot exceed eligible remaining quantity.
- Failed three-way match: preserve draft/exception; no partial payable/GRNI clearing.
- Closed period: no unauthorized posting.
- Rejected bank payment: preserve record and perform approved compensation.
- Event worker failure: authoritative posting survives and pending events recover.
- Cross-tenant access: reject without revealing foreign business information.

## 19. Numerical P2P acceptance example

All figures IDR; excludes tax and price variances.

- PO 100 units @ 50,000 = **5,000,000** commitment.
- Supplier delivers 60 units.
- Gate 1/receiving accepts 55 for valuation and rejects 5 unvalued (assuming legally eligible acceptance).
- Dr Inventory 2,750,000; Cr GRNI 2,750,000.
- Supplier bills 55 units for 2,750,000: Dr GRNI 2,750,000; Cr AP 2,750,000.
- Finance releases payment 1,000,000: Dr AP 1,000,000; Cr Bank Outbound Clearing 1,000,000.
- When statement reconciles payment: Dr Bank Outbound Clearing 1,000,000; Cr Main Bank 1,000,000.
- Remaining AP = **1,750,000**, GRNI for billed part = **0**, owned inventory received = **55 units**, before later stock movements.

Remaining purchase fulfillment: 45 accepted units, absent allowed short closure/replacement adjustments.

## 20. Business acceptance scenarios

| Test | Scenario | Core evidence |
|---|---|---|
| UAT-P2P-001 | New PO with supplier/item snapshots | Correct tenant/entity and frozen approved terms |
| UAT-P2P-002 | Approval by prohibited actor | Four-eyes validation denies |
| UAT-P2P-003 | Dock arrival | Custody tracked with no inventory/GRNI |
| UAT-P2P-004 | Gate 1 rejection | No available stock or valuation |
| UAT-P2P-005 | Valued accept with Gate 2 pending | Stock + GRNI atomically committed, unavailable |
| UAT-P2P-006 | Gate 2 pass | Released stock becomes eligible availability |
| UAT-P2P-007 | Gate 2 latent defect | Valued but blocked; no automatic expense |
| UAT-P2P-008 | Within overdelivery tolerance | Correct permitted decision under configured policy |
| UAT-P2P-009 | Excess without approval | No unauthorized inventory/GRNI posting |
| UAT-P2P-010 | Duplicate acceptance retry | One stock/journal effect |
| UAT-P2P-011 | Concurrent acceptance | No over-acceptance |
| UAT-P2P-012 | Partial invoice matching | Partial GRNI clearing, no duplicated billing capacity |
| UAT-P2P-013 | Price-difference stock coverage | Remaining stock adjusted and uncovered P&L recognized |
| UAT-P2P-014 | PP/DQ/BD threshold breach | Correct posting/payment block and audit |
| UAT-P2P-015 | BD small difference | Only approved minor write-off posted |
| UAT-P2P-016 | Appropriate PPN type and special facility | Versioned correct eligible tax calculation |
| UAT-P2P-017 | PPh 23 eligible and ineligible lines | Proper taxable base, rate and withholding evidence |
| UAT-P2P-018 | File generated, not released | No premature AP/main-bank movement |
| UAT-P2P-019 | Authorized payment released | AP / Outbound Clearing journal balanced |
| UAT-P2P-020 | Bank statement matched | Outbound Clearing closed once and bank posted once |
| UAT-P2P-021 | Replacement after dock rejection | Correct source entitlement, no duplicate charge |
| UAT-P2P-022 | Valued return after invoice/payment | Compensating stock, GL and claim/credit reconcile |
| UAT-P2P-023 | Closed financial period | No unauthorized financial posting |
| UAT-P2P-024 | Tenant/entity crafted reference | Request denied without data leak |
| UAT-P2P-025 | Event worker interruption | At-least-once delivery without duplicate business effects |

Acceptance scenarios with unresolved thresholds/accounting/tax rules cannot receive a full pass until the policy is explicitly resolved and tests execute.

## 21. Implementation milestones and dependencies

1. **ATH-SLICE-001:** Secure Tenant Workspace: real sign-in, memberships, legal-entity permission, EF query filters, SQL RLS, tests. **No Procurement or Item CRUD here.**
2. Supplier/Item/UoM master-data slices.
3. Purchase order draft/approval and immutable revision/approval evidence.
4. Physical dock receipt and custody controls.
5. Atomic valued receipt + Inventory moving average + GRNI + outbox.
6. Supplier bill matching, tax determination, price difference adjustment and AP posting.
7. Payment release, bank clearing and reconciliation.
8. Exception pathways: returns/replacements, advanced tolerances and reporting.

The system must not implement all 70 design-reference tables in the first slice.

## 22. Remaining decision and operational gates

1. Delivery-tolerance percentages, limits and approval routing.
2. The precise actor distinctness and exceptional segregation-of-duties rules.
3. Supplier replacement/return journal matrices across invoiced/uninvoiced and paid/unpaid status.
4. Full moving-average edge cases, valuation pool restrictions and quantity/cost precision.
5. Landed cost, nonrecoverable tax and late supplier invoice corrections.
6. Current Indonesian tax legality, Batam facility evidence and Bukti Potong/withholding timing.
7. Payment-release posting trigger and bank failure/partial settlement flow.
8. Multi-entity live operational limits and intercompany exclusions.

**Business policy precedence:** Explicitly approved latest Product Owner decision > approved ADR > approved domain/lifecycle model > illustrative SQL design. Missing rules must be surfaced, not invented.

## 23. Review status

**Product-level PRD direction:** Approved. **Approved P2P policy directions:** Captured in section 5. **Unresolved configuration/legal/accounting detail:** Listed in section 22. **Production readiness:** Not established by this document.

**Related documents:** `ATH-PRD-001-v0.2.md`, `docs/architecture/ATH-ADR-STACK-001.md`, `docs/architecture/ATH-DOMAIN-BOUNDARIES-001.md`, `docs/product/ATH-MVP-SLICES-001.md`.
