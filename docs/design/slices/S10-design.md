# ATH-S10 Design — Supplier Returns, Replacements and Financial Corrections

- **Slice:** `S10` ([S10_supplier_returns_replacements_and_financial_corrections.md](../../implementation/slices/S10_supplier_returns_replacements_and_financial_corrections.md))
- **Requirements:** `BRS Section 13 & 14`, `DEC-P2P-005`, `DEC-P2P-012`, `BRS-RCV-001/007`, `UAT-P2P-021..024`, `ATH-DOMAIN-BOUNDARIES-001`, `OD-02` (`ATH-CR-001`), `OD-04` (`ATH-CR-004`), `OD-05`/`OD-07` (`ATH-CR-007`)
- **Status:** Design complete; awaiting implementation & verification (`Product Owner Acceptance: PENDING`)

---

## 1. Authoritative Module Ownership & Schema Boundaries

| Domain Module | Authoritative Tables Owned (`M10`) | Boundary Invariants |
|---|---|---|
| `Procurement` (`procurement` schema) | `procurement.return_sequences`, `procurement.replacement_sequences`, `procurement.supplier_returns`, `procurement.supplier_return_lines`, `procurement.replacement_authorizations`, plus nullable `replacement_authorization_id` on `procurement.goods_receipts` | Owns commercial return claims (`M1`–`M7` matrix evaluation), four-eyes approval (`OD-02`), linked `ReplacementAuthorization` (`CommercialIncrementalUnitPrice = 0`, `AuthorizedInventoryUnitCostBase > 0`), and PO/GR capacity conservation so ordinary + replacement deliveries cannot double-receive or double-bill (`no free extra quantity`). |
| `AccountsPayable` (`ap` schema) | `ap.credit_sequences`, `ap.supplier_credits` (append-only header), `ap.supplier_credit_lines` (append-only lines), plus `credited_amount` & `PayableStatuses.Credited` on `ap.payable_items` | Owns append-only credit memos (`SCN-YYYY-NNNNN`) for `M3` (`VALUED_BILLED_RETURN` + `AP_CREDIT_NOTE` on unpaid bills) reducing `PayableItem.RemainingOpenAmount` while keeping `ap.supplier_bills` and `ap.supplier_bill_lines` untouched/immutable. |
| `Inventory` (`inventory` schema) | `StockMovementTypes.PurchaseReturnOutbound`, `ValuationEntryTypes.PurchaseReturnOutbound`, `StockPosition.ApplyPurchaseReturnOutbound`, `ValuationPool.ApplyPurchaseReturn` | Executes outbound physical & valuation reversals (`PURCHASE_RETURN_OUTBOUND`) from unreserved `UNRESTRICTED` or `BLOCKED` balances (`OD-10`), enforces `OD-04` valuation floor (`newCarrying > 0` when `newQty > 0`), and values linked Gate 2 replacement acceptances at `AuthorizedInventoryUnitCostBase` (> 0). |
| `GeneralLedger` (`gl` schema) | `returned_amount` on `gl.receipt_accruals`, `returned_base_quantity` + `returned_line_amount` on `gl.receipt_accrual_lines`, immutable reversing/correcting `JournalEntry`/`JournalLine` | Reverses unbilled GRNI (`Dr 2110` ± `5200 PPV`) on `M2` while conserving `Accepted = Billed + Returned + RemainingUnbilled` so returned quantity can never be billed later; posts balanced `M3` credit memos (`Dr 2100 AP / Cr 1300 Inv / Cr 1410 PPN / ±5200 PPV`) strictly in `OPEN` periods. |
| `Tax` (`tax` schema) | `TaxRoles.PpnInputReversal` (`"PPN_INPUT_REVERSAL"`) in append-only `tax.tax_determinations` | Records `Nota Retur` PPN input tax reversal snapshot (`OD-07`) with counterparty NPWP & `TaxReturnNoteNumber`; blocks withholding (`PPh 23`) auto-reversal (`M6`) until Owner/tax policy signed. |

---

## 2. Explicit Support Matrix (`M1`–`M7`), State Machines & Conservation

