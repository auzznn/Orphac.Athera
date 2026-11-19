# Manual UAT & Release Candidate Demonstration Script — Slice S18

**Environment:** Local PostgreSQL 18 (`127.0.0.1:55433/athera_dev`), ASP.NET Core 10 API (`http://127.0.0.1:5200`), Next.js 16 (`http://127.0.0.1:3200`).  
**Demo Password:** `AtheraDemo!2026` (local synthetic fixtures only).

## Demonstration Walkthrough
1. **End-to-End Procure-to-Pay (P2P):**
   - Sign in as `rina.utami@demo.athera.local` (`NUSA-JKT`): create/submit PO -> sign in as `budi.santoso@demo.athera.local` to independently approve PO (`OD-02`) -> Gate 1 dock receipt (0 GL) -> Gate 2 valued acceptance (`Dr 1300 / Cr 2110`) -> `siti.rahayu@demo.athera.local` drafts 3-way matched AP bill -> `budi` approves/posts (`Dr 2110 / Dr 1150 / Cr 2100`) -> simulate payment file (0 GL) -> independently release (`Dr 2100 / Cr 1120`) -> import bank debit CSV & reconcile (`Dr 1120 / Cr 1110`).
2. **End-to-End Order-to-Cash (O2C):**
   - Sign in as `dewi.lestari@demo.athera.local` (`NUSA-JKT`): create/submit SO with live stock availability & Output PPN preview -> `budi` approves SO -> reserve `UNRESTRICTED` stock, pick, and dispatch with atomic weighted-average COGS (`Dr 5100 / Cr 1300`) -> bill customer with `Faktur Pajak` (`Dr 1200 / Cr 4100 / Cr 2210`) -> import inbound bank credit CSV, confirm receipt & allocate to invoice (`Dr 1110 / Cr 1200`) -> inspect AR Aging.
3. **Accounting Reports, Control Reconciliations, Period Close & Evidence Trail:**
   - Open `/reports` as `budi`: verify Trial Balance, P&L, Balance Sheet, and 5/5 Control Reconciliations (`1120`, `1200`, `1300`, `2110`, `2100`), drill down to GL journals, export CSV, close & reopen period `2026-11`.
   - Open `/documents`: attach PDF evidence (`SHA-256`), download via signed token, export evidence summary PDF & audit CSV.
4. **Security & Isolation Negative Checks:**
   - Verify `hendra.wijaya@demo.athera.local` (unassigned) sees default-deny workspace and receives `403`/`404` on entity APIs.
   - Verify `budi` on `NUSA-SBY` operates in strict read-only mode with mutation controls withheld and `403` enforced server-side.
   - Verify cross-tenant requests targeting `SMDR` return `403`/`404`.
5. **Backup/Restore & Rollback Drill:**
   - Follow [LOCAL_AND_STAGING_OPERATIONS_RUNBOOK.md](../../runbooks/LOCAL_AND_STAGING_OPERATIONS_RUNBOOK.md) Sections 3–4 (evidence logged in [backup-restore-and-migration-drill.log](backup-restore-and-migration-drill.log)).
