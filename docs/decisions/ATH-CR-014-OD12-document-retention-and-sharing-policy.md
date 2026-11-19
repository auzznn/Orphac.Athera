# ATH-CR-014 — Open Decision Request: Document Retention & External Sharing Policy (OD-12)

- **Decision ID:** `OD-12`
- **Slice:** `S17 — Documents, Evidence Trails and Operational UX`
- **Status:** `OPEN` (Awaiting Product Owner Sign-off)
- **Date Raised:** 2026-11-19

## Policy Question
1. **Statutory Retention & Legal Hold:** Under Indonesian tax and commercial law (UU KUP / UU PT), accounting and tax evidence (Faktur Pajak, Nota Retur, delivery proofs, bank statements) is typically retained for 10 years. Should Athera enforce a strict append-only non-deletion policy for all `documents.business_documents` and `documents.document_links`, or permit soft-archiving after a configured retention window?
2. **External / Public Link Sharing:** Should any business document ever be shareable via an unauthenticated public URL, or must all document downloads remain strictly authenticated + entity-authorized + time-limited signed tokens?

## Safe Engineering Default Implemented in S17
- **Append-Only Immutability:** `documents.business_documents` and `documents.document_links` are append-only (`SELECT, INSERT` only for `athera_app`, protected by PostgreSQL immutability triggers). No hard deletion is exposed.
- **No Public URLs:** Every download requires an authenticated session, verified tenant/entity authorization, and a short-lived (5-minute) HMAC-SHA256 signed download token bound to `(tenantId, entityId, documentId, actorUserId)`.

## Product Owner Sign-off
- **Decision:** `[ ] Approved as default   [ ] Revision required`
- **Product Owner Name & Signature:** ___________________________
- **Date:** ___________________________
