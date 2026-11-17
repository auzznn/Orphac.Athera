# ATH-CR-002 — Open Decision Requests OD-07 & OD-13: Statutory Indonesian Tax Matrix & Fiscal Calendar / Numbering

- **Decision IDs:** `OD-07`, `OD-13` (`docs/product/ATH-OPEN-DECISIONS-001.md`)
- **Affected Slices:** `S04` (Financial Configuration & Immutable Posting Kernel), `S05`, `S09`, `S12`, `S14`, `S16`
- **Status:** **OPEN — Awaiting Product Owner & Indonesian Accountant Decision**

---

## 1. Decision Questions
1. **`OD-07` — Statutory Indonesian Tax Matrix (PPN & PPh 23):**
   - Which effective dates, statutory rates, and taxable-base (`DPP`) methods apply per transaction context (e.g. standard PPN 11% vs. nominal 12% on an `11/12` `DPP` factor producing an effective `11%`, luxury goods, zero-rated exports, PPN-exempt, and PPN non-collected/Batam free-trade-zone facilities with required documentary evidence)?
   - Which transaction categories trigger `PPh 23` withholding (`2%` standard vs. `4%` non-NPWP surcharge, or excluded raw-material purchases), what documentary evidence (`Faktur Pajak`, `Bukti Potong`, FTZ endorsement) is required before posting vs. reporting, and what exact recognition moment applies?
2. **`OD-13` — Fiscal Calendar & Statutory Document Numbering:**
   - What default fiscal year boundaries (`1..12` calendar vs. custom fiscal year-end or optional `13`th adjustment period) apply per tenant/legal entity?
   - Are tax-invoice (`Faktur Pajak`) or legal voucher sequences strictly client-configured external references or tenant-generated gapless series requiring statutory lockouts?

## 2. Options Considered
- **Option A (Safe Configurable Kernel Active in S04):**
  - Store `tax.tax_codes` as effective-dated, explicitly reviewed/unreviewed configurable templates (`UNREVIEWED_CONFIGURABLE` default; never claim automatic statutory compliance or tax filing).
  - Require explicit `taxCodeId` selection per evaluated transaction line (`explicit_tax_code_required`); never infer tax rates solely from partner `PKP`/`NPWP` master metadata (`OD-07`).
  - Support exact `decimal` `TaxableBaseRatioNumerator / TaxableBaseRatioDenominator` (`11/12` DPP support) and NPWP presence checks (`counterparty_npwp_required_for_rate` block when `RequiresCounterpartyNpwp == true` and counterparty NPWP is missing).
  - Maintain explicit per-legal-entity `gl.financial_periods` (`OPEN` / `CLOSED` / `LOCKED`, strictly blocking postings outside `OPEN` periods) and deterministic per-period sequence numbers (`JRN-{PeriodCode}-{Seq:D5}`) without claiming statutory tax-invoice gapless compliance.
- **Option B (Approved Tenant Tax & Statutory Calendar Policy):**
  - Apply Product Owner & certified Indonesian tax consultant sign-off marking curated `tax.tax_codes` as `REVIEWED_INTERNAL`, locking mandatory facility evidence gates and statutory fiscal calendars/prefixes.

## 3. Safe Default Behavior Until Signed
- **Active Now:** Option A — configurable effective-dated rules explicitly flagged `UNREVIEWED_CONFIGURABLE`, closed-period posting blocked in both `GlPostingKernel` and PostgreSQL deferred constraint trigger, and zero automated statutory filing or tax inference from supplier master flags alone.

## 4. Product Owner & Tax/Accounting Sign-Off (Owner Only)
- **Reviewer Name / Role (PO / Tax Advisor):** `____________________`
- **Approved Option (`Option A` / `Option B` / `Custom Policy`):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
