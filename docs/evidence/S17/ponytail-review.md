# Ponytail Review — Slice S17 (Documents, Evidence Trails & Operational UX)

- **Level:** `full`
- **Scope:** `DocumentsModels.cs`, `DocumentsService.cs`, `M17_DocumentsAndEvidenceTrails.cs`, `DocumentsEndpoints.cs`, `DocumentsContracts.cs`, `apps/web/src/app/(shell)/w/[tenantId]/[entityId]/documents/page.tsx`, tests.

## Engineering & Minimalism Review
1. **Zero external PDF binary dependencies:**
   - Implemented deterministic `%PDF-1.4` evidence-pack generation in `DocumentSecurity.BuildSimplePdf` (~70 lines of pure C#) rather than adding heavyweight browser/PDF renderer dependencies. Verified with unit + PG18 integration tests asserting `%PDF-1.4`, `%%EOF`, document number, and SHA-256 presence.
2. **Defense-in-depth authorization on every file access:**
   - No public bucket URLs. Files are stored in `documents.business_documents.content_bytes` (`bytea`) protected by `ENABLE` + `FORCE ROW LEVEL SECURITY` (`tenant_id = platform.current_tenant_id()`), least-privilege `SELECT, INSERT` for `athera_app`, and `BEFORE UPDATE OR DELETE` immutability triggers (`OD-12`).
   - Every download validates session auth + entity read permission + short-lived (5-minute) HMAC-SHA256 signed token bound to `(tenantId, entityId, documentId, actorUserId)` and sets `X-Content-Type-Options: nosniff`.
3. **Upload safety & no financial mutation:**
   - Rejects disallowed MIME types, files > 5 MB (`413`), and executable/script magic bytes (`MZ`, `ELF`, `#!`, `<script`, `<html`) with `422 unsafe_file_content`.
   - Uploading or linking evidence never mutates any posted GL, AP, AR, or Inventory record.
4. **Operational UX & Performance:**
   - Verified realistic 50-document insert + filtered join query under `athera_app` completes well under 5 seconds (real PG18).
   - Built with official `globals.css` tokens, zero drop shadows, icons on all status chips, ARIA-wired tabs/tabpanels, and stacked mobile `390px` ledger rows.
