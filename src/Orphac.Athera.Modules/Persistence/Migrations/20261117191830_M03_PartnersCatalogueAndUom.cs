using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M03_PartnersCatalogueAndUom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "partners");

            migrationBuilder.CreateTable(
                name: "partners",
                schema: "partners",
                columns: table => new
                {
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    is_customer = table.Column<bool>(type: "boolean", nullable: false),
                    tax_registration_npwp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tax_pkp_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    entity_scope_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners", x => x.partner_id);
                    table.UniqueConstraint("AK_partners_tenant_id_partner_id", x => new { x.tenant_id, x.partner_id });
                    table.CheckConstraint("ck_partners_role", "is_supplier OR is_customer");
                    table.CheckConstraint("ck_partners_scope", "entity_scope_mode IN ('ALL_ENTITIES', 'RESTRICTED_ENTITIES')");
                    table.CheckConstraint("ck_partners_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_partners_tax_pkp", "tax_pkp_status IN ('UNKNOWN', 'PKP', 'NON_PKP')");
                    table.ForeignKey(
                        name: "FK_partners_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                schema: "catalog",
                columns: table => new
                {
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity_precision_scale = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.uom_id);
                    table.UniqueConstraint("AK_units_of_measure_tenant_id_uom_id", x => new { x.tenant_id, x.uom_id });
                    table.CheckConstraint("ck_uoms_scale", "quantity_precision_scale BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_uoms_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_units_of_measure_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_contacts",
                schema: "partners",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    contact_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_contacts", x => x.contact_id);
                    table.CheckConstraint("ck_partner_contacts_role", "contact_role IN ('COMMERCIAL', 'BILLING', 'RECEIVING_DISPATCH')");
                    table.CheckConstraint("ck_partner_contacts_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_partner_contacts_partners_tenant_id_partner_id",
                        columns: x => new { x.tenant_id, x.partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partner_entity_profiles",
                schema: "partners",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    customer_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    default_payment_terms_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_entity_profiles", x => x.profile_id);
                    table.CheckConstraint("ck_partner_entity_profiles_terms", "default_payment_terms_days BETWEEN 0 AND 365");
                    table.ForeignKey(
                        name: "FK_partner_entity_profiles_legal_entities_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_partner_entity_profiles_partners_tenant_id_partner_id",
                        columns: x => new { x.tenant_id, x.partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "catalog",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_purchase_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_sales_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_scope_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.item_id);
                    table.UniqueConstraint("AK_items_tenant_id_item_id", x => new { x.tenant_id, x.item_id });
                    table.CheckConstraint("ck_items_kind", "item_kind IN ('STOCKED_GOOD', 'NON_STOCK_SERVICE')");
                    table.CheckConstraint("ck_items_scope", "entity_scope_mode IN ('ALL_ENTITIES', 'RESTRICTED_ENTITIES')");
                    table.CheckConstraint("ck_items_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_units_of_measure_tenant_id_default_purchase_uom_id",
                        columns: x => new { x.tenant_id, x.default_purchase_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_units_of_measure_tenant_id_default_sales_uom_id",
                        columns: x => new { x.tenant_id, x.default_sales_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_entity_policies",
                schema: "catalog",
                columns: table => new
                {
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    sales_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_entity_policies", x => x.policy_id);
                    table.ForeignKey(
                        name: "FK_item_entity_policies_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_entity_policies_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_uom_conversions",
                schema: "catalog",
                columns: table => new
                {
                    item_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_to_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_uom_conversions", x => x.item_uom_id);
                    table.CheckConstraint("ck_item_uoms_factor", "factor_to_base > 0");
                    table.CheckConstraint("ck_item_uoms_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_units_of_measure_tenant_id_uom_id",
                        columns: x => new { x.tenant_id, x.uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_entity_policies_tenant_id_item_id",
                schema: "catalog",
                table: "item_entity_policies",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_entity_policies_tenant_id_legal_entity_id_item_id",
                schema: "catalog",
                table: "item_entity_policies",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_tenant_id_item_id_uom_id",
                schema: "catalog",
                table: "item_uom_conversions",
                columns: new[] { "tenant_id", "item_id", "uom_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_tenant_id_uom_id",
                schema: "catalog",
                table: "item_uom_conversions",
                columns: new[] { "tenant_id", "uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_tenant_id_base_uom_id",
                schema: "catalog",
                table: "items",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_tenant_id_default_purchase_uom_id",
                schema: "catalog",
                table: "items",
                columns: new[] { "tenant_id", "default_purchase_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_tenant_id_default_sales_uom_id",
                schema: "catalog",
                table: "items",
                columns: new[] { "tenant_id", "default_sales_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_tenant_id_item_code",
                schema: "catalog",
                table: "items",
                columns: new[] { "tenant_id", "item_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_tenant_id_status",
                schema: "catalog",
                table: "items",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_partner_contacts_tenant_id_partner_id",
                schema: "partners",
                table: "partner_contacts",
                columns: new[] { "tenant_id", "partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_partner_entity_profiles_tenant_id_legal_entity_id_partner_id",
                schema: "partners",
                table: "partner_entity_profiles",
                columns: new[] { "tenant_id", "legal_entity_id", "partner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partner_entity_profiles_tenant_id_partner_id",
                schema: "partners",
                table: "partner_entity_profiles",
                columns: new[] { "tenant_id", "partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_partners_tenant_id_partner_code",
                schema: "partners",
                table: "partners",
                columns: new[] { "tenant_id", "partner_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partners_tenant_id_status",
                schema: "partners",
                table: "partners",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_tenant_id_uom_code",
                schema: "catalog",
                table: "units_of_measure",
                columns: new[] { "tenant_id", "uom_code" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE partners.partners ENABLE ROW LEVEL SECURITY;
                ALTER TABLE partners.partners FORCE ROW LEVEL SECURITY;

                ALTER TABLE partners.partner_contacts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE partners.partner_contacts FORCE ROW LEVEL SECURITY;

                ALTER TABLE partners.partner_entity_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE partners.partner_entity_profiles FORCE ROW LEVEL SECURITY;

                ALTER TABLE catalog.units_of_measure ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catalog.units_of_measure FORCE ROW LEVEL SECURITY;

                ALTER TABLE catalog.items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catalog.items FORCE ROW LEVEL SECURITY;

                ALTER TABLE catalog.item_uom_conversions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catalog.item_uom_conversions FORCE ROW LEVEL SECURITY;

                ALTER TABLE catalog.item_entity_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE catalog.item_entity_policies FORCE ROW LEVEL SECURITY;

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA partners, catalog FROM PUBLIC;
                    GRANT USAGE ON SCHEMA partners, catalog TO athera_app;

                    REVOKE ALL ON ALL TABLES IN SCHEMA partners, catalog FROM PUBLIC, athera_app;
                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      partners.partners,
                      partners.partner_contacts,
                      partners.partner_entity_profiles,
                      catalog.units_of_measure,
                      catalog.items,
                      catalog.item_uom_conversions,
                      catalog.item_entity_policies
                    TO athera_app;

                    CREATE POLICY p_partners_select ON partners.partners FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partners_insert ON partners.partners FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partners_update ON partners.partners FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_partner_contacts_select ON partners.partner_contacts FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partner_contacts_insert ON partners.partner_contacts FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partner_contacts_update ON partners.partner_contacts FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_partner_entity_profiles_select ON partners.partner_entity_profiles FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partner_entity_profiles_insert ON partners.partner_entity_profiles FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_partner_entity_profiles_update ON partners.partner_entity_profiles FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_units_of_measure_select ON catalog.units_of_measure FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_units_of_measure_insert ON catalog.units_of_measure FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_units_of_measure_update ON catalog.units_of_measure FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_items_select ON catalog.items FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_items_insert ON catalog.items FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_items_update ON catalog.items FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_item_uom_conversions_select ON catalog.item_uom_conversions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_item_uom_conversions_insert ON catalog.item_uom_conversions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_item_uom_conversions_update ON catalog.item_uom_conversions FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_item_entity_policies_select ON catalog.item_entity_policies FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_item_entity_policies_insert ON catalog.item_entity_policies FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_item_entity_policies_update ON catalog.item_entity_policies FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_partners_migrator_all ON partners.partners FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_partner_contacts_migrator_all ON partners.partner_contacts FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_partner_entity_profiles_migrator_all ON partners.partner_entity_profiles FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_units_of_measure_migrator_all ON catalog.units_of_measure FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_items_migrator_all ON catalog.items FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_item_uom_conversions_migrator_all ON catalog.item_uom_conversions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_item_entity_policies_migrator_all ON catalog.item_entity_policies FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_entity_policies",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "item_uom_conversions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "partner_contacts",
                schema: "partners");

            migrationBuilder.DropTable(
                name: "partner_entity_profiles",
                schema: "partners");

            migrationBuilder.DropTable(
                name: "items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "partners",
                schema: "partners");

            migrationBuilder.DropTable(
                name: "units_of_measure",
                schema: "catalog");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS catalog;
                DROP SCHEMA IF EXISTS partners;
                """);
        }
    }
}
