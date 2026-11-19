# ATH-CR-015 — Owner Ruling Request: Posting Paths That Are Live While Their Decisions Are Open

- **Decision IDs:** `OD-03`, `OD-05`, `OD-06`, `OD-07`, `OD-09` (`docs/product/ATH-OPEN-DECISIONS-001.md`); companion to `ATH-CR-002`, `ATH-CR-006`, `ATH-CR-007`, `ATH-CR-008`, `ATH-CR-011`, `ATH-CR-012`
- **Affected Slices:** `S04`, `S09`, `S10`, `S11`, `S14`, `S15` (and every later slice that consumes their postings)
- **Raised by:** Engineering, during the S18 release-gate review
- **Status:** **OPEN — Awaiting Product Owner & Finance Controller Decision**

---

## 1. Why this request exists

The master goal says: *"Build safe reversible prerequisites and disable unapproved irreversible posting paths."* The decision register gives a "safe behavior until decided" for each open decision.

For five decisions the build does **more** than the register's safe behavior: it posts immutable GL / inventory / subledger entries under an engineering-chosen default ("Option A" in the linked CRs). Each default is disclosed in its own CR, but no CR states plainly that the default goes beyond the register. This request states it in one place so the Owner can rule once.

The BRS approves the *direction* of each transaction (price-difference split `DEC-P2P-013`, controlled replacement, two-stage payment clearing `DEC-P2P-015`, effective-dated tax `DEC-P2P-016`). What is unapproved is the *condition* under which each one may post.

No path below moves real money or files anything: payment files are simulated, there is no bank or DJP integration, and all data is synthetic. The exposure begins when a pilot runs on real business data.

## 2. The five paths

| # | Decision | Register: safe behavior until decided | What the build does today | Where |
|---|---|---|---|---|
| 1 | `OD-03` match tolerances | "Exact-match posting only; discrepancies visible and blocked." | Quantity (`DQ`) and header (`BD`) differences are blocked. A **unit-price difference is not blocked**: after an independent approver ticks "approve price variance", the bill posts and the difference is split between Inventory `1300` and PPV `5200` per `DEC-P2P-013`. | `ATH-CR-006`, `SupplierBillCoordinator` |
| 2 | `OD-05` supplier returns | "Document claims/custody, defer irreversible financial correction unless defined." | Two financial corrections post after four-eyes approval: `M2` valued-unbilled return (`Dr 2110 / Cr 1300`, ± `5200`) and `M3` billed-unpaid return with supplier credit note (`Dr 2100 / Cr 1300 / Cr 1410`). `M4`–`M7` are blocked. | `ATH-CR-007`, `SupplierReturnCoordinator` |
| 3 | `OD-06` payment release | "Draft/file simulation only until release evidence & approval policy specified." | File generation is simulation only and posts nothing. **Release does post** `Dr 2100 / Cr 1120` once an independent releaser enters any non-empty free-text release reference. Bank reconciliation then posts `Dr 1120 / Cr 1110`. | `ATH-CR-008`, `SupplierSettlementCoordinator`, `BankReconciliationCoordinator` |
| 4 | `OD-07` Indonesian tax | "Effective-dated configurable model and explicit review; no unverified statutory claim or automated filing." | Tax codes whose review status is `UNREVIEWED_CONFIGURABLE` can be used on posted bills and invoices: PPN input `1410`, PPN output `2210`, PPh 23 withheld `2220`. Nothing is filed and each code carries its unreviewed status. | `ATH-CR-002`, `ATH-CR-006`, `ATH-CR-011`, `TaxModels` |
| 5 | `OD-09` sales corrections | "Basic approved-price/positive-quantity workflow; block uncertain accounting/fiscal corrections." | Customer credit notes post (`Dr 4100 / Dr 2210 / Cr 1200`) and customer receipts can be reversed with a compensating entry. Discounts, credit-limit overrides and cash refunds of unapplied funds are blocked. | `ATH-CR-011`, `ATH-CR-012`, `BillingAndReceivablesCoordinator`, `CustomerReceiptsCoordinator` |

## 3. Options

- **Option A — Ratify the current defaults for demonstration and pilot.** No code change. The Owner signs `ATH-CR-002` (with a local tax review), `ATH-CR-006`, `-007`, `-008`, `-011` and `-012` Option A as the interim policy and accepts that these entries are immutable once posted.
- **Option B — Gate each path behind an explicit per-legal-entity activation that is off by default.** Engineering adds one reviewed-policy switch per path (the pattern already exists: `InvoiceMatchPolicy.review_status`, `TaxCode.review_status`). With the switch off the path returns `409 <decision>_policy_not_activated`; demo fixtures turn it on so the demonstrations still run. This restores the register's safe behavior for any entity the Owner has not explicitly activated. It requires migrations and test updates across the affected slices.
- **Option C — Rule path by path.** For example ratify paths 3 and 5, gate paths 1, 2 and 4.
- **Engineering recommendation:** Option B, unless the five paths are formally ruled (Option A or per-path Option C).

## 4. Behavior until signed

- **Active now:** the current build, unchanged. This is a deployment hold, not an application-enforced gate. The five paths remain executable on synthetic fixtures. Until rules are signed or the paths blocked and retested, do not connect real business data.
- The S18 engineering result is reported with this item listed as an open Owner decision, not as a pass.
- No pilot on real business data should start before this request is ruled.

## 5. Product Owner & Finance Controller Sign-Off (Owner Only)

- **Reviewer Name / Role (PO / Finance Controller):** `____________________`
- **Ruling (`Option A` / `Option B` / `Option C` — list per path):** `____________________`
- **Date:** `____________________`
- **Signature & Binding Conditions:** `____________________`
