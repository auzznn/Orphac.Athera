# ATH-S05 Impeccable UX & Finish Review

**Mode:** `operate` (extending `DESIGN.md` "Indonesian Commercial Ledger Sheet" direction)  
**Detector (`impeccable detect --json apps/web/src`):** `[]` (zero anti-pattern violations)

## Round 1 -> Material Fixes Applied & Verified (`impeccable-finish-reviewer` `43934d5a-0a70-40d6-965d-214af219fa68`)
Round 1 returned `disposition: fix` with 7 material findings across `procurement/page.tsx`, `messages.ts`, `s05-procurement-po.test.tsx`, and `s05-procurement-po.spec.ts`. All 7 material fixes were implemented, re-tested, recaptured across desktop (`1280x800`, including new PO draft editor capture `s05-po-editor-desktop.png`) and mobile (`390x844`), visually inspected, and scored **`resolved`** in Round 2 -> **`disposition: ship`**:

1. **Action-Only Sumatra Teal Accent Rule (`procurement/page.tsx`) — RESOLVED:** Removed `text-[var(--accent-primary)]` from static `Sisa Komitmen Terbuka` readout, detail/editor `Grand Total` numbers, and static `Info` icon; hid `role="tablist"` in read-only mode (`s05-po-readonly-desktop.png`); restyled active Revision Switcher buttons using neutral `#f1f3ee` wash + `--border-strong` + embedded semantic `StatusBadge`.
2. **Diff Table Readability & `colDiffSubtotal` (`procurement/page.tsx`) — RESOLVED:** Added `StatusBadge` (`Diubah` / `Tetap`), semantic row tint (`status-warn-bg` on `MODIFIED`), stacked `Perubahan Subtotal` (`previousSubtotal` -> `currentSubtotal`), `formatSignedMoneyDelta` negative `-Rp` formatting, and non-breaking `Rp\u00A0` + `whitespace-nowrap` on diff unit prices so mobile (`390px`) never splits `Rp` across lines.
3. **Sticky Footer Trap Removed (`procurement/page.tsx`) — RESOLVED:** Replaced `md:sticky md:bottom-0` on the multi-form commitment panel inside `#po-detail-sheet` with natural document flow so `1280x800` viewports view line items and comparison diffs without occlusion; added `terminalPoControlNote` when `CLOSED` or `CANCELLED`.
4. **Four-Eyes (`OD-02`) & Lifecycle Controls Clarity (`messages.ts`, `procurement/page.tsx`) — RESOLVED:** Replaced raw `procurement.approve` in `selfApprovalBlockedBanner` with localized role prose; relabeled frozen-pending submitter row action buttons from `Kelola & Revisi` to `Pantau Status` / `View Status`; styled non-approver pending notice with a `Clock` + `status-warn` frame; rendered `pt.cancelPoHeading` (`Batalkan Pesanan Pembelian (Tanpa Penerimaan)`) above `Batalkan PO`.
5. **Register & Line-Table Wrapping & Localization (`procurement/page.tsx`) — RESOLVED:** Tightened register cells (`md:px-2.5 md:pl-4/pr-4`), added `whitespace-nowrap` to codes/`Rev`/`StatusBadge`/action buttons, localized `"open"` via `pt.openQtySuffix` (`terbuka`), darkened selected-row highlight to `bg-[var(--bg-subtle)]` (`#f1f3ee`) to preserve green `Disetujui` badge contrast, stacked `Pajak (OD-07)` without a dangling dot, and turned mobile subtotal footer into a clean 2-column summary.
6. **`36px–44px` Hit Targets, Control Strokes & Tabular Numerals (`procurement/page.tsx`) — RESOLVED:** Upgraded `cancelEditorButton`, `addLineButton`, `removeLineButton` (`h-9 w-9`), row action buttons (`min-h-9`), revision pills (`min-h-9`), and `po-line-desc` (`min-h-9 border-[var(--border-strong)]`) to >= `36px`, and paired `tabular-nums` with `.font-code` elements.
7. **Editor WHT Parity, Mobile Placeholders & Editor Capture (`procurement/page.tsx`, `messages.ts`, `s05-procurement-po.spec.ts`) — RESOLVED:** Added conditional `editorTotals.whtIdr > 0n` (`pt.whtTotalLabel`) display in `po-editor-totals`, shortened `searchPlaceholder` / `decisionReasonPlaceholder` / `amendmentReasonPlaceholder` to prevent mid-word clipping at `390px`, aligned `Termin Pembayaran (Hari)` row (`sm:col-span-5` + `sm:col-span-3`), and captured `s05-po-editor-desktop.png`.

## Visual & Automated Artifacts
- [s05-po-editor-desktop.png](./screenshots/s05-po-editor-desktop.png)
- [s05-po-pending-sod-desktop.png](./screenshots/s05-po-pending-sod-desktop.png)
- [s05-po-diff-approval-desktop.png](./screenshots/s05-po-diff-approval-desktop.png)
- [s05-po-mobile.png](./screenshots/s05-po-mobile.png)
- [s05-po-closed-desktop.png](./screenshots/s05-po-closed-desktop.png)
- [s05-po-readonly-desktop.png](./screenshots/s05-po-readonly-desktop.png)
- **Reviewer disposition note:** Round 2 `ship` covers all 7 scored Round 1 material fixes across the refreshed S05 captures with zero regressions (`remaining: clear`).