# ATH-LONG-HORIZON — Greenfield workstream state ledger

**Initial state:** 2026-11-17 | New repository / planning documents only | No code, tests, migrations or PO acceptance exist yet.

- Stack: Next.js 16.4.6 + ASP.NET Core 10 + PostgreSQL 18.
- Current target: **S07 — Valued Acceptance, Moving-Average Inventory and GRNI** (S00 `920bc78`, S01 `737971a`, S02 `57618ad`, S03 `287d7e5`, S04 `0fcb7b5`, S05 `d2bf6b5`, S06 `876a916` implemented/verified, PO acceptance pending).
- Current Git commit SHA: `876a916` (`greenfield/mvp`; baseline `e25008f`).
- PostgreSQL test instance: VERIFIED (`athera-greenfield-pg` healthy on `127.0.0.1:55433/athera_dev`; low-privilege `athera_app` + `athera_migrator` verified).
- Ponytail skill availability: AVAILABLE (`v5.3.3` `full`).
- Impeccable skill availability: AVAILABLE (`v4.9.1`).
- First Owner acceptance document: NOT SIGNED (S00–S06 PO acceptance packets staged under `docs/evidence/S00/README.md`, `docs/evidence/S01/README.md`, `docs/evidence/S02/README.md`, `docs/evidence/S03/README.md`, `docs/evidence/S04/README.md`, `docs/evidence/S05/README.md`, and `docs/evidence/S06/README.md`).

| Slice | Implementation | Engineering verification | PO acceptance | SHA | Blocker / evidence |
|---|---|---|---|---|---|
| S00 | CODE_COMPLETE | PASS | PENDING | 920bc78 | [S00 Evidence Packet](../evidence/S00/README.md) |
| S01 | CODE_COMPLETE | PASS | PENDING | 737971a | [S01 Evidence Packet](../evidence/S01/README.md) |
| S02 | CODE_COMPLETE | PASS | PENDING | 57618ad | [S02 Evidence Packet](../evidence/S02/README.md); [ATH-CR-001 (OD-02)](../decisions/ATH-CR-001-OD02-four-eyes-and-role-matrix.md) |
| S03 | CODE_COMPLETE | PASS | PENDING | 287d7e5 | [S03 Evidence Packet](../evidence/S03/README.md) |
| S04 | CODE_COMPLETE | PASS | PENDING | 0fcb7b5 | [S04 Evidence Packet](../evidence/S04/README.md); [ATH-CR-002 (OD-07/OD-13)](../decisions/ATH-CR-002-OD07-OD13-tax-and-fiscal-calendar.md) |
| S05 | CODE_COMPLETE | PASS | PENDING | d2bf6b5 | [S05 Evidence Packet](../evidence/S05/README.md); [ATH-CR-003 (OD-01)](../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) |
| S06 | CODE_COMPLETE | PASS | PENDING | 876a916 | [S06 Evidence Packet](../evidence/S06/README.md); [ATH-CR-003 (OD-01)](../decisions/ATH-CR-003-OD01-po-delivery-tolerance-policy.md) |
| S07 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S08 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S09 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S10 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S11 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S12 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S13 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S14 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S15 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S16 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S17 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
| S18 | NOT_STARTED | NOT_RUN | NOT_REQUESTED | — | — |
