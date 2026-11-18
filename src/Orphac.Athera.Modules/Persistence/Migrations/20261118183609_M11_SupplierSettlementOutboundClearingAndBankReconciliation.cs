using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M11_SupplierSettlementOutboundClearingAndBankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cash");

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                schema: "cash",
                columns: table => new
                {
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    branch_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    account_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_holder_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    gl_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clearing_gl_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.bank_account_id);
                    table.UniqueConstraint("AK_bank_accounts_tenant_id_legal_entity_id_bank_account_id", x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id });
                    table.CheckConstraint("ck_bank_accounts_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_bank_accounts_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.ForeignKey(
                        name: "FK_bank_accounts_accounts_tenant_id_legal_entity_id_clearing_g~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.clearing_gl_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_accounts_accounts_tenant_id_legal_entity_id_gl_account~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.gl_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_accounts_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_accounts_tenant_memberships_tenant_id_created_by_user_~",
                        columns: x => new { x.tenant_id, x.created_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instruction_sequences",
                schema: "cash",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruction_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_instruction_sequences_legal_entities_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_sequences",
                schema: "cash",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_match_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_sequences",
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
                    table.PrimaryKey("PK_payment_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_payment_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "statement_sequences",
                schema: "cash",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statement_sequences", x => new { x.tenant_id, x.legal_entity_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_statement_sequences_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_statements",
                schema: "cash",
                columns: table => new
                {
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    statement_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    statement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    closing_balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_debit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    source_format = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    imported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statements", x => x.bank_statement_id);
                    table.UniqueConstraint("AK_bank_statements_tenant_id_legal_entity_id_bank_statement_id", x => new { x.tenant_id, x.legal_entity_id, x.bank_statement_id });
                    table.CheckConstraint("ck_bank_statements_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_bank_statements_totals", "line_count >= 1 AND total_debit_amount >= 0 AND total_credit_amount >= 0");
                    table.ForeignKey(
                        name: "FK_bank_statements_bank_accounts_tenant_id_legal_entity_id_ban~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id },
                        principalSchema: "cash",
                        principalTable: "bank_accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_statements_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_statements_tenant_memberships_tenant_id_imported_by_us~",
                        columns: x => new { x.tenant_id, x.imported_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                schema: "ap",
                columns: table => new
                {
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payment_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_code_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bank_name_snapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    supplier_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code_snapshot = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    supplier_legal_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    beneficiary_bank_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    beneficiary_account_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    beneficiary_account_holder = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_allocated_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reconciled_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    remaining_clearing_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    simulated_instruction_count = table.Column<int>(type: "integer", nullable: false),
                    latest_instruction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latest_instruction_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    release_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    release_idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    release_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reversal_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.supplier_payment_id);
                    table.UniqueConstraint("AK_supplier_payments_tenant_id_legal_entity_id_supplier_paymen~", x => new { x.tenant_id, x.legal_entity_id, x.supplier_payment_id });
                    table.CheckConstraint("ck_supplier_payments_amounts", "total_allocated_amount > 0 AND reconciled_amount >= 0 AND remaining_clearing_amount >= 0 AND ((status IN ('RELEASED', 'PARTIALLY_RECONCILED', 'RECONCILED') AND total_allocated_amount = reconciled_amount + remaining_clearing_amount) OR status NOT IN ('RELEASED', 'PARTIALLY_RECONCILED', 'RECONCILED'))");
                    table.CheckConstraint("ck_supplier_payments_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_supplier_payments_method", "payment_method IN ('BANK_TRANSFER_IDR', 'CLEARING_RTGS', 'BIFAST_IDR')");
                    table.CheckConstraint("ck_supplier_payments_status", "status IN ('DRAFT', 'PENDING_RELEASE', 'RELEASED', 'PARTIALLY_RECONCILED', 'RECONCILED', 'BANK_REJECTED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_supplier_payments_bank_accounts_tenant_id_legal_entity_id_b~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id },
                        principalSchema: "cash",
                        principalTable: "bank_accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_journal_entries_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.rejection_reversal_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_journal_entries_tenant_id_legal_entity_i~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.release_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_partners_tenant_id_supplier_partner_id",
                        columns: x => new { x.tenant_id, x.supplier_partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_tenant_memberships_tenant_id_prepared_by_~",
                        columns: x => new { x.tenant_id, x.prepared_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_lines",
                schema: "cash",
                columns: table => new
                {
                    bank_statement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    value_date = table.Column<DateOnly>(type: "date", nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    matched_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unmatched_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    counterparty_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    line_fingerprint_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_lines", x => x.bank_statement_line_id);
                    table.UniqueConstraint("AK_bank_statement_lines_tenant_id_legal_entity_id_bank_stateme~", x => new { x.tenant_id, x.legal_entity_id, x.bank_statement_line_id });
                    table.CheckConstraint("ck_bank_statement_lines_amounts", "amount > 0 AND matched_amount >= 0 AND unmatched_amount >= 0 AND amount = matched_amount + unmatched_amount");
                    table.CheckConstraint("ck_bank_statement_lines_direction", "direction IN ('DEBIT', 'CREDIT')");
                    table.CheckConstraint("ck_bank_statement_lines_status", "status IN ('UNMATCHED', 'PARTIALLY_MATCHED', 'MATCHED', 'IGNORED')");
                    table.ForeignKey(
                        name: "FK_bank_statement_lines_bank_accounts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id },
                        principalSchema: "cash",
                        principalTable: "bank_accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_statement_lines_bank_statements_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_statement_id },
                        principalSchema: "cash",
                        principalTable: "bank_statements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_statement_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "ap",
                columns: table => new
                {
                    payment_allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    payable_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payable_number_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_number_snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payable_open_before_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    payable_remaining_after_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.payment_allocation_id);
                    table.UniqueConstraint("AK_payment_allocations_tenant_id_legal_entity_id_payment_alloc~", x => new { x.tenant_id, x.legal_entity_id, x.payment_allocation_id });
                    table.CheckConstraint("ck_payment_allocations_amounts", "payable_open_before_amount > 0 AND allocated_amount > 0 AND payable_remaining_after_amount >= 0 AND allocated_amount <= payable_open_before_amount");
                    table.CheckConstraint("ck_payment_allocations_line_number", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_payment_allocations_payable_items_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.payable_item_id },
                        principalSchema: "ap",
                        principalTable: "payable_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_supplier_bills_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bill_id },
                        principalSchema: "ap",
                        principalTable: "supplier_bills",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_supplier_payments_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_payment_id },
                        principalSchema: "ap",
                        principalTable: "supplier_payments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_instructions",
                schema: "cash",
                columns: table => new
                {
                    payment_instruction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instruction_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    simulation_only = table.Column<bool>(type: "boolean", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payload_text = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_instructions", x => x.payment_instruction_id);
                    table.UniqueConstraint("AK_payment_instructions_tenant_id_legal_entity_id_payment_inst~", x => new { x.tenant_id, x.legal_entity_id, x.payment_instruction_id });
                    table.CheckConstraint("ck_payment_instructions_amount", "total_amount > 0");
                    table.CheckConstraint("ck_payment_instructions_currency", "currency_code = 'IDR'");
                    table.CheckConstraint("ck_payment_instructions_simulation_only", "simulation_only = true");
                    table.ForeignKey(
                        name: "FK_payment_instructions_bank_accounts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id },
                        principalSchema: "cash",
                        principalTable: "bank_accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_instructions_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_instructions_supplier_payments_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_payment_id },
                        principalSchema: "ap",
                        principalTable: "supplier_payments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_instructions_tenant_memberships_tenant_id_generated~",
                        columns: x => new { x.tenant_id, x.generated_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_payment_matches",
                schema: "cash",
                columns: table => new
                {
                    bank_payment_match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    matched_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    match_idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    clearing_gl_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reversal_gl_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    matched_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reversed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_payment_matches", x => x.bank_payment_match_id);
                    table.UniqueConstraint("AK_bank_payment_matches_tenant_id_legal_entity_id_bank_payment~", x => new { x.tenant_id, x.legal_entity_id, x.bank_payment_match_id });
                    table.CheckConstraint("ck_bank_payment_matches_amount", "matched_amount > 0");
                    table.CheckConstraint("ck_bank_payment_matches_status", "status IN ('POSTED', 'REVERSED')");
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_bank_accounts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_account_id },
                        principalSchema: "cash",
                        principalTable: "bank_accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_bank_statement_lines_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_statement_line_id },
                        principalSchema: "cash",
                        principalTable: "bank_statement_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_statement_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_bank_statements_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.bank_statement_id },
                        principalSchema: "cash",
                        principalTable: "bank_statements",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "bank_statement_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_journal_entries_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.clearing_gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_journal_entries_tenant_id_legal_entit~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.reversal_gl_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_legal_entities_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_supplier_payments_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_payment_id },
                        principalSchema: "ap",
                        principalTable: "supplier_payments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_payment_matches_tenant_memberships_tenant_id_matched_b~",
                        columns: x => new { x.tenant_id, x.matched_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_created_by_user_id",
                schema: "cash",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_legal_entity_id_clearing_gl_account~",
                schema: "cash",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "clearing_gl_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_legal_entity_id_gl_account_id",
                schema: "cash",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "gl_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bank_accounts_code",
                schema: "cash",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "account_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_payment_matches_payment",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_legal_entity_id_bank_account~",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_legal_entity_id_bank_statem~1",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_statement_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_legal_entity_id_bank_stateme~",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_statement_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_legal_entity_id_clearing_gl_~",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "clearing_gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_legal_entity_id_reversal_gl_~",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "reversal_gl_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_payment_matches_tenant_id_matched_by_user_id",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "matched_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bank_payment_matches_idemp",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "match_idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bank_payment_matches_number",
                schema: "cash",
                table: "bank_payment_matches",
                columns: new[] { "tenant_id", "legal_entity_id", "match_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bank_statement_lines_fingerprint",
                schema: "cash",
                table: "bank_statement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_account_id", "line_fingerprint_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bank_statement_lines_number",
                schema: "cash",
                table: "bank_statement_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_statement_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_statements_tenant_id_imported_by_user_id",
                schema: "cash",
                table: "bank_statements",
                columns: new[] { "tenant_id", "imported_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bank_statements_number",
                schema: "cash",
                table: "bank_statements",
                columns: new[] { "tenant_id", "legal_entity_id", "statement_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bank_statements_reference",
                schema: "cash",
                table: "bank_statements",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_account_id", "statement_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_tenant_id_legal_entity_id_payable_item_~",
                schema: "ap",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "payable_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_tenant_id_legal_entity_id_supplier_bill~",
                schema: "ap",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_allocations_line",
                schema: "ap",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_allocations_payable",
                schema: "ap",
                table: "payment_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id", "payable_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_instructions_payment",
                schema: "cash",
                table: "payment_instructions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_payment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_instructions_tenant_id_generated_by_user_id",
                schema: "cash",
                table: "payment_instructions",
                columns: new[] { "tenant_id", "generated_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_instructions_tenant_id_legal_entity_id_bank_account~",
                schema: "cash",
                table: "payment_instructions",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_instructions_number",
                schema: "cash",
                table: "payment_instructions",
                columns: new[] { "tenant_id", "legal_entity_id", "instruction_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payments_status",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_tenant_id_legal_entity_id_bank_account_id",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "bank_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_tenant_id_legal_entity_id_rejection_rever~",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "rejection_reversal_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_tenant_id_legal_entity_id_release_journal~",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "release_journal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_tenant_id_prepared_by_user_id",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "prepared_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_tenant_id_supplier_partner_id",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "supplier_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_payments_draft_code",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_payments_number",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "payment_number" },
                unique: true,
                filter: "payment_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_supplier_payments_release_idemp",
                schema: "ap",
                table: "supplier_payments",
                columns: new[] { "tenant_id", "legal_entity_id", "release_idempotency_key" },
                unique: true,
                filter: "release_idempotency_key IS NOT NULL");

            migrationBuilder.Sql("""
                ALTER TABLE ap.payment_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.payment_sequences FORCE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_payments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.supplier_payments FORCE ROW LEVEL SECURITY;
                ALTER TABLE ap.payment_allocations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ap.payment_allocations FORCE ROW LEVEL SECURITY;

                ALTER TABLE cash.bank_accounts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_accounts FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.instruction_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.instruction_sequences FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.statement_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.statement_sequences FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.match_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.match_sequences FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.payment_instructions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.payment_instructions FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_statements ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_statements FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_statement_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_statement_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_payment_matches ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cash.bank_payment_matches FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION cash.guard_payment_instruction_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'payment_instruction_immutable: Simulated payment instructions are strictly append-only audit records.';
                END;
                $$;

                CREATE TRIGGER trg_cash_payment_instructions_immutability
                  BEFORE UPDATE OR DELETE ON cash.payment_instructions
                  FOR EACH ROW EXECUTE FUNCTION cash.guard_payment_instruction_immutability();

                CREATE OR REPLACE FUNCTION cash.guard_bank_statement_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'bank_statement_immutable: Imported bank statements are strictly append-only.';
                END;
                $$;

                CREATE TRIGGER trg_cash_bank_statements_immutability
                  BEFORE UPDATE OR DELETE ON cash.bank_statements
                  FOR EACH ROW EXECUTE FUNCTION cash.guard_bank_statement_immutability();

                CREATE OR REPLACE FUNCTION ap.guard_released_supplier_payment()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('supplier_payment_delete_forbidden: Supplier payment %s cannot be deleted.', OLD.supplier_payment_id);
                  END IF;

                  IF OLD.status IN ('RELEASED', 'PARTIALLY_RECONCILED', 'RECONCILED', 'BANK_REJECTED') THEN
                    IF OLD.status IN ('RECONCILED', 'BANK_REJECTED') AND OLD.status <> NEW.status AND
                       NOT (OLD.status = 'RECONCILED' AND NEW.status IN ('RELEASED', 'PARTIALLY_RECONCILED')) THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('settled_supplier_payment_immutable: Payment %s in status %s cannot transition to %s.', OLD.supplier_payment_id, OLD.status, NEW.status);
                    END IF;

                    IF NEW.tenant_id <> OLD.tenant_id OR
                       NEW.legal_entity_id <> OLD.legal_entity_id OR
                       NEW.draft_code <> OLD.draft_code OR
                       NEW.payment_number IS DISTINCT FROM OLD.payment_number OR
                       NEW.bank_account_id <> OLD.bank_account_id OR
                       NEW.supplier_partner_id <> OLD.supplier_partner_id OR
                       NEW.total_allocated_amount <> OLD.total_allocated_amount OR
                       NEW.release_journal_id IS DISTINCT FROM OLD.release_journal_id OR
                       NEW.release_reference IS DISTINCT FROM OLD.release_reference OR
                       NEW.prepared_by_user_id <> OLD.prepared_by_user_id OR
                       NEW.released_by_user_id IS DISTINCT FROM OLD.released_by_user_id THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('released_supplier_payment_immutable: Released financial fields on payment %s cannot be altered.', OLD.supplier_payment_id);
                    END IF;

                    IF NEW.status = 'BANK_REJECTED' AND OLD.reconciled_amount <> 0 THEN
                      RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'payment_already_reconciled_reverse_match_first: Reconciled payments cannot be bank-rejected directly.';
                    END IF;
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_ap_supplier_payments_immutability
                  BEFORE UPDATE OR DELETE ON ap.supplier_payments
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_released_supplier_payment();

                CREATE OR REPLACE FUNCTION ap.guard_released_payment_allocation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_status text;
                  v_payment_id uuid;
                  v_tenant_id uuid;
                  v_entity_id uuid;
                BEGIN
                  v_payment_id := COALESCE(NEW.supplier_payment_id, OLD.supplier_payment_id);
                  v_tenant_id := COALESCE(NEW.tenant_id, OLD.tenant_id);
                  v_entity_id := COALESCE(NEW.legal_entity_id, OLD.legal_entity_id);

                  SELECT status INTO v_status
                  FROM ap.supplier_payments
                  WHERE tenant_id = v_tenant_id
                    AND legal_entity_id = v_entity_id
                    AND supplier_payment_id = v_payment_id;

                  IF v_status NOT IN ('DRAFT', 'PENDING_RELEASE') THEN
                    IF TG_OP = 'UPDATE' AND
                       NEW.allocated_amount = OLD.allocated_amount AND
                       NEW.payable_item_id = OLD.payable_item_id AND
                       NEW.supplier_payment_id = OLD.supplier_payment_id THEN
                      RETURN NEW;
                    END IF;

                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('released_payment_allocation_immutable: Allocations of released payment %s are immutable.', v_payment_id);
                  END IF;

                  IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_ap_payment_allocations_immutability
                  BEFORE INSERT OR UPDATE OR DELETE ON ap.payment_allocations
                  FOR EACH ROW EXECUTE FUNCTION ap.guard_released_payment_allocation();

                CREATE OR REPLACE FUNCTION cash.guard_bank_payment_match_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'bank_payment_match_delete_forbidden: Bank payment matches cannot be deleted.';
                  END IF;

                  IF OLD.status = 'REVERSED' THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = format('reversed_bank_match_immutable: Reversed match %s is immutable.', OLD.bank_payment_match_id);
                  END IF;

                  IF NEW.matched_amount <> OLD.matched_amount OR
                     NEW.clearing_gl_journal_id <> OLD.clearing_gl_journal_id OR
                     NEW.supplier_payment_id <> OLD.supplier_payment_id OR
                     NEW.bank_statement_line_id <> OLD.bank_statement_line_id THEN
                    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'posted_bank_match_fields_immutable: Match core financial fields cannot be altered.';
                  END IF;

                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_cash_bank_payment_matches_immutability
                  BEFORE UPDATE OR DELETE ON cash.bank_payment_matches
                  FOR EACH ROW EXECUTE FUNCTION cash.guard_bank_payment_match_immutability();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    GRANT USAGE ON SCHEMA cash, ap TO athera_app;

                    REVOKE ALL ON TABLE
                      ap.payment_sequences,
                      ap.supplier_payments,
                      ap.payment_allocations,
                      cash.bank_accounts,
                      cash.instruction_sequences,
                      cash.statement_sequences,
                      cash.match_sequences,
                      cash.payment_instructions,
                      cash.bank_statements,
                      cash.bank_statement_lines,
                      cash.bank_payment_matches
                    FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      ap.payment_sequences,
                      ap.supplier_payments,
                      cash.bank_accounts,
                      cash.instruction_sequences,
                      cash.statement_sequences,
                      cash.match_sequences,
                      cash.bank_statement_lines,
                      cash.bank_payment_matches
                    TO athera_app;

                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      ap.payment_allocations
                    TO athera_app;

                    -- Append-only banking & instruction evidence: zero UPDATE/DELETE for runtime role
                    GRANT SELECT, INSERT ON TABLE
                      cash.payment_instructions,
                      cash.bank_statements
                    TO athera_app;

                    CREATE POLICY p_ap_pay_seq_select ON ap.payment_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_pay_seq_insert ON ap.payment_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_pay_seq_update ON ap.payment_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_payments_select ON ap.supplier_payments FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_payments_insert ON ap.supplier_payments FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_payments_update ON ap.supplier_payments FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_ap_allocs_select ON ap.payment_allocations FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_allocs_insert ON ap.payment_allocations FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_allocs_update ON ap.payment_allocations FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_ap_allocs_delete ON ap.payment_allocations FOR DELETE TO athera_app USING (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_banks_select ON cash.bank_accounts FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_banks_insert ON cash.bank_accounts FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_banks_update ON cash.bank_accounts FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_inst_seq_select ON cash.instruction_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_inst_seq_insert ON cash.instruction_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_inst_seq_update ON cash.instruction_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_stmt_seq_select ON cash.statement_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_stmt_seq_insert ON cash.statement_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_stmt_seq_update ON cash.statement_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_match_seq_select ON cash.match_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_match_seq_insert ON cash.match_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_match_seq_update ON cash.match_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_inst_select ON cash.payment_instructions FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_inst_insert ON cash.payment_instructions FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_stmt_select ON cash.bank_statements FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_stmt_insert ON cash.bank_statements FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_stmt_lines_select ON cash.bank_statement_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_stmt_lines_insert ON cash.bank_statement_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_stmt_lines_update ON cash.bank_statement_lines FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_cash_matches_select ON cash.bank_payment_matches FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_matches_insert ON cash.bank_payment_matches FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_cash_matches_update ON cash.bank_payment_matches FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    GRANT USAGE ON SCHEMA cash, ap TO athera_migrator;
                    CREATE POLICY p_ap_pay_seq_migrator_all ON ap.payment_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_payments_migrator_all ON ap.supplier_payments FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_ap_allocs_migrator_all ON ap.payment_allocations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_banks_migrator_all ON cash.bank_accounts FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_inst_seq_migrator_all ON cash.instruction_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_stmt_seq_migrator_all ON cash.statement_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_match_seq_migrator_all ON cash.match_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_inst_migrator_all ON cash.payment_instructions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_stmt_migrator_all ON cash.bank_statements FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_stmt_lines_migrator_all ON cash.bank_statement_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_cash_matches_migrator_all ON cash.bank_payment_matches FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_cash_payment_instructions_immutability ON cash.payment_instructions;
                DROP TRIGGER IF EXISTS trg_cash_bank_statements_immutability ON cash.bank_statements;
                DROP TRIGGER IF EXISTS trg_ap_supplier_payments_immutability ON ap.supplier_payments;
                DROP TRIGGER IF EXISTS trg_ap_payment_allocations_immutability ON ap.payment_allocations;
                DROP TRIGGER IF EXISTS trg_cash_bank_payment_matches_immutability ON cash.bank_payment_matches;
                DROP FUNCTION IF EXISTS cash.guard_payment_instruction_immutability();
                DROP FUNCTION IF EXISTS cash.guard_bank_statement_immutability();
                DROP FUNCTION IF EXISTS ap.guard_released_supplier_payment();
                DROP FUNCTION IF EXISTS ap.guard_released_payment_allocation();
                DROP FUNCTION IF EXISTS cash.guard_bank_payment_match_immutability();
                """);
            migrationBuilder.DropTable(
                name: "bank_payment_matches",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "instruction_sequences",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "match_sequences",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "payment_allocations",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "payment_instructions",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "payment_sequences",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "statement_sequences",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "bank_statement_lines",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "supplier_payments",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "bank_statements",
                schema: "cash");

            migrationBuilder.DropTable(
                name: "bank_accounts",
                schema: "cash");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS cash CASCADE;");
        }
    }
}
