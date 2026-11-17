# ATH-S04 Impeccable UX & Finish Review

**Mode:** `operate` (extending `DESIGN.md` "Indonesian Commercial Ledger Sheet" direction)  
**Detector (`impeccable detect --json apps/web/src`):** `[]` (zero anti-pattern violations)

## Round 1 -> Material Fixes Applied & Verified (`impeccable-finish-reviewer` `cd04a8b5`)
Round 1 returned `disposition: fix` with 8 material findings across `finance/page.tsx`, `messages.ts`, `DemoDataSeeder.cs`, `DESIGN.md`, `s04-finance-gl.test.tsx`, and `s04-finance-gl.spec.ts`. All 8 fixes were implemented, re-tested, recaptured across desktop (`1280x800`) and mobile (`390x844`), visually inspected, and scored **`resolved`** in Round 2 -> **`disposition: ship`**:

1. **Read-Only Evidence & Immutable Journal Auditability (`DemoDataSeeder.cs`, `finance/page.tsx`, `s04-finance-gl.spec.ts`) — RESOLVED:** Detail inspection displays readable monospace `payloadHash` (`Hash Payload:`), adds origin navigation (`Lihat Jurnal Asal`) on reversals, and adds forward navigation (`Lihat Jurnal Pembalik`) on reversed entries. `NUSA-SBY` seeds an opening capital entry (`JRN-2026-11-00001`), and `S04-E2E-02` verifies `Lihat Baris & Sumber` opens `Hash Payload` with zero reversal controls.
2. **Bilingual `id-ID` / `en-US` Parity & Enum Hygiene (`messages.ts`, `finance/page.tsx`) — RESOLVED:** Added `accountCategoryLabels`, `taxFamilyLabels`, `taxDirectionLabels`, and `taxTreatmentLabels` dictionaries; replaced raw enum codes across CoA categories, subledger mapping selects, tax filters/creation forms, and accessible `aria-label`s; updated `entitySheet.availableState` wording from `S01–S03` to `S01–S04`.
3. **Form `noValidate`, `aria-invalid`, `aria-describedby` & Local `role="alert"` (`finance/page.tsx`) — RESOLVED:** Added explicit `id="new-tax-dpp-den"` and `id="tax-preview-error"`, wired field-scoped `aria-invalid` + `aria-describedby` across Journal Composer (`period_closed` -> date, `duplicate_posting_source_conflict` -> sourceKey), Reversal, CoA, Periods, Tax Preview, and Tax Create, and separated row-action alerts into `#account-table-error` and `#period-close-strip`.
4. **Action-Only Accent Rule on Subledger Mappings (`finance/page.tsx`) — RESOLVED:** Mapping row `Simpan` buttons apply `#0e5a53` primary styling only when `isDirty` (`selectedAccountId !== currentAccountId`), remaining neutral white-outline buttons when unchanged.
5. **Period-Close Strip Grouping & `390px` Mobile Reflow (`finance/page.tsx`, `DESIGN.md`) — RESOLVED:** Separated `Buka Periode Fiskal Baru` from `#period-close-strip` (`Kontrol Penutupan Periode — Catatan Penutupan`) directly above the periods table, added `md:hidden` period-code/status mobile labels, and hid the empty draft line spacer below `sm`.
6. **Crisp Vector Hierarchy & Monospace Scope (`finance/page.tsx`) — RESOLVED:** Replaced text `↳` with `lucide-react` `CornerDownRight` SVG icons on CoA child rows, kept `.font-code` strictly on source/hash values rather than prose labels (`Jejak Sumber:`), and scoped `whitespace-nowrap` to inner currency spans in `formatIdr`.
7. **Decimal-Safe Draft Balance Arithmetic (`finance/page.tsx`, `s04-finance-gl.test.tsx`) — RESOLVED:** Added `parseMinorTenThousandths` exact integer ten-thousandths arithmetic for live debit/credit totals and variance checks, covered by `100.10 == 100.10` in Vitest.
8. **Semantic Status Icons, Readiness Copy & Roving Tab Focus (`finance/page.tsx`, `messages.ts`) — RESOLVED:** Paired journal, period, CoA active/inactive, and tax status badges with `Clock`, `CheckCircle2`, `AlertCircle`, and `Lock` SVGs, and implemented `ArrowLeft`/`ArrowRight`/`Home`/`End` keyboard navigation across the 3 tabs.

## Visual & Automated Artifacts
- [s04-finance-journals-desktop.png](./screenshots/s04-finance-journals-desktop.png)
- [s04-finance-journals-mobile.png](./screenshots/s04-finance-journals-mobile.png)
- [s04-finance-reversal-desktop.png](./screenshots/s04-finance-reversal-desktop.png)
- [s04-finance-coa-desktop.png](./screenshots/s04-finance-coa-desktop.png)
- [s04-finance-tax-periods-desktop.png](./screenshots/s04-finance-tax-periods-desktop.png)
- [s04-finance-readonly-desktop.png](./screenshots/s04-finance-readonly-desktop.png)
- **Reviewer disposition note:** `ship` verdict covers all 8 resolved Round 1 findings across the refreshed S04 files and captures with zero regressions (`remaining: clear`).
