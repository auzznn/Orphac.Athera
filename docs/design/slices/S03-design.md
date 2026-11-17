# ATH-S03 Design — Partners, Catalogue and Unit of Measure

**Slice:** S03 (`docs/implementation/slices/S03_partners_catalogue_and_unit_of_measure.md`)  
**Status:** Workstream-authorized engineering scope; Product Owner acceptance pending  
**Prerequisites:** S01 (`737971a`), S02 (`57618ad`)  
**Related Open Decision:** `OD-07` (`ATH-OPEN-DECISIONS-001.md` — partner `TaxRegistrationNpwp` and `TaxPkpStatus` are stored strictly as master-data metadata pending statutory Indonesian tax rules in S04)

---

## 1. Business Behavior & User Journey
1. **Tenant-Shared Business Partners (`partners.read` / `partners.manage`):** An authorized purchasing/sales user creates and maintains tenant-wide Partners capable of acting as **Supplier**, **Customer**, or **both** (`is_supplier`, `is_customer`), attaches typed contacts (`COMMERCIAL`, `BILLING`, `RECEIVING_DISPATCH`), and controls per-entity operational eligibility (`ALL_ENTITIES` default vs. `RESTRICTED_ENTITIES` opt-in allowlist via `PartnerEntityProfile`) without duplicating partner identity across legal entities.
2. **Tenant-Shared Item Catalogue & Exact UoM (`catalog.read` / `catalog.manage`):** An authorized user maintains reusable `UnitsOfMeasure` (`QuantityPrecisionScale` `0..6`), creates `STOCKED_GOOD` or `NON_STOCK_SERVICE` items with an authoritative `BaseUomId` plus default purchase/sales UoMs, defines explicit positive conversion factors (`1 BOX = 24 PCS`, `factor_to_base > 0`, strict `decimal`), and restricts purchase/sales usage per legal entity (`ALL_ENTITIES` vs. `RESTRICTED_ENTITIES` via `ItemEntityPolicy`).
3. **Entity-Specific Eligibility & Deactivation:** Master records are never hard-deleted (`DELETE` is revoked from runtime role `athera_app`). Deactivating a partner or item (`INACTIVE`) immediately marks effective entity eligibility `false` for new operations while preserving referential history and audit trails. Downstream slices (`S05+`) capture immutable `ItemTransactionSnapshot` values rather than mutating historical documents when master definitions evolve.
4. **Live Conversion Preview (`convert-preview`):** Users verify exact UoM conversions (`QuantityInUom -> BaseQuantity`) in decimal arithmetic, rejecting unconfigured UoMs (`unsupported_uom_conversion`) and fractional quantities targeting discrete `scale = 0` base units (`fractional_discrete_base_quantity`).

### Explicit Non-Goals
- **No current stock quantity or location balance** (owned by `inventory` in S07–S08).
- **No moving-average cost or valuation fields** (owned by `inventory` / `gl` in S07).
- **No statutory tax rate computation or filing logic** (owned by `tax` in S04 / S09 / S14; `OD-07`).
- **No foundry/manufacturing-specific attributes** (strictly industry-neutral trading/distribution core).

---

## 2. Module Ownership & Bounded Contexts (`ATH-DOMAIN-BOUNDARIES-001`)

| Module | Authoritative Tables / Services | Owns | Must NOT Own |
|---|---|---|---|
| `Partners` | `partners.partners`, `partners.partner_contacts`, `partners.partner_entity_profiles`, `PartnerEligibilityEvaluator` | Partner identity, dual supplier/customer role flags, contacts, metadata-only tax profile (`NPWP`/`PKP`), per-entity eligibility policy | AP/AR balances, open POs, statutory tax calculations |
| `Catalog` | `catalog.units_of_measure`, `catalog.items`, `catalog.item_uom_conversions`, `catalog.item_entity_policies`, `UomConverter`, `ItemTransactionSnapshot`, `ItemEligibilityEvaluator` | Item master identity, stocked vs service kind, UoMs, exact `decimal` conversions, per-entity purchase/sales eligibility | Stock on hand, warehouse bins, moving-average unit cost |
| `Identity` (extension) | `PermissionCodes`, `RoleTemplates` | `partners.read`, `partners.manage`, `catalog.read`, `catalog.manage`, `SALES_OFFICER`, extended `PROCUREMENT_OFFICER` / `INVENTORY_CLERK` | Master-data entity records |
| `Audit` / `Integration` (consumers via endpoint tx) | `AuditEvent`, `OutboxMessage` | Append-only master-data change evidence (`PARTNER_CREATED`, `ITEM_CREATED`, etc.) | Master-data validation rules |

---

## 3. Deterministic Eligibility & UoM Conversion Rules

### Partner & Item Entity Eligibility (`PartnerEligibilityEvaluator` / `ItemEligibilityEvaluator`)
- Master `Status == 'INACTIVE'` -> effective eligibility is always `false`.
- `EntityScopeMode == 'ALL_ENTITIES'`:
  - Partner direction (`supplier` / `customer`) is `true` iff role flag (`IsSupplier` / `IsCustomer`) is `true` AND there is no `PartnerEntityProfile` for `(TenantId, LegalEntityId, PartnerId)` overriding that flag to `false`.
  - Item direction (`purchase` / `sales`) is `true` iff there is no `ItemEntityPolicy` for `(TenantId, LegalEntityId, ItemId)` overriding `PurchaseAllowed` / `SalesAllowed` to `false`.
