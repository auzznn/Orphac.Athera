using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M01_TenantAndLegalEntityWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "platform",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.tenant_id);
                    table.CheckConstraint("ck_tenants_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
                });

            migrationBuilder.CreateTable(
                name: "legal_entities",
                schema: "platform",
                columns: table => new
                {
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_registration_npwp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    functional_currency_code = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "IDR"),
                    country_code = table.Column<string>(type: "char(2)", nullable: false, defaultValue: "ID"),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Asia/Jakarta"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entities", x => x.legal_entity_id);
                    table.UniqueConstraint("AK_legal_entities_tenant_id_legal_entity_id", x => new { x.tenant_id, x.legal_entity_id });
                    table.CheckConstraint("ck_legal_entities_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_legal_entities_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "identity",
                columns: table => new
                {
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => x.membership_id);
                    table.UniqueConstraint("AK_tenant_memberships_tenant_id_user_id", x => new { x.tenant_id, x.user_id });
                    table.CheckConstraint("ck_tenant_memberships_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
                    table.ForeignKey(
                        name: "FK_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_memberships_user_accounts_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user_accounts",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entity_access_grants",
                schema: "identity",
                columns: table => new
                {
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valid_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_access_grants", x => x.grant_id);
                    table.CheckConstraint("ck_entity_access_grants_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
                    table.CheckConstraint("ck_entity_access_grants_validity_window", "valid_until_utc IS NULL OR valid_until_utc > valid_from_utc");
                    table.ForeignKey(
                        name: "FK_entity_access_grants_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entity_access_grants_tenant_memberships_tenant_id_user_id",
                        columns: x => new { x.tenant_id, x.user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_access_grants_tenant_id_legal_entity_id_user_id_perm~",
                schema: "identity",
                table: "entity_access_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "user_id", "permission_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_access_grants_tenant_id_user_id_legal_entity_id_stat~",
                schema: "identity",
                table: "entity_access_grants",
                columns: new[] { "tenant_id", "user_id", "legal_entity_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_entities_tenant_id_code",
                schema: "platform",
                table: "legal_entities",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_entities_tenant_id_status",
                schema: "platform",
                table: "legal_entities",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id_user_id_membership_id",
                schema: "identity",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id", "membership_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_user_id_status",
                schema: "identity",
                table: "tenant_memberships",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_code",
                schema: "platform",
                table: "tenants",
                column: "code",
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.current_tenant_id()
                RETURNS uuid
                LANGUAGE plpgsql
                STABLE
                SECURITY INVOKER
                SET search_path = pg_catalog
                AS $$
                DECLARE
                  raw_value text;
                BEGIN
                  raw_value := NULLIF(current_setting('app.tenant_id', true), '');
                  IF raw_value IS NULL THEN
                    RETURN NULL;
                  END IF;
                  RETURN raw_value::uuid;
                EXCEPTION WHEN invalid_text_representation THEN
                  RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION platform.current_actor_user_id()
                RETURNS uuid
                LANGUAGE plpgsql
                STABLE
                SECURITY INVOKER
                SET search_path = pg_catalog
                AS $$
                DECLARE
                  raw_value text;
                BEGIN
                  raw_value := NULLIF(current_setting('app.actor_user_id', true), '');
                  IF raw_value IS NULL THEN
                    RETURN NULL;
                  END IF;
                  RETURN raw_value::uuid;
                EXCEPTION WHEN invalid_text_representation THEN
                  RETURN NULL;
                END;
                $$;

                ALTER TABLE platform.tenants ENABLE ROW LEVEL SECURITY;
                ALTER TABLE platform.tenants FORCE ROW LEVEL SECURITY;

                ALTER TABLE platform.legal_entities ENABLE ROW LEVEL SECURITY;
                ALTER TABLE platform.legal_entities FORCE ROW LEVEL SECURITY;

                ALTER TABLE identity.tenant_memberships ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.tenant_memberships FORCE ROW LEVEL SECURITY;

                ALTER TABLE identity.entity_access_grants ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.entity_access_grants FORCE ROW LEVEL SECURITY;

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA platform FROM PUBLIC;
                    GRANT USAGE ON SCHEMA platform TO athera_app;
                    GRANT EXECUTE ON FUNCTION platform.current_tenant_id(), platform.current_actor_user_id() TO athera_app;

                    REVOKE ALL ON TABLE platform.tenants FROM athera_app;
                    GRANT SELECT ON TABLE platform.tenants TO athera_app;

                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      platform.legal_entities,
                      identity.tenant_memberships,
                      identity.entity_access_grants
                    TO athera_app;

                    CREATE POLICY p_legal_entities_tenant_isolation
                      ON platform.legal_entities
                      FOR ALL
                      TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_entity_access_grants_tenant_isolation
                      ON identity.entity_access_grants
                      FOR ALL
                      TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tenant_memberships_select
                      ON identity.tenant_memberships
                      FOR SELECT
                      TO athera_app
                      USING (
                        tenant_id = platform.current_tenant_id()
                        OR (user_id = platform.current_actor_user_id() AND status = 'ACTIVE')
                      );

                    CREATE POLICY p_tenant_memberships_insert
                      ON identity.tenant_memberships
                      FOR INSERT
                      TO athera_app
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tenant_memberships_update
                      ON identity.tenant_memberships
                      FOR UPDATE
                      TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tenant_memberships_delete
                      ON identity.tenant_memberships
                      FOR DELETE
                      TO athera_app
                      USING (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tenants_select
                      ON platform.tenants
                      FOR SELECT
                      TO athera_app
                      USING (
                        tenant_id = platform.current_tenant_id()
                        OR EXISTS (
                          SELECT 1
                          FROM identity.tenant_memberships tm
                          WHERE tm.tenant_id = tenants.tenant_id
                            AND tm.user_id = platform.current_actor_user_id()
                            AND tm.status = 'ACTIVE'
                            AND tenants.status = 'ACTIVE'
                        )
                      );
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_tenants_migrator_all
                      ON platform.tenants FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_legal_entities_migrator_all
                      ON platform.legal_entities FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_tenant_memberships_migrator_all
                      ON identity.tenant_memberships FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_entity_access_grants_migrator_all
                      ON identity.entity_access_grants FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS p_tenants_select ON platform.tenants;
                """);

            migrationBuilder.DropTable(
                name: "entity_access_grants",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "legal_entities",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "platform");

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS platform.current_tenant_id();
                DROP FUNCTION IF EXISTS platform.current_actor_user_id();
                DROP SCHEMA IF EXISTS platform;
                """);
        }
    }
}
