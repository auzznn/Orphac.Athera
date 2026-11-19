# Orphac Athera — Product Owner Four-Pillar Release Candidate Acceptance Packet (`S00`–`S18`)

> [!IMPORTANT]
> **Product Owner Acceptance Status: `PENDING` (UNSIGNED)**  
> Engineering verification (`CODE_COMPLETE` / `ENGINEERING_PASS`) is **NOT** Product Owner acceptance. Only a human Product Owner may sign acceptance, resolve open business policy decisions (`OD-01`–`OD-13`), and authorize any staging-to-production deployment.

## 1. Slice Traceability Ledger (`greenfield/mvp`)
| Slice | Scope | Engineering Status | Commit SHA | Open Decision Requests |
| --- | --- | --- | --- | --- |
| `S00` | Greenfield Architecture, PG18 RLS Foundation & App Shell | `CODE_COMPLETE` / `PASS` | `920bc78` | — |
| `S01` | Identity, Same-Origin Session, CSRF & Workspace Isolation | `CODE_COMPLETE` / `PASS` | `737971a` | — |
| `S02` | Organization Admin, Invitations & Four-Eyes Approvals | `CODE_COMPLETE` / `PASS` | `57618ad` | `ATH-CR-001` (`OD-02`) |
| `S03` | Partners, Item Catalogue & Exact Decimal UoM Engine | `CODE_COMPLETE` / `PASS` | `287d7e5` | — |
| `S04` | Finance Core, CoA, Indonesian Tax Engine & GL Kernel | `CODE_COMPLETE` / `PASS` | `0fcb7b5` | `ATH-CR-002` (`OD-07`, `OD-13`) |
| `S05` | Procurement, Purchase Orders, Revisions & Four-Eyes | `CODE_COMPLETE` / `PASS` | `d2bf6b5` | `ATH-CR-003` (`OD-01`) |
| `S06` | Gate 1 Dock Receiving (Unvalued Physical Custody) | `CODE_COMPLETE` / `PASS` | `876a916` | `ATH-CR-003` (`OD-01`) |
| `S07` | Gate 2 Valued Acceptance, Moving-Average Stock & GRNI | `CODE_COMPLETE` / `PASS` | `e64f46b` | `ATH-CR-004` (`OD-04`) |
| `S08` | Warehouses, Locations, Lots, Quality & Reservations | `CODE_COMPLETE` / `PASS` | `606f138` | `ATH-CR-005` (`OD-10`) |
| `S09` | Supplier Invoices, 3-Way Match, AP & Price Difference | `CODE_COMPLETE` / `PASS` | `b42150e` | `ATH-CR-006` (`OD-03`, `OD-08`) |
| `S10` | Supplier Returns (`M1`–`M7`), Replacements & Credits | `CODE_COMPLETE` / `PASS` | `0d7a969` | `ATH-CR-007` (`OD-05`, `OD-07`) |
| `S11` | Supplier Payments, Outbound Clearing & Bank Reconcile | `CODE_COMPLETE` / `PASS` | `1b84521` | `ATH-CR-008` (`OD-06`) |
| `S12` | Customer Sales Orders & Commercial Commitments | `CODE_COMPLETE` / `PASS` | `d60e309` | `ATH-CR-009` (`OD-09`) |
| `S13` | Sales Reservation, Pick, Dispatch & Atomic COGS | `CODE_COMPLETE` / `PASS` | `2633364` | `ATH-CR-010` (`OD-10`, `OD-09`) |
| `S14` | Customer Billing, Output Tax (`PPN`) & AR Subledger | `CODE_COMPLETE` / `PASS` | `b4625e8` | `ATH-CR-011` (`OD-07`, `OD-09`) |
| `S15` | Customer Receipts, Allocation, Unapplied & AR Aging | `CODE_COMPLETE` / `PASS` | `20e0ab2` | `ATH-CR-012` (`OD-09`) |
| `S16` | Accounting Reports, 5-Control Reconciliations & Close | `CODE_COMPLETE` / `PASS` | `308be0e` | `ATH-CR-013` (`OD-11`, `OD-13`) |
| `S17` | Business Documents, Signed Downloads, PDF & Audit UX | `CODE_COMPLETE` / `PASS` | `6814aec` | `ATH-CR-014` (`OD-12`) |
| `S18` | Cross-Module UAT, Security, Concurrency & RC Runbook | `CODE_COMPLETE` / `PASS` | *Recorded in S18 commit* | All `OD-01`–`OD-13` |

