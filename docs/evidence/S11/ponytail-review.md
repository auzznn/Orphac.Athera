# Ponytail (`full`) Review — Slice S11 (`Supplier Settlement, Outbound Clearing and Bank Reconciliation`)

- **Reviewer Mode:** Ponytail (`full`) — smallest complete solution preserving every financial, four-eyes, concurrency, and PostgreSQL RLS invariant.
- **Outcome:** **PASS**

## Reused Code & Avoided Bloat
1. **Shared GL Posting & Reversal Kernel:** Reused `GlPostingKernel.PostJournalAsync` and `GlPostingKernel.ReverseJournalAsync` (`AP_CASH` / `CASH` sources) and pre-seeded `1110` (`CASH_BANK`), `1120` (`BANK_OUTBOUND_CLEARING`), `2100` (`AP_TRADE_CONTROL`) accounts & `AccountMappingValidator.ValidateAccountForPurpose` instead of duplicating COA validation or journal posting SQL.
2. **Shared Sequence Helper:** Extended `InventoryValuationService.IncrementEntityYearSequenceAsync` with 4 parameterized sequence tables (`ap.payment_sequences`, `cash.instruction_sequences`, `cash.statement_sequences`, `cash.match_sequences`) rather than copy-pasting ADO command boilerplate.
3. **RBAC Compatibility:** Reused existing `PermissionCodes` (`ap.read`, `ap.manage`, `ap.approve`, `ap.post`, `finance.read`, `finance.config.manage`, `finance.journal.post`) across S11 endpoints instead of multiplying permission rows that would invalidate existing role templates.
4. **Zero-Impact Endpoint Compatibility:** Resolved `SupplierPartnerId` on draft creation from `PayableItem` when omitted so existing `PayableItemDto` fixtures in S09/S10 remain untouched.
5. **No Over-Engineered Bank Integration:** Kept payment file generation strictly simulated (`SimulationOnly = true` enforced via DB check constraint + append-only `payment_instructions` trigger) with exact-match (`0` tolerance) reconciliation under `OD-06` (`ATH-CR-008`) safe defaults until Owner sign-off.
6. **Shared UI Pattern:** Reused existing paper/hairline CSS tokens and archival table/sheet composition across `/payments` and `SupplierPaymentDetailSheet` without introducing extra component libraries.

## Risks & Open Governance
- **Non-blocking Open Decisions:** `OD-06` (`ATH-CR-008`), `OD-02` (`ATH-CR-001`), and `OD-13` (`ATH-CR-002`) remain `OPEN`; bank-fee write-offs, live H2H bank transmission, and multi-currency FX clearing stay fail-closed until Product Owner sign-off.
