# ATH-S09 Design — Supplier Invoice Three-Way Match, AP and Price Difference

- **Slice:** `S09` ([S09_supplier_invoice_three_way_match_ap_and_price_difference.md](../../implementation/slices/S09_supplier_invoice_three_way_match_ap_and_price_difference.md))
- **Requirements:** `BRS-AP`, `DEC-P2P-006`, `DEC-P2P-012`, `DEC-P2P-013`, `DEC-P2P-016`, `UAT-P2P-012..017`, `ATH-DOMAIN-BOUNDARIES-001`, `OD-02` (`ATH-CR-001`), `OD-03`/`OD-08` (`ATH-CR-006`), `OD-04` (`ATH-CR-004`), `OD-07` (`ATH-CR-002`)
- **Status:** Design complete; awaiting implementation & verification (`Product Owner Acceptance: PENDING`)

---

## 1. Authoritative Module Ownership & Schema Boundaries

| Domain Module | Authoritative Tables Owned | Boundary Invariants |
|---|---|---|
| `AccountsPayable` (`ap` schema) | `ap.bill_sequences`, `ap.payable_sequences`, `ap.invoice_match_policies`, `ap.supplier_bills`, `ap.supplier_bill_lines`, `ap.invoice_match_evaluations` (append-only), `ap.supplier_bill_receipt_matches` (append-only), `ap.payable_items` | Owns PO-backed supplier bill drafts, independent `PP`/`DQ`/`BD` match evaluation history, bill-to-accrual match capacity links, AP trade payables, and payment block/release state (`PAYMENT_BLOCKED` / `RELEASED`). Rejects `NON_PO_EXPENSE` / `SUPPLIER_ADVANCE` (`OD-08`). Never writes GL or stock directly outside `SupplierBillCoordinator`. |
| `GeneralLedger` (`gl` schema) | `gl.receipt_accrual_clearings` (append-only), updates to `gl.receipt_accruals` & `gl.receipt_accrual_lines`, immutable `gl.journal_entries` & `gl.journal_lines` | Tracks billed vs unbilled GRNI quantity/amount capacity atomically when clearing GRNI upon `TX-BILL`; forbids clearing above `RemainingUnbilledBaseQuantity` or `RemainingOpenLineAmount`. |
| `Inventory` (`inventory` schema) | `inventory.invoice_price_adjustments` (append-only), nullable `stock_movement_id`/`movement_line_id` on `inventory.valuation_entries`, `StockMovementTypes.OperationalIssue`, updated `ValuationPool` & `StockPosition` | Owns operational consumption (`OPERATIONAL_ISSUE`: `Dr COGS 5100 / Cr Inventory Asset 1300`) and `DEC-P2P-013` stock-coverage weighted-average valuation adjustments (`ApplyInvoicePriceAdjustment`) without mutating physical quantities. Enforces `OD-04` valuation floor (`newCarrying > 0` when `valuedQty > 0`). |
| `Tax` (`tax` schema) | `tax.tax_determinations` (append-only) | Persists effective-dated line-level PPN (`INPUT`) and optional PPh 23 (`WITHHOLDING`) calculation snapshots, DPP ratios (`11/12` etc.), NPWP checks, and documentary evidence metadata (`Faktur Pajak` / `Bukti Potong`) with explicit `OD-07` disclaimer. |
| `Identity` (`identity` schema) | `PermissionCodes` (`ap.read`, `ap.manage`, `ap.approve`, `ap.post`), `RoleTemplates.ApClerk` | Enforces separation of duties: preparer (`ap.manage` — `U4 siti.rahayu`), independent approver (`ap.approve`, `submitter != approver` — `U2 budi.santoso`), poster (`ap.post`), and `U1` (`rina.utami`) denied `ap.*` (`403`). |

---

## 2. State Machine, Match Calculation & Transaction Flow (`TX-BILL`)

