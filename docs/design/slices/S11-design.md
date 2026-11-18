# ATH-S11 Design — Supplier Settlement, Outbound Clearing and Bank Reconciliation

- **Slice:** `S11` ([S11_supplier_settlement_outbound_clearing_and_bank_reconciliation.md](../../implementation/slices/S11_supplier_settlement_outbound_clearing_and_bank_reconciliation.md))
- **Requirements:** `BRS Section 15` (`Two-Stage Bank Clearing`), `UAT-P2P-018..020`, `UAT-P2P-023..025`, `ATH-DOMAIN-BOUNDARIES-001`, `OD-02` (`ATH-CR-001`), `OD-06` (`ATH-CR-008`), `OD-13` (`ATH-CR-002`)
- **Status:** Design complete; awaiting implementation & verification (`Product Owner Acceptance: PENDING`)

---

## 1. Authoritative Module Ownership & Schema Boundaries

| Domain Module | Authoritative Tables Owned (`M11`) | Boundary Invariants |
|---|---|---|
| `AccountsPayable` (`ap` schema) | `ap.payment_sequences`, `ap.supplier_payments`, `ap.payment_allocations`, plus settlement updates on `ap.payable_items` | Owns supplier payable settlement state (`OPEN -> PARTIALLY_PAID -> PAID`), multi-payable allocation lines (`AllocatedAmount <= RemainingOpenAmount - CommittedPending`), `PAYMENT_BLOCKED` guard, four-eyes `OD-02` release metadata, and restoration on bank rejection (`BANK_REJECTED`). |
| `Cash` (`cash` schema — new in `M11`) | `cash.bank_accounts`, `cash.instruction_sequences`, `cash.statement_sequences`, `cash.match_sequences`, `cash.payment_instructions`, `cash.bank_statements`, `cash.bank_statement_lines`, `cash.bank_payment_matches` | Owns entity bank master accounts (`1110` Cash & Bank Operating, `1120` Bank Outbound Clearing), append-only non-live payment file simulations (`SimulationOnly = true`), imported bank statement staging + fingerprint deduplication, and idempotent bank reconciliation / reversal matches (`POSTED -> REVERSED`). |
| `GeneralLedger` (`gl` schema) | Balanced `gl.JournalEntry` / `gl.JournalLine` via `GlPostingKernel` | Release posts `Dr 2100 AP Trade Control / Cr 1120 Bank Outbound Clearing`; Bank Reconciliation posts `Dr 1120 Bank Outbound Clearing / Cr 1110 Cash & Bank Operating`; Rejections/reversals post linked `JournalEntryKinds.Reversal` in `OPEN` periods only. |
| `Approval` / `Audit` / `Integration` | `SUPPLIER_PAYMENT_RELEASE` policy, `ApprovalCase`/`ApprovalDecision`, `AuditEvent`, `OutboxMessage` | Enforces `OD-02` (`submitter != releaser`, `preparer != releaser`), records immutable audit events and outbox domain notifications (`cash.supplier_payment.released.v1`, `cash.bank_payment.reconciled.v1`, etc.) in the same PostgreSQL transaction. |

---

## 2. Two-Stage Outbound Clearing State Machines & Conservation

### `SupplierPayment` Lifecycle
- **`DRAFT`** (mutable header/allocations; generate simulated `PaymentInstruction` with **0 GL / 0 AP impact**)
- **`PENDING_RELEASE`** (submitted for four-eyes release; still **0 GL / 0 AP impact**)
- **`RELEASED`** (four-eyes `ReleasePaymentAsync` with non-empty `ReleaseReference` -> posts `Dr 2100 / Cr 1120`, assigns `PAY-YYYY-NNNNN`, reduces `PayableItem.RemainingOpenAmount`, sets `RemainingClearingAmount = TotalAllocatedAmount`)
- **`PARTIALLY_RECONCILED`** / **`RECONCILED`** (`ReconcileStatementLineAsync` -> posts `Dr 1120 / Cr 1110`, conserves `TotalAllocatedAmount == ReconciledAmount + RemainingClearingAmount`)
- **`BANK_REJECTED`** (only allowed when `Status == RELEASED && ReconciledAmount == 0m`; posts `Dr 1120 / Cr 2100`, calls `PayableItem.ReversePaymentSettlement`)
- **`CANCELLED`** (allowed before release only; releases pending allocation reservation)

