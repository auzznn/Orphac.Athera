# ATH-S12 — Impeccable UX & Visual Audit Record

**Slice:** S12 — Customer Sales Orders and Commercial Commitments  
**Automated Anti-Pattern Scan (`npx --yes impeccable detect --json apps/web/src`):** `[]` (`0` findings)  
**Subagent Review Pass 1 (`471bb8cd-879a-433b-a392-de284acaf613`):** `disposition: fix` (5 items identified)  
**Subagent Review Pass 2 (`edd66f17-544f-4526-8ef0-4bb57551c149`):** `disposition: ship` (all 5 items resolved)

---

## 1. Visual & Functional Verification Summary

- **Aesthetic Direction & Token Discipline**:
  - Follows the warm-paper light workbench (`#F7F6F2` canvas, `#FFFFFF` elevated sheet, `#1F5E4B` forest emerald primary CTA, `1px` hairline grid dividers, `Plus Jakarta Sans` UI + `JetBrains Mono` tabular numbers for SO/draft codes, NPWP, SHA-256 hashes, quantities, and IDR amounts).
- **Pass 1 Findings & Resolution Verified in Pass 2 (`edd66f17-544f-4526-8ef0-4bb57551c149`)**:
  1. **Approved vs. Pending Revision KPI Separation**: `Komitmen Disetujui Aktif` and `Total Unit Basis Dipesan` aggregate strictly from each order's currently approved revision (`approvedRev`), never counting unapproved draft/pending amendments.
  2. **Ledger Row Revision Badge & Open Revision Tag**: `#so-orders-table` displays `currentApprovedRevisionNumber` for committed orders and renders a distinct amber `Rev {latestRevisionNumber} · Revisi Terbuka` badge when an amendment revision is open.
  3. **Four-Eyes Approval Queue Pending Total**: Added `PendingGrandTotalAmount` (`pendingGrandTotalAmount`) to `SalesOrderSummaryDto` so `#so-queue-table` shows the pending revision's total (`Rp 632.700` on `Rev 2`) to the approver rather than the superseded `Rev 1` total (`Rp 421.800`).
  4. **Requested Delivery Date Preservation Across Amendments**: Verified `Target Kirim: 2026-11-26` carries forward from `Rev 1` to `Rev 2` across desktop (`03`, `04`) and mobile (`06`) views.
  5. **Post-Approval Diff Screenshot Recapture**: `s12-sales-orders.spec.ts` awaits completion of the `Rev 2` approval mutation before capturing `04-sales-order-revision-diff-desktop.png`, showing `Rev 2 · Disetujui`, `Selisih Nilai Total: Rp 210.900`, `Komitmen Disetujui Aktif: Rp 1.687.200`, and `Antrean Empat Mata: 1`.

---

## 2. Captured Evidence Screenshots (`docs/evidence/S12/screenshots/`)

1. `01-sales-orders-overview-desktop.png` (`1440×900`): `NUSA-JKT` Sales Orders ledger with expanded `OD-09` governance strip, 4-metric KPI strip, `SO-2026-00001` (`APPROVED`), and seeded `SO-DRF-...` (`PENDING_APPROVAL`).
2. `02-sales-order-editor-and-draft-desktop.png` (`1440×900`): Sales Order composer showing live stock availability (`Fisik: 40 · Bebas: 40 PCS` for `ITM-S09-VALVE-01`), `PPN-OUT-11 (11%)`, and live totals (`Rp 380.000` DPP + `Rp 41.800` PPN = `Rp 421.800`).
3. `03-sales-order-four-eyes-approve-desktop.png` (`1440×900`): Independent Controller (`budi.santoso`) four-eyes approval committing `SO-2026-00002` (`Rev 1 · Disetujui`, `Target Kirim: 2026-11-26`, `Rp 421.800`).
4. `04-sales-order-revision-diff-desktop.png` (`1440×900`): `SO-2026-00002` `Rev 2` approved with `#so-revision-diff` (`Rev 1 → Rev 2`, `4 PCS → 6 PCS`, `+Rp 210.900`) and `#so-rev-select` historical revision selector.
5. `05-sales-orders-readonly-sby-desktop.png` (`1440×900`): Multi-entity read-only mode on `NUSA-SBY` showing `#so-readonly-banner` and hiding `#so-tab-editor` and `#so-new-draft-btn`.
6. `06-sales-orders-mobile.png` (`390×844`): Mobile responsive stacked cards on `NUSA-JKT` with zero horizontal overflow.
