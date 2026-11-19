using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M15_CustomerReceiptsAllocationAndArAging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_receipts",
                schema: "ar",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false),
                    bank_reference_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    receipt_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    unapplied_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reversal_gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_gl_journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    reversed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_receipts", x => x.receipt_id);
                    table.UniqueConstraint("AK_customer_receipts_tenant_id_legal_entity_id_receipt_id", x => new { x.tenant_id, x.legal_entity_id, x.receipt_id });
                    table.CheckConstraint("ck_customer_receipts_amounts", "receipt_amount > 0 AND allocated_amount >= 0 AND unapplied_amount >= 0 AND unapplied_amount = receipt_amount - allocated_amount");
                    table.CheckConstraint("ck_customer_receipts_status", "status IN ('CONFIRMED', 'PARTIALLY_ALLOCATED', 'ALLOCATED', 'REVERSED')");
                });

            migrationBuilder.CreateTable(
                name: "receipt_sequences",
                schema: "ar",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_sequences1", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_receipt_sequences_last_number", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "receipt_allocations",
                schema: "ar",
                columns: table => new
                {
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    allocated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reversed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_allocations", x => x.allocation_id);
                    table.UniqueConstraint("AK_receipt_allocations_tenant_id_legal_entity_id_allocation_id", x => new { x.tenant_id, x.legal_entity_id, x.allocation_id });
                    table.CheckConstraint("ck_receipt_allocations_amount", "allocated_amount > 0");
                    table.CheckConstraint("ck_receipt_allocations_status", "status IN ('ACTIVE', 'REVERSED')");
                    table.ForeignKey(
                        name: "FK_receipt_allocations_customer_receipts_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_id },
                        principalSchema: "ar",
                        principalTable: "customer_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_allocations_customer_receivables_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receivable_id },
                        principalSchema: "ar",
                        principalTable: "customer_receivables",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receivable_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_customer_receipts_active_bank_line",
                schema: "ar",
                table: "customer_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_statement_line_id" },
                unique: true,
                filter: "status <> 'REVERSED'");

            migrationBuilder.CreateIndex(
                name: "ux_customer_receipts_idempotency",
                schema: "ar",
                table: "customer_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_receipts_number",
                schema: "ar",
                table: "customer_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_allocations_receipt",
                schema: "ar",
                table: "receipt_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_allocations_receivable",
                schema: "ar",
                table: "receipt_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "receivable_id" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION ar.guard_reversed_receipt_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF OLD.status = 'REVERSED' THEN
                        RAISE EXCEPTION 'ar_reversed_receipt_immutable: Reversed customer receipt (%) cannot be modified or deleted.', OLD.receipt_number
                            USING ERRCODE = '55000';
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_reversed_receipt_immutability
                BEFORE UPDATE OR DELETE ON ar.customer_receipts
                FOR EACH ROW
                EXECUTE FUNCTION ar.guard_reversed_receipt_immutability();

                DO $$
                DECLARE
                    tbl text;
                BEGIN
                    FOREACH tbl IN ARRAY ARRAY[
                        'ar.receipt_sequences',
                        'ar.customer_receipts',
                        'ar.receipt_allocations'
                    ] LOOP
                        EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY', tbl);
                        EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY', tbl);
                        EXECUTE format(
                            'CREATE POLICY tenant_isolation_app ON %s FOR ALL TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id())',
                            tbl);
                        EXECUTE format(
                            'CREATE POLICY tenant_isolation_migrator ON %s FOR ALL TO athera_migrator USING (true) WITH CHECK (true)',
                            tbl);
                    END LOOP;
                END
                $$;

                GRANT USAGE ON SCHEMA ar TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON ar.receipt_sequences TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON ar.customer_receipts TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON ar.receipt_allocations TO athera_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_reversed_receipt_immutability ON ar.customer_receipts;
                DROP FUNCTION IF EXISTS ar.guard_reversed_receipt_immutability();
                """);

            migrationBuilder.DropTable(
                name: "receipt_allocations",
                schema: "ar");

            migrationBuilder.DropTable(
                name: "receipt_sequences",
                schema: "ar");

            migrationBuilder.DropTable(
                name: "customer_receipts",
                schema: "ar");
        }
    }
}