### Conservation Invariants (Database & Domain)
1. **`PayableItem`:** `NetPayableAmount == PaidAmount + CreditedAmount + RemainingOpenAmount` (`PaidAmount >= 0`, `RemainingOpenAmount >= 0`).
2. **`SupplierPayment`:** When released/reconciled, `TotalAllocatedAmount == ReconciledAmount + RemainingClearingAmount`.
3. **`BankStatementLine`:** `Amount == MatchedAmount + UnmatchedAmount` (`MatchedAmount >= 0`, `UnmatchedAmount >= 0`).
4. **Live Outbound Clearing Reconciliation Proof:** `SUM(gl.journal_lines on 1120 Credit - Debit for entity) == SUM(ap.supplier_payments.remaining_clearing_amount where status IN ('RELEASED','PARTIALLY_RECONCILED'))`.
5. **Anti-Double-Allocation & Anti-Duplicate-Statement Guard:**
   - `PaymentAllocation` creation locks target `PayableItem` rows `FOR UPDATE` and caps allocation by `RemainingOpenAmount - SUM(other active DRAFT/PENDING_RELEASE allocations)`.
   - `BankStatement` enforces unique `(tenant_id, legal_entity_id, bank_account_id, statement_reference)` and `BankStatementLine` enforces unique `(tenant_id, legal_entity_id, bank_account_id, line_fingerprint_sha256)`.

---

## 3. Database & Security Design (`M11_SupplierSettlementOutboundClearingAndBankReconciliation`)

- **New Schema & Tables (11 tables):**
  - `ap`: `ap.payment_sequences`, `ap.supplier_payments`, `ap.payment_allocations`.
  - `cash`: `cash.bank_accounts`, `cash.instruction_sequences`, `cash.statement_sequences`, `cash.match_sequences`, `cash.payment_instructions`, `cash.bank_statements`, `cash.bank_statement_lines`, `cash.bank_payment_matches`.
- **PostgreSQL 18 RLS & Least Privilege (`athera_app`):**
  - `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` scoped by `tenant_id = platform.current_tenant_id()` on all 11 tables.
  - Append-only (`GRANT SELECT, INSERT ON cash.payment_instructions TO athera_app`) + DB immutability trigger `trg_cash_payment_instructions_immutability`.
  - Controlled-state DB triggers on `ap.supplier_payments`, `ap.payment_allocations`, and `cash.bank_payment_matches` blocking `DELETE` and preventing mutation of released financial snapshots, journal links, or reversed matches.
  - `Down()` cleanly drops triggers, policies, and all 11 tables + `cash` schema (`S11_PG_02`).

---

## 4. API & Impeccable Next.js Experience (`/w/[tenantId]/[entityId]/payments`)

- **API Endpoints (`PaymentsAndBankingEndpoints`):**
  - `GET /payments/overview`, `POST /payments/bank-accounts`, `GET /payments/{paymentId}`
  - `POST /payments`, `PUT /payments/{paymentId}`, `POST /payments/{paymentId}/simulate-file`
  - `POST /payments/{paymentId}/submit`, `POST /payments/{paymentId}/release`, `POST /payments/{paymentId}/reject-released`
  - `POST /payments/bank-statements`, `POST /payments/reconcile`, `POST /payments/matches/{matchId}/reverse`
