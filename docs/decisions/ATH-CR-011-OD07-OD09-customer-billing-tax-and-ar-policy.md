# ATH-CR-011 — Customer Billing, Output Tax Evidence & Credit Note Policy (OD-07 / OD-09)

- **Decision ID:** `ATH-CR-011`
- **Related Open Decisions:** `OD-07` (Indonesian tax legal & Faktur Pajak evidence treatment), `OD-09` (Sales billing, credit notes, and cancellation policy)
- **Slice:** `S14` — Customer Billing, Tax and Accounts Receivable
- **Status:** `OPEN` (Safe deterministic defaults enforced in engineering build; Product Owner sign-off required before production release)
- **Product Owner Sign-Off:** *(BLANK — PENDING HUMAN APPROVAL)*
- **Date Approved:** *(BLANK)*

## Context

Slice `S14` introduces customer invoicing (`billing.customer_invoices`), output tax determination (`tax.tax_codes` with `direction = 'OUTPUT'`), credit notes (`DOCUMENT_TYPE = 'CREDIT_NOTE'`), and Accounts Receivable subledger (`ar.customer_receivables`) linked atomically to General Ledger (`gl.journal_entries`).

## Safe Engineering Defaults Implemented Pending Owner Confirmation

1. **Fulfillment-Backed Billing Capacity (`OD-09`):**
   - Customer invoices may only bill quantities against posted outbound shipments (`fulfillment.shipment_lines`, status `DISPATCHED` or `DELIVERED`).
   - Standalone non-delivery invoices are blocked until explicitly approved by the Product Owner.
   - Concurrent billing attempts lock `fulfillment.shipment_lines` (`FOR UPDATE`) and enforce `billed_qty_net_of_credits <= dispatched_base_quantity`, preventing double billing.
2. **Indonesian Output Tax & Evidence Gate (`OD-07`):**
   - Output tax rates are never hardcoded; each invoice resolves an active, effective-dated `OUTPUT` tax code configured on the legal entity (`PPN-OUT-11` 11% standard or `PPN-OUT-EXEMPT` 0% exempt/facility).
   - Posting an invoice or credit note requires an explicit `TaxEvidenceReference` (e.g., `Faktur Pajak` number, `SKB`/exemption facility reference, or `Nota Retur` reference) whenever the selected tax code requires tax invoice evidence (`requires_tax_invoice_evidence = true`) or uses a non-standard facility (`EXEMPT`, `ZERO_RATED`, `NOT_COLLECTED_FACILITY`).
   - No automated DJP/Coretax e-Faktur filing or submission is performed.
3. **Immutable Posting & Compensating Credit Notes (`OD-09`):**
   - Once posted (`POSTED`), a customer invoice and its lines are permanently immutable at the PostgreSQL trigger level (`billing.guard_posted_invoice_immutability`).
   - Post-posting billing corrections must be issued as linked `CREDIT_NOTE` documents referencing the original posted invoice lines, posting compensating GL entries (`Dr 4100 Sales Revenue`, `Dr 2210 PPN Keluaran` / `Cr 1200 AR Control`) and reducing the linked `ar.customer_receivables.open_amount`.
   - Credit notes cannot exceed remaining uncredited invoiced quantities and cannot drive AR `open_amount` negative.
4. **Role & Separation Boundaries:**
   - Sales (`sales.manage`) may prepare and submit draft invoices (`DRAFT` → `SUBMITTED`) without creating AR or GL postings.
   - Only Finance (`finance.journal.post`) may finalize/post invoices and credit notes to AR (`1200`), Revenue (`4100`), and Output VAT (`2210`).

## Owner Decisions Requested

- [ ] Confirm whether billing is permitted at `DISPATCHED` (default) or must wait until `DELIVERED` (POD confirmed).
- [ ] Confirm whether non-delivery service/milestone invoices should be enabled in a future change request.
- [ ] Confirm Indonesian tax evidence format validation rules (`Faktur Pajak` / `Nota Retur` / `SKB`) with tax counsel (`OD-07`).
- [ ] Confirm whether sales credit notes should debit `4100 Pendapatan Penjualan` directly (default) or a dedicated contra-revenue account `4110 Retur & Potongan Penjualan`.
