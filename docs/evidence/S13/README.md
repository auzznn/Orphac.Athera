# ATH-S13 Evidence Packet — Sales Reservation, Pick and Dispatch

- **Slice:** `S13` (`ATH-S13`)
- **Engineering Status:** `CODE_COMPLETE` / `ENGINEERING_PASS`
- **Product Owner Acceptance:** `PENDING` (requires human sign-off on the Orphac four-pillar acceptance record)
- **Design Contract:** [S13-design.md](../../design/slices/S13-design.md)
- **Open Decision Request:** [ATH-CR-010-OD10-OD09-sales-fulfillment-pick-dispatch-policy.md](../../decisions/ATH-CR-010-OD10-OD09-sales-fulfillment-pick-dispatch-policy.md) (`Status: OPEN`, Owner sign-off blank)

## Verification Summary (Real Commands & Logs)

| Check | Result | Evidence |
|---|---|---|
| `.NET Unit Tests` (`S00`–`S13`) | **PASS (`68/68`)** | [dotnet-test.log](dotnet-test.log) |
| `.NET PG18 Integration Tests` (`S00`–`S13`, real `athera_app` least-privilege RLS) | **PASS (`84/84`)** | [dotnet-test.log](dotnet-test.log) |
| Total `.NET` Suite | **PASS (`152/152`)** | [dotnet-test.log](dotnet-test.log) |
| OpenAPI Sync (`npm run api:check`) | **PASS (`0` diff)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| TypeScript + ESLint + Next.js Build | **PASS** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Vitest Component Suite (`S00`–`S13`) | **PASS (`14/14` files, `39/39` tests)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| `impeccable detect --json src` | **PASS (`[]`)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Playwright E2E Regression (`S00`–`S13`) | **PASS (`26/26` specs)** | [frontend-and-e2e.log](frontend-and-e2e.log) |
| Impeccable Finish Review (Pass 2) | **PASS (`disposition: ship`)** | [impeccable-review.md](impeccable-review.md) |
| Ponytail Engineering Review | **PASS** | [ponytail-review.md](ponytail-review.md) |
| Secrets Scan | **PASS (`SECRETS_SCAN_CLEAN`)** | [secrets-scan.log](secrets-scan.log) |

## PostgreSQL Migration & Rollback Instructions

- **Migration:** `20261118202955_M13_SalesReservationPickAndDispatch`
- **Schemas / Tables Added:** `fulfillment.pick_sequences`, `fulfillment.shipment_sequences`, `fulfillment.pick_orders`, `fulfillment.pick_lines`, `fulfillment.shipments`, `fulfillment.shipment_lines`.
- **Hardening Applied:** `ENABLE` + `FORCE ROW LEVEL SECURITY` on all 6 `fulfillment.*` tables, `fulfillment.guard_dispatched_shipment_immutability()`, `fulfillment.guard_dispatched_shipment_line_immutability()`, least-privilege `athera_app` grants (`SELECT, INSERT` only on `fulfillment.shipment_lines`; no `DELETE`), and updated `inventory.stock_movements` / `inventory.valuation_entries` check constraints for `'SALES_DISPATCH_OUTBOUND'`.
- **Apply:** `dotnet run --no-build --project src/Orphac.Athera.Api -- --migrate` (using `ConnectionStrings__Migrator`).
- **Rollback:** `dotnet ef database update 20261118193000_M12_CustomerSalesOrders --project src/Orphac.Athera.Modules --startup-project src/Orphac.Athera.Api` (verified clean `Down()` drops `fulfillment` objects and restores `inventory` check constraints).

## Known Defects & Risk Register

- **No open P0/P1/P2 defects.**
- **Policy Governance (`ATH-CR-010` — `OPEN`):** Safe defaults active (`UNRESTRICTED` stock only, no negative inventory, COGS recognized at physical dispatch `Dr 5100 / Cr 1300`, SO cancellation blocked once any reservation/dispatch exists). Requires Product Owner sign-off on `OD-10` / `OD-09` before production release.
- **UAT Script:** [uat-script.md](uat-script.md).
