# ATH-CR-008 — Open Decision Request OD-06: Qualified Payment Release Triggers and Two-Stage Bank Clearing

- **Decision ID:** `OD-06` (`docs/product/ATH-OPEN-DECISIONS-001.md`; companion to `OD-02` [`ATH-CR-001`], `OD-08` [`ATH-CR-006`], and `OD-13` [`ATH-CR-002`])
- **Affected Slices:** `S11` (Supplier Settlement, Outbound Clearing and Bank Reconciliation), `S16` (Accounting Reports and Period Close Reconciliation)
- **Status:** **OPEN — Awaiting Product Owner & Treasury / Finance Controller Decision**

---

## 1. Decision Questions

1. **Qualified Point of Payment Release & Banking Evidence (`OD-06`):**
   - What exact internal and banking evidence constitutes a **qualified payment release** (`TX-PAY-RELEASE`) authorizing the system to derecognize supplier Accounts Payable (`Dr 2100 AP Trade Control`) against outbound clearing (`Cr 1120 Bank Outbound Clearing`)?
   - Specifically: is manual four-eyes treasury sign-off plus an explicit bank-portal batch/token reference sufficient, or does corporate policy require bank-uploaded acknowledgment (e.g., signed H2H/portal confirmation upload) before releasing AP?
2. **Partial Settlement, Delayed Bank Debit, Failed Files & Bank Charges:**
   - When a bank statement debit line settles only part of a released payment batch, or incurs bank transfer fees/short deductions, should `OD-06` require zero tolerance (exact functional-currency `IDR` line clearing match) or allow configured fee write-off / FX clearing accounts?
   - If a released payment is rejected by the clearing bank before settlement, what authorization restores the open payable liability (`Dr 1120 / Cr 2100`)?

---

## 2. Options Considered

- **Option A (`SIMULATED_FILE_EXPLICIT_RELEASE_OD06_SAFE` Active in S11):**
  - **Stage 1 — Proposal & File Simulation (Zero Ledger Impact):** Generating, previewing, or downloading a simulated payment instruction file (`cash.payment_instructions`, flagged `SimulationOnly = true`, format `ATH_CSV_SIM_V1`) NEVER connects to live bank APIs, NEVER initiates real fund transfers, NEVER creates GL journals, and NEVER reduces `ap.payable_items.RemainingOpenAmount`.
  - **Stage 2 — Four-Eyes Explicit Release (`TX-PAY-RELEASE`):** Moving `ap.supplier_payments` from `DRAFT`/`PENDING_RELEASE` to `RELEASED` requires:
    1. Independent four-eyes actor (`released_by_user_id != prepared_by_user_id && != submitted_by_user_id`, `OD-02`).
    2. Explicit non-empty `ReleaseReference` (portal authorization token / manual release reference).
    3. Re-verified `OPEN` fiscal period and unlocked/non-blocked payable allocations (`PaymentBlockStatus != PAYMENT_BLOCKED`, `0 < allocated <= remaining_open`).
    - Atomically posts **`Dr 2100 AP Trade Control / Cr 1120 Bank Outbound Clearing`** and reduces `PayableItem.RemainingOpenAmount`.
  - **Stage 3 — Bank Statement Reconciliation (`TX-BANK-RECONCILE`, Exact Match / Zero Unapproved Fee Variance):**
    - Matching an imported `DEBIT` statement line (`cash.bank_statement_lines`) to a `RELEASED` / `PARTIALLY_RECONCILED` payment in the same `(tenant_id, legal_entity_id, bank_account_id, currency_code)` atomically posts **`Dr 1120 Bank Outbound Clearing / Cr 1110 Cash & Bank Operating`** using exact decimal arithmetic (`0` automatic fee/FX variance tolerance).
  - **Stage 4 — Linked Compensating Reversal Only:** Bank rejection of an unreconciled released payment or reversal of a posted bank match always posts a linked reversing GL entry rather than editing posted ledgers or bank statement history.
- **Option B (Owner-Approved Bank H2H / Fee Tolerance Policy):**
  - Product Owner and Finance Controller approve binding H2H file-acknowledgment rules, automatic bank-fee tolerance limits, and fee GL accounts prior to production bank integration.

---

## 3. Safe Default Behavior Until Signed

- **Active Now:** Option A (`SIMULATED_FILE_EXPLICIT_RELEASE_OD06_SAFE`). Live bank integration remains disabled; file generation is explicitly non-posting simulation only; release requires four-eyes authorization (`OD-02`) plus explicit `ReleaseReference`; bank reconciliation enforces exact matching without automatic bank-charge or FX variance write-offs until the Product Owner signs Option B.

---

## 4. Product Owner & Finance Controller Sign-Off (Owner Only)

- **Reviewer Name / Role (PO / Treasury Controller):** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