1. **Bill Document States:** `DRAFT -> PENDING_APPROVAL -> APPROVED -> POSTED` (plus `CANCELLED` pre-post and `REJECTED` on approval decision returning `Status = DRAFT` with recorded `LastRejectionReason` so AP clerk can remediate and resubmit).
2. **Independent `PP` / `DQ` / `BD` Three-Way Evaluation (`InvoiceThreeWayMatchCalculator`):**
   - **`DQ` (Quantity Check against Eligible Accepted GRNI Capacity):** `InvoicedBaseQty <= ReceiptAccrualLine.RemainingUnbilledBaseQuantity` is `MATCHED` (allows partial billing while blocking unaccepted/duplicate billing); any excess triggers **`POSTING_BLOCKED`** (`dq_exceeds_unbilled_accepted_capacity`).
   - **`PP` (Unit Price vs Approved PO/GRNI Receipt Cost):** `PriceVarianceAmount = LineNetAmount - GrniClearAmount`. Exact zero -> `MATCHED`; `REVIEWED_INTERNAL` within `PpTolerancePercent`/`PpToleranceAmount` -> `WITHIN_TOLERANCE`; otherwise -> **`APPROVAL_REQUIRED`** (cannot post without independent four-eyes `ApprovePriceVariance = true`).
   - **`BD` (Header Small Difference):** `BdDifference = DeclaredGrossAmount - (LinesNetSubtotal + PpnTaxTotal)`. Zero -> `MATCHED`; `REVIEWED_INTERNAL` with configured `BdWriteOffAccountId` within limit -> `WITHIN_TOLERANCE`; otherwise -> **`POSTING_BLOCKED`** (`bd_header_difference_blocked`).
3. **`DEC-P2P-013` Stock-Coverage Apportionment & Balanced Journal (`TX-BILL`):**
   - For each bill line with `PriceVarianceAmount != 0`:
     - `CoveredQty = Min(ValuationPool.ValuedBaseQuantity, InvoicedBaseQty)`, `UncoveredQty = InvoicedBaseQty - CoveredQty`.
     - `InventoryDelta = CoveredQty == 0 ? 0 : CoveredQty == InvoicedBaseQty ? PriceVarianceAmount : Round(PriceVarianceAmount * CoveredQty / InvoicedBaseQty, 4, AwayFromZero)`.
     - `PnlDelta = PriceVarianceAmount - InventoryDelta`.
     - Positive `InventoryDelta` posts `Dr 1300 Inventory Asset`; negative `InventoryDelta` posts `Cr 1300 Inventory Asset` (guarded by `OD-04` floor `Carrying + InventoryDelta > 0` when `ValuedQty > 0`).
     - Positive `PnlDelta` posts `Dr 5200 Purchase Price Variance`; negative `PnlDelta` posts `Cr 5200 Purchase Price Variance`.
   - Balanced Journal Entries:
     - `Dr 2110 GRNI Clearing` (`SUM ClearAmount`)
     - `Dr/Cr 1300 Inventory Asset` (`SUM InventoryDelta`), `Dr/Cr 5200 PPV` (`SUM PnlDelta`), `Dr/Cr BD Write-Off` (when reviewed & non-zero)
     - `Dr 1410 PPN Input Tax` (`SUM PPN > 0`)
     - `Cr 2220 PPh Withholding Payable` (`SUM PPh23 > 0`)
     - `Cr 2100 AP Trade Control` (`NetPayableAmount = GrossInvoiceTotal - WithholdingTaxTotal`)
4. **Concurrency & Savepoint Rollback (`TX-BILL`):**
   - Deterministic `FOR UPDATE` row lock order inside `TenantScopeExecutor.ExecuteInEntityAsync`: `SupplierBill` -> `PurchaseOrder` -> ordered `ReceiptAccrual` & `ReceiptAccrualLine` -> ordered `ValuationPool` -> `FinancialPeriod` (via `GlPostingKernel`).
   - Idempotent commit via `ux_supplier_bills_post_idempotency`; savepoint `sp_post_supplier_bill` + test fault hook guarantees zero partial subledger/GL/AP/outbox state on failure.
