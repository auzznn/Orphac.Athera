using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M08_StockAvailabilityLocationsAndQualityRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.EnsureSchema(
                name: "quality");

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "inventory",
                table: "movement_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "stock_lot_id",
                schema: "inventory",
                table: "movement_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                schema: "inventory",
                table: "movement_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lot_tracking_policy",
                schema: "catalog",
                table: "items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.CreateTable(
                name: "inspection_sequences",
                schema: "quality",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_inspection_seq_nonneg", "last_number >= 0");
                    table.CheckConstraint("ck_inspection_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_inspection_sequences_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservation_sequences",
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
                    table.PrimaryKey("PK_reservation_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_reservation_seq_nonneg", "last_number >= 0");
                    table.CheckConstraint("ck_reservation_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_reservation_sequences_legal_entities_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_lots",
                schema: "inventory",
                columns: table => new
                {
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_lot_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    manufactured_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    source_valued_acceptance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_lots", x => x.stock_lot_id);
                    table.UniqueConstraint("AK_stock_lots_tenant_id_legal_entity_id_stock_lot_id", x => new { x.tenant_id, x.legal_entity_id, x.stock_lot_id });
                    table.CheckConstraint("ck_stock_lots_dates", "manufactured_date IS NULL OR expiry_date IS NULL OR expiry_date >= manufactured_date");
                    table.ForeignKey(
                        name: "FK_stock_lots_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_lots_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_lots_tenant_memberships_tenant_id_created_by_user_id",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_lots_valued_acceptances_tenant_id_legal_entity_id_sou~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_valued_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "inventory",
                columns: table => new
                {
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.warehouse_id);
                    table.UniqueConstraint("AK_warehouses_tenant_id_legal_entity_id_warehouse_id", x => new { x.tenant_id, x.legal_entity_id, x.warehouse_id });
                    table.CheckConstraint("ck_warehouses_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_warehouses_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "inventory",
                columns: table => new
                {
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    zone_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_default_receiving = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.location_id);
                    table.UniqueConstraint("AK_locations_tenant_id_legal_entity_id_location_id", x => new { x.tenant_id, x.legal_entity_id, x.location_id });
                    table.CheckConstraint("ck_locations_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_locations_zone", "zone_type IN ('RECEIVING_STAGING', 'STORAGE', 'QUARANTINE', 'BLOCKED_HOLD', 'DISPATCH')");
                    table.ForeignKey(
                        name: "FK_locations_warehouses_tenant_id_legal_entity_id_warehouse_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.warehouse_id },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_inspections",
                schema: "quality",
                columns: table => new
                {
                    receipt_inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valued_acceptance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valued_acceptance_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quarantine_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inspected_total_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    pending_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    blocked_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_inspections", x => x.receipt_inspection_id);
                    table.UniqueConstraint("AK_receipt_inspections_tenant_id_legal_entity_id_receipt_inspe~", x => new { x.tenant_id, x.legal_entity_id, x.receipt_inspection_id });
                    table.CheckConstraint("ck_receipt_inspections_qty", "inspected_total_base_quantity > 0 AND pending_base_quantity >= 0 AND released_base_quantity >= 0 AND blocked_base_quantity >= 0 AND inspected_total_base_quantity = pending_base_quantity + released_base_quantity + blocked_base_quantity");
                    table.CheckConstraint("ck_receipt_inspections_status", "status IN ('PENDING', 'PARTIALLY_DECIDED', 'COMPLETED')");
                    table.ForeignKey(
                        name: "FK_receipt_inspections_goods_receipts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_inspections_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_inspections_locations_tenant_id_legal_entity_id_qua~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.quarantine_location_id },
                        principalSchema: "inventory",
                        principalTable: "locations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_inspections_stock_lots_tenant_id_legal_entity_id_st~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_lot_id },
                        principalSchema: "inventory",
                        principalTable: "stock_lots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_inspections_valued_acceptance_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_line_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptance_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_inspections_valued_acceptances_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.valued_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "valued_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_location_balances",
                schema: "inventory",
                columns: table => new
                {
                    stock_location_balance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    on_hand_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    available_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_location_balances", x => x.stock_location_balance_id);
                    table.UniqueConstraint("AK_stock_location_balances_tenant_id_legal_entity_id_stock_loc~", x => new { x.tenant_id, x.legal_entity_id, x.stock_location_balance_id });
                    table.CheckConstraint("ck_loc_bal_availability", "(stock_status = 'UNRESTRICTED' AND available_base_quantity = on_hand_base_quantity - reserved_base_quantity) OR (stock_status IN ('QUALITY_INSPECTION', 'BLOCKED') AND reserved_base_quantity = 0 AND available_base_quantity = 0)");
                    table.CheckConstraint("ck_loc_bal_nonnegative", "on_hand_base_quantity >= 0 AND reserved_base_quantity >= 0 AND available_base_quantity >= 0");
                    table.CheckConstraint("ck_loc_bal_status", "stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                    table.ForeignKey(
                        name: "FK_stock_location_balances_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_location_balances_locations_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.location_id },
                        principalSchema: "inventory",
                        principalTable: "locations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_location_balances_stock_lots_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_lot_id },
                        principalSchema: "inventory",
                        principalTable: "stock_lots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_location_balances_stock_positions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_position_id },
                        principalSchema: "inventory",
                        principalTable: "stock_positions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_location_balances_units_of_measure_tenant_id_base_uom~",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_location_balances_warehouses_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.warehouse_id },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inspection_dispositions",
                schema: "quality",
                columns: table => new
                {
                    inspection_disposition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposition_date = table.Column<DateOnly>(type: "date", nullable: false),
                    released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    target_release_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    blocked_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    target_block_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inspection_dispositions", x => x.inspection_disposition_id);
                    table.CheckConstraint("ck_inspection_disp_qty", "released_base_quantity >= 0 AND blocked_base_quantity >= 0 AND (released_base_quantity + blocked_base_quantity) > 0");
                    table.CheckConstraint("ck_inspection_disp_reason", "reason_code IN ('PASSED_SPECIFICATION', 'DAMAGED_OR_CONTAMINATED', 'SPEC_DEVIATION_HOLD', 'MISSING_CERTIFICATE', 'SUPERVISOR_REEVALUATION')");
                    table.CheckConstraint("ck_inspection_disp_targets", "(released_base_quantity = 0 OR target_release_location_id IS NOT NULL) AND (blocked_base_quantity = 0 OR target_block_location_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_inspection_dispositions_receipt_inspections_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_inspection_id },
                        principalSchema: "quality",
                        principalTable: "receipt_inspections",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "receipt_inspection_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inspection_dispositions_stock_movements_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inspection_dispositions_tenant_memberships_tenant_id_decide~",
                        columns: x => new { x.tenant_id, x.decided_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                schema: "inventory",
                columns: table => new
                {
                    stock_reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stock_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_balance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_document_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    consumed_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    remaining_reserved_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    released_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.stock_reservation_id);
                    table.UniqueConstraint("AK_stock_reservations_tenant_id_legal_entity_id_stock_reservat~", x => new { x.tenant_id, x.legal_entity_id, x.stock_reservation_id });
                    table.CheckConstraint("ck_stock_reservations_qty", "reserved_base_quantity > 0 AND consumed_base_quantity >= 0 AND released_base_quantity >= 0 AND remaining_reserved_base_quantity >= 0 AND reserved_base_quantity = consumed_base_quantity + released_base_quantity + remaining_reserved_base_quantity");
                    table.CheckConstraint("ck_stock_reservations_source_type", "source_type IN ('SALES_ORDER', 'MANUAL_HOLD', 'FULFILLMENT')");
                    table.CheckConstraint("ck_stock_reservations_status", "status IN ('ACTIVE', 'PARTIALLY_CONSUMED', 'FULFILLED', 'RELEASED')");
                    table.ForeignKey(
                        name: "FK_stock_reservations_locations_tenant_id_legal_entity_id_loca~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.location_id },
                        principalSchema: "inventory",
                        principalTable: "locations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_stock_location_balances_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_location_balance_id },
                        principalSchema: "inventory",
                        principalTable: "stock_location_balances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_location_balance_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_stock_lots_tenant_id_legal_entity_id_sto~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_lot_id },
                        principalSchema: "inventory",
                        principalTable: "stock_lots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_stock_positions_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_position_id },
                        principalSchema: "inventory",
                        principalTable: "stock_positions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_tenant_memberships_tenant_id_created_by_~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE')");

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_location_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_stock_lot_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_warehouse_id",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines",
                sql: "base_quantity_delta <> 0 AND unit_valuation_cost >= 0 AND line_valuation_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_lot_tracking_policy",
                schema: "catalog",
                table: "items",
                sql: "lot_tracking_policy IN ('NONE', 'OPTIONAL', 'REQUIRED') AND (item_kind = 'STOCKED_GOOD' OR lot_tracking_policy = 'NONE')");

            migrationBuilder.CreateIndex(
                name: "IX_inspection_dispositions_tenant_id_decided_by_user_id",
                schema: "quality",
                table: "inspection_dispositions",
                columns: new[] { "tenant_id", "decided_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_inspection_dispositions_tenant_id_legal_entity_id_stock_mov~",
                schema: "quality",
                table: "inspection_dispositions",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inspection_dispositions_idempotency",
                schema: "quality",
                table: "inspection_dispositions",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_inspection_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_tenant_id_legal_entity_id_warehouse_id",
                schema: "inventory",
                table: "locations",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ux_locations_code",
                schema: "inventory",
                table: "locations",
                columns: new[] { "tenant_id", "legal_entity_id", "location_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_item_id",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_quarantine_lo~",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "quarantine_location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_stock_lot_id",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_valued_accept~",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receipt_inspections_acceptance_line",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "valued_acceptance_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receipt_inspections_number",
                schema: "quality",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "inspection_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_balances_tenant_id_base_uom_id",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_balances_tenant_id_item_id",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_balances_tenant_id_legal_entity_id_location_~",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "legal_entity_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_balances_tenant_id_legal_entity_id_stock_lot~",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_balances_tenant_id_legal_entity_id_stock_pos~",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" });

            migrationBuilder.CreateIndex(
                name: "ux_loc_bal_lotted",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id", "location_id", "item_id", "stock_lot_id", "stock_status" },
                unique: true,
                filter: "stock_lot_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_loc_bal_unlotted",
                schema: "inventory",
                table: "stock_location_balances",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id", "location_id", "item_id", "stock_status" },
                unique: true,
                filter: "stock_lot_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_tenant_id_created_by_user_id",
                schema: "inventory",
                table: "stock_lots",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_tenant_id_item_id",
                schema: "inventory",
                table: "stock_lots",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_lots_tenant_id_legal_entity_id_source_valued_acceptan~",
                schema: "inventory",
                table: "stock_lots",
                columns: new[] { "tenant_id", "legal_entity_id", "source_valued_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_lots_number",
                schema: "inventory",
                table: "stock_lots",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id", "lot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_item_status",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_tenant_id_created_by_user_id",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_tenant_id_legal_entity_id_location_id",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_tenant_id_legal_entity_id_stock_location~",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_location_balance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_tenant_id_legal_entity_id_stock_lot_id",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_tenant_id_legal_entity_id_stock_position~",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_position_id" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservations_idempotency",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservations_number",
                schema: "inventory",
                table: "stock_reservations",
                columns: new[] { "tenant_id", "legal_entity_id", "reservation_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_warehouses_code",
                schema: "inventory",
                table: "warehouses",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_movement_lines_locations_tenant_id_legal_entity_id_location~",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                principalSchema: "inventory",
                principalTable: "locations",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "location_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movement_lines_stock_lots_tenant_id_legal_entity_id_stock_l~",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                principalSchema: "inventory",
                principalTable: "stock_lots",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_lot_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_movement_lines_warehouses_tenant_id_legal_entity_id_warehou~",
                schema: "inventory",
                table: "movement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" },
                principalSchema: "inventory",
                principalTable: "warehouses",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                -- Backfill WH-MAIN, default zone locations, and location balances for any existing M07 stock_positions.
                INSERT INTO inventory.warehouses (
                    warehouse_id, tenant_id, legal_entity_id, warehouse_code, name, is_default, status, created_at_utc, updated_at_utc, version
                )
                SELECT DISTINCT
                    gen_random_uuid(), sp.tenant_id, sp.legal_entity_id, 'WH-MAIN', 'Gudang Distribusi Utama', true, 'ACTIVE', now(), now(), 1
                FROM inventory.stock_positions sp
                ON CONFLICT (tenant_id, legal_entity_id, warehouse_code) DO NOTHING;

                INSERT INTO inventory.locations (
                    location_id, tenant_id, legal_entity_id, warehouse_id, location_code, name, zone_type, is_default_receiving, status, created_at_utc, updated_at_utc, version
                )
                SELECT
                    gen_random_uuid(), w.tenant_id, w.legal_entity_id, w.warehouse_id, loc.location_code, loc.name, loc.zone_type, loc.is_default_receiving, 'ACTIVE', now(), now(), 1
                FROM inventory.warehouses w
                CROSS JOIN (VALUES
                    ('LOC-MAIN', 'Rak Penyimpanan Utama', 'STORAGE', true),
                    ('LOC-QC', 'Zona Karantina Inspeksi Mutu', 'QUARANTINE', false),
                    ('LOC-BLK', 'Area Barang Diblokir', 'BLOCKED_HOLD', false),
                    ('LOC-DSP', 'Area Persiapan Pengiriman', 'DISPATCH', false)
                ) AS loc(location_code, name, zone_type, is_default_receiving)
                WHERE w.warehouse_code = 'WH-MAIN'
                ON CONFLICT (tenant_id, legal_entity_id, location_code) DO NOTHING;

                INSERT INTO inventory.stock_location_balances (
                    stock_location_balance_id, tenant_id, legal_entity_id, stock_position_id, item_id, base_uom_id,
                    warehouse_id, location_id, stock_lot_id, stock_status, on_hand_base_quantity, reserved_base_quantity,
                    available_base_quantity, updated_at_utc, version
                )
                SELECT
                    gen_random_uuid(), sp.tenant_id, sp.legal_entity_id, sp.stock_position_id, sp.item_id, sp.base_uom_id,
                    l.warehouse_id, l.location_id, NULL, 'UNRESTRICTED',
                    sp.unrestricted_base_quantity, sp.reserved_base_quantity, sp.available_base_quantity, now(), 1
                FROM inventory.stock_positions sp
                JOIN inventory.locations l
                  ON l.tenant_id = sp.tenant_id AND l.legal_entity_id = sp.legal_entity_id AND l.location_code = 'LOC-MAIN'
                WHERE sp.unrestricted_base_quantity > 0
                ON CONFLICT (tenant_id, legal_entity_id, warehouse_id, location_id, item_id, stock_status)
                WHERE stock_lot_id IS NULL DO NOTHING;

                INSERT INTO inventory.stock_location_balances (
                    stock_location_balance_id, tenant_id, legal_entity_id, stock_position_id, item_id, base_uom_id,
                    warehouse_id, location_id, stock_lot_id, stock_status, on_hand_base_quantity, reserved_base_quantity,
                    available_base_quantity, updated_at_utc, version
                )
                SELECT
                    gen_random_uuid(), sp.tenant_id, sp.legal_entity_id, sp.stock_position_id, sp.item_id, sp.base_uom_id,
                    l.warehouse_id, l.location_id, NULL, 'QUALITY_INSPECTION',
                    sp.quality_inspection_base_quantity, 0, 0, now(), 1
                FROM inventory.stock_positions sp
                JOIN inventory.locations l
                  ON l.tenant_id = sp.tenant_id AND l.legal_entity_id = sp.legal_entity_id AND l.location_code = 'LOC-QC'
                WHERE sp.quality_inspection_base_quantity > 0
                ON CONFLICT (tenant_id, legal_entity_id, warehouse_id, location_id, item_id, stock_status)
                WHERE stock_lot_id IS NULL DO NOTHING;

                INSERT INTO inventory.stock_location_balances (
                    stock_location_balance_id, tenant_id, legal_entity_id, stock_position_id, item_id, base_uom_id,
                    warehouse_id, location_id, stock_lot_id, stock_status, on_hand_base_quantity, reserved_base_quantity,
                    available_base_quantity, updated_at_utc, version
                )
                SELECT
                    gen_random_uuid(), sp.tenant_id, sp.legal_entity_id, sp.stock_position_id, sp.item_id, sp.base_uom_id,
                    l.warehouse_id, l.location_id, NULL, 'BLOCKED',
                    sp.blocked_base_quantity, 0, 0, now(), 1
                FROM inventory.stock_positions sp
                JOIN inventory.locations l
                  ON l.tenant_id = sp.tenant_id AND l.legal_entity_id = sp.legal_entity_id AND l.location_code = 'LOC-BLK'
                WHERE sp.blocked_base_quantity > 0
                ON CONFLICT (tenant_id, legal_entity_id, warehouse_id, location_id, item_id, stock_status)
                WHERE stock_lot_id IS NULL DO NOTHING;

                ALTER TABLE inventory.warehouses ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.warehouses FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.locations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.locations FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.stock_lots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.stock_lots FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.stock_location_balances ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.stock_location_balances FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.reservation_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.reservation_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE inventory.stock_reservations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inventory.stock_reservations FORCE ROW LEVEL SECURITY;

                ALTER TABLE quality.inspection_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE quality.inspection_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE quality.receipt_inspections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE quality.receipt_inspections FORCE ROW LEVEL SECURITY;

                ALTER TABLE quality.inspection_dispositions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE quality.inspection_dispositions FORCE ROW LEVEL SECURITY;

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA quality FROM PUBLIC;
                    GRANT USAGE ON SCHEMA quality TO athera_app;

                    REVOKE ALL ON TABLE
                      inventory.warehouses,
                      inventory.locations,
                      inventory.stock_lots,
                      inventory.stock_location_balances,
                      inventory.reservation_sequences,
                      inventory.stock_reservations,
                      quality.inspection_sequences,
                      quality.receipt_inspections,
                      quality.inspection_dispositions
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      inventory.warehouses,
                      inventory.locations,
                      inventory.stock_lots,
                      inventory.stock_location_balances,
                      inventory.reservation_sequences,
                      inventory.stock_reservations,
                      quality.inspection_sequences,
                      quality.receipt_inspections
                    TO athera_app;

                    -- Strictly append-only quality disposition table: zero UPDATE or DELETE for runtime role.
                    GRANT SELECT, INSERT ON TABLE
                      quality.inspection_dispositions
                    TO athera_app;

                    CREATE POLICY p_inv_wh_select ON inventory.warehouses FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_wh_insert ON inventory.warehouses FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_wh_update ON inventory.warehouses FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_loc_select ON inventory.locations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_loc_insert ON inventory.locations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_loc_update ON inventory.locations FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_lot_select ON inventory.stock_lots FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_lot_insert ON inventory.stock_lots FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_lot_update ON inventory.stock_lots FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_loc_bal_select ON inventory.stock_location_balances FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_loc_bal_insert ON inventory.stock_location_balances FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_loc_bal_update ON inventory.stock_location_balances FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_rsv_seq_select ON inventory.reservation_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_rsv_seq_insert ON inventory.reservation_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_rsv_seq_update ON inventory.reservation_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inv_rsv_select ON inventory.stock_reservations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_rsv_insert ON inventory.stock_reservations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_inv_rsv_update ON inventory.stock_reservations FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ql_seq_select ON quality.inspection_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ql_seq_insert ON quality.inspection_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ql_seq_update ON quality.inspection_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ql_insp_select ON quality.receipt_inspections FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ql_insp_insert ON quality.receipt_inspections FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ql_insp_update ON quality.receipt_inspections FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ql_disp_select ON quality.inspection_dispositions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ql_disp_insert ON quality.inspection_dispositions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    GRANT USAGE ON SCHEMA quality TO athera_migrator;
                    CREATE POLICY p_inv_wh_migrator_all ON inventory.warehouses FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_loc_migrator_all ON inventory.locations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_lot_migrator_all ON inventory.stock_lots FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_loc_bal_migrator_all ON inventory.stock_location_balances FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_rsv_seq_migrator_all ON inventory.reservation_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inv_rsv_migrator_all ON inventory.stock_reservations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ql_seq_migrator_all ON quality.inspection_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ql_insp_migrator_all ON quality.receipt_inspections FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ql_disp_migrator_all ON quality.inspection_dispositions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movement_lines_locations_tenant_id_legal_entity_id_location~",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_movement_lines_stock_lots_tenant_id_legal_entity_id_stock_l~",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_movement_lines_warehouses_tenant_id_legal_entity_id_warehou~",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropTable(
                name: "inspection_dispositions",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "inspection_sequences",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "reservation_sequences",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "receipt_inspections",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "stock_location_balances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_lots",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_location_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_stock_lot_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropIndex(
                name: "IX_movement_lines_tenant_id_legal_entity_id_warehouse_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_lot_tracking_policy",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropColumn(
                name: "stock_lot_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                schema: "inventory",
                table: "movement_lines");

            migrationBuilder.DropColumn(
                name: "lot_tracking_policy",
                schema: "catalog",
                table: "items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_movement_lines_positive",
                schema: "inventory",
                table: "movement_lines",
                sql: "base_quantity_delta <> 0 AND unit_valuation_cost > 0 AND line_valuation_amount > 0");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS quality;
                """);
        }
    }
}
