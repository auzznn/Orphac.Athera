# ATH-MVP-SLICES-001 - Full MVP implementation roadmap

Version 2.0 (greenfield) | 2026-11-17 | Status: **Product Owner-authorized engineering scope, pending per-slice final acceptance**.

## Why this order
Establish security/identity, controlled approvals, business master data and accounting mappings before financial transactions. Then finish Procure-to-Pay end-to-end, finish Order-to-Cash, and finally prove accounting close, operational UX and release readiness. No all-at-once 70-table migration. All slices deliver end-to-end behavior with tests and acceptance packets. Future manufacturing/foundry remains separate.

## Full slice sequence
| Slice | Name | Depends on | Business outcome / demonstration | Engineering risk |
|---|---|---|---|---|
| S00 | Greenfield full-stack foundation | Product requirements and greenfield ADR | From empty repo, Next.js + .NET API authenticate and use a disposable PostgreSQL database | Platform |
| S01 | Secure tenant and legal-entity workspace | S00 | User sees only authorized tenants/entities; SQL isolation proved | SECURITY CRITICAL |
| S02 | Organization, policy, approval and audit foundation | S01 | Admin invites/grants scoped users; independent approval and audit evidence | SECURITY CRITICAL |
| S03 | Suppliers, customers, items and units | S02 | Tenant masters searchable/editable with entity eligibility and snapshots | Data integrity |
| S04 | Financial configuration and posting kernel | S02-S03 | Legal entity has CoA/period/mapping; balanced immutable journals | FINANCIAL CRITICAL |
| S05 | Purchase orders, revisions and approvals | S03-S04 | Draft, submit, approve, revise and close a PO with traceable actor | Commitment |
| S06 | Physical dock custody and receiving | S05 | Warehouse records unvalued arrival, discrepancy and dock decision | Quantity |
| S07 | Accepted receipt, stock, valuation and GRNI | S04-S06 | One acceptance atomically posts Stock + moving-average + Dr Inventory Cr GRNI | FINANCIAL CRITICAL |
| S08 | Inventory locations, restricted stock and availability | S07 | Quarantine/block/release, lot policy, stock/availability reconciles | Inventory |
| S09 | Supplier bills, 3-way matching and AP | S07-S08 | Match receipt to supplier bill and post GRNI clearance/AP atomically | FINANCIAL CRITICAL |
| S10 | Supplier returns, replacement and corrections | S06-S09 | Trace supplier claims, accepted returns, credits, reversals without duplication | FINANCIAL CRITICAL |
| S11 | Supplier payment, outbound clearing and bank match | S04,S09-S10 | AP clears only at qualified release; bank clears only at reconciliation | FINANCIAL CRITICAL |
| S12 | Customer and sales order workflow | S03-S04 | Sales draft, approval, revision and source-linked demand | Commercial |
| S13 | Reservation, pick, dispatch and delivery | S08,S12 | Available stock is reserved and consumed on approved fulfillment | Inventory |
| S14 | Customer invoicing, tax and AR | S04,S12-S13 | Correct customer bill, AR/GL posting and compliant invoice controls | FINANCIAL CRITICAL |
| S15 | Customer payment, allocation and credit corrections | S11,S14 | Partial receipts, allocation, banking, aging and supported credits | FINANCIAL CRITICAL |
| S16 | GL reporting, reconciliation and period close | S04,S07,S09,S11,S14-S15 | Trial balance, ledger, AP/AR/GRNI reconciliation and controlled close | FINANCIAL CRITICAL |
| S17 | Documents, audit usability and operational reporting | S05-S16 | Reliable linked documents, exports, exception queues and useful executive UI | Cross-cutting |
| S18 | System-wide UAT, security, backup and release readiness | All prior | Full P2P/O2C demos, recovery tests and unsigned owner acceptance dossier | RELEASE GATE |

### Suggested long-horizon campaign
Complete S00-S01 with real PostgreSQL verification before trusting security-dependent integration. S02-S04 create reusable primitives. S05-S11 prioritize one financially correct vertical workflow before starting Sales. S12-S16 complete a minimum commercial cycle. S17-S18 polish and prove the product.

### Boundaries intentionally excluded
P1: advanced quality inspections, BOM/routing/manufacturing and advanced traceability.
P2: MRP/APS, foundry extensions, service project workflows, POS, payroll, autonomous AI, intercompany consolidation, automated tax filing, bank API integrations and dedicated tenant DB routing.

### Outcome to show the Owner
1. Procurement creates an approved PO; warehouse receives it in unvalued custody; acceptance posts stock and GRNI together; finance matches supplier bill, adjusts remaining-stock valuation and pays/reconciles.
2. Sales books an order; inventory reserves/distributes; billing posts receivable; cashier allocates customer receipt; finance closes period with balanced ledger and reconciled AR/AP/GRNI.
3. Tenant B never sees Tenant A's records, even by forged APIs, raw SQL under runtime role or reused pooled connections.

### Product governance
The plan preauthorizes design/coding/testing across slices. It does NOT preapprove unverified financial policies, irreversible transactions, production rollout or acceptance signatures. Mandatory evidence can remain blocked; agent must be explicit and cannot claim a completed MVP in that state.
