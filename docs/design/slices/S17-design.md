# Slice S17 Design — Documents, Evidence Trails and Operational UX

- **Slice:** `ATH-S17`
- **Prerequisites:** `S02`, `S05`–`S16`
- **Authoritative Module Owners:** `Documents`, `Audit`, `Integration (Outbox)`, `Reporting`
- **Open Decisions:** `ATH-CR-014` (`OD-12` — retention & external sharing policy)

## 1. Domain & Database Ownership (`documents` schema)
- `documents.business_documents`:
  - `document_id` (PK), `tenant_id`, `legal_entity_id`, `document_number` (unique per entity, `DOC-YYYY-NNNNN`), `file_name`, `content_type`, `byte_size`, `sha256_hash`, `category` (`TAX_EVIDENCE`, `DELIVERY_PROOF`, `SUPPLIER_INVOICE`, `BANK_ADVICE`, `CONTRACT`, `GENERAL`), `scan_status` (`CLEAN`), `content_bytes` (`bytea`), `notes`, `uploaded_by_user_id`, `created_at_utc`.
- `documents.document_links`:
  - `link_id` (PK), `tenant_id`, `legal_entity_id`, `document_id` (FK -> `documents.business_documents`), `source_module`, `source_document_type`, `source_document_id`, `source_document_number`, `created_by_user_id`, `created_at_utc`.
- `documents.document_sequences`: per-entity sequence counter for `DOC-YYYY-NNNNN`.
- **PostgreSQL 18 Security & Invariants (`M17`):**
  - `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 3 `documents.*` tables (`tenant_id = platform.current_tenant_id()`).
  - Immutability triggers `documents.guard_business_document_immutability()` and `documents.guard_document_link_immutability()` reject `UPDATE`/`DELETE`.
  - `athera_app` granted `SELECT, INSERT` on `business_documents` and `document_links`, and `SELECT, INSERT, UPDATE` on `document_sequences`.
  - No attachment operation can mutate any posted GL, AP, AR, or Inventory record.

## 2. API Contract (`/api/workspaces/{tenantId}/entities/{entityId}/documents`)
| Method & Path | Permission | Behavior |
| --- | --- | --- |
| `GET /overview?q=&category=&sourceType=` | Any entity read permission | Returns documents + links, operational exception queue, entity audit timeline, and outbox diagnostics. |
| `POST /upload` | Entity write permission (`finance.journal.post`, `procurement.manage`, `ap.manage`, `sales.manage`, `inventory.manage`, or `admin.manage`) | Validates allowlisted MIME types (`application/pdf`, `image/png`, `image/jpeg`, `text/csv`, `text/plain`), max 5 MB, rejects executable magic bytes (`422 unsafe_file_content`), computes SHA-256, persists document + link + `AuditEvent`. |
| `GET /{documentId}/download-token` | Any entity read permission | Issues a 5-minute HMAC-SHA256 signed download token bound to `(tenantId, entityId, documentId, actorUserId)`. |
| `GET /{documentId}/download?token=...` | Any entity read permission + valid signed token | Verifies session, tenant/entity authorization, and HMAC token before streaming bytes (`Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`). |
| `GET /evidence-pack/{sourceType}/{sourceId}/summary.pdf` | Any entity read permission | Generates a deterministic valid `%PDF-1.4` evidence summary containing entity scope, linked documents with SHA-256 hashes, and related audit events. |
| `GET /audit-export.csv` | Any entity read permission | Streams entity-scoped CSV of `audit.audit_events`. |
| `POST /outbox/{messageId}/retry` | `admin.manage` or `finance.config.manage` | Requeues a `FAILED` outbox message in the authorized entity back to `PENDING` and appends an `AuditEvent`. |

## 3. UI & Operational UX (`/w/[tenantId]/[entityId]/documents`)
- Built strictly with official `globals.css` tokens (`--bg-surface`, `--bg-subtle`, `--bg-chrome`, `--border-hairline`, `--border-strong`, `--status-*`, `--radius-md`/`--radius-sm`, zero drop shadows).
- 4 baseline-aligned KPIs, collapsible `OD-12` notice, 3 ARIA-wired segmented tabs (`Dokumen & Bukti`, `Antrean Pengecualian & Outbox`, `Jejak Audit Entitas`), inline Upload & Link Evidence drawer, signed download & PDF summary buttons, CSV audit export, outbox retry button, read-only enforcement on `NUSA-SBY`, and stacked mobile `390px` ledger rows.

## 4. Mandatory Tests
- **Unit (`S17DocumentsUnitTests`):** MIME/size/magic-byte validation, SHA-256 hashing, HMAC download token signing/tamper/expiry checks, deterministic PDF `%PDF-1.4` structure check.
- **Real PG18 Integration (`S17DocumentsPostgresTests`):** RLS + immutability trigger verification under `athera_app`, upload -> signed download -> forged cross-tenant/cross-user token denial (`403`/`404`), PDF content check, CSV audit export scope check, outbox `FAILED` -> `PENDING` retry, and realistic 50-document query/filter load test.
- **Frontend Vitest & Playwright E2E:** Upload evidence, verify SHA-256 & linked source, download PDF summary, inspect Exceptions & Audit tabs, verify `NUSA-SBY` read-only & `390x844` mobile layout.