## 2. Open Business Policy Decisions Register (All `OPEN` — Owner Resolution Required Before Pilot/Production)
| CR ID | Decision IDs | Topic | Safe Default Active |
| --- | --- | --- | --- |
| `ATH-CR-001` | `OD-02` | Four-Eyes Segregation of Duties & Role Matrix | Requester cannot approve/release own submission |
| `ATH-CR-002` | `OD-07`, `OD-13` | Indonesian Tax Determinations & Fiscal Calendar | Effective-dated configurable codes; explicit evidence required |
| `ATH-CR-003` | `OD-01` | PO Delivery Tolerance Policy | Exact approved quantity only (`EXACT_QTY_OD01_SAFE`) |
| `ATH-CR-004` | `OD-04` | Moving-Average Valuation Edge Cases | Weighted-average per legal entity; zero negative stock |
| `ATH-CR-005` | `OD-10` | Negative Stock & Reservation Policy | Strict non-negative available stock; `UNRESTRICTED` only |
| `ATH-CR-006` | `OD-03`, `OD-08` | 3-Way Match Tolerances & AP Price Difference Split | `DEC-P2P-013` stock coverage vs PPV `5200`; variance blocks payment |
| `ATH-CR-007` | `OD-05`, `OD-07` | Supplier Returns (`M1`–`M7`), RPA & Nota Retur | Strict `M4` block on billed replacement; mandatory `Nota Retur` |
| `ATH-CR-008` | `OD-06` | Payment Release & Simulated Bank File Clearing | Simulated payload only; zero live bank transmission; 4-stage clearing |
| `ATH-CR-009` | `OD-09` | Sales Commercial Terms & Customer Credit Policy | Explicit approved SO commitment; four-eyes required |
| `ATH-CR-010` | `OD-10`, `OD-09` | Sales Fulfillment, Pick & Dispatch COGS | Atomic weighted-average COGS (`Dr 5100 / Cr 1300`) on dispatch |
| `ATH-CR-011` | `OD-07`, `OD-09` | Customer Billing, Output PPN & Faktur Pajak Evidence | Mandatory `Faktur Pajak` / `SKB` evidence; irreversible posting |
| `ATH-CR-012` | `OD-09` | Customer Receipts, Unapplied Cash & AR Aging | Bank-statement-backed receipts; explicit allocation & compensating reversal |
| `ATH-CR-013` | `OD-11`, `OD-13` | Opening Balances Cutover & Locked Period Override | Balanced TB required; `LOCKED` period permanently frozen |
| `ATH-CR-014` | `OD-12` | Document Retention & External Sharing | Append-only immutability; authenticated 5-min signed tokens; zero public URLs |

## 3. Known Residual Risks & Operational Boundaries
1. **No Live Bank or DJP e-Faktur Integration:** Payment files are explicitly tagged `"SIMULATED FILE · ZERO LIVE BANK TRANSFER"`, and Indonesian tax evidence (`Faktur Pajak`, `SKB`, `Nota Retur`) is captured as auditable structured evidence rather than claiming live DJP filing.
2. **Backup Role Separation:** Because `FORCE ROW LEVEL SECURITY` constrains even table owner `athera_migrator`, scheduled `pg_dump` backups must execute under a dedicated `BYPASSRLS` backup role (documented and rehearsed in [LOCAL_AND_STAGING_OPERATIONS_RUNBOOK.md](../../runbooks/LOCAL_AND_STAGING_OPERATIONS_RUNBOOK.md)).
3. **Production Secret Provisioning:** Local defaults (`Security:DocumentDownloadSigningKey`, local demo password) must never be reused in staging/production; `--seed-demo` automatically refuses execution when `ASPNETCORE_ENVIRONMENT=Production`.

## 4. Human Product Owner Four-Pillar Sign-off (LEAVE BLANK UNTIL HUMAN REVIEW)
| Pillar | Evaluation Criteria | Status | Signer Name | Signature & Date |
| --- | --- | --- | --- | --- |
| **1. Business & Functional Fit** | P2P, O2C, Inventory, Indonesian Tax, Accounting & Reports match approved business scope and `OD-01`–`OD-13` resolutions | `[ ] ACCEPTED` `[ ] REVISIONS` | ___________________ | ___________________ |
| **2. Financial & Accounting Integrity** | Balanced GL, 5/5 control account reconciliations, immutability, compensating reversals, and period close controls verified | `[ ] ACCEPTED` `[ ] REVISIONS` | ___________________ | ___________________ |
| **3. Security, Tenancy & Recoverability** | Forced PG18 RLS (`109` tables), least-privilege `athera_app`, CSRF/same-origin sessions, signed document downloads, backup/restore verified | `[ ] ACCEPTED` `[ ] REVISIONS` | ___________________ | ___________________ |
| **4. Operational UX & Pilot Readiness** | Bilingual `id-ID`/`en-US` clarity, desktop/tablet/mobile polish, keyboard accessibility, and staging runbook approved | `[ ] ACCEPTED` `[ ] REVISIONS` | ___________________ | ___________________ |

**Overall Release Candidate Decision:** `[ ] APPROVED FOR STAGING PILOT   [ ] HOLD — SEE NOTES`  
**Product Owner Signature:** ___________________________   **Date:** ___________________________
