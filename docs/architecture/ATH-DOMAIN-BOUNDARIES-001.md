# ATH-DOMAIN-BOUNDARIES-001 — Logical Ownership and Transaction Boundaries

**Version:** 1.0 | **Scope:** Greenfield, industry-neutral trading/distribution ERP core

## One source of truth per fact
| Domain module | Owns | Must not directly own |
|---|---|---|
| Platform / Identity | Tenant, LegalEntity registry, global user, membership, entity permission | Stock, bills, journal postings |
| Approval / Audit | Approval policy/decision evidence, immutable audit trail | Applicant module source documents |
| Partners / Catalogue | Partner identity, roles, item identity, UoM | AP/AR balances, stock quantities |
| Procurement | PO, revision, commitment, supplier and terms snapshots | Stock movement or journal entries |
| Receiving / Quality | Dock custody, inspection, acceptance decisions, supplier claim evidence | Authoritative inventory value or GL ledger |
| Inventory | Locations, lot movement, stock balance, reservations, valuation pools and immutable valuation entries | GL journal entry |
| General Ledger | Chart of accounts, periods, immutable journals/lines and account balances as projections | PO and receipt source facts |
| Accounts Payable | Supplier bill, invoice matching, bill allocation, payable state | Inventory physical position, GL journal ownership |
| Cash / Banking | Payment proposal, release, outbound clearing event, bank match and cash evidence | Supplier bill source facts |
| Sales / Fulfillment | Sales commitments, reservations/picks/dispatch/delivery evidence | AR and GL journal lines |
| Billing / Accounts Receivable | Customer invoice, credits, collections and allocation | Stock ledger, cash bank reconciliation |
| Tax | Versioned tax code/treatment reference, determination/evidence | Generic rule that all items share one statutory rate |
| Documents / Integration | Authorized document metadata and file access, durable outbox/inbox/worker status | Financial source of truth |

## Cross-module invariants
- A receipt accepted as *valued stock* must atomically post eligible inventory quantity/valuation and Dr Inventory / Cr GRNI (plus source link and outbox). Dock custody before acceptance has no inventory asset or GRNI impact.
- Bill posting atomically matches approved PO + accepted receipt + invoice, clears GRNI as applicable, books AP and splits price difference between remaining inventory coverage and P&L under approved policy. No guesswork on partially consumed/negative pools.
- Payment preparation does not clear AP. A separately authorized release posts AP -> outbound bank clearing; bank reconciliation clears outbound clearing -> bank account. Do not mistake payment file creation for money transfer.
- Posted stock movements, valuation entries, journals and financial corrections are append-only. Use compensating entries and idempotent source keys.
- An outbox event never supplies a missing required financial posting. Eventual delivery is for downstream projections/notifications.
- No direct SQL writes across module ownership except through a reviewed, explicit transaction coordinator using module handlers/contracts. A module-owned schema is not a license for another module to modify it.
- Approved commercial terms have immutable revisions and snapshots; when amending, historical documents remain traceable. Independent approvals apply when required.
- Cross-tenant read/write forbidden; legal-entity scopes enforced on transactions and permission checks. Platform-wide membership is not an entity-level grant.

## Boundaries for future features
Manufacturing, foundry, MRP/APS, payroll, POS, project accounting and intercompany consolidation are *future* modules. New business concepts integrate through owned contracts and source-linked events, not a global do-everything table.

## Design rule for agent
For every slice's `Sxx-design.md`, include: authoritative record owner; aggregate boundary; transaction owner/coordinator; allowed read/write contracts; append-only vs mutable data; expected failed/retry behavior; and negative tests. Any material change to this map requires a Product Owner-reviewed change request.
