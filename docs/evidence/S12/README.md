# ATH-S12 — Engineering Evidence Packet: Customer Sales Orders and Commercial Commitments

**Slice:** S12 — Customer Sales Orders and Commercial Commitments  
**Branch:** `greenfield/mvp`  
**Engineering Status:** `CODE_COMPLETE`  
**Verification Status:** `ENGINEERING_PASS`  
**Product Owner Acceptance:** `PENDING` (awaiting human four-pillar sign-off)

---

## 1. Deliverables & Traceability

- **Open Decision Record (`OD-09`)**: [`docs/decisions/ATH-CR-009-OD09-sales-order-commercial-terms-and-credit-policy.md`](../../decisions/ATH-CR-009-OD09-sales-order-commercial-terms-and-credit-policy.md) (`Status: OPEN`, Owner sign-off blank; safe defaults enforce `discount_amount == 0` and block automated credit-limit overrides).
- **Slice Design Document**: [`docs/design/slices/S12-design.md`](../../design/slices/S12-design.md).
- **Domain & Coordinator (`Orphac.Athera.Modules.Sales`)**:
  - `src/Orphac.Athera.Modules/Sales/SalesOrderModels.cs`
  - `src/Orphac.Athera.Modules/Sales/SalesOrderCoordinator.cs`
  - `src/Orphac.Athera.Modules/Inventory/InventoryAvailabilityService.cs` (`GetItemAvailabilityMapAsync`)
  - `src/Orphac.Athera.Modules/Inventory/InventoryValuationService.cs` (`sales.sales_order_sequences`)
  - `src/Orphac.Athera.Modules/Identity/{EntityAccessGrant.cs,RoleTemplates.cs}` (`sales.manage`, `sales.approve`)
- **PostgreSQL 18 Migration (`M12`)**:
  - `src/Orphac.Athera.Modules/Persistence/Migrations/20261118193048_M12_CustomerSalesOrdersAndCommercialCommitments.cs` (`sales.sales_order_sequences`, `sales.sales_orders`, `sales.sales_order_revisions`, `sales.sales_order_lines`, `ENABLE`/`FORCE ROW LEVEL SECURITY`, `sales.guard_frozen_so_revision_snapshot()`, `sales.guard_frozen_so_revision_lines()`, `athera_app` least-privilege grants).
- **API, Seeder & OpenAPI Contract**:
  - `src/Orphac.Athera.Api/Contracts/SalesOrdersContracts.cs`
  - `src/Orphac.Athera.Api/Endpoints/SalesOrdersEndpoints.cs`
  - `src/Orphac.Athera.Api/Cli/DemoDataSeeder.S12.cs`
  - `docs/contracts/openapi.v1.json` & `apps/web/src/lib/api/schema.d.ts`
- **Next.js 16.4.6 Workbench**:
  - `apps/web/src/app/(shell)/w/[tenantId]/[entityId]/sales-orders/page.tsx`
  - `apps/web/src/components/sales/SalesOrderDetailSheet.tsx`
  - `apps/web/src/components/AppShell.tsx`
  - `apps/web/src/lib/api/client.ts`
  - `apps/web/src/lib/i18n/messages.ts`

---

## 2. Executable Verification Summary

| Suite | Command | Result | Evidence Log |
|---|---|---|---|
| **.NET Unit + PostgreSQL 18 Integration** | `dotnet test Orphac.Athera.sln --logger "console;verbosity=normal"` | `65/65` Unit + `79/79` PG18 Integration = **`144/144` Passed** (`0` failures, `0` skips) | [`dotnet-test.log`](./dotnet-test.log) |
| **OpenAPI Sync, TypeScript, ESLint, Vitest, Next Build & Full Playwright E2E (`S00`–`S12`)** | `npm run api:check && npm run typecheck && npm run lint && npx vitest run && npm run build && npx --yes impeccable detect --json src && npm run e2e` | `13/13` Vitest files (**`36/36` tests**), `impeccable detect` = `[]`, **`25/25` Playwright E2E specs Passed** | [`frontend-and-e2e.log`](./frontend-and-e2e.log) |
| **Ponytail Engineering Review (`full`)** | Domain & invariant audit | **`PASS`** | [`ponytail-review.md`](./ponytail-review.md) |
| **Impeccable UX Finish Review** | Automated detector + `impeccable-finish-reviewer` subagent (`471bb8cd` -> `edd66f17`) | **`disposition: ship`** | [`impeccable-review.md`](./impeccable-review.md) & [`screenshots/`](./screenshots/) |
| **Manual UAT Script** | Step-by-step multi-persona walkthrough (`dewi.lestari` & `budi.santoso`) | Ready for PO execution | [`uat-script.md`](./uat-script.md) |
| **Secrets & Credential Hygiene Scan** | Tracked working tree scan | **`SECRETS_SCAN_CLEAN`** | [`secrets-scan.log`](./secrets-scan.log) |

---

## 3. Migration & Recovery Instructions

- **Apply forward**:
  ```bash
  dotnet run --project src/Orphac.Athera.Api -- --migrate
  ```
- **Rollback (`M12` Down)**:
  `20261118193048_M12_CustomerSalesOrdersAndCommercialCommitments.Down` drops the immutability triggers/functions (`sales.guard_frozen_so_revision_lines()`, `sales.guard_frozen_so_revision_snapshot()`) and the 4 `sales.*` tables plus schema cleanly.

---

## 4. Known Defects, Open Risks & Sign-Off Boundary

- **Known Defects**: None (`0` open defects).
- **Open Product Owner Decision (`OD-09` / `ATH-CR-009`)**: Sales line discounts (`> 0`) and automated credit-limit overrides remain locked in safe-default mode (`409 Conflict`) pending Product Owner policy sign-off.
- **Sign-Off Boundary**: Product Owner acceptance remains **`PENDING`**.
