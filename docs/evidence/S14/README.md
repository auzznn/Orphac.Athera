# ATH-S14 Evidence Packet — Customer Billing, Tax and Accounts Receivable

- **Slice:** `S14` (`ATH-S14`)
- **Engineering Status:** `CODE_COMPLETE` / `ENGINEERING_PASS`
- **Product Owner Acceptance:** `PENDING` (requires human sign-off on the Orphac four-pillar acceptance record)
- **Design Contract:** [S14-design.md](../../design/slices/S14-design.md)
- **Open Decision Request:** [ATH-CR-011-OD07-OD09-customer-billing-tax-and-ar-policy.md](../../decisions/ATH-CR-011-OD07-OD09-customer-billing-tax-and-ar-policy.md) (`Status: OPEN`, Owner sign-off blank)

## Verification Summary (Real Commands & Logs)

| Check | Result | Evidence |
|---|---|---|
| `.NET Unit Tests` (`S00`–`S14`) | **PASS (`71/71`)** | [dotnet-test.log](dotnet-test.log) |
| `.NET PG18 Integration Tests` (`S00`–`S14`, real `athera_app` least-privilege RLS) | **PASS (`88/88`)** | [dotnet-test.log](dotnet-test.log) |
| Total `.NET` Suite | **PASS (`159/159`)** | [dotnet-test.log](dotnet-test.log) |
| OpenAPI Sync (`npm run api:check`) | **PASS (`0` diff)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| TypeScript + ESLint + Next.js Build | **PASS** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Vitest Component Suite (`S00`–`S14`) | **PASS (`15/15` files, `42/42` tests)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| `impeccable detect --json src` | **PASS (`[]`)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Playwright E2E Regression (`S00`–`S14`) | **PASS (`27/27` specs)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Finish Review (Pass 2) | **PASS (`disposition: ship`)** | [impeccable-review.md](impeccable-review.md) |
| Ponytail Engineering Review | **PASS** | [ponytail-review.md](ponytail-review.md) |
| Secrets Scan | **PASS (`SECRETS_SCAN_CLEAN`)** | [secrets-scan.log](secrets-scan.log) |

## PostgreSQL Migration & Rollback Instructions

- **Migration:** `20261119002526_M14_CustomerBillingTaxAndReceivables`
- **Schemas / Tables Added:** `billing.invoice_sequences`, `billing.credit_note_sequences`, `billing.customer_invoices`, `billing.customer_invoice_lines`, `ar.receivable_sequences`, `ar.customer_receivables`.
- **Hardening Applied:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 6 tables, `billing.guard_posted_invoice_immutability()`, `billing.guard_posted_invoice_line_immutability()`, least-privilege `athera_app` grants (`SELECT, INSERT` only on `billing.customer_invoice_lines`; no `DELETE`), and `pg_advisory_xact_lock` shipment-line serialization preventing double billing.
- **Apply:** `dotnet run --no-build --project src/Orphac.Athera.Api -- --migrate` (using `ConnectionStrings__Migrator`).
- **Rollback:** `dotnet ef database update 20261118202955_M13_SalesReservationPickAndDispatch --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (clean `Down()` drops triggers, functions, tables, and `billing`/`ar` schemas).

## Known Defects & Risk Register

- **No open P0/P1/P2 defects.**
- **Policy Governance (`ATH-CR-011` — `OPEN`):** Safe defaults active (shipment-backed billing only, `OD-07` mandatory Faktur Pajak / SKB / Nota Retur evidence, zero automated DJP filing, immutable posted invoices corrected via linked Credit Notes). Requires Product Owner sign-off before production release.
- **UAT Script:** [uat-script.md](uat-script.md).
