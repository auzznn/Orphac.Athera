using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M13_SalesReservationPickAndDispatch : Migration
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

            migrationBuilder.EnsureSchema(
                name: "fulfillment");

            migrationBuilder.CreateTable(
                name: "pick_orders",
                schema: "fulfillment",
                columns: table => new
                {
                    pick_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pick_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    total_requested_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_picked_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    picked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    picked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pick_orders", x => x.pick_order_id);
                    table.UniqueConstraint("AK_pick_orders_tenant_id_legal_entity_id_pick_order_id", x => new { x.tenant_id, x.legal_entity_id, x.pick_order_id });
                    table.CheckConstraint("ck_pick_orders_quantities", "total_requested_base_quantity > 0 AND total_picked_base_quantity >= 0 AND total_dispatched_base_quantity >= 0 AND total_picked_base_quantity <= total_requested_base_quantity AND total_dispatched_base_quantity <= total_picked_base_quantity");
                    table.CheckConstraint("ck_pick_orders_status", "status IN ('OPEN', 'PICKED', 'DISPATCHED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_pick_orders_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_orders_sales_orders_tenant_id_legal_entity_id_sales_or~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sales_order_id },
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sales_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_orders_warehouses_tenant_id_legal_entity_id_warehouse_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.warehouse_id },
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pick_sequences",
                schema: "fulfillment",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pick_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_pick_sequences_nonnegative", "last_number >= 0");
                    table.CheckConstraint("ck_pick_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_pick_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_sequences",
                schema: "fulfillment",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_shipment_sequences_nonnegative", "last_number >= 0");
                    table.CheckConstraint("ck_shipment_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_shipment_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pick_lines",
                schema: "fulfillment",
                columns: table => new
                {
                    pick_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pick_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    so_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stock_reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_pick_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    picked_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pick_lines", x => x.pick_line_id);
                    table.UniqueConstraint("AK_pick_lines_tenant_id_legal_entity_id_pick_line_id", x => new { x.tenant_id, x.legal_entity_id, x.pick_line_id });
                    table.CheckConstraint("ck_pick_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_pick_lines_quantities", "requested_pick_base_quantity > 0 AND picked_base_quantity >= 0 AND dispatched_base_quantity >= 0 AND picked_base_quantity <= requested_pick_base_quantity AND dispatched_base_quantity <= picked_base_quantity");
                    table.ForeignKey(
                        name: "FK_pick_lines_pick_orders_tenant_id_legal_entity_id_pick_order~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.pick_order_id },
                        principalSchema: "fulfillment",
                        principalTable: "pick_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "pick_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_lines_stock_reservations_tenant_id_legal_entity_id_sto~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_reservation_id },
                        principalSchema: "inventory",
                        principalTable: "stock_reservations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_reservation_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "fulfillment",
                columns: table => new
                {
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pick_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    customer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    delivery_address_snapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    dispatch_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    carrier_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tracking_or_vehicle_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    delivery_note_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    delivery_confirmed_by_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    delivery_evidence_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    delivered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_movement_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    total_dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_cogs_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dispatched_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.shipment_id);
                    table.UniqueConstraint("AK_shipments_tenant_id_legal_entity_id_shipment_id", x => new { x.tenant_id, x.legal_entity_id, x.shipment_id });
                    table.CheckConstraint("ck_shipments_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_shipments_status", "status IN ('DISPATCHED', 'DELIVERED')");
                    table.CheckConstraint("ck_shipments_totals", "total_dispatched_base_quantity > 0 AND total_cogs_amount >= 0");
                    table.ForeignKey(
                        name: "FK_shipments_journal_entries_tenant_id_legal_entity_id_gl_jour~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_pick_orders_tenant_id_legal_entity_id_pick_order_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.pick_order_id },
                        principalSchema: "fulfillment",
                        principalTable: "pick_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "pick_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_sales_orders_tenant_id_legal_entity_id_sales_orde~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sales_order_id },
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sales_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_stock_movements_tenant_id_legal_entity_id_stock_m~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.stock_movement_id },
                        principalSchema: "inventory",
                        principalTable: "stock_movements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_lines",
                schema: "fulfillment",
                columns: table => new
                {
                    shipment_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    pick_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stock_reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    stock_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_number_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    dispatched_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unit_cost_applied = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    line_cogs_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    movement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_entry_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_lines", x => x.shipment_line_id);
                    table.UniqueConstraint("AK_shipment_lines_tenant_id_legal_entity_id_shipment_line_id", x => new { x.tenant_id, x.legal_entity_id, x.shipment_line_id });
                    table.CheckConstraint("ck_shipment_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_shipment_lines_quantities", "dispatched_base_quantity > 0 AND unit_cost_applied >= 0 AND line_cogs_amount >= 0");
                    table.ForeignKey(
                        name: "FK_shipment_lines_pick_lines_tenant_id_legal_entity_id_pick_li~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.pick_line_id },
                        principalSchema: "fulfillment",
                        principalTable: "pick_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "pick_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_lines_shipments_tenant_id_legal_entity_id_shipment~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.shipment_id },
                        principalSchema: "fulfillment",
                        principalTable: "shipments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "shipment_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_type",
                schema: "inventory",
                table: "valuation_entries",
                sql: "entry_type IN ('RECEIPT_ACCEPTANCE', 'OPERATIONAL_ISSUE', 'INVOICE_PRICE_ADJUSTMENT', 'PURCHASE_RETURN_OUTBOUND', 'SALES_DISPATCH_OUTBOUND')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valuation_entries_values",
                schema: "inventory",
                table: "valuation_entries",
                sql: "previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity >= 0 AND new_pool_carrying_value >= 0 AND new_moving_average_cost >= 0 AND ((entry_type = 'RECEIPT_ACCEPTANCE' AND quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type IN ('OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND', 'SALES_DISPATCH_OUTBOUND') AND quantity_delta < 0 AND value_delta < 0 AND unit_cost_applied > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type = 'INVOICE_PRICE_ADJUSTMENT' AND quantity_delta = 0 AND value_delta <> 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND', 'SALES_DISPATCH_OUTBOUND')");

            migrationBuilder.CreateIndex(
                name: "ix_pick_lines_reservation",
                schema: "fulfillment",
                table: "pick_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_reservation_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pick_lines_number",
                schema: "fulfillment",
                table: "pick_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "pick_order_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pick_orders_so_status",
                schema: "fulfillment",
                table: "pick_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "sales_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_pick_orders_tenant_id_legal_entity_id_warehouse_id",
                schema: "fulfillment",
                table: "pick_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pick_orders_idempotency",
                schema: "fulfillment",
                table: "pick_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pick_orders_number",
                schema: "fulfillment",
                table: "pick_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "pick_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_lines_tenant_id_legal_entity_id_pick_line_id",
                schema: "fulfillment",
                table: "shipment_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "pick_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_shipment_lines_number",
                schema: "fulfillment",
                table: "shipment_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "shipment_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_so",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "sales_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_legal_entity_id_gl_journal_id",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_legal_entity_id_pick_order_id",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "pick_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_legal_entity_id_stock_movement_id",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "stock_movement_id" });

            migrationBuilder.CreateIndex(
                name: "ux_shipments_idempotency",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_shipments_number",
                schema: "fulfillment",
                table: "shipments",
                columns: new[] { "tenant_id", "legal_entity_id", "shipment_number" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE fulfillment.pick_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.pick_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE fulfillment.shipment_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.shipment_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE fulfillment.pick_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.pick_orders FORCE ROW LEVEL SECURITY;

                ALTER TABLE fulfillment.pick_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.pick_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE fulfillment.shipments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.shipments FORCE ROW LEVEL SECURITY;

                ALTER TABLE fulfillment.shipment_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fulfillment.shipment_lines FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION fulfillment.guard_dispatched_shipment_immutability()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('shipment_dispatched_immutable: Dispatched shipment %s cannot be deleted.', OLD.shipment_id);
                  END IF;

                  IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                    OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                    OR NEW.shipment_number IS DISTINCT FROM OLD.shipment_number
                    OR NEW.pick_order_id IS DISTINCT FROM OLD.pick_order_id
                    OR NEW.sales_order_id IS DISTINCT FROM OLD.sales_order_id
                    OR NEW.sales_order_number_snapshot IS DISTINCT FROM OLD.sales_order_number_snapshot
                    OR NEW.revision_id IS DISTINCT FROM OLD.revision_id
                    OR NEW.customer_partner_id IS DISTINCT FROM OLD.customer_partner_id
                    OR NEW.customer_code_snapshot IS DISTINCT FROM OLD.customer_code_snapshot
                    OR NEW.customer_name_snapshot IS DISTINCT FROM OLD.customer_name_snapshot
                    OR NEW.delivery_address_snapshot IS DISTINCT FROM OLD.delivery_address_snapshot
                    OR NEW.dispatch_date IS DISTINCT FROM OLD.dispatch_date
                    OR NEW.delivery_note_reference IS DISTINCT FROM OLD.delivery_note_reference
                    OR NEW.stock_movement_id IS DISTINCT FROM OLD.stock_movement_id
                    OR NEW.stock_movement_number IS DISTINCT FROM OLD.stock_movement_number
                    OR NEW.gl_journal_id IS DISTINCT FROM OLD.gl_journal_id
                    OR NEW.gl_journal_number IS DISTINCT FROM OLD.gl_journal_number
                    OR NEW.currency_code IS DISTINCT FROM OLD.currency_code
                    OR NEW.total_dispatched_base_quantity IS DISTINCT FROM OLD.total_dispatched_base_quantity
                    OR NEW.total_cogs_amount IS DISTINCT FROM OLD.total_cogs_amount
                    OR NEW.idempotency_key IS DISTINCT FROM OLD.idempotency_key
                    OR NEW.dispatched_by_user_id IS DISTINCT FROM OLD.dispatched_by_user_id
                    OR NEW.dispatched_at_utc IS DISTINCT FROM OLD.dispatched_at_utc
                  THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('shipment_dispatched_immutable: Dispatched shipment %s quantity, costing, and ledger links are immutable.', OLD.shipment_id);
                  END IF;

                  IF OLD.status = 'DELIVERED' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('shipment_dispatched_immutable: Delivered shipment %s cannot be modified.', OLD.shipment_id);
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_dispatched_shipment_immutability
                  BEFORE UPDATE OR DELETE ON fulfillment.shipments
                  FOR EACH ROW
                  EXECUTE FUNCTION fulfillment.guard_dispatched_shipment_immutability();

                CREATE OR REPLACE FUNCTION fulfillment.guard_dispatched_shipment_line_immutability()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('shipment_dispatched_immutable: Dispatched shipment line %s is immutable.', OLD.shipment_line_id);
                END;
                $$;

                CREATE TRIGGER trg_guard_dispatched_shipment_line_immutability
                  BEFORE UPDATE OR DELETE ON fulfillment.shipment_lines
                  FOR EACH ROW
                  EXECUTE FUNCTION fulfillment.guard_dispatched_shipment_line_immutability();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA fulfillment FROM PUBLIC;
                    GRANT USAGE ON SCHEMA fulfillment TO athera_app;

                    REVOKE ALL ON TABLE
                      fulfillment.pick_sequences,
                      fulfillment.shipment_sequences,
                      fulfillment.pick_orders,
                      fulfillment.pick_lines,
                      fulfillment.shipments,
                      fulfillment.shipment_lines
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      fulfillment.pick_sequences,
                      fulfillment.shipment_sequences,
                      fulfillment.pick_orders,
                      fulfillment.pick_lines,
                      fulfillment.shipments
                    TO athera_app;

                    GRANT SELECT, INSERT ON TABLE
                      fulfillment.shipment_lines
                    TO athera_app;

                    CREATE POLICY p_pick_seq_select ON fulfillment.pick_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_seq_insert ON fulfillment.pick_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_seq_update ON fulfillment.pick_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_shp_seq_select ON fulfillment.shipment_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_shp_seq_insert ON fulfillment.shipment_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_shp_seq_update ON fulfillment.shipment_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_pick_orders_select ON fulfillment.pick_orders FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_orders_insert ON fulfillment.pick_orders FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_orders_update ON fulfillment.pick_orders FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_pick_lines_select ON fulfillment.pick_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_lines_insert ON fulfillment.pick_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_pick_lines_update ON fulfillment.pick_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_shipments_select ON fulfillment.shipments FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_shipments_insert ON fulfillment.shipments FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_shipments_update ON fulfillment.shipments FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_shipment_lines_select ON fulfillment.shipment_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_shipment_lines_insert ON fulfillment.shipment_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_pick_seq_migrator ON fulfillment.pick_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_shp_seq_migrator ON fulfillment.shipment_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_pick_orders_migrator ON fulfillment.pick_orders FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_pick_lines_migrator ON fulfillment.pick_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_shipments_migrator ON fulfillment.shipments FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_shipment_lines_migrator ON fulfillment.shipment_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_guard_dispatched_shipment_line_immutability ON fulfillment.shipment_lines;
                DROP FUNCTION IF EXISTS fulfillment.guard_dispatched_shipment_line_immutability();
                DROP TRIGGER IF EXISTS trg_guard_dispatched_shipment_immutability ON fulfillment.shipments;
                DROP FUNCTION IF EXISTS fulfillment.guard_dispatched_shipment_immutability();
                """);

            migrationBuilder.DropTable(
                name: "pick_sequences",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "shipment_lines",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "shipment_sequences",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "pick_lines",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "pick_orders",
                schema: "fulfillment");

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
                name: "ck_stock_movements_type",
                schema: "inventory",
                table: "stock_movements",
                sql: "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND')");
        }
    }
}