5. **Payment Block Control:** Posting initializes `PayableItem` (`AP-YYYY-NNNNN`, `Status = OPEN`, `PaymentBlockStatus = NONE` or `PAYMENT_BLOCKED` if flagged during approval/policy), supporting audited `PAYMENT_BLOCKED <-> RELEASED` transitions (`ap.approve` or `ap.post`) prior to S11 settlement.

---

## 3. Database & Security Design (`M09_SupplierInvoiceThreeWayMatchApAndPriceDifference`)

- **New Schema:** `ap` (`USAGE` granted to `athera_app`, `athera_migrator`).
- **PostgreSQL 18 RLS & Least Privilege (`athera_app`):**
  - All 11 new tables enforce `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `tenant_id = platform.current_tenant_id()` with composite `(tenant_id, legal_entity_id, ...)` foreign keys.
  - Append-only (`SELECT, INSERT` only for `athera_app`): `ap.invoice_match_evaluations`, `ap.supplier_bill_receipt_matches`, `gl.receipt_accrual_clearings`, `inventory.invoice_price_adjustments`, `tax.tax_determinations`.
  - Posted-bill immutability trigger `ap.enforce_posted_supplier_bill_immutability()`: rejects `UPDATE` of commercial columns or `DELETE` on `ap.supplier_bill_lines` / `ap.supplier_bills` once `Status = 'POSTED'` (allowing only `PaymentBlockStatus`/`PaidAmount`/version updates on `ap.supplier_bills` and `ap.payable_items`).
  - Constraint upgrades on existing tables: `ck_stock_movements_type` (+ `'OPERATIONAL_ISSUE'`), `ck_movement_lines_positive` (allow negative `base_quantity_delta` & `line_valuation_amount` when outbound), nullable `stock_movement_id`/`movement_line_id` on `inventory.valuation_entries`, updated `ck_valuation_entries_type` (+ `'OPERATIONAL_ISSUE'`, `'INVOICE_PRICE_ADJUSTMENT'`), and updated `ck_valuation_entries_values` (supporting negative issue deltas & zero-qty price adjustments). `M09.Down()` reverses cleanly (`S09_PG_02`).

---

## 4. API & UI Contracts

- **Backend Endpoints (`PayablesEndpoints`, `InventoryEndpoints`, `WorkspaceEndpoints`):**
  - `POST /api/workspaces/{tenantId}/entities/{entityId}/inventory/issues` (`inventory.manage`) — records `OPERATIONAL_ISSUE` (`Dr 5100 COGS / Cr 1300 Inventory`) so live entities can consume stock prior to late supplier billing.
  - `GET /api/workspaces/{tenantId}/entities/{entityId}/payables/overview` (`ap.read | ap.manage | ap.approve | ap.post`) — returns KPI summary, bills, open payables, eligible unbilled `ReceiptAccrualLine` capacities, active `InvoiceMatchPolicy`, tax codes, and recent match/adjustment events.
  - `GET /api/.../payables/bills/{billId}` — full header, line breakdown, latest `InvoiceMatchEvaluation`, `SupplierBillReceiptMatch`es, `TaxDetermination`s, `InvoicePriceAdjustment`s, and GL journal linkage.
  - `POST /api/.../payables/bills/preview` (`ap.read | ap.manage | ap.approve | ap.post`) — stateless PO vs Receipt vs Invoice three-way comparison + coverage split + tax + GL preview.
  - `POST /api/.../payables/bills` & `PUT /api/.../payables/bills/{billId}` (`ap.manage`) — create/update draft bill.
  - `POST /api/.../payables/bills/{billId}/submit` (`ap.manage`) — run three-way match and submit into four-eyes approval (`409` on `POSTING_BLOCKED`).
  - `POST /api/.../payables/bills/{billId}/decision` (`ap.approve`) — independent approval/rejection (requires `ApprovePriceVariance = true` when `PP` variance exists).
  - `POST /api/.../payables/bills/{billId}/post` (`ap.post`) — atomic `TX-BILL` commit (clears GRNI, adjusts stock pool & PPV, records tax & AP payable, emits outbox).
  - `POST /api/.../payables/bills/{billId}/payment-block` (`ap.approve | ap.post`) — block/release payment with audit reason.
  - `PUT /api/.../payables/match-policy` (`ap.approve` + `OD-03` guard rejecting unreviewed non-zero tolerances with `409`).
- **Next.js Impeccable Experience (`/w/[tenantId]/[entityId]/payables` + `/inventory` update):**
  - Calm flat archival paper architecture (`divide-y`, `border-t`, `gap-px bg-[var(--border-hairline)]` cells, zero nested bordered cards, `min-h-9` targets, structured locale-reactive status banners, `.font-code tabular-nums` figures, desktop `1280x800` & `390x844` mobile stacking).
  - Side-by-side PO vs Accepted Receipt/GRNI vs Supplier Invoice comparison grid highlighting exact `PP`/`DQ`/`BD` discrepancies, covered vs uncovered `DEC-P2P-013` valuation split preview, `Faktur Pajak`/`Bukti Potong` tax metadata, balanced GL preview, and explicit `PAYMENT_BLOCKED` / four-eyes badges with `1.75px` Lucide icons.

---

## 5. Verification Plan & Owner Decisions

- **UnitTests (`S09SupplierInvoiceMatchValuationTests`):** Exact vs `REVIEWED_INTERNAL` `PP`/`DQ`/`BD` tolerances, `100 -> 60 consumed -> 40 remaining` positive (`50k -> 52k`) & negative (`50k -> 48k`) price adjustments, `0`-coverage (100% PPV), `100%`-coverage, `OD-04` valuation floor breach, PPN `11/12` + PPh 23 NPWP rules, and `OD-08` non-PO rejection.
- **PostgreSQL 18 IntegrationTests (`S09SupplierInvoicePostgresTests`):** `S09_PG_01` (forced RLS, missing tenant context denial, append-only denials, posted-bill trigger immutability), `S09_PG_02` (`M09 -> M08 -> M09` round-trip), `S09_API_01` (full 100/60/40 apportionment, partial bills `60 + 40`, `0` coverage, `REVIEWED_INTERNAL` tolerance & `BD` write-off boundary, closed period `2026-10`, expired/NPWP tax block, duplicate invoice block, `OD-08` block, savepoint fault rollback), `S09_API_02` (concurrent `Task.WhenAll` race on same unbilled GRNI line + idempotent replay), `S09_API_03` (`OD-02` self-approval block, RBAC separation, `PAYMENT_BLOCKED -> RELEASED` audit, cross-tenant/entity `403`/`404`).
- **Frontend & E2E (`s09-supplier-bills-ap.test.tsx`, `s09-supplier-bills-ap.spec.ts`, `Impeccable S09 Finish Reviewer`):** Full `s00`–`s09` verification, `/payables` `U4`/`U2` 3-way match + price adjustment + AP + `U2` read-only `NUSA-SBY` + `U1` `403`/`404`, bilingual `id-ID`/`en-US`, desktop & `390x844` screenshots, `impeccable detect --json` = `[]`, and `disposition: ship`.
- **Owner-Only Decisions:** `OD-03` & `OD-08` ([ATH-CR-006](../decisions/ATH-CR-006-OD03-OD08-invoice-match-tolerances-and-ap-scope.md)), `OD-04` (`ATH-CR-004`), and `OD-07` (`ATH-CR-002`) stay `OPEN`; S09 evidence stays `Product Owner Acceptance: PENDING`.
