# ATH-S13 — Ponytail Engineering Minimalism & Invariant Review (`full`)

**Slice:** S13 — Sales Reservation, Pick and Dispatch  
**Skill Level:** `ponytail` (`full`)  
**Verdict:** `PASS`

---

## 1. Scope & Minimalism Assessment

- **Focused Domain & Coordinator Ownership (`Orphac.Athera.Modules.Fulfillment`)**:
  - Implemented in two cohesive files (`FulfillmentModels.cs` and `SalesFulfillmentCoordinator.cs`) without over-engineered WMS routing, wave-planning engines, or external carrier SDK bloat.
  - Directly coordinates existing authoritative primitives across `Inventory` (`StockReservation.Consume`, `StockLocationBalance.ConsumeReservedOnHand`, `StockPosition.ConsumeReservedOutboundIssue`, `ValuationPool.ApplyOperationalIssue`, `StockMovement`, `ValuationEntry`), `Sales` (`SalesOrder.ApplyFulfillmentCounters`, `SalesOrderLine.ApplyFulfillmentDelta`, `SalesFulfillmentProgressStatuses.Derive`), and `GeneralLedger` (`GlPostingKernel.PostJournalAsync` for `Dr 5100 COGS_DEFAULT / Cr 1300 INVENTORY_ASSET`).

---

## 2. Invariant Verification

| Invariant | Implementation & Test Proof |
|---|---|
| **1 & 2. Tenant/Entity Isolation & PostgreSQL RLS** | All 6 `fulfillment.*` tables (`pick_sequences`, `shipment_sequences`, `pick_orders`, `pick_lines`, `shipments`, `shipment_lines`) enforce composite `(tenant_id, legal_entity_id)` foreign keys and `ENABLE` + `FORCE ROW LEVEL SECURITY` using `platform.current_tenant_id()`. Verified in `S13_PG_01` and `S13_API_03`. |
| **3. Server-Resolved Permissions** | Read endpoints require `inventory.read`, `inventory.manage`, `sales.read`, `sales.manage`, or `sales.approve`; all state-changing operations (reserve, release, pick, dispatch, delivery confirm) require `inventory.manage` resolved server-side before setting SQL tenant context. |
| **4. Decimal-Safe Arithmetic** | All quantities (`numeric(18,6)`), unit costs (`numeric(18,6)`), and COGS amounts (`numeric(18,2)`) use `decimal` with `MidpointRounding.AwayFromZero`. |
| **5, 6 & 7. Atomic Weighted-Average Stock Issue, Balanced GL COGS & Immutability** | Outbound shipment dispatch atomically consumes the active reservation, decrements `OnHandBaseQuantity` and `ReservedBaseQuantity` in lockstep (keeping `AvailableBaseQuantity` conserved), deducts the moving-average `ValuationPool`, appends `StockMovement` (`SALES_DISPATCH_OUTBOUND`) + `ValuationEntry`, posts balanced GL `Dr 5100 / Cr 1300`, and records outbox/audit events. PostgreSQL triggers `fulfillment.guard_dispatched_shipment_immutability()` and `fulfillment.guard_dispatched_shipment_line_immutability()` block any mutation or deletion of dispatched quantity/cost/ledger fields (`S13_PG_01`, `S13_API_02`). |
| **OD-10 No Negative or Restricted Stock & Concurrency Serialization** | Reserving from `QUALITY_INSPECTION` (`LOC-QC`), `BLOCKED` (`LOC-BLK`), or expired lots is rejected (`409 Conflict`). Competing concurrent reservations serialize via PostgreSQL `FOR UPDATE` locks (`S13_PG_02`, `S13_API_01`). |
| **OD-09 Commitment & Cancellation Guard** | Sales orders with active reservations or dispatched shipments cannot be cancelled (`409 so_has_downstream_fulfillment`); releasing unpicked reservations restores availability and unblocks cancellation (`S13_API_03`). |
| **12. No Industry-Specific Foundry/Manufacturing Entities** | Zero foundry/casting/manufacturing fields exist in `fulfillment.*` schema or contracts. |
