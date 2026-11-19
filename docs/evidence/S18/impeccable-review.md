# Impeccable Review — Slice S18 (final critique of key daily journeys)

- **Skill:** Impeccable `4.9.1` (engine `0.1.24`), command `critique`, mode Operate. `impeccable context` loaded PRODUCT.md, DESIGN.md and the surface brief.
- **Method:** independent source review and deterministic detector scan, plus visual spot checks of rendered screenshots.
- **Limits, stated plainly:**
  - The heuristic review is from source; four screenshots from the fresh-database run (`S18/02`, `S18/05`, `S09/06`, `S06/s06-receiving-mobile`) were spot-checked for the rendered result — the receiving-page length finding comes from that look.
  - No rendered-page detector pass: the detector ran on source only, so element-level rules (contrast, tiny text) did not run.
  - Keyboard evidence is limited to source reading plus the one Tab-focus assertion in `s18-release-candidate-uat.spec.ts`.
- **Result:** 27/40 (Acceptable). 0 × P0, 2 × P1, 3 × P2. **No UI code was changed in S18 in response**; the P1 items are carried in the S18 risk register as open UX defects for the Owner to prioritise.
- **Snapshot:** `.impeccable/critique/2026-11-19T12-54-41Z__apps-web-src-app-shell-w-tenantid-entityid.md`

---

## Heuristic scores (Assessment A)

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | Initial receipt-detail load fails silently (`receiving/page.tsx` `.catch(() => {})`); no live region after mutations |
| 2 | Match system / real world | 3 | Domain-fluent, but `OD-01`, `DQ`/`PP`, `1300/5200` shorthand has no inline gloss |
| 3 | User control and freedom | 3 | No undo by design; compensated by preview-first flows where they exist |
| 4 | Consistency and standards | 3 | `ReceivingErrorCard` reverses primary/secondary versus `ScopedBoundaryError`; payables tabs lack the `aria-controls`/`tabpanel` wiring that reports and documents have |
| 5 | Error prevention | 3 | Strong at Gate 2 and approval; supplier-bill Post is one click |
| 6 | Recognition rather than recall | 3 | No keyboard hints; match-dimension chips need recall |
| 7 | Flexibility and efficiency | 2 | No bulk actions or shortcuts; dock check submits per line |
| 8 | Aesthetic and minimalist design | 3 | Permanent three-paragraph governance banner on a daily screen |
| 9 | Error recovery | 3 | Raw English `detail` can surface in the Indonesian UI; blocking reasons joined with " | " |
| 10 | Help and documentation | 1 | No contextual help or glossary; nothing teaches the two-gate model |
| **Total** | | **27/40** | **Acceptable** |

## Design specificity verdict

**LLM assessment:** authored for this product, not a generic admin skin. The ledger-sheet composition, tabular numerals on every amount and identifier, icon-plus-text status chips, the dock-check conservation equation and the Gate 2 journal preview could not be reused by an unrelated product. The four-tile metric strip on Payables and the flat sixteen-item navigation rail are the only category-interchangeable parts.

**Deterministic scan:** `impeccable detect --json apps/web/src` returned `[]` (exit 0) on the whole tree and on each of the five target files, with config, inline ignores and design-system context also disabled. The detector states this was regex-only over 58 source files: element-level rules (tiny text, low contrast) need a rendered page and did not run, so this is an undercount, not a clean bill.

**Visual overlays:** none injected; review relied on source reading and screenshot spot checks.

## Overall impression

A calm, precise working surface whose best moment is Gate 2: nothing posts until the operator has seen the journal it will create. The biggest opportunity is to apply that same pattern to the other irreversible postings and to stop making warehouse staff scroll one very long page.

## What's working

