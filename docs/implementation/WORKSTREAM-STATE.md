# ATH-LONG-HORIZON — Greenfield workstream state ledger

**Initial state:** 2026-11-17 | New repository / planning documents only | No code, tests, migrations or PO acceptance exist yet.

- Stack: Next.js 16.4.6 + ASP.NET Core 10 + PostgreSQL 18.
- Current target: **S16 — Accounting Reports, Reconciliations and Period Close** (S00 `920bc78`, S01 `737971a`, S02 `57618ad`, S03 `287d7e5`, S04 `0fcb7b5`, S05 `d2bf6b5`, S06 `876a916`, S07 `e64f46b`, S08 `606f138`, S09 `b42150e`, S10 `0d7a969`, S11 `1b84521`, S12 `d60e309`, S13 `2633364`, S14 `b4625e8`, S15 `20e0ab2` implemented/verified, PO acceptance pending).
- Current Git commit SHA: `20e0ab2` (`greenfield/mvp`; baseline `e25008f`).
- PostgreSQL test instance: VERIFIED (`athera-greenfield-pg` healthy on `127.0.0.1:55433/athera_dev`; low-privilege `athera_app` + `athera_migrator` verified).
- Ponytail skill availability: AVAILABLE (`v5.3.3` `full`).
- Impeccable skill availability: AVAILABLE (`v4.9.1`).
- First Owner acceptance document: NOT SIGNED (S00–S15 PO acceptance packets staged under `docs/evidence/S00/README.md` through `docs/evidence/S15/README.md`).

| Slice | Implementation | Engineering verification | PO acceptance | SHA | Blocker / evidence |
|---|---|---|---|---|---|
| S00 | CODE_COMPLETE | PASS | PENDING | 920bc78 | [S00 Evidence Packet](../evidence/S00/README.md) |
| S01 | CODE_COMPLETE | PASS | PENDING | 737971a | [S01 Evidence Packet](../evidence/S01/README.md) |
| S02 | CODE_COMPLETE | PASS | PENDING | 57618ad | [S02 Evidence Packet](../evidence/S02/README.md); [ATH-CR-001 (OD-02)](../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md) |
| S03 | CODE_COMPLETE | PASS | PENDING | 287d7e5 | [S03 Evidence Packet](../evidence/S03/README.md) |
| S04 | CODE_COMPLETE | PASS | PENDING | 0fcb7b5 | [S04 Evidence Packet](../evidence/S04/README.md); [ATH-CR-002 (OD-07/OD-13)](../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md) |
| S05 | CODE_COMPLETE | PASS | PENDING | d2bf6b5 | [S05 Evidence Packet](../evidence/S05/README.md); [ATH-CR-003 (OD-01)](../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) |
| S06 | CODE_COMPLETE | PASS | PENDING | 876a916 | [S06 Evidence Packet](../evidence/S06/README.md); [ATH-CR-003 (OD-01)](../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) |
| S07 | CODE_COMPLETE | PASS | PENDING | e64f46b | [S07 Evidence Packet](../evidence/S07/README.md); [ATH-CR-004 (OD-04)](../decisions/ATH-CR-004-OD04-moving-average-valuation-edge-cases.md) |
| S08 | CODE_COMPLETE | PASS | PENDING | 606f138 | [S08 Evidence Packet](../evidence/S08/README.md); [ATH-CR-005 (OD-10)](../decisions/ATH-CR-005-OD10-negative-stock-and-reservation-policy.md) |
| S09 | CODE_COMPLETE | PASS | PENDING | b42150e | [S09 Evidence Packet](../evidence/S09/README.md); [ATH-CR-006 (OD-03/OD-08)](../decisions/ATH-CR-006-OD03-OD08-invoice-match-tolerances-and-ap-scope.md) |
| S10 | CODE_COMPLETE | PASS | PENDING | 0d7a969 | [S10 Evidence Packet](../evidence/S10/README.md); [ATH-CR-007 (OD-05/OD-07)](../decisions/ATH-CR-007-OD05-OD07-supplier-returns-replacements-and-credits.md) |
| S11 | CODE_COMPLETE | PASS | PENDING | 1b84521 | [S11 Evidence Packet](../evidence/S11/README.md); [ATH-CR-008 (OD-06)](../decisions/ATH-CR-008-OD06-payment-release-and-bank-clearing.md) |
| S12 | CODE_COMPLETE | PASS | PENDING | d60e309 | [S12 Evidence Packet](../evidence/S12/README.md); [ATH-CR-009 (OD-09)](../decisions/ATH-CR-009-OD09-sales-order-commercial-terms-and-credit-policy.md) |
| S13 | CODE_COMPLETE | PASS | PENDING | 2633364 | [S13 Evidence Packet](../evidence/S13/README.md); [ATH-CR-010 (OD-10/OD-09)](../decisions/ATH-CR-010-OD10-OD09-sales-fulfillment-pick-dispatch-policy.md) |
| S14 | CODE_COMPLETE | PASS | PENDING | b4625e8 | [S14 Evidence Packet](../evidence/S14/README.md); [ATH-CR-011 (OD-07/OD-09)](../decisions/ATH-CR-011-OD07-OD09-customer-billing-tax-and-ar-policy.md) |
| S15 | CODE_COMPLETE | PASS | PENDING | 20e0ab2 | [S15 Evidence Packet](../evidence/S15/README.md); [ATH-CR-012 (OD-09)](../decisions/ATH-CR-012-OD09-customer-receipts-unapplied-and-ar-aging-policy.md) |
| S16 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S17 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S18 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
