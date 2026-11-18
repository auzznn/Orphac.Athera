using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M06_DockCustodyAndReceivingGateOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "procurement",
                columns: table => new
                {
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    po_revision_number_snapshot = table.Column<int>(type: "integer", nullable: false),
                    po_snapshot_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_delivery_note_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    arrival_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    receiving_station = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    vehicle_or_seal_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    document_evidence_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    total_delivered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_rejected_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_valued_accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_unresolved_custody_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_held_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    has_financial_posting = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    received_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.goods_receipt_id);
                    table.UniqueConstraint("AK_goods_receipts_tenant_id_legal_entity_id_goods_receipt_id", x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id });
                    table.CheckConstraint("ck_goods_receipts_conservation", "total_delivered_base_quantity = total_rejected_base_quantity + total_valued_accepted_base_quantity + total_unresolved_custody_base_quantity");
                    table.CheckConstraint("ck_goods_receipts_nonnegative", "total_delivered_base_quantity > 0 AND total_rejected_base_quantity >= 0 AND total_valued_accepted_base_quantity >= 0 AND total_unresolved_custody_base_quantity >= 0 AND total_released_base_quantity >= 0 AND total_held_base_quantity >= 0");
                    table.CheckConstraint("ck_goods_receipts_status", "status IN ('ARRIVED', 'IN_DOCK_INSPECTION', 'RELEASED_FOR_ACCEPTANCE', 'FULLY_REJECTED_UNVALUED')");
                    table.CheckConstraint("ck_goods_receipts_unvalued_gate1", "has_financial_posting = false AND total_valued_accepted_base_quantity = 0");
                    table.ForeignKey(
                        name: "FK_goods_receipts_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_order_revisions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_orders_tenant_id_legal_entity_id_pu~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_tenant_memberships_tenant_id_received_by_use~",
                        columns: x => new { x.tenant_id, x.received_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_sequences",
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
                    table.PrimaryKey("PK_receipt_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_receipt_sequences_nonnegative", "last_number >= 0");
                    table.CheckConstraint("ck_receipt_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_receipt_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "procurement",
                columns: table => new
                {
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineage_line_key = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_kind_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_uom_code_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    uom_factor_to_base_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    po_ordered_base_quantity_snapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    physically_delivered_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    physically_delivered_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    pending_check_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_held_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_rejected_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    valued_accepted_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    unresolved_custody_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    overdelivery_excess_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    custody_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    discrepancy_reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    discrepancy_notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    condition_evidence_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.goods_receipt_line_id);
                    table.UniqueConstraint("AK_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~", x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id });
                    table.CheckConstraint("ck_gr_lines_conservation", "physically_delivered_base_quantity = dock_rejected_base_quantity + valued_accepted_base_quantity + unresolved_custody_base_quantity AND unresolved_custody_base_quantity = pending_check_base_quantity + dock_held_base_quantity + (dock_released_base_quantity - valued_accepted_base_quantity)");
                    table.CheckConstraint("ck_gr_lines_custody_status", "custody_status IN ('ARRIVED', 'DOCK_CHECK_PENDING', 'DOCK_HELD', 'DOCK_REJECTED', 'DOCK_RELEASED')");
                    table.CheckConstraint("ck_gr_lines_nonnegative_buckets", "pending_check_base_quantity >= 0 AND dock_held_base_quantity >= 0 AND dock_released_base_quantity >= 0 AND dock_rejected_base_quantity >= 0 AND valued_accepted_base_quantity >= 0 AND unresolved_custody_base_quantity >= 0 AND overdelivery_excess_base_quantity >= 0");
                    table.CheckConstraint("ck_gr_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_gr_lines_positive_delivered", "physically_delivered_quantity > 0 AND physically_delivered_base_quantity > 0 AND uom_factor_to_base_snapshot > 0");
                    table.CheckConstraint("ck_gr_lines_s06_unvalued", "valued_accepted_base_quantity = 0");
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_purchase_order_lines_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.po_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "po_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_units_of_measure_tenant_id_base_uom_id",
                        columns: x => new { x.tenant_id, x.base_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_units_of_measure_tenant_id_received_uom~",
                        columns: x => new { x.tenant_id, x.received_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dock_checks",
                schema: "procurement",
                columns: table => new
                {
                    dock_check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    previous_custody_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    resulting_custody_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    pending_check_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_held_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_released_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    dock_rejected_base_quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discrepancy_reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inspection_notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    evidence_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dock_checks", x => x.dock_check_id);
                    table.CheckConstraint("ck_dock_checks_nonnegative", "pending_check_base_quantity >= 0 AND dock_held_base_quantity >= 0 AND dock_released_base_quantity >= 0 AND dock_rejected_base_quantity >= 0");
                    table.CheckConstraint("ck_dock_checks_status", "resulting_custody_status IN ('ARRIVED', 'DOCK_CHECK_PENDING', 'DOCK_HELD', 'DOCK_REJECTED', 'DOCK_RELEASED')");
                    table.ForeignKey(
                        name: "FK_dock_checks_goods_receipt_lines_tenant_id_legal_entity_id_g~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dock_checks_goods_receipts_tenant_id_legal_entity_id_goods_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dock_checks_tenant_memberships_tenant_id_checked_by_user_id",
                        columns: x => new { x.tenant_id, x.checked_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dock_checks_tenant_id_checked_by_user_id",
                schema: "procurement",
                table: "dock_checks",
                columns: new[] { "tenant_id", "checked_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_dock_checks_tenant_id_legal_entity_id_goods_receipt_id_chec~",
                schema: "procurement",
                table: "dock_checks",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "checked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_dock_checks_idempotency",
                schema: "procurement",
                table: "dock_checks",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_base_uom_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_item_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "po_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_purchase_orde~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "lineage_line_key" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_received_uom_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "received_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_po_revision_id",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "po_revision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_purchase_order_id_~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "received_date" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_received_by_user_id",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "received_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipts_idempotency",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipts_number",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipts_supplier_delivery_note",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_partner_id", "supplier_delivery_note_number" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE procurement.receipt_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.receipt_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.goods_receipts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.goods_receipts FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.goods_receipt_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.goods_receipt_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.dock_checks ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.dock_checks FORCE ROW LEVEL SECURITY;

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON TABLE
                      procurement.receipt_sequences,
                      procurement.goods_receipts,
                      procurement.goods_receipt_lines,
                      procurement.dock_checks
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      procurement.receipt_sequences,
                      procurement.goods_receipts,
                      procurement.goods_receipt_lines
                    TO athera_app;

                    -- Strictly append-only inspection ledger: zero UPDATE or DELETE for runtime role.
                    GRANT SELECT, INSERT ON TABLE
                      procurement.dock_checks
                    TO athera_app;

                    CREATE POLICY p_proc_rcpt_seq_select ON procurement.receipt_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rcpt_seq_insert ON procurement.receipt_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rcpt_seq_update ON procurement.receipt_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_gr_select ON procurement.goods_receipts FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_gr_insert ON procurement.goods_receipts FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_gr_update ON procurement.goods_receipts FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_gr_lines_select ON procurement.goods_receipt_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_gr_lines_insert ON procurement.goods_receipt_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_gr_lines_update ON procurement.goods_receipt_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_dock_checks_select ON procurement.dock_checks FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_dock_checks_insert ON procurement.dock_checks FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_proc_rcpt_seq_migrator_all ON procurement.receipt_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_gr_migrator_all ON procurement.goods_receipts FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_gr_lines_migrator_all ON procurement.goods_receipt_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_dock_checks_migrator_all ON procurement.dock_checks FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dock_checks",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receipt_sequences",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_po_line_id",
                schema: "procurement",
                table: "purchase_order_lines");
        }
    }
}
