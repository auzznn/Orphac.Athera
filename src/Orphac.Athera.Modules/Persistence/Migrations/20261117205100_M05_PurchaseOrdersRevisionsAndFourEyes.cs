using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M05_PurchaseOrdersRevisionsAndFourEyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "procurement");

            migrationBuilder.CreateTable(
                name: "delivery_tolerance_policies",
                schema: "procurement",
                columns: table => new
                {
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    overdelivery_tolerance_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    overdelivery_tolerance_base_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    allow_unlimited_overdelivery = table.Column<bool>(type: "boolean", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_tolerance_policies", x => x.policy_id);
                    table.UniqueConstraint("AK_delivery_tolerance_policies_tenant_id_legal_entity_id_polic~", x => new { x.tenant_id, x.legal_entity_id, x.policy_id });
                    table.CheckConstraint("ck_delivery_tolerance_nonnegative", "overdelivery_tolerance_percent >= 0 AND overdelivery_tolerance_base_qty >= 0");
                    table.CheckConstraint("ck_delivery_tolerance_od01_safe", "overdelivery_tolerance_percent = 0 AND overdelivery_tolerance_base_qty = 0 AND allow_unlimited_overdelivery = false");
                    table.CheckConstraint("ck_delivery_tolerance_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_delivery_tolerance_policies_legal_entities_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_approvals",
                schema: "approval",
                columns: table => new
                {
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    request_summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    approver_mfa_verified = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_approvals", x => x.approval_id);
                    table.UniqueConstraint("AK_document_approvals_tenant_id_legal_entity_id_approval_id", x => new { x.tenant_id, x.legal_entity_id, x.approval_id });
                    table.CheckConstraint("ck_document_approvals_four_eyes", "decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id");
                    table.CheckConstraint("ck_document_approvals_revision", "revision_number >= 1");
                    table.CheckConstraint("ck_document_approvals_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_document_approvals_approval_policies_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_id },
                        principalSchema: "approval",
                        principalTable: "approval_policies",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_approvals_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_approvals_tenant_memberships_tenant_id_requested_b~",
                        columns: x => new { x.tenant_id, x.requested_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "po_sequences",
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
                    table.PrimaryKey("PK_po_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.CheckConstraint("ck_po_sequences_nonnegative", "last_number >= 0");
                    table.CheckConstraint("ck_po_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_po_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "procurement",
                columns: table => new
                {
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    po_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivery_destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_terms_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    current_approved_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_approved_revision_number = table.Column<int>(type: "integer", nullable: false),
                    latest_revision_number = table.Column<int>(type: "integer", nullable: false),
                    downstream_reference_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_base_quantity_total = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
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
                    table.PrimaryKey("PK_purchase_orders", x => x.purchase_order_id);
                    table.UniqueConstraint("AK_purchase_orders_tenant_id_legal_entity_id_purchase_order_id", x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id });
                    table.CheckConstraint("ck_purchase_orders_downstream_nonnegative", "downstream_reference_count >= 0 AND accepted_base_quantity_total >= 0");
                    table.CheckConstraint("ck_purchase_orders_revisions_nonnegative", "latest_revision_number >= 1 AND current_approved_revision_number >= 0 AND current_approved_revision_number <= latest_revision_number");
                    table.CheckConstraint("ck_purchase_orders_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'CLOSED', 'CANCELLED')");
                    table.CheckConstraint("ck_purchase_orders_terms", "payment_terms_days >= 0 AND payment_terms_days <= 365");
                    table.ForeignKey(
                        name: "FK_purchase_orders_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_tenant_memberships_tenant_id_created_by_use~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_revisions",
                schema: "procurement",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_npwp_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    supplier_pkp_status_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivery_destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_terms_days = table.Column<int>(type: "integer", nullable: false),
                    tolerance_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tolerance_mode_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    change_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ppn_tax_total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    withholding_tax_total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_revisions", x => x.revision_id);
                    table.UniqueConstraint("AK_purchase_order_revisions_tenant_id_legal_entity_id_revision~", x => new { x.tenant_id, x.legal_entity_id, x.revision_id });
                    table.CheckConstraint("ck_po_revisions_four_eyes", "decided_by_user_id IS NULL OR submitted_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id");
                    table.CheckConstraint("ck_po_revisions_number", "revision_number >= 1");
                    table.CheckConstraint("ck_po_revisions_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'REJECTED', 'SUPERSEDED', 'CANCELLED')");
                    table.CheckConstraint("ck_po_revisions_totals_nonnegative", "subtotal_amount >= 0 AND ppn_tax_total_amount >= 0 AND withholding_tax_total_amount >= 0 AND grand_total_amount >= 0 AND total_ordered_base_quantity >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_delivery_tolerance_policies_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tolerance_policy_id },
                        principalSchema: "procurement",
                        principalTable: "delivery_tolerance_policies",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_document_approvals_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_record_id },
                        principalSchema: "approval",
                        principalTable: "document_approvals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "approval_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_partners_tenant_id_supplier_partne~",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_purchase_order_revisions_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supersedes_revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_purchase_orders_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "procurement",
                columns: table => new
                {
                    po_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_subtotal_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tax_family_snapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    tax_rate_percent_snapshot = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    taxable_base_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    overdelivery_tolerance_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    overdelivery_tolerance_base_qty = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    allow_unlimited_overdelivery = table.Column<bool>(type: "boolean", nullable: false),
                    line_description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.po_line_id);
                    table.CheckConstraint("ck_po_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_po_lines_od01_zero_tolerance", "overdelivery_tolerance_percent = 0 AND overdelivery_tolerance_base_qty = 0 AND allow_unlimited_overdelivery = false");
                    table.CheckConstraint("ck_po_lines_positive_qty_price", "ordered_quantity > 0 AND ordered_base_quantity > 0 AND uom_factor_to_base_snapshot > 0 AND unit_price > 0 AND line_subtotal_amount > 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_order_revisions_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "revision_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_tax_codes_tenant_id_legal_entity_id_ta~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_units_of_measure_tenant_id_ordered_uom~",
                        columns: x => new { x.tenant_id, x.ordered_uom_id },
                        principalSchema: "catalog",
                        principalTable: "units_of_measure",
                        principalColumns: new[] { "tenant_id", "uom_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tolerance_policies_tenant_id_legal_entity_id_code",
                schema: "procurement",
                table: "delivery_tolerance_policies",
                columns: new[] { "tenant_id", "legal_entity_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_approvals_tenant_id_legal_entity_id_policy_id",
                schema: "approval",
                table: "document_approvals",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_document_approvals_tenant_id_legal_entity_id_status_request~",
                schema: "approval",
                table: "document_approvals",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_document_approvals_tenant_id_requested_by_user_id",
                schema: "approval",
                table: "document_approvals",
                columns: new[] { "tenant_id", "requested_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_approvals_source_revision",
                schema: "approval",
                table: "document_approvals",
                columns: new[] { "tenant_id", "legal_entity_id", "source_module", "source_document_type", "source_revision_id" },
                unique: true,
                filter: "status IN ('PENDING', 'APPROVED')");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_item_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_revision_id_~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_revision_id~1",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "lineage_line_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_tax_code_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_ordered_uom_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "ordered_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_legal_entity_id_purchase~",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_legal_entity_id_supersed~",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "supersedes_revision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_legal_entity_id_toleranc~",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "tolerance_policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_po_revisions_approval_record",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_record_id" },
                unique: true,
                filter: "approval_record_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_created_by_user_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_draft_code",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_status_order_date",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "order_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_supplier_partner_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_purchase_orders_entity_po_number",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "po_number" },
                unique: true,
                filter: "po_number IS NOT NULL");

            migrationBuilder.Sql("""
                ALTER TABLE approval.document_approvals ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval.document_approvals FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.delivery_tolerance_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.delivery_tolerance_policies FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.po_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.po_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.purchase_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.purchase_orders FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.purchase_order_revisions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.purchase_order_revisions FORCE ROW LEVEL SECURITY;

                ALTER TABLE procurement.purchase_order_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE procurement.purchase_order_lines FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION procurement.guard_frozen_po_revision_lines()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_frozen boolean;
                BEGIN
                  IF TG_OP IN ('DELETE', 'UPDATE') THEN
                    SELECT r.is_frozen INTO v_frozen
                    FROM procurement.purchase_order_revisions r
                    WHERE r.tenant_id = OLD.tenant_id
                      AND r.legal_entity_id = OLD.legal_entity_id
                      AND r.revision_id = OLD.revision_id;
                    IF COALESCE(v_frozen, false) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('po_revision_frozen_immutable: Revision %s is frozen; lines cannot be modified or deleted.', OLD.revision_id);
                    END IF;
                  END IF;

                  IF TG_OP IN ('INSERT', 'UPDATE') THEN
                    SELECT r.is_frozen INTO v_frozen
                    FROM procurement.purchase_order_revisions r
                    WHERE r.tenant_id = NEW.tenant_id
                      AND r.legal_entity_id = NEW.legal_entity_id
                      AND r.revision_id = NEW.revision_id;
                    IF COALESCE(v_frozen, false) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('po_revision_frozen_immutable: Revision %s is frozen; lines cannot be inserted or moved into it.', NEW.revision_id);
                    END IF;
                  END IF;

                  IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_frozen_po_revision_lines
                  BEFORE INSERT OR UPDATE OR DELETE ON procurement.purchase_order_lines
                  FOR EACH ROW
                  EXECUTE FUNCTION procurement.guard_frozen_po_revision_lines();

                CREATE OR REPLACE FUNCTION procurement.guard_frozen_po_revision_snapshot()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF OLD.is_frozen THEN
                    IF NEW.is_frozen IS DISTINCT FROM true THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('po_revision_frozen_immutable: Frozen revision %s cannot be unfrozen.', OLD.revision_id);
                    END IF;

                    IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                      OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                      OR NEW.purchase_order_id IS DISTINCT FROM OLD.purchase_order_id
                      OR NEW.revision_number IS DISTINCT FROM OLD.revision_number
                      OR NEW.supersedes_revision_id IS DISTINCT FROM OLD.supersedes_revision_id
                      OR NEW.supplier_partner_id IS DISTINCT FROM OLD.supplier_partner_id
                      OR NEW.supplier_code_snapshot IS DISTINCT FROM OLD.supplier_code_snapshot
                      OR NEW.supplier_legal_name_snapshot IS DISTINCT FROM OLD.supplier_legal_name_snapshot
                      OR NEW.supplier_npwp_snapshot IS DISTINCT FROM OLD.supplier_npwp_snapshot
                      OR NEW.supplier_pkp_status_snapshot IS DISTINCT FROM OLD.supplier_pkp_status_snapshot
                      OR NEW.currency_code IS DISTINCT FROM OLD.currency_code
                      OR NEW.order_date IS DISTINCT FROM OLD.order_date
                      OR NEW.expected_delivery_date IS DISTINCT FROM OLD.expected_delivery_date
                      OR NEW.delivery_destination IS DISTINCT FROM OLD.delivery_destination
                      OR NEW.payment_terms_days IS DISTINCT FROM OLD.payment_terms_days
                      OR NEW.tolerance_policy_id IS DISTINCT FROM OLD.tolerance_policy_id
                      OR NEW.tolerance_mode_snapshot IS DISTINCT FROM OLD.tolerance_mode_snapshot
                      OR NEW.change_reason IS DISTINCT FROM OLD.change_reason
                      OR NEW.notes IS DISTINCT FROM OLD.notes
                      OR NEW.subtotal_amount IS DISTINCT FROM OLD.subtotal_amount
                      OR NEW.ppn_tax_total_amount IS DISTINCT FROM OLD.ppn_tax_total_amount
                      OR NEW.withholding_tax_total_amount IS DISTINCT FROM OLD.withholding_tax_total_amount
                      OR NEW.grand_total_amount IS DISTINCT FROM OLD.grand_total_amount
                      OR NEW.total_ordered_base_quantity IS DISTINCT FROM OLD.total_ordered_base_quantity
                      OR NEW.snapshot_hash IS DISTINCT FROM OLD.snapshot_hash
                      OR NEW.created_by_user_id IS DISTINCT FROM OLD.created_by_user_id
                      OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
                      OR NEW.submitted_by_user_id IS DISTINCT FROM OLD.submitted_by_user_id
                      OR NEW.submitted_at_utc IS DISTINCT FROM OLD.submitted_at_utc
                      OR NEW.approval_record_id IS DISTINCT FROM OLD.approval_record_id
                    THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('po_revision_frozen_immutable: Approved or submitted snapshot on revision %s cannot be mutated.', OLD.revision_id);
                    END IF;

                    IF NOT (
                      NEW.status = OLD.status
                      OR (OLD.status = 'PENDING_APPROVAL' AND NEW.status IN ('APPROVED', 'REJECTED', 'CANCELLED'))
                      OR (OLD.status = 'APPROVED' AND NEW.status = 'SUPERSEDED')
                    ) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('po_revision_frozen_immutable: Invalid frozen revision status transition %s -> %s.', OLD.status, NEW.status);
                    END IF;
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_guard_frozen_po_revision_snapshot
                  BEFORE UPDATE ON procurement.purchase_order_revisions
                  FOR EACH ROW
                  EXECUTE FUNCTION procurement.guard_frozen_po_revision_snapshot();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA procurement FROM PUBLIC;
                    GRANT USAGE ON SCHEMA procurement, approval TO athera_app;

                    REVOKE ALL ON TABLE
                      approval.document_approvals,
                      procurement.delivery_tolerance_policies,
                      procurement.po_sequences,
                      procurement.purchase_orders,
                      procurement.purchase_order_revisions,
                      procurement.purchase_order_lines
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      approval.document_approvals,
                      procurement.delivery_tolerance_policies,
                      procurement.po_sequences,
                      procurement.purchase_orders,
                      procurement.purchase_order_revisions
                    TO athera_app;

                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      procurement.purchase_order_lines
                    TO athera_app;

                    CREATE POLICY p_approval_doc_approvals_select ON approval.document_approvals FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_approval_doc_approvals_insert ON approval.document_approvals FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_approval_doc_approvals_update ON approval.document_approvals FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_tolerance_select ON procurement.delivery_tolerance_policies FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_tolerance_insert ON procurement.delivery_tolerance_policies FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_tolerance_update ON procurement.delivery_tolerance_policies FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_sequences_select ON procurement.po_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_sequences_insert ON procurement.po_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_sequences_update ON procurement.po_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_po_select ON procurement.purchase_orders FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_po_insert ON procurement.purchase_orders FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_po_update ON procurement.purchase_orders FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_rev_select ON procurement.purchase_order_revisions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rev_insert ON procurement.purchase_order_revisions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_rev_update ON procurement.purchase_order_revisions FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_proc_lines_select ON procurement.purchase_order_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_lines_insert ON procurement.purchase_order_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_lines_update ON procurement.purchase_order_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_proc_lines_delete ON procurement.purchase_order_lines FOR DELETE TO athera_app USING (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_approval_doc_approvals_migrator_all ON approval.document_approvals FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_tolerance_migrator_all ON procurement.delivery_tolerance_policies FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_sequences_migrator_all ON procurement.po_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_po_migrator_all ON procurement.purchase_orders FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_rev_migrator_all ON procurement.purchase_order_revisions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_proc_lines_migrator_all ON procurement.purchase_order_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_frozen_po_revision_lines ON procurement.purchase_order_lines;
                DROP TRIGGER IF EXISTS trg_guard_frozen_po_revision_snapshot ON procurement.purchase_order_revisions;
                DROP FUNCTION IF EXISTS procurement.guard_frozen_po_revision_lines();
                DROP FUNCTION IF EXISTS procurement.guard_frozen_po_revision_snapshot();
                """);

            migrationBuilder.DropTable(
                name: "po_sequences",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "delivery_tolerance_policies",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "document_approvals",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "procurement");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS procurement;
                """);
        }
    }
}
