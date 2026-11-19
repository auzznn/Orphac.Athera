using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M16_PeriodCloseAuditAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "period_close_reviews",
                schema: "gl",
                columns: table => new
                {
                    close_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_code_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trial_balance_debits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    trial_balance_credits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_trial_balance_balanced = table.Column<bool>(type: "boolean", nullable: false),
                    reconciliation_variance_count = table.Column<int>(type: "integer", nullable: false),
                    total_variance_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    unbilled_grni_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    checklist_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_close_reviews", x => x.close_review_id);
                    table.CheckConstraint("ck_period_close_reviews_action", "action_type IN ('CLOSED', 'LOCKED', 'REOPENED')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_period_close_reviews_period",
                schema: "gl",
                table: "period_close_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "period_id", "created_at_utc" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION gl.guard_period_close_review_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'gl_period_close_review_immutable: Period close audit records are append-only and cannot be modified or deleted.'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER trg_guard_period_close_review_immutability
                BEFORE UPDATE OR DELETE ON gl.period_close_reviews
                FOR EACH ROW
                EXECUTE FUNCTION gl.guard_period_close_review_immutability();

                ALTER TABLE gl.period_close_reviews ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.period_close_reviews FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_app ON gl.period_close_reviews
                    FOR ALL TO athera_app
                    USING (tenant_id = platform.current_tenant_id())
                    WITH CHECK (tenant_id = platform.current_tenant_id());
                CREATE POLICY tenant_isolation_migrator ON gl.period_close_reviews
                    FOR ALL TO athera_migrator
                    USING (true)
                    WITH CHECK (true);

                GRANT SELECT, INSERT ON gl.period_close_reviews TO athera_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_period_close_review_immutability ON gl.period_close_reviews;
                DROP FUNCTION IF EXISTS gl.guard_period_close_review_immutability();
                """);

            migrationBuilder.DropTable(
                name: "period_close_reviews",
                schema: "gl");
        }
    }
}