- `EntityScopeMode == 'RESTRICTED_ENTITIES'` (fail-closed across sibling entities):
  - Partner direction is `true` iff role flag is `true` AND an explicit `PartnerEntityProfile` exists for `(TenantId, LegalEntityId, PartnerId)` with that direction set to `true`.
  - Item direction is `true` iff an explicit `ItemEntityPolicy` exists for `(TenantId, LegalEntityId, ItemId)` with that direction set to `true`.

### Exact UoM Arithmetic (`UomConverter`)
1. `quantity > 0m` required for positive conversion checks (or `>= 0m` only if zero preview rejected cleanly — S03 enforces `quantity > 0m`, rejecting `<= 0` with `400 invalid_quantity`).
2. If `uomId == item.BaseUomId`, factor is implicitly `1m`.
3. Otherwise, an `ACTIVE` `ItemUom` row for `(TenantId, ItemId, UomId)` is mandatory; absent or `INACTIVE` throws/returns `400 unsupported_uom_conversion` (never guesses global UoM-to-UoM conversions).
4. `exactBase = quantity * factorToBase` (`decimal` `numeric(18,6)`):
   - If `baseUom.QuantityPrecisionScale == 0` and `exactBase != decimal.Truncate(exactBase)` -> reject `400 fractional_discrete_base_quantity`.
   - Otherwise `roundedBase = decimal.Round(exactBase, baseUom.QuantityPrecisionScale, MidpointRounding.AwayFromZero)` with `RoundingResidual = exactBase - roundedBase`.
5. `ItemTransactionSnapshot.Capture(...)` materializes an immutable value record (`ItemId`, `ItemCode`, `ItemName`, `ItemKind`, `BaseUomCode`, `TransactionUomCode`, `FactorToBase`, `ConvertedBaseQuantity`, `CapturedAtUtc`) at the moment of preview/consumption.

---

## 4. Database Schema & Forced RLS (`M03_PartnersCatalogueAndUom`)

1. **`partners.partners`**: `partner_id uuid PK`, `tenant_id uuid`, `partner_code varchar(32)`, `legal_name varchar(200)`, `display_name varchar(160)`, `is_supplier bool`, `is_customer bool`, `tax_registration_npwp varchar(64) NULL`, `tax_pkp_status varchar(16)` (`UNKNOWN`/`PKP`/`NON_PKP`), `entity_scope_mode varchar(32)` (`ALL_ENTITIES`/`RESTRICTED_ENTITIES`), `status varchar(16)` (`ACTIVE`/`INACTIVE`), `created_at_utc`, `updated_at_utc`, `deactivated_at_utc NULL`, `version bigint DEFAULT 1`. AK `(tenant_id, partner_id)`, unique `(tenant_id, partner_code)`, FK `tenant_id -> platform.tenants` (`RESTRICT`), check `is_supplier OR is_customer`.
2. **`partners.partner_contacts`**: `contact_id uuid PK`, `tenant_id uuid`, `partner_id uuid`, `contact_name varchar(160)`, `contact_role varchar(32)` (`COMMERCIAL`/`BILLING`/`RECEIVING_DISPATCH`), `email varchar(256) NULL`, `phone varchar(64) NULL`, `is_primary bool`, `status varchar(16)`, `created_at_utc`. Composite FK `(tenant_id, partner_id) -> partners.partners` (`RESTRICT`).
3. **`partners.partner_entity_profiles`**: `profile_id uuid PK`, `tenant_id uuid`, `legal_entity_id uuid`, `partner_id uuid`, `supplier_eligible bool`, `customer_eligible bool`, `default_payment_terms_days int` (`0..365`), `notes varchar(300) NULL`, `updated_at_utc`, `version bigint DEFAULT 1`. Unique `(tenant_id, legal_entity_id, partner_id)`, composite FKs to `platform.legal_entities` and `partners.partners` (`RESTRICT`).
4. **`catalog.units_of_measure`**: `uom_id uuid PK`, `tenant_id uuid`, `uom_code varchar(16)`, `name varchar(100)`, `quantity_precision_scale int` (`0..6`), `status varchar(16)` (`ACTIVE`/`INACTIVE`), `created_at_utc`, `updated_at_utc`, `version bigint DEFAULT 1`. AK `(tenant_id, uom_id)`, unique `(tenant_id, uom_code)`.
5. **`catalog.items`**: `item_id uuid PK`, `tenant_id uuid`, `item_code varchar(32)`, `name varchar(200)`, `item_kind varchar(24)` (`STOCKED_GOOD`/`NON_STOCK_SERVICE`), `base_uom_id uuid`, `default_purchase_uom_id uuid`, `default_sales_uom_id uuid`, `entity_scope_mode varchar(32)`, `status varchar(16)`, `created_at_utc`, `updated_at_utc`, `deactivated_at_utc NULL`, `version bigint DEFAULT 1`. AK `(tenant_id, item_id)`, unique `(tenant_id, item_code)`, three composite FKs `(tenant_id, *uom_id) -> catalog.units_of_measure` (`RESTRICT`).
6. **`catalog.item_uom_conversions`**: `item_uom_id uuid PK`, `tenant_id uuid`, `item_id uuid`, `uom_id uuid`, `factor_to_base numeric(18,6) CHECK (factor_to_base > 0)`, `status varchar(16)`, `created_at_utc`, `updated_at_utc`, `version bigint DEFAULT 1`. Unique `(tenant_id, item_id, uom_id)`, composite FKs to `items` & `units_of_measure`.
7. **`catalog.item_entity_policies`**: `policy_id uuid PK`, `tenant_id uuid`, `legal_entity_id uuid`, `item_id uuid`, `purchase_allowed bool`, `sales_allowed bool`, `notes varchar(300) NULL`, `updated_at_utc`, `version bigint DEFAULT 1`. Unique `(tenant_id, legal_entity_id, item_id)`, composite FKs to `platform.legal_entities` & `catalog.items`.