### Supported vs Blocked Matrix (`ATH-CR-007`)
1. **`M1` (`UNVALUED_DOCK_REJECTION` + `CLOSE_WITHOUT_REPLACEMENT | REPLACEMENT_ONLY`):**
   - Eligible quantity per GR line: `GoodsReceiptLine.DockRejectedBaseQuantity - AlreadyReturnedUnvaluedQty`.
   - Zero GL, zero inventory movement, zero GRNI clearing; if `REPLACEMENT_ONLY`, issues `ReplacementAuthorization` (`CommercialIncrementalUnitPrice = 0`, `AuthorizedInventoryUnitCostBase = ApprovedPoUnitCostBase`).
2. **`M2` (`VALUED_UNBILLED_RETURN` + `CLOSE_WITHOUT_REPLACEMENT | REPLACEMENT_ONLY`, `OPEN` period):**
   - Eligible quantity per `ReceiptAccrualLine`: `min(RemainingUnbilledBaseQty, EligibleLocationBalanceAvailableOrBlocked)`.
   - Four-eyes approval (`OD-02`, `submitter != approver`) -> atomic savepoint execution: outbound `PURCHASE_RETURN_OUTBOUND` movement (`Cr 1300`), `ApplyUnbilledReturn` on `ReceiptAccrualLine`/`ReceiptAccrual` (`Dr 2110`), `5200 PPV` delta if pool MAC != receipt unit cost, `PurchaseOrder.ReverseAcceptedQuantityForReturn`, plus `ReplacementAuthorization` if `REPLACEMENT_ONLY`.
3. **`M3` (`VALUED_BILLED_RETURN` + `AP_CREDIT_NOTE`, unpaid bill, `WithholdingTaxAmount == 0`, `OPEN` period):**
   - Eligible quantity per `SupplierBillLine`: `BilledBaseQuantity - AlreadyCreditedQty`; requires `ap.approve` independent decision (`OD-02`) and `TaxReturnNoteNumber` (`Nota Retur`) whenever `PpnTaxAmount > 0`.
   - Atomically posts `PURCHASE_RETURN_OUTBOUND` (`Cr 1300`), `ap.SupplierCredit` + lines (`SCN-YYYY-NNNNN`), `PayableItem.ApplySupplierCredit` (`Dr 2100`), `TaxDetermination` `PPN_INPUT_REVERSAL` (`Cr 1410`), and `Dr/Cr 5200 PPV` for MAC delta.
4. **Explicit Fail-Closed Blocks:**
   - **`M4` (`VALUED_BILLED_RETURN` + `REPLACEMENT_ONLY`):** `409 od05_billed_replacement_policy_blocked` on submit/execute.
   - **`M5` (`PayableItem.PaidAmount > 0`):** `409 od05_paid_bill_return_policy_blocked` on submit/execute.
   - **`M6` (`SupplierBillLine.WithholdingTaxAmount > 0`):** `409 od07_withholding_return_reversal_blocked` on submit/execute.
   - **`M7` (`CLOSED`/`LOCKED` period on `M2`/`M3`):** `409 period_closed` on execute.

### State Transitions & Capacity Conservation
- **`SupplierReturn`:** `DRAFT -> PENDING_APPROVAL -> APPROVED -> EXECUTED` (or `REJECTED` returning `DRAFT` with `LastRejectionReason`, or `CANCELLED` pre-execution). Executed returns are immutable (`M10` DB trigger).
- **`ReplacementAuthorization`:** `ACTIVE -> PARTIALLY_RECEIVED -> FULFILLED` (or `CANCELLED` if zero delivered). Quantity conservation: `AuthorizedBaseQuantity = DeliveredBaseQuantity + RemainingBaseQuantity`; ordinary PO receiving subtracts active `ReplacementAuthorization.RemainingBaseQuantity` (and credits back returned valued-unbilled capacity net of replacement) so total received/accepted never exceeds approved entitlement (`409 replacement_entitlement_exceeded`).

---

## 3. Database & Security Design (`M10_SupplierReturnsReplacementsAndFinancialCorrections`)

- **New Tables (8):** `procurement.return_sequences`, `procurement.replacement_sequences`, `procurement.supplier_returns`, `procurement.supplier_return_lines`, `procurement.replacement_authorizations`, `ap.credit_sequences`, `ap.supplier_credits`, `ap.supplier_credit_lines`.
- **PostgreSQL 18 RLS & Least Privilege (`athera_app`):**
  - `ENABLE` + `FORCE ROW LEVEL SECURITY` scoped by `tenant_id = platform.current_tenant_id()` across all 8 tables; composite `(tenant_id, legal_entity_id, ...)` foreign keys.
  - Append-only (`SELECT, INSERT` only for `athera_app`): `ap.supplier_credits`, `ap.supplier_credit_lines`, plus DB immutability triggers `trg_procurement_supplier_returns_immutability` (locks `EXECUTED` rows) and `trg_ap_supplier_credits_immutability` (denies `UPDATE`/`DELETE`).
  - Migration `Down()` removes triggers/functions, cleans S10-specific movement/valuation/tax rows if reverting cleanly, restores M09 check constraints, drops added S10 columns, and drops the 8 S10 tables (`S10_PG_02`).

