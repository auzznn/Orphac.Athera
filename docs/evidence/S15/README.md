# ATH-S15 Evidence Packet — Customer Receipts, Allocation, AR Aging and Credits

- **Slice:** `S15` (`ATH-S15`)
- **Engineering Status:** `CODE_COMPLETE` / `ENGINEERING_PASS`
- **Product Owner Acceptance:** `PENDING` (requires human sign-off on the Orphac four-pillar acceptance record)
- **Design Contract:** [S15-design.md](../../design/slices/S15-design.md)
- **Open Decision Request:** [ATH-CR-012-OD09-customer-receipts-unapplied-and-ar-aging-policy.md](../../decisions/ATH-CR-012-OD09-customer-receipts-unapplied-and-ar-aging-policy.md) (`Status: OPEN`, Owner sign-off blank)

## Verification Summary (Real Commands & Logs)

| Check | Result | Evidence |
|---|---|---|
| `.NET Unit Tests` (`S00`–`S15`) | **PASS (`74/74`)** | [dotnet-test.log](dotnet-test.log) |
| `.NET PG18 Integration Tests` (`S00`–`S15`, real `athera_app` least-privilege RLS) | **PASS (`91/91`)** | [dotnet-test.log](dotnet-test.log) |
| Total `.NET` Suite | **PASS (`165/165`)** | [dotnet-test.log](dotnet-test.log) |
| OpenAPI Sync (`npm run api:check`) | **PASS (`0` diff)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| TypeScript + ESLint + Next.js Build | **PASS** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Vitest Component Suite (`S00`–`S15`) | **PASS (`16/16` files, `45/45` tests)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| `impeccable detect --json src` | **PASS (`[]`)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Playwright E2E Regression (`S00`–`S15`) | **PASS (`28/28` specs)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Finish Review (Pass 2) | **PASS (`disposition: ship`)** | [impeccable-review.md](impeccable-review.md) |
| Ponytail Engineering Review | **PASS** | [ponytail-review.md](ponytail-review.md) |
| Secrets Scan | **PASS (`SECRETS_SCAN_CLEAN`)** | [secrets-scan.log](secrets-scan.log) |

## PostgreSQL Migration & Rollback Instructions

- **Migration:** `20261119005242_M15_CustomerReceiptsAllocationAndArAging`
- **Tables Added (`ar` schema):** `ar.receipt_sequences`, `ar.customer_receipts`, `ar.receipt_allocations`.
- **Hardening Applied:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 3 tables, `ar.guard_reversed_receipt_immutability()`, partial unique index `ux_customer_receipts_active_bank_line`, and least-privilege `athera_app` grants (no `DELETE`).
- **Apply:** `dotnet run --no-build --project src/Orphac.Athera.Api -- --migrate` (using `ConnectionStrings__Migrator`).
- **Rollback:** `dotnet ef database update 20261119002526_M14_CustomerBillingTaxAndReceivables --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api`.

## Known Defects & Risk Register

- **No open P0/P1/P2 defects.**
- **Policy Governance (`ATH-CR-012` — `OPEN`):** Safe defaults active (unapplied receipts carried on `1200 AR Control` with explicit subledger reconciliation, direct cash refunds of unapplied funds blocked pending `OD-09`).
- **UAT Script:** [uat-script.md](uat-script.md).
