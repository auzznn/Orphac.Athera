# UAT Script — Slice S17: Documents, Evidence Trails & Operational UX

**Environment:** Local PostgreSQL 18 (`127.0.0.1:55433/athera_dev`) + ASP.NET Core 10 API (`http://127.0.0.1:5200`) + Next.js 16 (`http://127.0.0.1:3200`).  
**Primary Actor:** `budi.santoso@demo.athera.local` (Group Controller — write on `NUSA-JKT`, read-only on `NUSA-SBY`).

## Scenarios
1. **Upload & Link Business Document (`NUSA-JKT`):**
   - Navigate to `/w/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101/documents`.
   - Click `Unggah & Tautkan Bukti`, attach a valid PDF (`faktur-pajak-s17.pdf`), select `Bukti Pajak`, link to `INV-2026-00001`, and submit.
   - Verify `DOC-2026-NNNNN` appears with SHA-256 fingerprint badge, file size, and WIB timestamp.
2. **Signed Download & PDF Evidence Summary:**
   - Click `Unduh Aman` -> issues time-limited HMAC token and downloads exact file bytes with `X-Content-Type-Options: nosniff`.
   - Click `Ringkasan PDF` -> downloads valid `%PDF-1.4` summary containing linked document number and SHA-256 hash.
3. **Exceptions Queue & Outbox Retry:**
   - Switch to `Pengecualian & Outbox` tab -> inspect active exceptions (payment-blocked AP, pending approvals, failed outbox) and outbox diagnostics.
   - If a `FAILED` outbox message exists, click `Coba Ulang` -> requeues to `PENDING` with audit trail entry.
4. **Immutable Entity Audit Trail & CSV Export:**
   - Switch to `Jejak Audit Entitas` tab -> confirm `DOCUMENT_EVIDENCE_ATTACHED` appears.
   - Click `Ekspor CSV Audit` -> downloads entity-scoped audit CSV.
5. **Read-Only (`NUSA-SBY`) & Mobile (`390x844`):**
   - Switch to `NUSA-SBY` -> confirm read-only notice is visible and upload/retry actions are absent (`403` enforced on API).
   - Verify `390x844` mobile layout renders stacked key-value ledger rows with zero horizontal overflow.

## Product Owner Acceptance
- **Status:** `PENDING` (Owner sign-off required; open decision `ATH-CR-014` for `OD-12`).
