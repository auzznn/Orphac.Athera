# ATH-S15 Ponytail Engineering Review (`full`)

## Minimalism & Reuse
- **Zero duplicate bank statement infrastructure:** Reused S11 `cash.bank_statements`, `cash.bank_statement_lines` (with SHA-256 fingerprint deduplication), and `BankReconciliationCoordinator.ImportBankStatementAsync` directly, adding only `ApplyInboundReceiptMatch` on `BankStatementLine`.
- **Lean `ar.*` schema:** Added only 3 focused tables (`ar.receipt_sequences`, `ar.customer_receipts`, `ar.receipt_allocations`) and extended `ar.CustomerReceivable` with `ApplyReceipt`/`ReverseReceipt`.
- **Single cohesive coordinator:** `CustomerReceiptsCoordinator` handles confirmation, partial/multi-invoice allocation, and compensating reversal with strict `FOR UPDATE` locking and amount conservation.

## Invariant Verification
- **Anti-double-collection:** Partial unique index `ux_customer_receipts_active_bank_line` + `FOR UPDATE` on `cash.bank_statement_lines` + SHA-256 fingerprint deduplication verified in `S15_PG_01` and `S15_API_01`.
- **Amount conservation & GL 1200 proof:** `receipt_amount == allocated_amount + unapplied_amount` and `GL 1200 == Open Receivables - Unapplied Receipts` verified live before allocation, after partial allocation, and after full reversal (`S15_API_01`).
- **Immutability & RLS:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 3 tables + `ar.guard_reversed_receipt_immutability` (`55000`) verified under least-privilege `athera_app` (`S15_PG_01`).

**Verdict:** `PASS`.