---

## 4. API & Impeccable Next.js Experience (`/w/[tenantId]/[entityId]/returns`)

- **API Endpoints (`ReturnsEndpoints`):** `/returns/overview`, `/returns/{returnId}`, `/returns/preview`, `/returns` (`POST`/`PUT`), `/returns/{returnId}/submit`, `/returns/{returnId}/decision`, `/returns/{returnId}/execute`, and `/returns/replacements/{authorizationId}/receive`.
- **Permissions:** Read via any of `procurement.* | receiving.* | inventory.* | ap.*`; Draft/Submit `M1`: `receiving.manage | procurement.manage`; Draft/Submit `M2`/`M3`: `inventory.manage | ap.manage`; Approve: `procurement.approve` (`M1`/`M2`) or `ap.approve` (`M3`) + `approval.decide` (`OD-02` `submitter != approver`); Execute: `receiving.manage` (`M1`), `inventory.manage | receiving.accept` (`M2`), or `ap.post` (`M3`).
- **Impeccable UI (`craft-floor.md` Compliant):** Flat archival paper surfaces (`#f6f5f0`, `#ffffff`, `1px` hairlines), `RotateCcw` sidebar entry, `M1`–`M7` governance banner, 4-metric strip, queued source tables (Dock Rejected / Valued Unbilled / Valued Billed Unpaid), live return composer with GL/stock/credit/replacement preview, keyed `SupplierReturnDetailSheet` with lineage badges (`PO -> GRN -> Return -> RPA / SCN / JRN`), linked Replacement Receipt modal, instant tabs, `min-h-9` targets, `md:hidden` mobile labels, and bilingual `id-ID`/`en-US` copy.
- **UI Cross-Slice Continuity:** `/receiving` displays linked `RPA-...` badge on replacement receipts; `/inventory` displays `-Q` on `PURCHASE_RETURN_OUTBOUND` movements; `/payables` displays `Credited` amounts (`SCN`) on payables.

---

## 5. Verification Plan & Owner Decisions

- **UnitTests (`S10SupplierReturnMatrixUnitTests`):** `M1`–`M7` matrix outcomes, zero commercial price / positive inventory cost guard on `ReplacementAuthorization`, `ValuationPool.ApplyPurchaseReturn` (partial, full drain to zero, `OD-04` floor), `ReceiptAccrualLine.ApplyUnbilledReturn`, and `PayableItem.ApplySupplierCredit`.
- **PostgreSQL 18 IntegrationTests (`S10SupplierReturnsPostgresTests`):** `S10_PG_01` (forced RLS, missing context denial, append-only `ap.supplier_credits`, `EXECUTED`/`POSTED` triggers), `S10_PG_02` (`M10 -> M09 -> M10` round-trip), `S10_API_01` (`M1` zero GL + linked replacement Gate 2 positive valuation + non-duplication on PO receiving, `M2` compensation + duplicate block, `M3` credit note + `Nota Retur` PPN reversal + duplicate block, `M4`–`M7` blocks, savepoint rollback), `S10_API_02` (concurrent `Task.WhenAll` replacement arrivals + concurrent returns + idempotent replay), `S10_API_03` (`OD-02` SoD, RBAC, `NUSA-SBY` read-only, `SMDR` 404, audit + outbox).
- **Frontend & E2E (`s10-supplier-returns-credits.test.tsx`, `s10-supplier-returns-credits.spec.ts`, `Impeccable S10 Finish Reviewer`):** Full `S00`–`S10` regression, `01`–`06` desktop/mobile screenshots, `impeccable detect --json` = `[]`, and `disposition: ship`.
- **Owner-Only Decisions:** `OD-05` & `OD-07` (`ATH-CR-007`), `OD-02` (`ATH-CR-001`), `OD-04` (`ATH-CR-004`) stay `OPEN`; S10 stays `Product Owner Acceptance: PENDING`.
