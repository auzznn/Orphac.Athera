# ATH-S07 Design — Valued Acceptance, Moving-Average Inventory and GRNI

- **Slice:** `S07` ([S07_valued_acceptance_moving_average_inventory_and_grni.md](../../implementation/slices/S07_valued_acceptance_moving_average_inventory_and_grni.md))
- **Requirements:** `BRS-RCV-004..008`, `BRS` §8 (Gate 2), §10 (`TX-RECEIPT`), `UAT-P2P-005..011`, `UAT-P2P-023..025`, `OD-01` (`ATH-CR-003`), `OD-04` (`ATH-CR-004`)
- **Status:** Design complete; awaiting implementation & verification (`Product Owner Acceptance: PENDING`)

---

## 1. Authoritative Ownership & Transaction Boundaries

| Domain Module | Authoritative Tables Owned | Writes Forbidden Outside Owner |
|---|---|---|
| `Procurement` (Coordinator) | `procurement.acceptance_sequences`, `procurement.valued_acceptances` (append-only), `procurement.valued_acceptance_lines` (append-only), controlled updates on `goods_receipts`, `goods_receipt_lines`, `purchase_orders` | Never mutates `inventory.*` or `gl.*` directly without invoking owner services |
| `Inventory` | `inventory.movement_sequences`, `inventory.stock_positions`, `inventory.stock_movements` (append-only), `inventory.movement_lines` (append-only), `inventory.valuation_pools`, `inventory.valuation_entries` (append-only) | Never writes `gl.journal_entries` or `gl.receipt_accruals` |
| `GeneralLedger` | `gl.journal_entries` / `journal_lines` / `journal_sequences`, `gl.receipt_accruals`, `gl.receipt_accrual_lines` (`INVENTORY_ASSET` Dr / `GRNI_CLEARING` Cr) | Never mutates `procurement.*` or `inventory.*` |
| `Integration` & `Audit` | `integration.outbox_messages` (`procurement.receipt.accepted.v1`), `audit.audit_events` (`GOODS_RECEIPT_VALUED_ACCEPTED`) | Financial truth never depends on async outbox dispatch |

---

## 2. State Transitions & `TX-RECEIPT` Flow

1. **Gate 1 Eligibility Precheck:**
   - Only `STOCKED_GOOD` lines on an `APPROVED` PO whose Gate 1 disposition placed base quantity into `DOCK_RELEASED` (`dock_released_base_quantity - valued_accepted_base_quantity > 0`) may be accepted at Gate 2.
   - `ARRIVED`, `DOCK_CHECK_PENDING`, `DOCK_HELD` (`OD-01` excess), and `DOCK_REJECTED` remain blocked (`409 acceptance_exceeds_released_quantity`).
2. **Atomic Lock Ordering Inside `TX-RECEIPT`:**
   - Caller uses `ExecutionStrategy` transaction via `ReceiptAcceptanceCoordinator.AcceptReceiptAsync`.
   - Deterministic `FOR UPDATE` row locks prevent deadlocks under concurrent acceptance:
     1. `procurement.purchase_orders` (`WHERE purchase_order_id = @poId`)
     2. `procurement.goods_receipts` (`WHERE goods_receipt_id = @receiptId`)
     3. Ordered `inventory.valuation_pools` & `inventory.stock_positions` by ascending `(item_id, valuation_group_code)` (`INSERT ... ON CONFLICT DO NOTHING` before `SELECT ... FOR UPDATE` on initial pool/position creation so concurrent first-time SKU arrivals never hit duplicate-key failures)
     4. `gl.financial_periods` inside `GlPostingKernel.PostJournalAsync`
3. **Atomic Writes Before Commit:**
   - Insert immutable `ValuedAcceptance` + `ValuedAcceptanceLine`s (`ACC-YYYY-NNNNN`).
   - Update `GoodsReceiptLine.ValuedAcceptedBaseQuantity`, `UnresolvedCustodyBaseQuantity`, `GoodsReceipt.Status` (`PARTIALLY_ACCEPTED` / `VALUED_ACCEPTED`), `HasFinancialPosting = true`, and `PurchaseOrder.AcceptedBaseQuantityTotal` + `ReceivingProgress` (`PARTIALLY_RECEIVED` / `FULLY_RECEIVED`).
   - Invoke `InventoryValuationService.PostAcceptedReceiptStockAsync`: creates `StockMovement` + `MovementLine`s (`MOV-YYYY-NNNNN`), updates `StockPosition` buckets (`UNRESTRICTED` updates `available = unrestricted - reserved`; `QUALITY_INSPECTION` / `BLOCKED` keep `available` unchanged while increasing `total_valued_on_hand`), updates `ValuationPool` (`newCarrying = prevCarrying + acceptedAmount`, `newAvgCost = Round(newCarrying / newQty, 6, AwayFromZero)`), and appends `ValuationEntry`.
   - Invoke `GlPostingKernel.PostReceiptAccrualAsync`: validates open financial period + active/postable `INVENTORY_ASSET` (`1300`) & `GRNI_CLEARING` (`2110`) mappings, posts balanced `Dr Inventory Asset / Cr GRNI` journal (`SourceDocumentType = "VALUED_ACCEPTANCE"`, `SourceEventKey = $"accept:{idempotencyKey}"`), and inserts `ReceiptAccrual` + `ReceiptAccrualLine`s (`GRNI-YYYY-NNNNN`, `OPEN`).
   - Append `integration.outbox_messages` (`procurement.receipt.accepted.v1`) and `audit.audit_events`.
   - Optional `beforeCommitFaultHook` allows real PostgreSQL rollback verification in integration tests.

