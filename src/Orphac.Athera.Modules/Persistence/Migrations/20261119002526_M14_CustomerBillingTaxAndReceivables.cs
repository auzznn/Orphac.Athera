using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M14_CustomerBillingTaxAndReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "ar");

            migrationBuilder.CreateTable(
                name: "credit_note_sequences",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_credit_note_sequences_last_number", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "customer_invoices",
                schema: "billing",
                columns: table => new
                {
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    original_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_invoice_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_npwp_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subtotal_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    grand_total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tax_treatment_class_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tax_rate_percent_snapshot = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    requires_tax_evidence_snapshot = table.Column<bool>(type: "boolean", nullable: false),
                    tax_evidence_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tax_determination_summary = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gl_journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ar_receivable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_invoices", x => x.invoice_id);
                    table.UniqueConstraint("AK_customer_invoices_tenant_id_legal_entity_id_invoice_id", x => new { x.tenant_id, x.legal_entity_id, x.invoice_id });
                    table.CheckConstraint("ck_customer_invoices_amounts", "subtotal_amount > 0 AND tax_amount >= 0 AND grand_total_amount = subtotal_amount + tax_amount");
                    table.CheckConstraint("ck_customer_invoices_dates", "due_date >= invoice_date");
                    table.CheckConstraint("ck_customer_invoices_doc_type", "document_type IN ('INVOICE', 'CREDIT_NOTE')");
                    table.CheckConstraint("ck_customer_invoices_status", "status IN ('DRAFT', 'SUBMITTED', 'POSTED', 'CANCELLED')");
                });

            migrationBuilder.CreateTable(
                name: "invoice_sequences",
                schema: "billing",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_invoice_sequences_last_number", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "receivable_sequences",
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
                    table.PrimaryKey("PK_receivable_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_receivable_sequences_last_number", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "customer_invoice_lines",
                schema: "billing",
                columns: table => new
                {
                    invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    shipment_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    line_subtotal_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tax_rate_percent = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    line_tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    original_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_invoice_lines", x => x.invoice_line_id);
                    table.UniqueConstraint("AK_customer_invoice_lines_tenant_id_legal_entity_id_invoice_li~", x => new { x.tenant_id, x.legal_entity_id, x.invoice_line_id });
                    table.CheckConstraint("ck_customer_invoice_lines_amounts", "billed_base_quantity > 0 AND unit_price >= 0 AND line_subtotal_amount > 0 AND line_tax_amount >= 0 AND line_total_amount = line_subtotal_amount + line_tax_amount");
                    table.CheckConstraint("ck_customer_invoice_lines_number", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_customer_invoice_lines_customer_invoices_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invoice_id },
                        principalSchema: "billing",
                        principalTable: "customer_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_invoice_lines_shipment_lines_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.shipment_line_id },
                        principalSchema: "fulfillment",
                        principalTable: "shipment_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "shipment_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_receivables",
                schema: "ar",
                columns: table => new
                {
                    receivable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receivable_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credited_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    open_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_receivables", x => x.receivable_id);
                    table.UniqueConstraint("AK_customer_receivables_tenant_id_legal_entity_id_receivable_id", x => new { x.tenant_id, x.legal_entity_id, x.receivable_id });
                    table.CheckConstraint("ck_customer_receivables_amounts", "original_amount > 0 AND credited_amount >= 0 AND received_amount >= 0 AND open_amount >= 0 AND open_amount = original_amount - credited_amount - received_amount");
                    table.CheckConstraint("ck_customer_receivables_status", "status IN ('OPEN', 'PARTIALLY_SETTLED', 'SETTLED', 'CREDITED')");
                    table.ForeignKey(
                        name: "FK_customer_receivables_customer_invoices_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invoice_id },
                        principalSchema: "billing",
                        principalTable: "customer_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_invoice_lines_shipment_line",
                schema: "billing",
                table: "customer_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "shipment_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_invoice_lines_number",
                schema: "billing",
                table: "customer_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "invoice_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_invoices_so",
                schema: "billing",
                table: "customer_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "sales_order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_invoices_idempotency",
                schema: "billing",
                table: "customer_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_invoices_number",
                schema: "billing",
                table: "customer_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_receivables_customer",
                schema: "ar",
                table: "customer_receivables",
                columns: new[] { "tenant_id", "legal_entity_id", "customer_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_receivables_invoice",
                schema: "ar",
                table: "customer_receivables",
                columns: new[] { "tenant_id", "legal_entity_id", "invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_receivables_number",
                schema: "ar",
                table: "customer_receivables",
                columns: new[] { "tenant_id", "legal_entity_id", "receivable_number" },
                unique: true);

            // Immutability triggers for posted customer invoices & credit notes (OD-09)
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION billing.guard_posted_invoice_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF OLD.status = 'POSTED' THEN
                        RAISE EXCEPTION 'billing_posted_invoice_immutable: Posted customer invoice or credit note (%) cannot be modified or deleted; issue a linked credit note instead.', OLD.invoice_number
                            USING ERRCODE = '55000';
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_posted_invoice_immutability
                BEFORE UPDATE OR DELETE ON billing.customer_invoices
                FOR EACH ROW
                EXECUTE FUNCTION billing.guard_posted_invoice_immutability();

                CREATE OR REPLACE FUNCTION billing.guard_posted_invoice_line_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_status text;
                BEGIN
                    SELECT status INTO v_status
                    FROM billing.customer_invoices
                    WHERE tenant_id = OLD.tenant_id
                      AND legal_entity_id = OLD.legal_entity_id
                      AND invoice_id = OLD.invoice_id;

                    IF v_status = 'POSTED' THEN
                        RAISE EXCEPTION 'billing_posted_invoice_line_immutable: Lines of a posted customer invoice cannot be modified or deleted.'
                            USING ERRCODE = '55000';
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_posted_invoice_line_immutability
                BEFORE UPDATE OR DELETE ON billing.customer_invoice_lines
                FOR EACH ROW
                EXECUTE FUNCTION billing.guard_posted_invoice_line_immutability();
                """);

            // Tenant RLS + least-privilege runtime grants
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tbl text;
                BEGIN
                    FOREACH tbl IN ARRAY ARRAY[
                        'billing.invoice_sequences',
                        'billing.credit_note_sequences',
                        'billing.customer_invoices',
                        'billing.customer_invoice_lines',
                        'ar.receivable_sequences',
                        'ar.customer_receivables'
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

                GRANT USAGE ON SCHEMA billing, ar TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON billing.invoice_sequences TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON billing.credit_note_sequences TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON billing.customer_invoices TO athera_app;
                GRANT SELECT, INSERT ON billing.customer_invoice_lines TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON ar.receivable_sequences TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON ar.customer_receivables TO athera_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_posted_invoice_line_immutability ON billing.customer_invoice_lines;
                DROP FUNCTION IF EXISTS billing.guard_posted_invoice_line_immutability();
                DROP TRIGGER IF EXISTS trg_guard_posted_invoice_immutability ON billing.customer_invoices;
                DROP FUNCTION IF EXISTS billing.guard_posted_invoice_immutability();
                """);

            migrationBuilder.DropTable(
                name: "credit_note_sequences",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "customer_invoice_lines",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "customer_receivables",
                schema: "ar");

            migrationBuilder.DropTable(
                name: "invoice_sequences",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "receivable_sequences",
                schema: "ar");

            migrationBuilder.DropTable(
                name: "customer_invoices",
                schema: "billing");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS ar;
                DROP SCHEMA IF EXISTS billing;
                """);
        }
    }
}
