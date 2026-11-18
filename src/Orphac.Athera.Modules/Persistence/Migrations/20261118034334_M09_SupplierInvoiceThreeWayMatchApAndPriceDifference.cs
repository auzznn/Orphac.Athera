using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M09_SupplierInvoiceThreeWayMatchApAndPriceDifference : Migration
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
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.EnsureSchema(
                name: "ap");

            migrationBuilder.AlterColumn<Guid>(
                name: "stock_movement_id",
                schema: "inventory",
                table: "valuation_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "movement_line_id",
                schema: "inventory",
                table: "valuation_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_valuation_entries_tenant_id_legal_entity_id_valuation_entry~",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "valuation_entry_id" });

            migrationBuilder.CreateTable(
                name: "bill_sequences",
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
                    table.PrimaryKey("PK_bill_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_bill_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_match_policies",
                schema: "ap",
                columns: table => new
                {
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pp_tolerance_percent = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    pp_tolerance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    dq_tolerance_percent = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    dq_tolerance_base_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    bd_write_off_limit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    bd_write_off_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_match_policies", x => x.policy_id);
                    table.UniqueConstraint("AK_invoice_match_policies_tenant_id_legal_entity_id_policy_id", x => new { x.tenant_id, x.legal_entity_id, x.policy_id });
                    table.CheckConstraint("ck_inv_match_policies_limits", "pp_tolerance_percent >= 0 AND pp_tolerance_amount >= 0 AND dq_tolerance_percent >= 0 AND dq_tolerance_base_qty >= 0 AND bd_write_off_limit_amount >= 0");
                    table.CheckConstraint("ck_inv_match_policies_review", "review_status IN ('UNREVIEWED_SAFE_DEFAULT', 'REVIEWED_INTERNAL')");
                    table.CheckConstraint("ck_inv_match_policies_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_invoice_match_policies_accounts_tenant_id_legal_entity_id_b~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bd_write_off_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_policies_legal_entities_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payable_sequences",
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
                    table.PrimaryKey("PK_payable_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_payable_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bills",
                schema: "ap",
                columns: table => new
                {
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bill_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bill_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    supplier_invoice_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tax_invoice_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    withholding_slip_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    declared_gross_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    lines_net_subtotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_grni_clear_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_price_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_inventory_adj_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_pnl_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_bd_write_off_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_tax_total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    withholding_tax_total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    computed_gross_total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    net_payable_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    match_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    payment_block_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    payment_block_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    approval_policy_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_approval_decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    last_rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    variance_approved = table.Column<bool>(type: "boolean", nullable: false),
                    posted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    posted_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    post_idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bills", x => x.supplier_bill_id);
                    table.UniqueConstraint("AK_supplier_bills_tenant_id_legal_entity_id_supplier_bill_id", x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id });
                    table.CheckConstraint("ck_supplier_bills_amounts", "declared_gross_amount > 0 AND lines_net_subtotal >= 0 AND total_grni_clear_amount >= 0 AND ppn_tax_total >= 0 AND withholding_tax_total >= 0 AND computed_gross_total >= 0 AND net_payable_amount >= 0 AND due_date >= invoice_date");
                    table.CheckConstraint("ck_supplier_bills_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_supplier_bills_match_status", "match_status IN ('MATCHED', 'WITHIN_TOLERANCE', 'APPROVAL_REQUIRED', 'POSTING_BLOCKED')");
                    table.CheckConstraint("ck_supplier_bills_pay_block", "payment_block_status IN ('NONE', 'PAYMENT_BLOCKED', 'RELEASED')");
                    table.CheckConstraint("ck_supplier_bills_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'POSTED', 'CANCELLED')");
                    table.CheckConstraint("ck_supplier_bills_type", "bill_type IN ('PO_THREE_WAY_MATCH')");
                    table.ForeignKey(
                        name: "FK_supplier_bills_journal_entries_tenant_id_legal_entity_id_po~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.posted_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_purchase_order_revisions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_purchase_orders_tenant_id_legal_entity_id_pu~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_tenant_memberships_tenant_id_created_by_user~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_determinations",
                schema: "tax",
                columns: table => new
                {
                    determination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tax_family = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    treatment_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    taxable_base_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    nominal_rate_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    effective_rate_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    evidence_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    documentary_evidence_ref = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    counterparty_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_determinations", x => x.determination_id);
                    table.CheckConstraint("ck_tax_determinations_amounts", "gross_amount > 0 AND taxable_base_amount >= 0 AND nominal_rate_percent >= 0 AND effective_rate_percent >= 0 AND tax_amount >= 0");
                    table.CheckConstraint("ck_tax_determinations_role", "tax_role IN ('PPN_INPUT', 'PPH_WITHHOLDING')");
                    table.ForeignKey(
                        name: "FK_tax_determinations_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_determinations_tax_codes_tenant_id_legal_entity_id_tax_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_determinations_tenant_memberships_tenant_id_created_by_~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_match_evaluations",
                schema: "ap",
                columns: table => new
                {
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_stage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    dq_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pp_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bd_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    overall_match_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_posting_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    requires_payment_block = table.Column<bool>(type: "boolean", nullable: false),
                    blocking_reasons_json = table.Column<string>(type: "jsonb", nullable: false),
                    comparison_json = table.Column<string>(type: "jsonb", nullable: false),
                    evaluated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_match_evaluations", x => x.evaluation_id);
                    table.CheckConstraint("ck_inv_match_eval_outcomes", "dq_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND pp_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND bd_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND overall_match_status IN ('MATCHED', 'WITHIN_TOLERANCE', 'APPROVAL_REQUIRED', 'POSTING_BLOCKED')");
                    table.CheckConstraint("ck_inv_match_eval_stage", "evaluation_stage IN ('DRAFT_PREVIEW', 'SUBMIT', 'POST_COMMIT')");
                    table.ForeignKey(
                        name: "FK_invoice_match_evaluations_invoice_match_policies_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_id },
                        principalSchema: "ap",
                        principalTable: "invoice_match_policies",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_evaluations_supplier_bills_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_evaluations_tenant_memberships_tenant_id_eval~",
                        columns: x => new { x.tenant_id, x.evaluated_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_price_adjustments",
                schema: "inventory",
                columns: table => new
                {
                    adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    pool_on_hand_before_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    covered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    uncovered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_price_diff_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    inventory_adjustment_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    pnl_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    previous_moving_average_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_moving_average_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_price_adjustments", x => x.adjustment_id);
                    table.CheckConstraint("ck_inv_price_adj_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_inv_price_adj_qty", "billed_base_quantity > 0 AND pool_on_hand_before_qty >= 0 AND covered_base_quantity >= 0 AND uncovered_base_quantity >= 0 AND billed_base_quantity = covered_base_quantity + uncovered_base_quantity AND total_price_diff_amount = inventory_adjustment_amount + pnl_variance_amount");
                    table.ForeignKey(
                        name: "FK_invoice_price_adjustments_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_price_adjustments_journal_entries_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_price_adjustments_supplier_bills_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_price_adjustments_valuation_entries_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valuation_entry_id },
                        principalSchema: "inventory",
                        principalTable: "valuation_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valuation_entry_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_price_adjustments_valuation_pools_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valuation_pool_id },
                        principalSchema: "inventory",
                        principalTable: "valuation_pools",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valuation_pool_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payable_items",
                schema: "ap",
                columns: table => new
                {
                    payable_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payable_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_invoice_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    gross_bill_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    withholding_tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    net_payable_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    remaining_open_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    payment_block_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    payment_block_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payable_items", x => x.payable_item_id);
                    table.UniqueConstraint("AK_payable_items_tenant_id_legal_entity_id_payable_item_id", x => new { x.tenant_id, x.legal_entity_id, x.payable_item_id });
                    table.CheckConstraint("ck_payable_items_amounts", "gross_bill_amount > 0 AND withholding_tax_amount >= 0 AND net_payable_amount > 0 AND paid_amount >= 0 AND remaining_open_amount >= 0 AND net_payable_amount = paid_amount + remaining_open_amount");
                    table.CheckConstraint("ck_payable_items_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_payable_items_pay_block", "payment_block_status IN ('NONE', 'PAYMENT_BLOCKED', 'RELEASED')");
                    table.CheckConstraint("ck_payable_items_status", "status IN ('OPEN', 'PARTIALLY_PAID', 'PAID', 'REVERSED')");
                    table.ForeignKey(
                        name: "FK_payable_items_journal_entries_tenant_id_legal_entity_id_gl_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payable_items_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payable_items_supplier_bills_tenant_id_legal_entity_id_supp~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_accrual_clearings",
                schema: "gl",
                columns: table => new
                {
                    clearing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_accrual_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cleared_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    cleared_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_accrual_clearings", x => x.clearing_id);
                    table.CheckConstraint("ck_receipt_accrual_clearings_pos", "cleared_base_quantity > 0 AND cleared_amount > 0");
                    table.ForeignKey(
                        name: "FK_receipt_accrual_clearings_journal_entries_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_clearings_receipt_accrual_lines_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_line_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accrual_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_clearings_receipt_accruals_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accruals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_clearings_supplier_bills_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bill_lines",
                schema: "ap",
                columns: table => new
                {
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    receipt_accrual_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordered_base_qty_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    accepted_unbilled_qty_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    po_unit_price_functional = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    invoice_unit_price_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    line_net_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    grni_clear_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    price_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ppn_tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ppn_tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    withholding_tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    withholding_tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    withholding_tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    dq_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pp_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    line_notes = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bill_lines", x => x.supplier_bill_line_id);
                    table.UniqueConstraint("AK_supplier_bill_lines_tenant_id_legal_entity_id_supplier_bill~", x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_line_id });
                    table.CheckConstraint("ck_supplier_bill_lines_num", "line_number >= 1");
                    table.CheckConstraint("ck_supplier_bill_lines_outcomes", "dq_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND pp_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED')");
                    table.CheckConstraint("ck_supplier_bill_lines_values", "billed_base_quantity > 0 AND invoice_unit_price_base > 0 AND line_net_amount > 0 AND grni_clear_amount >= 0 AND ppn_tax_amount >= 0 AND withholding_tax_amount >= 0");
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_goods_receipt_lines_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_purchase_order_lines_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_receipt_accrual_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_line_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accrual_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_supplier_bills_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_tax_codes_tenant_id_legal_entity_id_ppn~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.ppn_tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_tax_codes_tenant_id_legal_entity_id_wit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.withholding_tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_lines_valued_acceptance_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_line_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptance_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bill_receipt_matches",
                schema: "ap",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_accrual_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    receipt_unit_cost_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    invoice_unit_price_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    grni_cleared_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    price_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    covered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    inventory_adjustment_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    pnl_variance_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bill_receipt_matches", x => x.match_id);
                    table.CheckConstraint("ck_bill_receipt_match_qty", "matched_base_quantity > 0 AND grni_cleared_amount > 0 AND covered_base_quantity >= 0");
                    table.ForeignKey(
                        name: "FK_supplier_bill_receipt_matches_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_receipt_matches_receipt_accrual_lines_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_line_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accrual_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_receipt_matches_receipt_accruals_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accruals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_receipt_matches_supplier_bill_lines_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_line_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bill_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bill_receipt_matches_supplier_bills_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines",
                sql: "base_quantity_delta <> 0 AND unit_valuation_cost >= 0 AND ((base_quantity_delta > 0 AND line_valuation_amount >= 0) OR (base_quantity_delta < 0 AND line_valuation_amount <= 0))");

            migrationBuilder.CreateIndex(
                name: "ix_inv_match_eval_bill_time",
                schema: "ap",
                table: "invoice_match_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id", "evaluated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_evaluations_tenant_id_evaluated_by_user_id",
                schema: "ap",
                table: "invoice_match_evaluations",
                columns: new[] { "tenant_id", "evaluated_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_evaluations_tenant_id_legal_entity_id_policy_~",
                schema: "ap",
                table: "invoice_match_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_policies_tenant_id_legal_entity_id_bd_write_o~",
                schema: "ap",
                table: "invoice_match_policies",
                columns: new[] { "tenant_id", "legal_entity_id", "bd_write_off_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_match_policies_code",
                schema: "ap",
                table: "invoice_match_policies",
                columns: new[] { "tenant_id", "legal_entity_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_price_adjustments_tenant_id_item_id",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_price_adjustments_tenant_id_legal_entity_id_gl_jour~",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_price_adjustments_tenant_id_legal_entity_id_supplie~",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_price_adjustments_tenant_id_legal_entity_id_valuat~1",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "legal_entity_id", "valuation_pool_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_price_adjustments_tenant_id_legal_entity_id_valuati~",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "legal_entity_id", "valuation_entry_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_price_adj_bill_line",
                schema: "inventory",
                table: "invoice_price_adjustments",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payable_items_tenant_id_legal_entity_id_gl_journal_id",
                schema: "ap",
                table: "payable_items",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payable_items_tenant_id_supplier_partner_id",
                schema: "ap",
                table: "payable_items",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_payable_items_bill",
                schema: "ap",
                table: "payable_items",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payable_items_number",
                schema: "ap",
                table: "payable_items",
                columns: new[] { "tenant_id", "legal_entity_id", "payable_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_clearings_tenant_id_legal_entity_id_gl_jour~",
                schema: "gl",
                table: "receipt_accrual_clearings",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_clearings_tenant_id_legal_entity_id_receip~1",
                schema: "gl",
                table: "receipt_accrual_clearings",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_clearings_tenant_id_legal_entity_id_receipt~",
                schema: "gl",
                table: "receipt_accrual_clearings",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_clearings_tenant_id_legal_entity_id_supplie~",
                schema: "gl",
                table: "receipt_accrual_clearings",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receipt_accrual_clearings_bill_line",
                schema: "gl",
                table: "receipt_accrual_clearings",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_base_uom_id",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_item_id",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_ppn_tax_code_~",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "ppn_tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_receipt_accru~",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_valued_accept~",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_lines_tenant_id_legal_entity_id_withholding_t~",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "withholding_tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_bill_lines_accrual",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id", "receipt_accrual_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_bill_lines_num",
                schema: "ap",
                table: "supplier_bill_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_receipt_matches_tenant_id_item_id",
                schema: "ap",
                table: "supplier_bill_receipt_matches",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_receipt_matches_tenant_id_legal_entity_id_re~1",
                schema: "ap",
                table: "supplier_bill_receipt_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_receipt_matches_tenant_id_legal_entity_id_rec~",
                schema: "ap",
                table: "supplier_bill_receipt_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_receipt_matches_tenant_id_legal_entity_id_sup~",
                schema: "ap",
                table: "supplier_bill_receipt_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bill_receipt_match_bill_line",
                schema: "ap",
                table: "supplier_bill_receipt_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_bills_status_date",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_tenant_id_created_by_user_id",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_tenant_id_legal_entity_id_posted_journal_id",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "posted_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_tenant_id_legal_entity_id_purchase_order_id",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_tenant_id_legal_entity_id_purchase_order_rev~",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_revision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_tenant_id_supplier_partner_id",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_bills_bill_number",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "bill_number" },
                unique: true,
                filter: "bill_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_bills_draft_code",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_bills_vendor_invoice",
                schema: "ap",
                table: "supplier_bills",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_partner_id", "supplier_invoice_number" },
                unique: true,
                filter: "status <> 'CANCELLED'");

            migrationBuilder.CreateIndex(
                name: "IX_tax_determinations_tenant_id_created_by_user_id",
                schema: "tax",
                table: "tax_determinations",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_determinations_tenant_id_legal_entity_id_tax_code_id",
                schema: "tax",
                table: "tax_determinations",
                columns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "ux_tax_determinations_doc_line_role",
                schema: "tax",
                table: "tax_determinations",
                columns: new[] { "tenant_id", "legal_entity_id", "source_document_id", "source_document_line_id", "tax_role" },
                unique: true);

            migrationBuilder.Sql("""
                -- PostgreSQL RLS across all 11 S09 tables (fail-closed when app.tenant_id is absent/mismatched)
                ALTER TABLE ap.bill_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.bill_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.payable_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.payable_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.invoice_match_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.invoice_match_policies FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.supplier_bills ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_bills FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.supplier_bill_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_bill_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.invoice_match_evaluations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.invoice_match_evaluations FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.supplier_bill_receipt_matches ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_bill_receipt_matches FORCE ROW LEVEL SECURITY;

                ALTER TABLE ap.payable_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.payable_items FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.receipt_accrual_clearings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.receipt_accrual_clearings FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.invoice_price_adjustments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.invoice_price_adjustments FORCE ROW LEVEL SECURITY;

                ALTER TABLE tax.tax_determinations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tax.tax_determinations FORCE ROW LEVEL SECURITY;

                -- DB-level immutability guards on POSTED supplier bills and their lines
                CREATE OR REPLACE FUNCTION ap.guard_posted_supplier_bill()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    IF OLD.status = 'POSTED' THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('posted_supplier_bill_immutable: Posted supplier bill %s cannot be deleted.', OLD.supplier_bill_id);
                    END IF;
                    RETURN OLD;
                  END IF;

                  IF OLD.status = 'POSTED' THEN
                    IF NEW.supplier_bill_id IS DISTINCT FROM OLD.supplier_bill_id
                       OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                       OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                       OR NEW.draft_code IS DISTINCT FROM OLD.draft_code
                       OR NEW.bill_number IS DISTINCT FROM OLD.bill_number
                       OR NEW.bill_type IS DISTINCT FROM OLD.bill_type
                       OR NEW.purchase_order_id IS DISTINCT FROM OLD.purchase_order_id
                       OR NEW.purchase_order_revision_id IS DISTINCT FROM OLD.purchase_order_revision_id
                       OR NEW.po_number_snapshot IS DISTINCT FROM OLD.po_number_snapshot
                       OR NEW.supplier_partner_id IS DISTINCT FROM OLD.supplier_partner_id
                       OR NEW.supplier_code_snapshot IS DISTINCT FROM OLD.supplier_code_snapshot
                       OR NEW.supplier_legal_name_snapshot IS DISTINCT FROM OLD.supplier_legal_name_snapshot
                       OR NEW.supplier_npwp_snapshot IS DISTINCT FROM OLD.supplier_npwp_snapshot
                       OR NEW.supplier_invoice_number IS DISTINCT FROM OLD.supplier_invoice_number
                       OR NEW.tax_invoice_number IS DISTINCT FROM OLD.tax_invoice_number
                       OR NEW.withholding_slip_reference IS DISTINCT FROM OLD.withholding_slip_reference
                       OR NEW.invoice_date IS DISTINCT FROM OLD.invoice_date
                       OR NEW.due_date IS DISTINCT FROM OLD.due_date
                       OR NEW.currency_code IS DISTINCT FROM OLD.currency_code
                       OR NEW.declared_gross_amount IS DISTINCT FROM OLD.declared_gross_amount
                       OR NEW.lines_net_subtotal IS DISTINCT FROM OLD.lines_net_subtotal
                       OR NEW.total_grni_clear_amount IS DISTINCT FROM OLD.total_grni_clear_amount
                       OR NEW.total_price_variance_amount IS DISTINCT FROM OLD.total_price_variance_amount
                       OR NEW.total_inventory_adj_amount IS DISTINCT FROM OLD.total_inventory_adj_amount
                       OR NEW.total_pnl_variance_amount IS DISTINCT FROM OLD.total_pnl_variance_amount
                       OR NEW.total_bd_write_off_amount IS DISTINCT FROM OLD.total_bd_write_off_amount
                       OR NEW.ppn_tax_total IS DISTINCT FROM OLD.ppn_tax_total
                       OR NEW.withholding_tax_total IS DISTINCT FROM OLD.withholding_tax_total
                       OR NEW.computed_gross_total IS DISTINCT FROM OLD.computed_gross_total
                       OR NEW.net_payable_amount IS DISTINCT FROM OLD.net_payable_amount
                       OR NEW.status IS DISTINCT FROM OLD.status
                       OR NEW.match_status IS DISTINCT FROM OLD.match_status
                       OR NEW.approval_policy_code IS DISTINCT FROM OLD.approval_policy_code
                       OR NEW.last_approval_decision IS DISTINCT FROM OLD.last_approval_decision
                       OR NEW.last_rejection_reason IS DISTINCT FROM OLD.last_rejection_reason
                       OR NEW.submitted_by_user_id IS DISTINCT FROM OLD.submitted_by_user_id
                       OR NEW.submitted_at_utc IS DISTINCT FROM OLD.submitted_at_utc
                       OR NEW.approved_by_user_id IS DISTINCT FROM OLD.approved_by_user_id
                       OR NEW.approved_at_utc IS DISTINCT FROM OLD.approved_at_utc
                       OR NEW.variance_approved IS DISTINCT FROM OLD.variance_approved
                       OR NEW.posted_by_user_id IS DISTINCT FROM OLD.posted_by_user_id
                       OR NEW.posted_at_utc IS DISTINCT FROM OLD.posted_at_utc
                       OR NEW.posted_journal_id IS DISTINCT FROM OLD.posted_journal_id
                       OR NEW.post_idempotency_key IS DISTINCT FROM OLD.post_idempotency_key
                       OR NEW.notes IS DISTINCT FROM OLD.notes
                       OR NEW.created_by_user_id IS DISTINCT FROM OLD.created_by_user_id
                       OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('posted_supplier_bill_immutable: Accounting fields of posted supplier bill %s cannot be modified.', OLD.supplier_bill_id);
                    END IF;
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_ap_supplier_bills_immutability
                  BEFORE UPDATE OR DELETE ON ap.supplier_bills
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_posted_supplier_bill();

                CREATE OR REPLACE FUNCTION ap.guard_posted_supplier_bill_line()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_bill_status text;
                  v_bill_id uuid;
                  v_tenant_id uuid;
                  v_entity_id uuid;
                BEGIN
                  v_bill_id := COALESCE(NEW.supplier_bill_id, OLD.supplier_bill_id);
                  v_tenant_id := COALESCE(NEW.tenant_id, OLD.tenant_id);
                  v_entity_id := COALESCE(NEW.legal_entity_id, OLD.legal_entity_id);

                  SELECT status INTO v_bill_status
                  FROM ap.supplier_bills
                  WHERE tenant_id = v_tenant_id
                    AND legal_entity_id = v_entity_id
                    AND supplier_bill_id = v_bill_id;

                  IF v_bill_status = 'POSTED' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('posted_supplier_bill_lines_immutable: Lines of posted supplier bill %s are immutable.', v_bill_id);
                  END IF;

                  RETURN COALESCE(NEW, OLD);
                END;
                $$;

                CREATE TRIGGER trg_ap_supplier_bill_lines_immutability
                  BEFORE INSERT OR UPDATE OR DELETE ON ap.supplier_bill_lines
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_posted_supplier_bill_line();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA ap FROM PUBLIC;
                    GRANT USAGE ON SCHEMA ap TO athera_app;

                    REVOKE ALL ON TABLE
                      ap.bill_sequences,
                      ap.payable_sequences,
                      ap.invoice_match_policies,
                      ap.supplier_bills,
                      ap.supplier_bill_lines,
                      ap.invoice_match_evaluations,
                      ap.supplier_bill_receipt_matches,
                      ap.payable_items,
                      gl.receipt_accrual_clearings,
                      inventory.invoice_price_adjustments,
                      tax.tax_determinations
                    FROM PUBLIC, athera_app;

                    -- Mutable operational tables (protected by RLS and posted-bill immutability triggers)
                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      ap.bill_sequences,
                      ap.payable_sequences,
                      ap.invoice_match_policies,
                      ap.supplier_bills,
                      ap.payable_items
                    TO athera_app;

                    -- Draft/Rejected line editing (protected by trigger once header becomes POSTED)
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      ap.supplier_bill_lines
                    TO athera_app;

                    -- Strictly append-only ledger/audit/matching tables: zero UPDATE or DELETE for runtime role.
                    GRANT SELECT, INSERT ON TABLE
                      ap.invoice_match_evaluations,
                      ap.supplier_bill_receipt_matches,
                      gl.receipt_accrual_clearings,
                      inventory.invoice_price_adjustments,
                      tax.tax_determinations
                    TO athera_app;

                    CREATE POLICY p_ap_bill_seq_select ON ap.bill_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bill_seq_insert ON ap.bill_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bill_seq_update ON ap.bill_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_pay_seq_select ON ap.payable_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_pay_seq_insert ON ap.payable_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_pay_seq_update ON ap.payable_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_match_pol_select ON ap.invoice_match_policies FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_match_pol_insert ON ap.invoice_match_policies FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_match_pol_update ON ap.invoice_match_policies FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_bills_select ON ap.supplier_bills FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bills_insert ON ap.supplier_bills FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bills_update ON ap.supplier_bills FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_bill_lines_select ON ap.supplier_bill_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bill_lines_insert ON ap.supplier_bill_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bill_lines_update ON ap.supplier_bill_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_bill_lines_delete ON ap.supplier_bill_lines FOR DELETE TO athera_app USING (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_eval_select ON ap.invoice_match_evaluations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_eval_insert ON ap.invoice_match_evaluations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_rcpt_match_select ON ap.supplier_bill_receipt_matches FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_rcpt_match_insert ON ap.supplier_bill_receipt_matches FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_payables_select ON ap.payable_items FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_payables_insert ON ap.payable_items FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_payables_update ON ap.payable_items FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_acc_clr_select ON gl.receipt_accrual_clearings FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_acc_clr_insert ON gl.receipt_accrual_clearings FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_price_adj_select ON inventory.invoice_price_adjustments FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_price_adj_insert ON inventory.invoice_price_adjustments FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tax_det_select ON tax.tax_determinations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_tax_det_insert ON tax.tax_determinations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    GRANT USAGE ON SCHEMA ap TO athera_migrator;
                    CREATE POLICY p_ap_bill_seq_migrator_all ON ap.bill_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_pay_seq_migrator_all ON ap.payable_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_match_pol_migrator_all ON ap.invoice_match_policies FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_bills_migrator_all ON ap.supplier_bills FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_bill_lines_migrator_all ON ap.supplier_bill_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_eval_migrator_all ON ap.invoice_match_evaluations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_rcpt_match_migrator_all ON ap.supplier_bill_receipt_matches FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_payables_migrator_all ON ap.payable_items FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_acc_clr_migrator_all ON gl.receipt_accrual_clearings FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_price_adj_migrator_all ON inventory.invoice_price_adjustments FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_tax_det_migrator_all ON tax.tax_determinations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_ap_supplier_bills_immutability ON ap.supplier_bills;
                DROP TRIGGER IF EXISTS trg_ap_supplier_bill_lines_immutability ON ap.supplier_bill_lines;
                DROP FUNCTION IF EXISTS ap.guard_posted_supplier_bill();
                DROP FUNCTION IF EXISTS ap.guard_posted_supplier_bill_line();
                """);
            migrationBuilder.DropTable(
                name: "bill_sequences",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "invoice_match_evaluations",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "invoice_price_adjustments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "payable_items",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "payable_sequences",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "receipt_accrual_clearings",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "supplier_bill_receipt_matches",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "tax_determinations",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "invoice_match_policies",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "supplier_bill_lines",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "supplier_bills",
                schema: "ap");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_valuation_entries_tenant_id_legal_entity_id_valuation_entry~",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.Sql("""
                DELETE FROM inventory.valuation_entries
                WHERE entry_type IN ('OPERATIONAL_ISSUE', 'INVOICE_PRICE_ADJUSTMENT');

                DELETE FROM inventory.movement_lines ml
                USING inventory.stock_movements sm
                WHERE ml.tenant_id = sm.tenant_id
                  AND ml.legal_entity_id = sm.legal_entity_id
                  AND ml.stock_movement_id = sm.stock_movement_id
                  AND sm.movement_type = 'OPERATIONAL_ISSUE';

                DELETE FROM inventory.stock_movements
                WHERE movement_type = 'OPERATIONAL_ISSUE';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "stock_movement_id",
                schema: "inventory",
                table: "valuation_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "movement_line_id",
                schema: "inventory",
                table: "valuation_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries",
                sql: "entry_type IN ('RECEIPT_ACCEPTANCE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries",
                sql: "quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines",
                sql: "base_quantity_delta <> 0 AND unit_valuation_cost >= 0 AND line_valuation_amount >= 0");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS ap;
                """);
        }
    }
}