### Forced RLS & Hard-Delete Prevention
- `ENABLE ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY` on all 7 `partners.*` and `catalog.*` tables.
- Runtime role `athera_app` gets `SELECT, INSERT, UPDATE` ONLY (`DELETE` revoked/absent on all 7 tables) with `tenant_id = platform.current_tenant_id()`.
- `athera_migrator` receives `p_*_migrator_all` policies (`FOR ALL TO athera_migrator USING (true) WITH CHECK (true)`) on all 7 tables.

---

## 5. Permissions, API Endpoints & Next.js UX Contracts
- **Permissions:** `partners.read`, `partners.manage`, `catalog.read`, `catalog.manage`.
- **Base API Prefix:** `/api/workspaces/{tenantId:guid}/entities/{entityId:guid}`
  - Partners: `GET /partners`, `POST /partners`, `GET /partners/{partnerId:guid}`, `PUT /partners/{partnerId:guid}`, `POST /partners/{partnerId:guid}/contacts`, `PUT /partners/{partnerId:guid}/entity-profile`
  - Catalogue & UoM: `GET /catalog/uoms`, `POST /catalog/uoms`, `GET /catalog/items`, `POST /catalog/items`, `GET /catalog/items/{itemId:guid}`, `PUT /catalog/items/{itemId:guid}`, `POST /catalog/items/{itemId:guid}/conversions`, `PUT /catalog/items/{itemId:guid}/entity-policy`, `POST /catalog/items/{itemId:guid}/convert-preview`
  - Conflict handling (`23505`, `DbUpdateConcurrencyException`) caught outside `ExecuteInEntityAsync` so failed PG transactions roll back before returning `409`.
- **Next.js Experience (`DESIGN.md`):**
  - Unlocks `Mitra Bisnis` (`/w/[tenantId]/[entityId]/partners`) and `Katalog & Satuan` (`/w/[tenantId]/[entityId]/catalog`) in `AppShell` and `WorkspaceEndpoints.BuildNavigationCapabilities`.
  - Dense searchable/filterable ledger tables with localized role/status/eligibility chips, explicit `NPWP / PKP hanya metadata (OD-07)` disclosure notice, UoM live converter calculator, and strict read-only hiding of mutation actions for read-only actors.

---

## 6. S03 Mandatory Test Matrix
- **Unit (`S03PartnersCatalogueUomTests.cs`):** Partner code/role/eligibility evaluation, deactivation, UoM scales (`0..6`), `ItemUom` factor `> 0`, `UomConverter` exact/round-trip/discrete-fraction reject/continuous rounding/unsupported UoM, immutable `ItemTransactionSnapshot`, permission/role-template guards.
- **PostgreSQL Integration (`S03PartnersCataloguePostgresTests.cs`):** `S03_PG_01` (missing `app.tenant_id` & cross-tenant isolation across all 7 tables), `S03_PG_02` (`M02 -> M03 -> M02 -> M03` round-trip), `S03_PG_03` (`DELETE` blocked `42501` for `athera_app`), `S03_API_01` (dual partner, duplicate `409`, `RESTRICTED_ENTITIES` sibling check, deactivation, audit log), `S03_API_02` (UoM + item + conversion round-trip, `fractional_discrete_base_quantity`, `unsupported_uom_conversion`, per-entity item policy), `S03_API_03` (foreign-tenant `404`, unauthorized sibling `404`, read-only `.read` actor `200` GET / `403` POST/PUT).
- **UI & E2E (`s03-partners-catalog.test.tsx`, `s03-partners-catalog.spec.ts`):** Search/filter, dual partner creation & restriction, item + UoM conversion creation + `3 BOX = 72 PCS` live converter, read-only & ineligible sibling verification (`NUSA-SBY`), hostile foreign URL check, desktop (`1280x800`) + mobile (`390x844`) zero-overflow screenshots.
