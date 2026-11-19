# ATH-S14 Ponytail Engineering Review (`full`)

## Minimalism & Scope Check
- **Smallest complete vertical slice:** Added only the 6 required tables across `billing.*` and `ar.*` (`invoice_sequences`, `credit_note_sequences`, `customer_invoices`, `customer_invoice_lines`, `receivable_sequences`, `customer_receivables`), one cohesive coordinator (`BillingAndReceivablesCoordinator`), one endpoint module (`BillingEndpoints`), and one workbench page (`/billing`).
- **Zero speculative framework:** Reused existing `GlPostingKernel`, `InventoryValuationService.IncrementEntityYearSequenceAsync`, `TenantScopeExecutor`, `AuditEvent.Append`, `OutboxMessage.Create`, and `SalesOrder` / `SalesOrderLine` fulfillment counters without introducing generic repositories or duplicate services.
- **Permission discipline:** Reused existing `finance.read`, `finance.journal.post`, and `sales.*` permissions cleanly without mutating `u1Id`'s exact permission grants.

## Invariant & Concurrency Verification
- **Real PG18 least-privilege bug caught & fixed:** Initial `FOR UPDATE` on `fulfillment.shipment_lines` failed under least-privilege `athera_app` (which intentionally holds `SELECT, INSERT` only on immutable `shipment_lines`). Replaced with transaction-scoped `pg_advisory_xact_lock(hashtext('billing-shpline'), hashtext(id))`, preserving strict least privilege while deterministically serializing concurrent invoicing (`S14_PG_02` PASS).
- **Immutability & RLS:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 6 tables; `guard_posted_invoice_immutability` (`55000`) and `guard_posted_invoice_line_immutability` (`55000`, plus `42501` for `athera_app`) verified in `S14_PG_01`.
- **Tax & Period Controls (`OD-07` / `OD-09`):** Verified `PPN-OUT-11` (11%) vs `PPN-OUT-EXEMPT` (0% requiring SKB facility ref), closed-period rollback leaving zero orphan AR/GL rows, idempotent retries, and linked `CREDIT_NOTE` compensating postings (`S14_API_01`, `S14_API_02`).

**Verdict:** `PASS` — minimal, high-clarity, zero invariants traded away.
