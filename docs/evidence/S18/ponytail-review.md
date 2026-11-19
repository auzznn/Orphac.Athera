# Ponytail Review — Slice S18 (Cross-Module UAT, Security & Release Candidate)

- **Level:** `full`
- **Scope:** Zero new business tables or migrations (`M00`–`M17` frozen); `S18ReleaseCandidatePostgresTests.cs`, `s18-release-candidate-uat.spec.ts`, `LOCAL_AND_STAGING_OPERATIONS_RUNBOOK.md`, real PG18 backup/restore & rollback drills, dependency/secrets audits, and unsigned `PO-FOUR-PILLAR-ACCEPTANCE-PACKET.md`.

## Key Verifications & Hardening Findings
1. **Complete PostgreSQL 18 RLS & Role Audit (`S18_SEC_01`):**
   - Verified `109` tenant-scoped tables across all module schemas enforce BOTH `relrowsecurity = true` and `relforcerowsecurity = true` (zero unprotected tenant-scoped tables).
   - Verified `athera_app` has `rolsuper = false`, `rolbypassrls = false`, and owns `0` tables.
   - Verified 14 module API endpoints reject unauthenticated (`401`), foreign-tenant (`403`/`404`), and unassigned (`403`/`404`) callers with zero leakage.
2. **Real Concurrency Soak (`S18_CONCURRENCY_01`):**
   - 12 parallel GL journal postings (6 unique keys × 2 duplicate retries) produced zero `500` errors, zero duplicate journals, and an exact balanced Trial Balance (`1,210,000.00` Dr == `1,210,000.00` Cr).
   - 8 parallel stock reservations (4 PCS each against 10 PCS available) allowed exactly 2 (`8 PCS <= 10 PCS`) and cleanly rejected 6 (`409 Conflict`), proving zero negative/over-reserved stock under contention.
3. **Real Operational Discovery During Backup Drill:**
   - Rehearsing `pg_dump` under `athera_migrator` surfaced that `FORCE ROW LEVEL SECURITY` blocks `COPY`/`pg_dump` even for the table owner. Updated the runbook to mandate a dedicated `BYPASSRLS` backup principal and verified forced-RLS parity (`109` tables) plus four row counts (tenants `2`, entities `3`, journals `32`, documents `1`) after `pg_restore`. The `M17 -> M16 -> M17` down/up rehearsal verified table drop/recreate before reapply — not row preservation after downgrade.
4. **Zero Vulnerable Dependencies & Clean Secrets Scan:**
   - `dotnet list Orphac.Athera.sln package --vulnerable --include-transitive`: 0 vulnerable packages.
   - `npm audit --omit=dev`: 0 vulnerabilities.
   - Secrets scan: 0 matching files — scoped pattern scan of working-tree text only ([secrets-scan.log](secrets-scan.log)); no git-history or entropy coverage.
