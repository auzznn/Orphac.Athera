using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M10_SupplierReturnsReplacementsAndFinancialCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tax_determinations_role",
                schema: "tax",
                table: "tax_determinations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accruals_amounts",
                schema: "gl",
                table: "receipt_accruals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accrual_lines_amt",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accrual_lines_qty",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payable_items_amounts",
                schema: "ap",
                table: "payable_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payable_items_status",
                schema: "ap",
                table: "payable_items");

            migrationBuilder.AddColumn<decimal>(
                name: "returned_amount",
                schema: "gl",
                table: "receipt_accruals",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "returned_base_quantity",
                schema: "gl",
                table: "receipt_accrual_lines",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "returned_line_amount",
                schema: "gl",
                table: "receipt_accrual_lines",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "credited_amount",
                schema: "ap",
                table: "payable_items",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "replacement_authorization_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_sequences",
                schema: "ap",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_credit_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "replacement_sequences",
                schema: "procurement",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replacement_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_replacement_sequences_legal_entities_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_sequences",
                schema: "procurement",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_return_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_returns",
                schema: "procurement",
                columns: table => new
                {
                    supplier_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    return_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    return_case_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolution_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    matrix_disposition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    matrix_blocking_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    matrix_blocking_reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bill_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    payable_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    return_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    carrier_or_dispatch_ref = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tax_return_note_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    approval_policy_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_approval_decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    last_rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    execute_idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_returned_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_inventory_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_grni_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_ppn_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_note_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_ppv_difference_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_returns", x => x.supplier_return_id);
                    table.UniqueConstraint("AK_supplier_returns_tenant_id_legal_entity_id_supplier_return_~", x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_id });
                    table.CheckConstraint("ck_supplier_returns_case", "return_case_type IN ('UNVALUED_DOCK_REJECTION', 'VALUED_UNBILLED_RETURN', 'VALUED_BILLED_RETURN')");
                    table.CheckConstraint("ck_supplier_returns_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_supplier_returns_disposition", "matrix_disposition IN ('SUPPORTED', 'BLOCKED_POLICY_OD05', 'BLOCKED_POLICY_OD07', 'BLOCKED_CLOSED_PERIOD')");
                    table.CheckConstraint("ck_supplier_returns_mode", "resolution_mode IN ('CLOSE_WITHOUT_REPLACEMENT', 'REPLACEMENT_ONLY', 'AP_CREDIT_NOTE')");
                    table.CheckConstraint("ck_supplier_returns_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'EXECUTED', 'CANCELLED')");
                    table.CheckConstraint("ck_supplier_returns_totals", "total_returned_base_quantity > 0 AND total_inventory_credit_amount >= 0 AND total_grni_reversal_amount >= 0 AND total_credit_subtotal_amount >= 0 AND total_ppn_reversal_amount >= 0 AND total_credit_note_amount >= 0");
                    table.ForeignKey(
                        name: "FK_supplier_returns_goods_receipts_tenant_id_legal_entity_id_g~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_journal_entries_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_payable_items_tenant_id_legal_entity_id_pa~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.payable_item_id },
                        principalSchema: "ap",
                        principalTable: "payable_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_purchase_orders_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_receipt_accruals_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accruals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_stock_movements_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_supplier_bills_tenant_id_legal_entity_id_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_returns_tenant_memberships_tenant_id_created_by_us~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credits",
                schema: "ap",
                columns: table => new
                {
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payable_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tax_return_note_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    inventory_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppv_difference_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credits", x => x.supplier_credit_id);
                    table.UniqueConstraint("AK_supplier_credits_tenant_id_legal_entity_id_supplier_credit_~", x => new { x.tenant_id, x.legal_entity_id, x.supplier_credit_id });
                    table.CheckConstraint("ck_supplier_credits_amounts", "subtotal_credit_amount > 0 AND ppn_reversal_amount >= 0 AND total_credit_amount = subtotal_credit_amount + ppn_reversal_amount AND inventory_credit_amount > 0 AND (subtotal_credit_amount - inventory_credit_amount) = ppv_difference_amount");
                    table.CheckConstraint("ck_supplier_credits_currency", "currency_code = 'IDR'");
                    table.ForeignKey(
                        name: "FK_supplier_credits_journal_entries_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_payable_items_tenant_id_legal_entity_id_pa~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.payable_item_id },
                        principalSchema: "ap",
                        principalTable: "payable_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_stock_movements_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_supplier_bills_tenant_id_legal_entity_id_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_supplier_returns_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_returns",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credits_tenant_memberships_tenant_id_created_by_us~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_lines",
                schema: "procurement",
                columns: table => new
                {
                    supplier_return_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_accrual_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_stock_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    returned_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    authorized_unit_cost_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    inventory_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    grni_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    credit_subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppv_difference_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_notes = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_return_lines", x => x.supplier_return_line_id);
                    table.UniqueConstraint("AK_supplier_return_lines_tenant_id_legal_entity_id_supplier_re~", x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_line_id });
                    table.CheckConstraint("ck_supplier_return_lines_amounts", "returned_base_quantity > 0 AND authorized_unit_cost_base > 0 AND inventory_credit_amount >= 0 AND grni_reversal_amount >= 0 AND credit_subtotal_amount >= 0 AND ppn_reversal_amount >= 0 AND total_credit_amount >= 0");
                    table.CheckConstraint("ck_supplier_return_lines_number", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_goods_receipt_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_locations_tenant_id_legal_entity_id_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_location_id },
                        principalSchema: "inventory",
                        principalTable: "locations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_purchase_order_lines_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_receipt_accrual_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_line_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accrual_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_stock_lots_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_stock_lot_id },
                        principalSchema: "inventory",
                        principalTable: "stock_lots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_supplier_bill_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_line_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bill_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_supplier_returns_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_returns",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_valued_acceptance_lines_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_line_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptance_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "replacement_authorizations",
                schema: "procurement",
                columns: table => new
                {
                    replacement_authorization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_return_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_case_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    source_goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authorized_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    delivered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    remaining_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    commercial_incremental_unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    authorized_inventory_unit_cost_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replacement_authorizations", x => x.replacement_authorization_id);
                    table.UniqueConstraint("AK_replacement_authorizations_tenant_id_legal_entity_id_replac~", x => new { x.tenant_id, x.legal_entity_id, x.replacement_authorization_id });
                    table.CheckConstraint("ck_replacement_auth_case", "source_case_type IN ('UNVALUED_DOCK_REJECTION', 'VALUED_UNBILLED_RETURN', 'VALUED_BILLED_RETURN')");
                    table.CheckConstraint("ck_replacement_auth_cost", "commercial_incremental_unit_price = 0 AND authorized_inventory_unit_cost_base > 0");
                    table.CheckConstraint("ck_replacement_auth_qty", "authorized_base_quantity > 0 AND delivered_base_quantity >= 0 AND accepted_base_quantity >= 0 AND remaining_base_quantity >= 0 AND accepted_base_quantity <= delivered_base_quantity AND authorized_base_quantity = delivered_base_quantity + remaining_base_quantity");
                    table.CheckConstraint("ck_replacement_auth_status", "status IN ('ACTIVE', 'PARTIALLY_RECEIVED', 'FULFILLED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_legal_entities_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_partners_tenant_id_supplier_part~",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_purchase_order_lines_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_purchase_orders_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_supplier_return_lines_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_line_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_return_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_return_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_supplier_returns_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_returns",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_tenant_memberships_tenant_id_app~",
                        columns: x => new { x.tenant_id, x.approved_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_tenant_memberships_tenant_id_cre~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replacement_authorizations_units_of_measure_tenant_id_base_~",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_lines",
                schema: "ap",
                columns: table => new
                {
                    supplier_credit_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    supplier_return_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credited_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_price_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    subtotal_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_reversal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    inventory_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppv_difference_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_lines", x => x.supplier_credit_line_id);
                    table.CheckConstraint("ck_supplier_credit_lines_amounts", "credited_base_quantity > 0 AND unit_price_base > 0 AND subtotal_credit_amount > 0 AND ppn_reversal_amount >= 0 AND total_credit_amount = subtotal_credit_amount + ppn_reversal_amount AND inventory_credit_amount > 0 AND (subtotal_credit_amount - inventory_credit_amount) = ppv_difference_amount");
                    table.CheckConstraint("ck_supplier_credit_lines_number", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_supplier_bill_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_line_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bill_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_supplier_credits_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_credit_id },
                        principalSchema: "ap",
                        principalTable: "supplier_credits",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_credit_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_supplier_return_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_line_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_return_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_return_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries",
                sql: "entry_type IN ('RECEIPT_ACCEPTANCE', 'OPERATIONAL_ISSUE', 'INVOICE_PRICE_ADJUSTMENT', 'PURCHASE_RETURN_OUTBOUND')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries",
                sql: "previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity >= 0 AND new_pool_carrying_value >= 0 AND new_moving_average_cost >= 0 AND ((entry_type = 'RECEIPT_ACCEPTANCE' AND quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type IN ('OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND') AND quantity_delta < 0 AND value_delta < 0 AND unit_cost_applied > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type = 'INVOICE_PRICE_ADJUSTMENT' AND quantity_delta = 0 AND value_delta <> 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tax_determinations_role",
                schema: "tax",
                table: "tax_determinations",
                sql: "tax_role IN ('PPN_INPUT', 'PPH_WITHHOLDING', 'PPN_INPUT_REVERSAL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accruals_amounts",
                schema: "gl",
                table: "receipt_accruals",
                sql: "accrued_amount > 0 AND cleared_amount >= 0 AND returned_amount >= 0 AND remaining_open_amount >= 0 AND accrued_amount = cleared_amount + returned_amount + remaining_open_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accrual_lines_amt",
                schema: "gl",
                table: "receipt_accrual_lines",
                sql: "unit_cost_base > 0 AND accrued_line_amount > 0 AND cleared_line_amount >= 0 AND returned_line_amount >= 0 AND remaining_open_line_amount >= 0 AND accrued_line_amount = cleared_line_amount + returned_line_amount + remaining_open_line_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accrual_lines_qty",
                schema: "gl",
                table: "receipt_accrual_lines",
                sql: "accepted_base_quantity > 0 AND billed_base_quantity >= 0 AND returned_base_quantity >= 0 AND remaining_unbilled_base_quantity >= 0 AND accepted_base_quantity = billed_base_quantity + returned_base_quantity + remaining_unbilled_base_quantity");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payable_items_amounts",
                schema: "ap",
                table: "payable_items",
                sql: "gross_bill_amount > 0 AND withholding_tax_amount >= 0 AND net_payable_amount > 0 AND paid_amount >= 0 AND credited_amount >= 0 AND remaining_open_amount >= 0 AND net_payable_amount = paid_amount + credited_amount + remaining_open_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payable_items_status",
                schema: "ap",
                table: "payable_items",
                sql: "status IN ('OPEN', 'PARTIALLY_PAID', 'PAID', 'CREDITED', 'REVERSED')");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_replacement_auth",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "replacement_authorization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_replacement_auth_po_status",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_approved_by_user_id",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "approved_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_base_uom_id",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_created_by_user_id",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_item_id",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_legal_entity_id_po_lin~",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_legal_entity_id_suppli~",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" });

            migrationBuilder.CreateIndex(
                name: "IX_replacement_authorizations_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_replacement_auth_number",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "legal_entity_id", "authorization_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_replacement_auth_return_line",
                schema: "procurement",
                table: "replacement_authorizations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_tenant_id_item_id",
                schema: "ap",
                table: "supplier_credit_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_tenant_id_legal_entity_id_supplier_bi~",
                schema: "ap",
                table: "supplier_credit_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_tenant_id_legal_entity_id_supplier_re~",
                schema: "ap",
                table: "supplier_credit_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_credit_lines_number",
                schema: "ap",
                table: "supplier_credit_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_credit_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_created_by_user_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_legal_entity_id_gl_journal_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_legal_entity_id_payable_item_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_legal_entity_id_stock_movement_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_legal_entity_id_supplier_bill_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_tenant_id_supplier_partner_id",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_credits_number",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "credit_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_credits_return",
                schema: "ap",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_return_lines_gr_line",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_base_uom_id",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_item_id",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_receipt_acc~",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_source_loca~",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "source_location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_source_stoc~",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "source_stock_lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_supplier_bi~",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_tenant_id_legal_entity_id_valued_acce~",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_return_lines_number",
                schema: "procurement",
                table: "supplier_return_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_returns_status_date",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "return_date" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_created_by_user_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_gl_journal_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_goods_receipt_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_payable_item_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_purchase_order_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_receipt_accrual_~",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_stock_movement_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_supplier_bill_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_returns_draft_code",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_returns_execute_idempotency",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "execute_idempotency_key" },
                unique: true,
                filter: "execute_idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_returns_number",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "return_number" },
                unique: true,
                filter: "return_number IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_replacement_authorizations_tenant_id_legal_e~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "replacement_authorization_id" },
                principalSchema: "procurement",
                principalTable: "replacement_authorizations",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "replacement_authorization_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                ALTER TABLE procurement.return_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.return_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.replacement_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.replacement_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.supplier_returns ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.supplier_returns FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.supplier_return_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.supplier_return_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.replacement_authorizations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.replacement_authorizations FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.credit_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.credit_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.supplier_credits ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_credits FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.supplier_credit_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_credit_lines FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION procurement.guard_executed_supplier_return()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF OLD.status = 'EXECUTED' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('executed_supplier_return_immutable: Executed supplier return %s is immutable.', OLD.supplier_return_id);
                  END IF;

                  IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_procurement_supplier_returns_immutability
                  BEFORE UPDATE OR DELETE ON procurement.supplier_returns
                  FOR EACH ROW EXECUTE FUNCTION procurement.guard_executed_supplier_return();

                CREATE OR REPLACE FUNCTION procurement.guard_executed_supplier_return_line()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_status text;
                  v_return_id uuid;
                  v_tenant_id uuid;
                  v_entity_id uuid;
                BEGIN
                  v_return_id := COALESCE(NEW.supplier_return_id, OLD.supplier_return_id);
                  v_tenant_id := COALESCE(NEW.tenant_id, OLD.tenant_id);
                  v_entity_id := COALESCE(NEW.legal_entity_id, OLD.legal_entity_id);

                  SELECT status INTO v_status
                  FROM procurement.supplier_returns
                  WHERE tenant_id = v_tenant_id
                    AND legal_entity_id = v_entity_id
                    AND supplier_return_id = v_return_id;

                  IF v_status = 'EXECUTED' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('executed_supplier_return_lines_immutable: Lines of executed supplier return %s are immutable.', v_return_id);
                  END IF;

                  IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_procurement_supplier_return_lines_immutability
                  BEFORE INSERT OR UPDATE OR DELETE ON procurement.supplier_return_lines
                  FOR EACH ROW EXECUTE FUNCTION procurement.guard_executed_supplier_return_line();

                CREATE OR REPLACE FUNCTION ap.guard_supplier_credit_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'supplier_credit_immutable: Posted supplier credits and credit lines are strictly append-only.';
                END;
                $$;

                CREATE TRIGGER trg_ap_supplier_credits_immutability
                  BEFORE UPDATE OR DELETE ON ap.supplier_credits
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_supplier_credit_immutability();

                CREATE TRIGGER trg_ap_supplier_credit_lines_immutability
                  BEFORE UPDATE OR DELETE ON ap.supplier_credit_lines
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_supplier_credit_immutability();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    GRANT USAGE ON SCHEMA procurement, ap TO athera_app;

                    REVOKE ALL ON TABLE
                      procurement.return_sequences,
                      procurement.replacement_sequences,
                      procurement.supplier_returns,
                      procurement.supplier_return_lines,
                      procurement.replacement_authorizations,
                      ap.credit_sequences,
                      ap.supplier_credits,
                      ap.supplier_credit_lines
                    FROM PUBLIC, athera_app;

                    -- Mutable operational tables (protected by RLS and EXECUTED immutability triggers)
                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      procurement.return_sequences,
                      procurement.replacement_sequences,
                      procurement.supplier_returns,
                      procurement.replacement_authorizations,
                      ap.credit_sequences
                    TO athera_app;

                    -- Draft return line editing (blocked once header becomes EXECUTED)
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      procurement.supplier_return_lines
                    TO athera_app;

                    -- Strictly append-only AP credit ledger: zero UPDATE or DELETE for runtime role.
                    GRANT SELECT, INSERT ON TABLE
                      ap.supplier_credits,
                      ap.supplier_credit_lines
                    TO athera_app;

                    CREATE POLICY p_proc_ret_seq_select ON procurement.return_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_seq_insert ON procurement.return_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_seq_update ON procurement.return_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_rep_seq_select ON procurement.replacement_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rep_seq_insert ON procurement.replacement_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rep_seq_update ON procurement.replacement_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_ret_select ON procurement.supplier_returns FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_insert ON procurement.supplier_returns FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_update ON procurement.supplier_returns FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_ret_lines_select ON procurement.supplier_return_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_lines_insert ON procurement.supplier_return_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_lines_update ON procurement.supplier_return_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_ret_lines_delete ON procurement.supplier_return_lines FOR DELETE TO athera_app USING (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_rep_auth_select ON procurement.replacement_authorizations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rep_auth_insert ON procurement.replacement_authorizations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rep_auth_update ON procurement.replacement_authorizations FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_cred_seq_select ON ap.credit_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_cred_seq_insert ON ap.credit_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_cred_seq_update ON ap.credit_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_credits_select ON ap.supplier_credits FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_credits_insert ON ap.supplier_credits FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_credit_lines_select ON ap.supplier_credit_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_credit_lines_insert ON ap.supplier_credit_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    GRANT USAGE ON SCHEMA procurement, ap TO athera_migrator;
                    CREATE POLICY p_proc_ret_seq_migrator_all ON procurement.return_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_rep_seq_migrator_all ON procurement.replacement_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_ret_migrator_all ON procurement.supplier_returns FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_ret_lines_migrator_all ON procurement.supplier_return_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_rep_auth_migrator_all ON procurement.replacement_authorizations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_cred_seq_migrator_all ON ap.credit_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_credits_migrator_all ON ap.supplier_credits FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_credit_lines_migrator_all ON ap.supplier_credit_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_procurement_supplier_returns_immutability ON procurement.supplier_returns;
                DROP TRIGGER IF EXISTS trg_procurement_supplier_return_lines_immutability ON procurement.supplier_return_lines;
                DROP TRIGGER IF EXISTS trg_ap_supplier_credits_immutability ON ap.supplier_credits;
                DROP TRIGGER IF EXISTS trg_ap_supplier_credit_lines_immutability ON ap.supplier_credit_lines;
                DROP FUNCTION IF EXISTS procurement.guard_executed_supplier_return();
                DROP FUNCTION IF EXISTS procurement.guard_executed_supplier_return_line();
                DROP FUNCTION IF EXISTS ap.guard_supplier_credit_immutability();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_replacement_authorizations_tenant_id_legal_e~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropTable(
                name: "credit_sequences",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "replacement_authorizations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "replacement_sequences",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "return_sequences",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_credit_lines",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "supplier_credits",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "supplier_return_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_returns",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tax_determinations_role",
                schema: "tax",
                table: "tax_determinations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accruals_amounts",
                schema: "gl",
                table: "receipt_accruals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accrual_lines_amt",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_accrual_lines_qty",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payable_items_amounts",
                schema: "ap",
                table: "payable_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payable_items_status",
                schema: "ap",
                table: "payable_items");

            migrationBuilder.DropIndex(
                name: "ix_goods_receipts_replacement_auth",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "returned_amount",
                schema: "gl",
                table: "receipt_accruals");

            migrationBuilder.DropColumn(
                name: "returned_base_quantity",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropColumn(
                name: "returned_line_amount",
                schema: "gl",
                table: "receipt_accrual_lines");

            migrationBuilder.DropColumn(
                name: "credited_amount",
                schema: "ap",
                table: "payable_items");

            migrationBuilder.DropColumn(
                name: "replacement_authorization_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.Sql("""
                DELETE FROM tax.tax_determinations WHERE tax_role = 'PPN_INPUT_REVERSAL';
                DELETE FROM inventory.valuation_entries WHERE entry_type = 'PURCHASE_RETURN_OUTBOUND';
                DELETE FROM inventory.movement_lines ml
                USING inventory.stock_movements sm
                WHERE ml.stock_movement_id = sm.stock_movement_id
                  AND sm.movement_type = 'PURCHASE_RETURN_OUTBOUND';
                DELETE FROM inventory.stock_movements WHERE movement_type = 'PURCHASE_RETURN_OUTBOUND';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries",
                sql: "entry_type IN ('RECEIPT_ACCEPTANCE', 'OPERATIONAL_ISSUE', 'INVOICE_PRICE_ADJUSTMENT')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries",
                sql: "previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity >= 0 AND new_pool_carrying_value >= 0 AND new_moving_average_cost >= 0 AND ((entry_type = 'RECEIPT_ACCEPTANCE' AND quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type = 'OPERATIONAL_ISSUE' AND quantity_delta < 0 AND value_delta < 0 AND unit_cost_applied > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type = 'INVOICE_PRICE_ADJUSTMENT' AND quantity_delta = 0 AND value_delta <> 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tax_determinations_role",
                schema: "tax",
                table: "tax_determinations",
                sql: "tax_role IN ('PPN_INPUT', 'PPH_WITHHOLDING')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accruals_amounts",
                schema: "gl",
                table: "receipt_accruals",
                sql: "accrued_amount > 0 AND cleared_amount >= 0 AND remaining_open_amount >= 0 AND accrued_amount = cleared_amount + remaining_open_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accrual_lines_amt",
                schema: "gl",
                table: "receipt_accrual_lines",
                sql: "unit_cost_base > 0 AND accrued_line_amount > 0 AND cleared_line_amount >= 0 AND remaining_open_line_amount >= 0 AND accrued_line_amount = cleared_line_amount + remaining_open_line_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_accrual_lines_qty",
                schema: "gl",
                table: "receipt_accrual_lines",
                sql: "accepted_base_quantity > 0 AND billed_base_quantity >= 0 AND remaining_unbilled_base_quantity >= 0 AND accepted_base_quantity = billed_base_quantity + remaining_unbilled_base_quantity");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payable_items_amounts",
                schema: "ap",
                table: "payable_items",
                sql: "gross_bill_amount > 0 AND withholding_tax_amount >= 0 AND net_payable_amount > 0 AND paid_amount >= 0 AND remaining_open_amount >= 0 AND net_payable_amount = paid_amount + remaining_open_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payable_items_status",
                schema: "ap",
                table: "payable_items",
                sql: "status IN ('OPEN', 'PARTIALLY_PAID', 'PAID', 'REVERSED')");
        }
    }
}
