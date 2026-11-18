using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M12_CustomerSalesOrdersAndCommercialCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "sales_order_sequences",
                schema: "sales",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_sales_order_sequences_nonnegative", "last_number >= 0");
                    table.CheckConstraint("ck_sales_order_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_sales_order_sequences_legal_entities_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                schema: "sales",
                columns: table => new
                {
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    so_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivery_address_snapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    payment_terms_days = table.Column<int>(type: "integer", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    current_approved_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_approved_revision_number = table.Column<int>(type: "integer", nullable: false),
                    latest_revision_number = table.Column<int>(type: "integer", nullable: false),
                    total_reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closure_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.sales_order_id);
                    table.UniqueConstraint("AK_sales_orders_tenant_id_legal_entity_id_sales_order_id", x => new { x.tenant_id, x.legal_entity_id, x.sales_order_id });
                    table.CheckConstraint("ck_sales_orders_fulfillment_nonnegative", "total_reserved_base_quantity >= 0 AND total_dispatched_base_quantity >= 0 AND total_billed_base_quantity >= 0");
                    table.CheckConstraint("ck_sales_orders_revisions_nonnegative", "latest_revision_number >= 1 AND current_approved_revision_number >= 0 AND current_approved_revision_number <= latest_revision_number");
                    table.CheckConstraint("ck_sales_orders_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'CLOSED', 'CANCELLED')");
                    table.CheckConstraint("ck_sales_orders_terms", "payment_terms_days >= 0 AND payment_terms_days <= 365");
                    table.ForeignKey(
                        name: "FK_sales_orders_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_orders_partners_tenant_id_customer_partner_id",
                        columns: x => new { x.tenant_id, x.customer_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_orders_tenant_memberships_tenant_id_created_by_user_id",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_revisions",
                schema: "sales",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    customer_pkp_status_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivery_address_snapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    payment_terms_days = table.Column<int>(type: "integer", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    change_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    output_tax_total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    grand_total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_ordered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    snapshot_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_revisions", x => x.revision_id);
                    table.UniqueConstraint("AK_sales_order_revisions_tenant_id_legal_entity_id_revision_id", x => new { x.tenant_id, x.legal_entity_id, x.revision_id });
                    table.CheckConstraint("ck_so_revisions_four_eyes", "decided_by_user_id IS NULL OR submitted_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id");
                    table.CheckConstraint("ck_so_revisions_number", "revision_number >= 1");
                    table.CheckConstraint("ck_so_revisions_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'REJECTED', 'SUPERSEDED', 'CANCELLED')");
                    table.CheckConstraint("ck_so_revisions_terms", "payment_terms_days >= 0 AND payment_terms_days <= 365");
                    table.CheckConstraint("ck_so_revisions_totals_nonnegative", "subtotal_amount >= 0 AND output_tax_total_amount >= 0 AND grand_total_amount >= 0 AND total_ordered_base_quantity >= 0");
                    table.ForeignKey(
                        name: "FK_sales_order_revisions_document_approvals_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_record_id },
                        principalSchema: "approval",
                        principalTable: "document_approvals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "approval_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_revisions_partners_tenant_id_customer_partner_id",
                        columns: x => new { x.tenant_id, x.customer_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_revisions_sales_order_revisions_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supersedes_revision_id },
                        principalSchema: "sales",
                        principalTable: "sales_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_revisions_sales_orders_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sales_order_id },
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sales_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                schema: "sales",
                columns: table => new
                {
                    so_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_kind_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ordered_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordered_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    uom_factor_to_base_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ordered_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ordered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tax_treatment_class_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tax_rate_percent_snapshot = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    taxable_base_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    requested_delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    line_description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_lines", x => x.so_line_id);
                    table.UniqueConstraint("AK_sales_order_lines_tenant_id_legal_entity_id_so_line_id", x => new { x.tenant_id, x.legal_entity_id, x.so_line_id });
                    table.CheckConstraint("ck_so_lines_fulfillment_nonnegative", "reserved_base_quantity >= 0 AND dispatched_base_quantity >= 0 AND billed_base_quantity >= 0");
                    table.CheckConstraint("ck_so_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_so_lines_od09_zero_discount", "discount_amount = 0");
                    table.CheckConstraint("ck_so_lines_positive_qty_price", "ordered_quantity > 0 AND ordered_base_quantity > 0 AND uom_factor_to_base_snapshot > 0 AND unit_price > 0 AND line_subtotal_amount > 0 AND line_total_amount > 0");
                    table.ForeignKey(
                        name: "FK_sales_order_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_sales_order_revisions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id },
                        principalSchema: "sales",
                        principalTable: "sales_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_tax_codes_tenant_id_legal_entity_id_tax_c~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_units_of_measure_tenant_id_ordered_uom_id",
                        columns: x => new { x.tenant_id, x.ordered_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_tenant_id_item_id",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_tenant_id_legal_entity_id_revision_id_li~1",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "lineage_line_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_tenant_id_legal_entity_id_revision_id_lin~",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_tenant_id_legal_entity_id_tax_code_id",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_tenant_id_ordered_uom_id",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "tenant_id", "ordered_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_revisions_tenant_id_customer_partner_id",
                schema: "sales",
                table: "sales_order_revisions",
                columns: new[] { "tenant_id", "customer_partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_revisions_tenant_id_legal_entity_id_sales_order~",
                schema: "sales",
                table: "sales_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sales_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_revisions_tenant_id_legal_entity_id_supersedes_~",
                schema: "sales",
                table: "sales_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "supersedes_revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_so_revisions_approval_record",
                schema: "sales",
                table: "sales_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_record_id" },
                unique: true,
                filter: "approval_record_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_tenant_id_created_by_user_id",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_tenant_id_customer_partner_id",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "tenant_id", "customer_partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_tenant_id_legal_entity_id_draft_code",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_tenant_id_legal_entity_id_status_order_date",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ux_sales_orders_entity_so_number",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "so_number" },
                unique: true,
                filter: "so_number IS NOT NULL");

            migrationBuilder.Sql("""
                ALTER TABLE sales.sales_order_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales.sales_order_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE sales.sales_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales.sales_orders FORCE ROW LEVEL SECURITY;

                ALTER TABLE sales.sales_order_revisions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales.sales_order_revisions FORCE ROW LEVEL SECURITY;

                ALTER TABLE sales.sales_order_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales.sales_order_lines FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION sales.guard_frozen_so_revision_lines()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_frozen boolean;
                  v_status varchar(24);
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    SELECT r.is_frozen INTO v_frozen
                    FROM sales.sales_order_revisions r
                    WHERE r.tenant_id = OLD.tenant_id
                      AND r.legal_entity_id = OLD.legal_entity_id
                      AND r.revision_id = OLD.revision_id;
                    IF COALESCE(v_frozen, false) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Revision %s is frozen; lines cannot be deleted.', OLD.revision_id);
                    END IF;
                    RETURN OLD;
                  END IF;

                  IF TG_OP = 'INSERT' THEN
                    SELECT r.is_frozen INTO v_frozen
                    FROM sales.sales_order_revisions r
                    WHERE r.tenant_id = NEW.tenant_id
                      AND r.legal_entity_id = NEW.legal_entity_id
                      AND r.revision_id = NEW.revision_id;
                    IF COALESCE(v_frozen, false) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Revision %s is frozen; lines cannot be inserted.', NEW.revision_id);
                    END IF;
                    RETURN NEW;
                  END IF;

                  IF TG_OP = 'UPDATE' THEN
                    SELECT r.is_frozen, r.status INTO v_frozen, v_status
                    FROM sales.sales_order_revisions r
                    WHERE r.tenant_id = OLD.tenant_id
                      AND r.legal_entity_id = OLD.legal_entity_id
                      AND r.revision_id = OLD.revision_id;

                    IF COALESCE(v_frozen, false) THEN
                      IF NEW.so_line_id IS DISTINCT FROM OLD.so_line_id
                        OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                        OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                        OR NEW.sales_order_id IS DISTINCT FROM OLD.sales_order_id
                        OR NEW.revision_id IS DISTINCT FROM OLD.revision_id
                        OR NEW.line_number IS DISTINCT FROM OLD.line_number
                        OR NEW.lineage_line_key IS DISTINCT FROM OLD.lineage_line_key
                        OR NEW.item_id IS DISTINCT FROM OLD.item_id
                        OR NEW.item_code_snapshot IS DISTINCT FROM OLD.item_code_snapshot
                        OR NEW.item_name_snapshot IS DISTINCT FROM OLD.item_name_snapshot
                        OR NEW.item_kind_snapshot IS DISTINCT FROM OLD.item_kind_snapshot
                        OR NEW.ordered_uom_id IS DISTINCT FROM OLD.ordered_uom_id
                        OR NEW.ordered_uom_code_snapshot IS DISTINCT FROM OLD.ordered_uom_code_snapshot
                        OR NEW.base_uom_id IS DISTINCT FROM OLD.base_uom_id
                        OR NEW.base_uom_code_snapshot IS DISTINCT FROM OLD.base_uom_code_snapshot
                        OR NEW.uom_factor_to_base_snapshot IS DISTINCT FROM OLD.uom_factor_to_base_snapshot
                        OR NEW.ordered_quantity IS DISTINCT FROM OLD.ordered_quantity
                        OR NEW.ordered_base_quantity IS DISTINCT FROM OLD.ordered_base_quantity
                        OR NEW.unit_price IS DISTINCT FROM OLD.unit_price
                        OR NEW.discount_amount IS DISTINCT FROM OLD.discount_amount
                        OR NEW.line_subtotal_amount IS DISTINCT FROM OLD.line_subtotal_amount
                        OR NEW.tax_code_id IS DISTINCT FROM OLD.tax_code_id
                        OR NEW.tax_code_snapshot IS DISTINCT FROM OLD.tax_code_snapshot
                        OR NEW.tax_treatment_class_snapshot IS DISTINCT FROM OLD.tax_treatment_class_snapshot
                        OR NEW.tax_rate_percent_snapshot IS DISTINCT FROM OLD.tax_rate_percent_snapshot
                        OR NEW.taxable_base_amount IS DISTINCT FROM OLD.taxable_base_amount
                        OR NEW.tax_amount IS DISTINCT FROM OLD.tax_amount
                        OR NEW.line_total_amount IS DISTINCT FROM OLD.line_total_amount
                        OR NEW.requested_delivery_date IS DISTINCT FROM OLD.requested_delivery_date
                        OR NEW.line_description IS DISTINCT FROM OLD.line_description
                      THEN
                        RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Commercial fields on frozen revision %s cannot be modified.', OLD.revision_id);
                      END IF;

                      IF COALESCE(v_status, '') <> 'APPROVED' AND (
                        NEW.reserved_base_quantity IS DISTINCT FROM OLD.reserved_base_quantity
                        OR NEW.dispatched_base_quantity IS DISTINCT FROM OLD.dispatched_base_quantity
                        OR NEW.billed_base_quantity IS DISTINCT FROM OLD.billed_base_quantity
                      ) THEN
                        RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Non-approved revision %s fulfillment counters cannot be modified.', OLD.revision_id);
                      END IF;
                    END IF;
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_frozen_so_revision_lines
                  BEFORE INSERT OR UPDATE OR DELETE ON sales.sales_order_lines
                  FOR EACH ROW
                  EXECUTE FUNCTION sales.guard_frozen_so_revision_lines();

                CREATE OR REPLACE FUNCTION sales.guard_frozen_so_revision_snapshot()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF OLD.is_frozen THEN
                    IF NEW.is_frozen IS DISTINCT FROM true THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Frozen revision %s cannot be unfrozen.', OLD.revision_id);
                    END IF;

                    IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                      OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                      OR NEW.sales_order_id IS DISTINCT FROM OLD.sales_order_id
                      OR NEW.revision_number IS DISTINCT FROM OLD.revision_number
                      OR NEW.supersedes_revision_id IS DISTINCT FROM OLD.supersedes_revision_id
                      OR NEW.customer_partner_id IS DISTINCT FROM OLD.customer_partner_id
                      OR NEW.customer_code_snapshot IS DISTINCT FROM OLD.customer_code_snapshot
                      OR NEW.customer_legal_name_snapshot IS DISTINCT FROM OLD.customer_legal_name_snapshot
                      OR NEW.customer_npwp_snapshot IS DISTINCT FROM OLD.customer_npwp_snapshot
                      OR NEW.customer_pkp_status_snapshot IS DISTINCT FROM OLD.customer_pkp_status_snapshot
                      OR NEW.currency_code IS DISTINCT FROM OLD.currency_code
                      OR NEW.order_date IS DISTINCT FROM OLD.order_date
                      OR NEW.requested_delivery_date IS DISTINCT FROM OLD.requested_delivery_date
                      OR NEW.delivery_address_snapshot IS DISTINCT FROM OLD.delivery_address_snapshot
                      OR NEW.payment_terms_days IS DISTINCT FROM OLD.payment_terms_days
                      OR NEW.customer_reference IS DISTINCT FROM OLD.customer_reference
                      OR NEW.change_reason IS DISTINCT FROM OLD.change_reason
                      OR NEW.notes IS DISTINCT FROM OLD.notes
                      OR NEW.subtotal_amount IS DISTINCT FROM OLD.subtotal_amount
                      OR NEW.output_tax_total_amount IS DISTINCT FROM OLD.output_tax_total_amount
                      OR NEW.grand_total_amount IS DISTINCT FROM OLD.grand_total_amount
                      OR NEW.total_ordered_base_quantity IS DISTINCT FROM OLD.total_ordered_base_quantity
                      OR NEW.snapshot_hash IS DISTINCT FROM OLD.snapshot_hash
                      OR NEW.created_by_user_id IS DISTINCT FROM OLD.created_by_user_id
                      OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
                      OR NEW.submitted_by_user_id IS DISTINCT FROM OLD.submitted_by_user_id
                      OR NEW.submitted_at_utc IS DISTINCT FROM OLD.submitted_at_utc
                      OR NEW.approval_record_id IS DISTINCT FROM OLD.approval_record_id
                    THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Approved or submitted snapshot on revision %s cannot be mutated.', OLD.revision_id);
                    END IF;

                    IF NOT (
                      NEW.status = OLD.status
                      OR (OLD.status = 'PENDING_APPROVAL' AND NEW.status IN ('APPROVED', 'REJECTED', 'CANCELLED'))
                      OR (OLD.status = 'APPROVED' AND NEW.status = 'SUPERSEDED')
                    ) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('so_revision_frozen_immutable: Invalid frozen revision status transition %s -> %s.', OLD.status, NEW.status);
                    END IF;
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_frozen_so_revision_snapshot
                  BEFORE UPDATE ON sales.sales_order_revisions
                  FOR EACH ROW
                  EXECUTE FUNCTION sales.guard_frozen_so_revision_snapshot();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA sales FROM PUBLIC;
                    GRANT USAGE ON SCHEMA sales TO athera_app;

                    REVOKE ALL ON TABLE
                      sales.sales_order_sequences,
                      sales.sales_orders,
                      sales.sales_order_revisions,
                      sales.sales_order_lines
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      sales.sales_order_sequences,
                      sales.sales_orders,
                      sales.sales_order_revisions
                    TO athera_app;

                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      sales.sales_order_lines
                    TO athera_app;

                    CREATE POLICY p_sales_seq_select ON sales.sales_order_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_seq_insert ON sales.sales_order_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_seq_update ON sales.sales_order_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_sales_orders_select ON sales.sales_orders FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_orders_insert ON sales.sales_orders FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_orders_update ON sales.sales_orders FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_sales_revs_select ON sales.sales_order_revisions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_revs_insert ON sales.sales_order_revisions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_revs_update ON sales.sales_order_revisions FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_sales_lines_select ON sales.sales_order_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_lines_insert ON sales.sales_order_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_lines_update ON sales.sales_order_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_sales_lines_delete ON sales.sales_order_lines FOR DELETE TO athera_app USING (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_sales_seq_migrator_all ON sales.sales_order_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_sales_orders_migrator_all ON sales.sales_orders FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_sales_revs_migrator_all ON sales.sales_order_revisions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_sales_lines_migrator_all ON sales.sales_order_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_frozen_so_revision_lines ON sales.sales_order_lines;
                DROP TRIGGER IF EXISTS trg_guard_frozen_so_revision_snapshot ON sales.sales_order_revisions;
                DROP FUNCTION IF EXISTS sales.guard_frozen_so_revision_lines();
                DROP FUNCTION IF EXISTS sales.guard_frozen_so_revision_snapshot();
                """);

            migrationBuilder.DropTable(
                name: "sales_order_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_order_sequences",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_order_revisions",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_orders",
                schema: "sales");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS sales;
                """);
        }
    }
}