1. **Preview before post at Gate 2.** Computed moving-average and Dr/Cr lines, with a stale-input notice that blocks confirm.
2. **State vocabulary.** Every status is icon + border + text; read-only, no-permission and segregation-of-duties reasons are stated rather than hidden.
3. **Ledger typography.** Tabular numerals and monospace codes give true column scanning at 1280 and still read cleanly when rows stack at 768.

## Priority issues

- **[P1] Supplier-bill Post is one click with no irreversibility statement.** `SupplierBillDetailSheet.tsx` (`#ap-post-bill-btn`) posts to AP and GL from a date field and a button. The figures (AP 2100, 1300/5200 split, PPN, PPh) are shown in the sheet header, but there is no confirm step and no "this is permanent" copy, while customer-invoice posting and Gate 2 both have one. PRODUCT.md principle 4 rules out accidental one-click postings. *Fix:* reuse the Gate 2 pattern in the APPROVED band: show the journal lines and require an explicit confirm. *Command:* `$impeccable harden`.
- **[P1] Receiving is one very long page.** At 390 px the receiving screenshot is 10,315 px tall: ledger, arrival composer, dock inspection and Gate 2 are stacked. The warehouse officer on a phone or tablet scrolls past unrelated work to reach the receipt in hand. *Fix:* make the selected receipt the page (ledger as a list, receipt as a detail route or sheet), with Gate 1 and Gate 2 as steps. *Command:* `$impeccable layout`.
- **[P2] Payables stacks too many surfaces.** Governance banner, four metrics, tabs, composer and detail sheet on one scroll; the bill composer shows about fourteen fields at once. *Fix:* collapse the banner to one line with expand; let the composer be the only editable surface while open. *Command:* `$impeccable distill`.
- **[P2] Keyboard and assistive semantics are uneven.** Payables tabs have no `tabpanel` linkage; no tab list supports arrow keys; the mobile navigation drawer is not a focus-trapped dialog, so focus can move into the page behind the scrim. *Fix:* share one tabs component with the reports wiring plus arrow-key roving; give the drawer `role="dialog"`, `aria-modal` and a focus trap. *Command:* `$impeccable harden`.
- **[P2] Two error-card variants.** `ReceivingErrorCard` makes "return to workspaces" the primary action and Retry secondary, the reverse of `ScopedBoundaryError`. *Fix:* use `ScopedBoundaryError`. *Command:* `$impeccable polish`.

## Persona red flags

**Alex (power user):** a twelve-line receipt needs twelve separate dock-check submits; no bulk approve or post; no shortcuts.

**Sam (accessibility):** can tab behind the open mobile drawer; tabs are not arrow-key operable; 11 px micro-labels are hard at 200 % zoom.

**Warehouse officer (tablet at the dock):** heavy scrolling between Gate 1 and Gate 2 for the same receipt; delivery-note number is free text with no scan affordance.

**Finance officer (desktop):** the reversible draft save requires a preview while the irreversible Post does not; blocking reasons are prose joined by " | " rather than a list.

## Minor observations

- Hard-coded fallback dates (`2026-11-10`, `2026-11-18`, `2026-12-10`) seed invoice, due and posting fields.
- The accrual-line `<option>` text packs quantity, price and pool into a native select.
- `font-heading` is used on the receiving error card but is not in the DESIGN.md type scale.
- Raw `apiErr.detail` fallback can show English server text in the Indonesian interface.

## Questions to consider

- Why does the reversible action demand a preview while the irreversible one does not?
- Would an approvals-first "my work" view cut more daily navigation than any single screen fix?
- Do permanent policy banners reassure, or train people to ignore warning surfaces?

## Follow-up: supplier-bill posting

The payables detail now requires an explicit confirmation after review of the supplier invoice, posting date and net payable. Cancel and Escape restore focus without a posting request. `S09-E2E-01` verifies this against disposable PostgreSQL before the confirmed post and preserves exact monetary assertions. This addresses accidental one-click posting only; the panel is not a committed GL preview or an activation gate for ATH-CR-015. No other findings were closed.