- **RBAC Reuse (100% compatible with existing `RoleTemplates` & `EntityAccessGrant`):**
  - Read: `ap.read | ap.manage | ap.approve | ap.post | finance.read | finance.config.manage | finance.journal.post`
  - Configure bank account: `finance.config.manage`
  - Prepare / update / simulate file / submit: `ap.manage | ap.post | finance.journal.post`
  - Release (`OD-02` `actor != preparer && actor != submitter`): `ap.approve | ap.post | finance.journal.post`
  - Import statement / reconcile / reverse / bank reject: `finance.journal.post | ap.post`
- **Impeccable UI (`craft-floor.md` & `DESIGN.md` Compliant):**
  - Flat archival paper surfaces (`#f6f5f0`, `#ffffff`, `1px` hairlines), `Landmark` / `Wallet` nav link (`payments`) positioned cleanly after `payables`.
  - Collapsible `OD-06` / `OD-02` Two-Stage Clearing governance notice (`SIMULATED_FILE_EXPLICIT_RELEASE_OD06_SAFE`).
  - 4-metric hairline KPI strip (`Eligible Open AP`, `Simulated / Pending Release [0 GL]`, `Outbound Clearing 1120 Open`, `Reconciled Bank Outflows 1110`) with live clearing reconciliation check badge.
  - Instant client tabs (`role="tablist"`): **Payment Proposals & Outbound Clearing (`PAY`)**, **Bank Statements & Reconciliation (`BST`/`BMK`)**, **Bank Accounts & Clearing Proof (`1110`/`1120`)**.
  - Keyed `SupplierPaymentDetailSheet` with 4-stage lifecycle strip (`Draft -> File Simulated [0 GL] -> Released [Dr 2100 / Cr 1120] -> Bank Reconciled [Dr 1120 / Cr 1110]`), simulated CSV preview (`SIMULATED — NOT SENT TO BANK`), four-eyes release form (`ReleaseReference`), and bank rejection reversal action.
  - Cross-slice link from `/payables` showing `PaidAmount` and deep link to `/payments`.

---

## 5. Verification Plan & Owner Decisions

- **UnitTests (`S11SupplierSettlementAndBankReconciliationUnitTests`):** `PayableItem` settlement/reversal conservation, `PAYMENT_BLOCKED` guard, `SupplierPayment` state transitions, `OD-02` SoD guard, `OD-06` simulation non-posting invariant, `BankStatementLine` fingerprint & partial match conservation.
- **PostgreSQL 18 IntegrationTests (`S11SupplierSettlementPostgresTests`):** `S11_PG_01` (forced RLS, missing context deny, append-only `cash.payment_instructions`, DB triggers), `S11_PG_02` (`M11 -> M10 -> M11` round-trip), `S11_API_01` (simulation = 0 GL / 0 AP, blocked/over-allocated payable fail, self-release blocked, four-eyes release `Dr 2100 / Cr 1120` once + replay idempotent, CSV duplicate block, exact & partial bank match `Dr 1120 / Cr 1110`, clearing 1120 proof, reverse match & bank rejection recovery, closed period block, savepoint fault rollback), `S11_API_02` (concurrent `Task.WhenAll` double-allocation & double-reconciliation race tests), `S11_API_03` (`NUSA-SBY` read-only 403, foreign entity 404/409, `SMDR` 404, audit + outbox).
- **Frontend & E2E (`s11-payments-bank-reconciliation.test.tsx`, `s11-payments-bank-reconciliation.spec.ts`):** Full `S00`–`S11` regression, `01`–`06` desktop/mobile screenshots inspected via `read_file`, `impeccable detect --json` = `[]`, and `impeccable-finish-reviewer` `disposition: ship`.
- **Owner-Only Decisions:** `OD-06` (`ATH-CR-008`), `OD-02` (`ATH-CR-001`), `OD-13` (`ATH-CR-002`) remain `OPEN`; S11 stays `Product Owner Acceptance: PENDING`.