---

## 3. Schema & Database Hardening (`M07_ValuedAcceptanceMovingAverageInventoryAndGrni`)

- **New Schema:** `inventory` (`USAGE` granted to `athera_app`, `athera_migrator`).
- **PostgreSQL Forced RLS & Least Privilege (`athera_app`):**
  - `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` across all 11 new tables scoped by `NULLIF(current_setting('app.tenant_id', true), '')::uuid`.
  - Append-only (`SELECT, INSERT` ONLY): `procurement.valued_acceptances`, `procurement.valued_acceptance_lines`, `inventory.stock_movements`, `inventory.movement_lines`, `inventory.valuation_entries`.
  - Controlled update (`SELECT, INSERT, UPDATE` ONLY; zero `DELETE`): `procurement.acceptance_sequences`, `inventory.movement_sequences`, `inventory.stock_positions`, `inventory.valuation_pools`, `gl.receipt_accruals`, `gl.receipt_accrual_lines`.
  - Deferred SQL constraint trigger `procurement.enforce_valued_acceptance_backing()` ensures `goods_receipt_lines.valued_accepted_base_quantity` always equals `SUM(valued_acceptance_lines.accepted_base_quantity)`.
  - `ck_goods_receipts_financial_consistency` & `ck_gr_lines_valued_le_released` replace S06 zero-only checks cleanly.
- **Unique & Idempotency Constraints:** `(tenant_id, legal_entity_id, idempotency_key)` unique on `valued_acceptances` and `(tenant_id, legal_entity_id, acceptance_number)`; `(tenant_id, legal_entity_id, item_id, currency_code, valuation_group_code)` unique on `valuation_pools` and `(tenant_id, legal_entity_id, item_id, valuation_group_code)` unique on `stock_positions`.

---

## 4. API & UI Contracts

- **Permissions:** New `receiving.accept` added to `InventoryClerk` template (`U1`, `U2` on `NUSA-JKT`; withheld on `NUSA-SBY` and `SMDR-BDG`).
- **Endpoints:**
  - `POST /api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}/accept-preview` (`[ReceivingRead, ReceivingAccept]`)
  - `POST /api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{receiptId}/accept` (`[ReceivingAccept]`, CSRF)
  - `GET /api/workspaces/{tenantId}/entities/{entityId}/receiving/acceptances` & `.../acceptances/{acceptanceId}` (`[ReceivingRead, ReceivingAccept, InventoryRead, FinanceRead]`)
  - `GET /api/workspaces/{tenantId}/entities/{entityId}/inventory/overview` (`[InventoryRead]`)
  - `GET /api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{itemId}/ledger` (`[InventoryRead]`)
- **UI Experience (`/receiving` & `/inventory`):**
  - On `/receiving` (`#rcv-detail-sheet`), eligible `DOCK_RELEASED` balances expose a Gate 2 Acceptance & Valuation Preview panel (date, initial stock status `UNRESTRICTED` / `QUALITY_INSPECTION` / `BLOCKED`, line quantities, preview summary showing carrying value + moving-average delta + `Dr 1300 / Cr 2110` preview + blocking reasons, submit button, and completion card with deep links to `/inventory` and `/finance`).
  - Live `/inventory` workspace (`AVAILABLE`) displays KPI totals, stock positions (unrestricted/available vs QC/blocked), selectable SKU moving-average `ValuationEntry` chronological ledger, and open GRNI accruals.

---

## 5. Verification Plan

- **UnitTests (`S07ValuedAcceptanceValuationTests`):** Moving-average math, scale-6 non-terminating rounding, `OD-04` anomalous/zero-value blocks, `StockPosition` availability segregation, and `receiving.accept` role template check.
- **Real PostgreSQL 18 IntegrationTests (`S07ValuedAcceptancePostgresTests`):** `S07_PG_01` (missing context, cross-tenant RLS & FKs), `S07_PG_02` (`M06 <- M07 -> M07`), `S07_PG_03` (append-only `42501`, backing trigger, CHECK constraints), `S07_API_01` (multi-batch moving average & `QUALITY_INSPECTION` availability), `S07_API_02` (`6`x concurrent idempotent replay & `6`x over-capacity race), `S07_API_03` (closed period, missing mapping, fault-injected rollback atomicity, outbox recovery).
- **Frontend & E2E (`s07-valued-acceptance-inventory.{test.tsx,spec.ts}`):** Preview, multi-batch `UNRESTRICTED` + `QUALITY_INSPECTION` acceptance, live moving-average & GRNI ledger inspection on `/inventory`, `/finance` GL verification, `NUSA-SBY` read-only (`receiving.accept` absent), cross-tenant `403`/`404` checks, and `impeccable-finish-reviewer` `ship`.

---

## 6. Owner-Only Decisions

- `OD-04` ([ATH-CR-004-OD04-moving-average-valuation-edge-cases.md](../../decisions/ATH-CR-004-OD04-moving-average-valuation-edge-cases.md)) remains `OPEN` pending human Owner sign-off.
- S07 Deliverable Acceptance (`docs/evidence/S07/README.md`) stays `Product Owner Acceptance: PENDING`.
