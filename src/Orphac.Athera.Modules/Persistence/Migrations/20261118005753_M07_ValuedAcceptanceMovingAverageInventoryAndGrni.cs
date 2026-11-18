using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M07_ValuedAcceptanceMovingAverageInventoryAndGrni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_goods_receipts_status",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goods_receipts_unvalued_gate1",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gr_lines_s06_unvalued",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "acceptance_sequences",
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
                    table.PrimaryKey("PK_acceptance_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_acceptance_seq_nonneg", "last_number >= 0");
                    table.CheckConstraint("ck_acceptance_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_acceptance_sequences_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movement_sequences",
                schema: "inventory",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movement_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_movement_seq_nonneg", "last_number >= 0");
                    table.CheckConstraint("ck_movement_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_movement_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    movement_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source_module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.stock_movement_id);
                    table.UniqueConstraint("AK_stock_movements_tenant_id_legal_entity_id_stock_movement_id", x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id });
                    table.CheckConstraint("ck_stock_movements_type", "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE')");
                    table.ForeignKey(
                        name: "FK_stock_movements_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_tenant_memberships_tenant_id_created_by_use~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_positions",
                schema: "inventory",
                columns: table => new
                {
                    stock_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_group_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unrestricted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    quality_inspection_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    blocked_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_valued_on_hand_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    available_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_positions", x => x.stock_position_id);
                    table.UniqueConstraint("AK_stock_positions_tenant_id_legal_entity_id_stock_position_id", x => new { x.tenant_id, x.legal_entity_id, x.stock_position_id });
                    table.CheckConstraint("ck_stock_positions_balance", "total_valued_on_hand_base_quantity = unrestricted_base_quantity + quality_inspection_base_quantity + blocked_base_quantity AND available_base_quantity = unrestricted_base_quantity - reserved_base_quantity");
                    table.CheckConstraint("ck_stock_positions_nonnegative", "unrestricted_base_quantity >= 0 AND quality_inspection_base_quantity >= 0 AND blocked_base_quantity >= 0 AND reserved_base_quantity >= 0 AND total_valued_on_hand_base_quantity >= 0 AND available_base_quantity >= 0");
                    table.ForeignKey(
                        name: "FK_stock_positions_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_positions_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_positions_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valuation_pools",
                schema: "inventory",
                columns: table => new
                {
                    valuation_pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valuation_group_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    valued_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    carrying_value_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    moving_average_unit_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    last_entry_sequence = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valuation_pools", x => x.valuation_pool_id);
                    table.UniqueConstraint("AK_valuation_pools_tenant_id_legal_entity_id_valuation_pool_id", x => new { x.tenant_id, x.legal_entity_id, x.valuation_pool_id });
                    table.CheckConstraint("ck_valuation_pools_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_valuation_pools_od04", "valued_base_quantity >= 0 AND carrying_value_amount >= 0 AND moving_average_unit_cost >= 0 AND last_entry_sequence >= 0 AND (valued_base_quantity > 0 OR carrying_value_amount = 0)");
                    table.CheckConstraint("ck_valuation_pools_status", "status IN ('ACTIVE', 'BLOCKED')");
                    table.ForeignKey(
                        name: "FK_valuation_pools_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_pools_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_pools_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valued_acceptances",
                schema: "procurement",
                columns: table => new
                {
                    valued_acceptance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acceptance_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acceptance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    stock_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_accepted_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valued_acceptances", x => x.valued_acceptance_id);
                    table.UniqueConstraint("AK_valued_acceptances_tenant_id_legal_entity_id_valued_accepta~", x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_id });
                    table.CheckConstraint("ck_valued_acceptances_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_valued_acceptances_links", "stock_movement_id IS NOT NULL AND receipt_accrual_id IS NOT NULL AND gl_journal_id IS NOT NULL");
                    table.CheckConstraint("ck_valued_acceptances_positive", "total_accepted_base_quantity > 0 AND total_accepted_amount > 0");
                    table.CheckConstraint("ck_valued_acceptances_stock_status", "stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                    table.ForeignKey(
                        name: "FK_valued_acceptances_goods_receipts_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptances_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptances_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptances_purchase_order_revisions_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptances_purchase_orders_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptances_tenant_memberships_tenant_id_accepted_by~",
                        columns: x => new { x.tenant_id, x.accepted_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movement_lines",
                schema: "inventory",
                columns: table => new
                {
                    movement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    stock_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resulting_stock_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    base_quantity_delta = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_valuation_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    line_valuation_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movement_lines", x => x.movement_line_id);
                    table.UniqueConstraint("AK_movement_lines_tenant_id_legal_entity_id_movement_line_id", x => new { x.tenant_id, x.legal_entity_id, x.movement_line_id });
                    table.CheckConstraint("ck_movement_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_movement_lines_positive", "base_quantity_delta <> 0 AND unit_valuation_cost > 0 AND line_valuation_amount > 0");
                    table.CheckConstraint("ck_movement_lines_status", "resulting_stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                    table.ForeignKey(
                        name: "FK_movement_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movement_lines_stock_movements_tenant_id_legal_entity_id_st~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movement_lines_stock_positions_tenant_id_legal_entity_id_st~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_position_id },
                        principalSchema: "inventory",
                        principalTable: "stock_positions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movement_lines_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_accruals",
                schema: "gl",
                columns: table => new
                {
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accrual_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valued_acceptance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_asset_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grni_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    accrued_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    cleared_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    remaining_open_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_accruals", x => x.receipt_accrual_id);
                    table.UniqueConstraint("AK_receipt_accruals_tenant_id_legal_entity_id_receipt_accrual_~", x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_id });
                    table.CheckConstraint("ck_receipt_accruals_amounts", "accrued_amount > 0 AND cleared_amount >= 0 AND remaining_open_amount >= 0 AND accrued_amount = cleared_amount + remaining_open_amount");
                    table.CheckConstraint("ck_receipt_accruals_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_receipt_accruals_status", "status IN ('OPEN', 'PARTIALLY_CLEARED', 'CLEARED')");
                    table.ForeignKey(
                        name: "FK_receipt_accruals_accounts_tenant_id_legal_entity_id_grni_ac~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grni_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_accounts_tenant_id_legal_entity_id_invento~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.inventory_asset_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_goods_receipts_tenant_id_legal_entity_id_g~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_journal_entries_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_purchase_orders_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_tenant_memberships_tenant_id_created_by_us~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accruals_valued_acceptances_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valued_acceptance_lines",
                schema: "procurement",
                columns: table => new
                {
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valued_acceptance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_price_base_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    accepted_line_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valued_acceptance_lines", x => x.valued_acceptance_line_id);
                    table.UniqueConstraint("AK_valued_acceptance_lines_tenant_id_legal_entity_id_valued_ac~", x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_line_id });
                    table.CheckConstraint("ck_val_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_val_lines_positive", "accepted_base_quantity > 0 AND unit_price_base_snapshot > 0 AND accepted_line_amount > 0");
                    table.ForeignKey(
                        name: "FK_valued_acceptance_lines_goods_receipt_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptance_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptance_lines_purchase_order_lines_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptance_lines_units_of_measure_tenant_id_base_uom~",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valued_acceptance_lines_valued_acceptances_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valuation_entries",
                schema: "inventory",
                columns: table => new
                {
                    valuation_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pool_sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    value_delta = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_cost_applied = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    previous_pool_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    previous_pool_carrying_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    previous_moving_average_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_pool_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    new_pool_carrying_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    new_moving_average_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valuation_entries", x => x.valuation_entry_id);
                    table.CheckConstraint("ck_valuation_entries_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_valuation_entries_seq", "pool_sequence_number >= 1");
                    table.CheckConstraint("ck_valuation_entries_type", "entry_type IN ('RECEIPT_ACCEPTANCE')");
                    table.CheckConstraint("ck_valuation_entries_values", "quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_valuation_entries_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_entries_journal_entries_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_entries_movement_lines_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.movement_line_id },
                        principalSchema: "inventory",
                        principalTable: "movement_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "movement_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_entries_stock_movements_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_entries_tenant_memberships_tenant_id_created_by_u~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_valuation_entries_valuation_pools_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valuation_pool_id },
                        principalSchema: "inventory",
                        principalTable: "valuation_pools",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valuation_pool_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_accrual_lines",
                schema: "gl",
                columns: table => new
                {
                    receipt_accrual_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_accrual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    billed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    remaining_unbilled_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_cost_base = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    accrued_line_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    cleared_line_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    remaining_open_line_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_accrual_lines", x => x.receipt_accrual_line_id);
                    table.UniqueConstraint("AK_receipt_accrual_lines_tenant_id_legal_entity_id_receipt_acc~", x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_line_id });
                    table.CheckConstraint("ck_receipt_accrual_lines_amt", "unit_cost_base > 0 AND accrued_line_amount > 0 AND cleared_line_amount >= 0 AND remaining_open_line_amount >= 0 AND accrued_line_amount = cleared_line_amount + remaining_open_line_amount");
                    table.CheckConstraint("ck_receipt_accrual_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_receipt_accrual_lines_qty", "accepted_base_quantity > 0 AND billed_base_quantity >= 0 AND remaining_unbilled_base_quantity >= 0 AND accepted_base_quantity = billed_base_quantity + remaining_unbilled_base_quantity");
                    table.ForeignKey(
                        name: "FK_receipt_accrual_lines_goods_receipt_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_lines_purchase_order_lines_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_lines_receipt_accruals_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_accrual_id },
                        principalSchema: "gl",
                        principalTable: "receipt_accruals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_accrual_lines_valued_acceptance_lines_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_line_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptance_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_goods_receipts_financial_consistency",
                schema: "procurement",
                table: "goods_receipts",
                sql: "(total_valued_accepted_base_quantity = 0 AND has_financial_posting = false) OR (total_valued_accepted_base_quantity > 0 AND has_financial_posting = true)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goods_receipts_status",
                schema: "procurement",
                table: "goods_receipts",
                sql: "status IN ('ARRIVED', 'IN_DOCK_INSPECTION', 'RELEASED_FOR_ACCEPTANCE', 'PARTIALLY_ACCEPTED', 'VALUED_ACCEPTED', 'FULLY_REJECTED_UNVALUED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goods_receipts_valued_le_released",
                schema: "procurement",
                table: "goods_receipts",
                sql: "total_valued_accepted_base_quantity <= total_released_base_quantity");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gr_lines_valued_le_released",
                schema: "procurement",
                table: "goods_receipt_lines",
                sql: "valued_accepted_base_quantity <= dock_released_base_quantity");

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_base_uom_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_item_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_stock_position_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" });

            migrationBuilder.CreateIndex(
                name: "ux_movement_lines_number",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_lines_tenant_id_item_id",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_lines_tenant_id_legal_entity_id_goods_recei~",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accrual_lines_tenant_id_legal_entity_id_valued_acce~",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receipt_accrual_lines_accrual_vline",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id", "valued_acceptance_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receipt_accrual_lines_number",
                schema: "gl",
                table: "receipt_accrual_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_accrual_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_accruals_supplier_status",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_partner_id", "status", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_created_by_user_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_legal_entity_id_goods_receipt_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_legal_entity_id_grni_account_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "grni_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_legal_entity_id_inventory_asset_~",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "inventory_asset_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_legal_entity_id_journal_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_legal_entity_id_purchase_order_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_accruals_tenant_id_supplier_partner_id",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receipt_accruals_acceptance",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receipt_accruals_number",
                schema: "gl",
                table: "receipt_accruals",
                columns: new[] { "tenant_id", "legal_entity_id", "accrual_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_posting_date",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "tenant_id", "legal_entity_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_tenant_id_created_by_user_id",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_movements_number",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "tenant_id", "legal_entity_id", "movement_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stock_movements_source_idempotency",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "tenant_id", "legal_entity_id", "source_document_type", "source_document_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_positions_tenant_id_base_uom_id",
                schema: "inventory",
                table: "stock_positions",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_positions_tenant_id_item_id",
                schema: "inventory",
                table: "stock_positions",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_positions_item_group",
                schema: "inventory",
                table: "stock_positions",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id", "valuation_group_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_valuation_entries_item_date",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_entries_tenant_id_created_by_user_id",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_entries_tenant_id_item_id",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_entries_tenant_id_legal_entity_id_gl_journal_id",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_entries_tenant_id_legal_entity_id_movement_line_id",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "movement_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_entries_tenant_id_legal_entity_id_stock_movement_~",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" });

            migrationBuilder.CreateIndex(
                name: "ux_valuation_entries_pool_seq",
                schema: "inventory",
                table: "valuation_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "valuation_pool_id", "pool_sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valuation_pools_tenant_id_base_uom_id",
                schema: "inventory",
                table: "valuation_pools",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valuation_pools_tenant_id_item_id",
                schema: "inventory",
                table: "valuation_pools",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ux_valuation_pools_item_group",
                schema: "inventory",
                table: "valuation_pools",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id", "currency_code", "valuation_group_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptance_lines_tenant_id_base_uom_id",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptance_lines_tenant_id_item_id",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptance_lines_tenant_id_legal_entity_id_goods_rec~",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptance_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_val_lines_acceptance_grline",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id", "goods_receipt_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_val_lines_number",
                schema: "procurement",
                table: "valued_acceptance_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_valued_acceptances_gr_date",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "acceptance_date" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptances_tenant_id_accepted_by_user_id",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "accepted_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptances_tenant_id_legal_entity_id_po_revision_id",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "po_revision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptances_tenant_id_legal_entity_id_purchase_order~",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_valued_acceptances_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_valued_acceptances_idempotency",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_valued_acceptances_number",
                schema: "procurement",
                table: "valued_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "acceptance_number" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE procurement.acceptance_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.acceptance_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.valued_acceptances ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.valued_acceptances FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.valued_acceptance_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.valued_acceptance_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.movement_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.movement_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.stock_positions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.stock_positions FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.valuation_pools ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.valuation_pools FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.stock_movements ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.stock_movements FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.movement_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.movement_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.valuation_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.valuation_entries FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.receipt_accruals ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.receipt_accruals FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.receipt_accrual_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.receipt_accrual_lines FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION procurement.enforce_valued_acceptance_backing()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1
                    FROM inventory.stock_movements sm
                    WHERE sm.tenant_id = NEW.tenant_id
                      AND sm.legal_entity_id = NEW.legal_entity_id
                      AND sm.stock_movement_id = NEW.stock_movement_id
                      AND sm.source_document_id = NEW.valued_acceptance_id
                  ) THEN
                    RAISE EXCEPTION 'valued_acceptance_missing_stock_movement: ValuedAcceptance % lacks matching StockMovement %.', NEW.valued_acceptance_id, NEW.stock_movement_id
                      USING ERRCODE = '23514';
                  END IF;

                  IF NOT EXISTS (
                    SELECT 1
                    FROM gl.receipt_accruals ra
                    WHERE ra.tenant_id = NEW.tenant_id
                      AND ra.legal_entity_id = NEW.legal_entity_id
                      AND ra.receipt_accrual_id = NEW.receipt_accrual_id
                      AND ra.valued_acceptance_id = NEW.valued_acceptance_id
                      AND ra.journal_id = NEW.gl_journal_id
                  ) THEN
                    RAISE EXCEPTION 'valued_acceptance_missing_grni_accrual: ValuedAcceptance % lacks matching ReceiptAccrual %.', NEW.valued_acceptance_id, NEW.receipt_accrual_id
                      USING ERRCODE = '23514';
                  END IF;

                  IF NOT EXISTS (
                    SELECT 1
                    FROM gl.journal_entries je
                    WHERE je.tenant_id = NEW.tenant_id
                      AND je.legal_entity_id = NEW.legal_entity_id
                      AND je.journal_id = NEW.gl_journal_id
                      AND je.entry_kind = 'STANDARD'
                      AND je.total_debit = je.total_credit
                      AND je.total_debit = NEW.total_accepted_amount
                  ) THEN
                    RAISE EXCEPTION 'valued_acceptance_missing_posted_journal: ValuedAcceptance % lacks matching balanced GL Journal %.', NEW.valued_acceptance_id, NEW.gl_journal_id
                      USING ERRCODE = '23514';
                  END IF;

                  IF NOT EXISTS (
                    SELECT 1
                    FROM procurement.goods_receipts gr
                    WHERE gr.tenant_id = NEW.tenant_id
                      AND gr.legal_entity_id = NEW.legal_entity_id
                      AND gr.goods_receipt_id = NEW.goods_receipt_id
                      AND gr.has_financial_posting = true
                      AND gr.total_valued_accepted_base_quantity > 0
                  ) THEN
                    RAISE EXCEPTION 'valued_acceptance_missing_receipt_projection: GoodsReceipt % missing valued posting projection.', NEW.goods_receipt_id
                      USING ERRCODE = '23514';
                  END IF;

                  RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER trg_valued_acceptance_backing
                AFTER INSERT ON procurement.valued_acceptances
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION procurement.enforce_valued_acceptance_backing();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA inventory FROM PUBLIC;
                    GRANT USAGE ON SCHEMA inventory TO athera_app;

                    REVOKE ALL ON TABLE
                      procurement.acceptance_sequences,
                      procurement.valued_acceptances,
                      procurement.valued_acceptance_lines,
                      inventory.movement_sequences,
                      inventory.stock_positions,
                      inventory.valuation_pools,
                      inventory.stock_movements,
                      inventory.movement_lines,
                      inventory.valuation_entries,
                      gl.receipt_accruals,
                      gl.receipt_accrual_lines
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      procurement.acceptance_sequences,
                      inventory.movement_sequences,
                      inventory.stock_positions,
                      inventory.valuation_pools,
                      gl.receipt_accruals,
                      gl.receipt_accrual_lines
                    TO athera_app;

                    -- Strictly append-only Gate 2 and inventory ledger tables: zero UPDATE or DELETE for runtime role.
                    GRANT SELECT, INSERT ON TABLE
                      procurement.valued_acceptances,
                      procurement.valued_acceptance_lines,
                      inventory.stock_movements,
                      inventory.movement_lines,
                      inventory.valuation_entries
                    TO athera_app;

                    CREATE POLICY p_proc_acc_seq_select ON procurement.acceptance_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_acc_seq_insert ON procurement.acceptance_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_acc_seq_update ON procurement.acceptance_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_va_select ON procurement.valued_acceptances FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_va_insert ON procurement.valued_acceptances FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_val_lines_select ON procurement.valued_acceptance_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_val_lines_insert ON procurement.valued_acceptance_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_mov_seq_select ON inventory.movement_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_mov_seq_insert ON inventory.movement_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_mov_seq_update ON inventory.movement_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_sp_select ON inventory.stock_positions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_sp_insert ON inventory.stock_positions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_sp_update ON inventory.stock_positions FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_vp_select ON inventory.valuation_pools FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_vp_insert ON inventory.valuation_pools FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_vp_update ON inventory.valuation_pools FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_sm_select ON inventory.stock_movements FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_sm_insert ON inventory.stock_movements FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_ml_select ON inventory.movement_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_ml_insert ON inventory.movement_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_ve_select ON inventory.valuation_entries FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_ve_insert ON inventory.valuation_entries FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_ra_select ON gl.receipt_accruals FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_ra_insert ON gl.receipt_accruals FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_ra_update ON gl.receipt_accruals FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_ral_select ON gl.receipt_accrual_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_ral_insert ON gl.receipt_accrual_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_ral_update ON gl.receipt_accrual_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_proc_acc_seq_migrator_all ON procurement.acceptance_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_va_migrator_all ON procurement.valued_acceptances FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_val_lines_migrator_all ON procurement.valued_acceptance_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_mov_seq_migrator_all ON inventory.movement_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_sp_migrator_all ON inventory.stock_positions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_vp_migrator_all ON inventory.valuation_pools FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_sm_migrator_all ON inventory.stock_movements FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_ml_migrator_all ON inventory.movement_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_ve_migrator_all ON inventory.valuation_entries FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_ra_migrator_all ON gl.receipt_accruals FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_ral_migrator_all ON gl.receipt_accrual_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_valued_acceptance_backing ON procurement.valued_acceptances;
                DROP FUNCTION IF EXISTS procurement.enforce_valued_acceptance_backing();
                """);
            migrationBuilder.DropTable(
                name: "acceptance_sequences",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "movement_sequences",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "receipt_accrual_lines",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "valuation_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "receipt_accruals",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "valued_acceptance_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "movement_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "valuation_pools",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "valued_acceptances",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_positions",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goods_receipts_financial_consistency",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goods_receipts_status",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goods_receipts_valued_le_released",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gr_lines_valued_le_released",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goods_receipts_status",
                schema: "procurement",
                table: "goods_receipts",
                sql: "status IN ('ARRIVED', 'IN_DOCK_INSPECTION', 'RELEASED_FOR_ACCEPTANCE', 'FULLY_REJECTED_UNVALUED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goods_receipts_unvalued_gate1",
                schema: "procurement",
                table: "goods_receipts",
                sql: "has_financial_posting = false AND total_valued_accepted_base_quantity = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gr_lines_s06_unvalued",
                schema: "procurement",
                table: "goods_receipt_lines",
                sql: "valued_accepted_base_quantity = 0");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS inventory;
                """);
        }
    }
}
