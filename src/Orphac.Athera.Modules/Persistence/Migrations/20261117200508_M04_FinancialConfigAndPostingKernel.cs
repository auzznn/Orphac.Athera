using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M04_FinancialConfigAndPostingKernel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gl");

            migrationBuilder.EnsureSchema(
                name: "tax");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "gl",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normal_balance = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allow_posting = table.Column<bool>(type: "boolean", nullable: false),
                    is_control_account = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.account_id);
                    table.UniqueConstraint("AK_accounts_tenant_id_legal_entity_id_account_id", x => new { x.tenant_id, x.legal_entity_id, x.account_id });
                    table.CheckConstraint("ck_gl_accounts_header_no_post", "account_category <> 'HEADER_GROUP' OR allow_posting = false");
                    table.CheckConstraint("ck_gl_accounts_no_self_parent", "parent_account_id IS NULL OR parent_account_id <> account_id");
                    table.CheckConstraint("ck_gl_accounts_normal_balance", "normal_balance IN ('DEBIT', 'CREDIT')");
                    table.CheckConstraint("ck_gl_accounts_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_gl_accounts_type", "account_type IN ('ASSET', 'LIABILITY', 'EQUITY', 'REVENUE', 'EXPENSE')");
                    table.ForeignKey(
                        name: "FK_accounts_accounts_tenant_id_legal_entity_id_parent_account_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.parent_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_periods",
                schema: "gl",
                columns: table => new
                {
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    close_note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_periods", x => x.period_id);
                    table.UniqueConstraint("AK_financial_periods_tenant_id_legal_entity_id_period_id", x => new { x.tenant_id, x.legal_entity_id, x.period_id });
                    table.CheckConstraint("ck_gl_periods_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_gl_periods_number", "period_number >= 1 AND period_number <= 13");
                    table.CheckConstraint("ck_gl_periods_status", "status IN ('OPEN', 'CLOSED', 'LOCKED')");
                    table.CheckConstraint("ck_gl_periods_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                    table.ForeignKey(
                        name: "FK_financial_periods_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_mappings",
                schema: "gl",
                columns: table => new
                {
                    mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_mappings", x => x.mapping_id);
                    table.ForeignKey(
                        name: "FK_account_mappings_accounts_tenant_id_legal_entity_id_account~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_mappings_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_codes",
                schema: "tax",
                columns: table => new
                {
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_family = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    treatment_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    nominal_rate_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    taxable_base_ratio_numerator = table.Column<int>(type: "integer", nullable: false),
                    taxable_base_ratio_denominator = table.Column<int>(type: "integer", nullable: false),
                    requires_documentary_evidence = table.Column<bool>(type: "boolean", nullable: false),
                    requires_counterparty_npwp = table.Column<bool>(type: "boolean", nullable: false),
                    tax_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    statutory_reference_note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_codes", x => x.tax_code_id);
                    table.UniqueConstraint("AK_tax_codes_tenant_id_legal_entity_id_tax_code_id", x => new { x.tenant_id, x.legal_entity_id, x.tax_code_id });
                    table.CheckConstraint("ck_tax_codes_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_tax_codes_direction", "direction IN ('INPUT', 'OUTPUT', 'WITHHOLDING')");
                    table.CheckConstraint("ck_tax_codes_family", "tax_family IN ('PPN', 'PPH')");
                    table.CheckConstraint("ck_tax_codes_rate", "nominal_rate_percent >= 0 AND nominal_rate_percent <= 100");
                    table.CheckConstraint("ck_tax_codes_ratio", "taxable_base_ratio_numerator >= 1 AND taxable_base_ratio_denominator >= taxable_base_ratio_numerator");
                    table.CheckConstraint("ck_tax_codes_review", "review_status IN ('UNREVIEWED_CONFIGURABLE', 'REVIEWED_INTERNAL')");
                    table.CheckConstraint("ck_tax_codes_status", "status IN ('ACTIVE', 'INACTIVE')");
                    table.CheckConstraint("ck_tax_codes_treatment", "treatment_class IN ('STANDARD_TAXABLE', 'SPECIAL_DPP_11_12', 'ZERO_RATED', 'EXEMPT', 'NOT_COLLECTED_FACILITY', 'WITHHOLDING_DEDUCTION')");
                    table.ForeignKey(
                        name: "FK_tax_codes_accounts_tenant_id_legal_entity_id_tax_account_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tax_account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_codes_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "gl",
                columns: table => new
                {
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entry_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source_document_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_event_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reversal_of_journal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_debit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_credit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    posted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.journal_id);
                    table.UniqueConstraint("AK_journal_entries_tenant_id_legal_entity_id_journal_id", x => new { x.tenant_id, x.legal_entity_id, x.journal_id });
                    table.CheckConstraint("ck_gl_journals_balanced", "total_debit > 0 AND total_debit = total_credit");
                    table.CheckConstraint("ck_gl_journals_kind", "entry_kind IN ('STANDARD', 'REVERSAL')");
                    table.CheckConstraint("ck_gl_journals_line_count", "line_count >= 2");
                    table.CheckConstraint("ck_gl_journals_no_self_reversal", "reversal_of_journal_id IS NULL OR reversal_of_journal_id <> journal_id");
                    table.ForeignKey(
                        name: "FK_journal_entries_financial_periods_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.period_id },
                        principalSchema: "gl",
                        principalTable: "financial_periods",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "period_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_tenant_id_legal_entity_id_r~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.reversal_of_journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_sequences",
                schema: "gl",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_sequences", x => new { x.tenant_id, x.legal_entity_id, x.period_id });
                    table.CheckConstraint("ck_gl_journal_sequences_nonnegative", "last_number >= 0");
                    table.ForeignKey(
                        name: "FK_journal_sequences_financial_periods_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.period_id },
                        principalSchema: "gl",
                        principalTable: "financial_periods",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "period_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
                schema: "gl",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    debit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_lines", x => x.line_id);
                    table.CheckConstraint("ck_gl_lines_nonnegative", "debit_amount >= 0 AND credit_amount >= 0");
                    table.CheckConstraint("ck_gl_lines_number", "line_number >= 1");
                    table.CheckConstraint("ck_gl_lines_single_sided", "(debit_amount > 0 AND credit_amount = 0) OR (credit_amount > 0 AND debit_amount = 0)");
                    table.ForeignKey(
                        name: "FK_journal_lines_accounts_tenant_id_legal_entity_id_account_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.account_id },
                        principalSchema: "gl",
                        principalTable: "accounts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_items_tenant_id_item_id",
                        columns: x => new { x.tenant_id, x.item_id },
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumns: new[] { "tenant_id", "item_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_journal_entries_tenant_id_legal_entity_id_jou~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.journal_id },
                        principalSchema: "gl",
                        principalTable: "journal_entries",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "journal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_partners_tenant_id_partner_id",
                        columns: x => new { x.tenant_id, x.partner_id },
                        principalSchema: "partners",
                        principalTable: "partners",
                        principalColumns: new[] { "tenant_id", "partner_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_lines_tax_codes_tenant_id_legal_entity_id_tax_code_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.tax_code_id },
                        principalSchema: "tax",
                        principalTable: "tax_codes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_mappings_tenant_id_legal_entity_id_account_id",
                schema: "gl",
                table: "account_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_account_mappings_tenant_id_legal_entity_id_purpose_code",
                schema: "gl",
                table: "account_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "purpose_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_tenant_id_legal_entity_id_account_code",
                schema: "gl",
                table: "accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "account_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_tenant_id_legal_entity_id_account_type_status",
                schema: "gl",
                table: "accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "account_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_tenant_id_legal_entity_id_parent_account_id",
                schema: "gl",
                table: "accounts",
                columns: new[] { "tenant_id", "legal_entity_id", "parent_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_periods_tenant_id_legal_entity_id_fiscal_year_per~",
                schema: "gl",
                table: "financial_periods",
                columns: new[] { "tenant_id", "legal_entity_id", "fiscal_year", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_periods_tenant_id_legal_entity_id_period_code",
                schema: "gl",
                table: "financial_periods",
                columns: new[] { "tenant_id", "legal_entity_id", "period_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_tenant_id_legal_entity_id_journal_number",
                schema: "gl",
                table: "journal_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "journal_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_tenant_id_legal_entity_id_period_id",
                schema: "gl",
                table: "journal_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "period_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_tenant_id_legal_entity_id_posting_date",
                schema: "gl",
                table: "journal_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_single_reversal",
                schema: "gl",
                table: "journal_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "reversal_of_journal_id" },
                unique: true,
                filter: "reversal_of_journal_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_source_event",
                schema: "gl",
                table: "journal_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "source_module", "source_event_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_tenant_id_item_id",
                schema: "gl",
                table: "journal_lines",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_tenant_id_legal_entity_id_account_id",
                schema: "gl",
                table: "journal_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_tenant_id_legal_entity_id_journal_id_line_num~",
                schema: "gl",
                table: "journal_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "journal_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_tenant_id_legal_entity_id_tax_code_id",
                schema: "gl",
                table: "journal_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "tax_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_tenant_id_partner_id",
                schema: "gl",
                table: "journal_lines",
                columns: new[] { "tenant_id", "partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_codes_tenant_id_legal_entity_id_code_effective_from",
                schema: "tax",
                table: "tax_codes",
                columns: new[] { "tenant_id", "legal_entity_id", "code", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_codes_tenant_id_legal_entity_id_tax_account_id",
                schema: "tax",
                table: "tax_codes",
                columns: new[] { "tenant_id", "legal_entity_id", "tax_account_id" });

            migrationBuilder.Sql("""
                ALTER TABLE gl.accounts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.accounts FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.financial_periods ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.financial_periods FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.account_mappings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.account_mappings FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.journal_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.journal_sequences FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.journal_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.journal_entries FORCE ROW LEVEL SECURITY;

                ALTER TABLE gl.journal_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gl.journal_lines FORCE ROW LEVEL SECURITY;

                ALTER TABLE tax.tax_codes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tax.tax_codes FORCE ROW LEVEL SECURITY;

                CREATE OR REPLACE FUNCTION gl.enforce_journal_entry_integrity()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  v_period_status text;
                  v_start_date date;
                  v_end_date date;
                  v_line_count integer;
                  v_sum_debit numeric(18,4);
                  v_sum_credit numeric(18,4);
                BEGIN
                  SELECT p.status, p.start_date, p.end_date
                  INTO v_period_status, v_start_date, v_end_date
                  FROM gl.financial_periods p
                  WHERE p.tenant_id = NEW.tenant_id
                    AND p.legal_entity_id = NEW.legal_entity_id
                    AND p.period_id = NEW.period_id;

                  IF NOT FOUND THEN
                    RAISE EXCEPTION 'gl_period_not_found: Financial period % missing for journal %.', NEW.period_id, NEW.journal_id;
                  END IF;

                  IF v_period_status <> 'OPEN' THEN
                    RAISE EXCEPTION 'gl_period_not_open: Cannot post journal % into % period %.', NEW.journal_id, v_period_status, NEW.period_id;
                  END IF;

                  IF NEW.posting_date < v_start_date OR NEW.posting_date > v_end_date THEN
                    RAISE EXCEPTION 'gl_posting_date_outside_period: Posting date % falls outside period %.', NEW.posting_date, NEW.period_id;
                  END IF;

                  SELECT COUNT(*)::integer,
                         COALESCE(SUM(l.debit_amount), 0::numeric(18,4)),
                         COALESCE(SUM(l.credit_amount), 0::numeric(18,4))
                  INTO v_line_count, v_sum_debit, v_sum_credit
                  FROM gl.journal_lines l
                  WHERE l.tenant_id = NEW.tenant_id
                    AND l.legal_entity_id = NEW.legal_entity_id
                    AND l.journal_id = NEW.journal_id;

                  IF v_line_count < 2 OR v_line_count <> NEW.line_count THEN
                    RAISE EXCEPTION 'gl_journal_line_count_mismatch: Journal % expected % lines, found %.', NEW.journal_id, NEW.line_count, v_line_count;
                  END IF;

                  IF v_sum_debit <= 0 OR v_sum_debit <> v_sum_credit OR v_sum_debit <> NEW.total_debit OR v_sum_credit <> NEW.total_credit THEN
                    RAISE EXCEPTION 'gl_journal_unbalanced: Journal % debit (%) credit (%) mismatch against header (% / %)', NEW.journal_id, v_sum_debit, v_sum_credit, NEW.total_debit, NEW.total_credit;
                  END IF;

                  RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER trg_enforce_journal_entry_integrity
                  AFTER INSERT ON gl.journal_entries
                  DEFERRABLE INITIALLY DEFERRED
                  FOR EACH ROW
                  EXECUTE FUNCTION gl.enforce_journal_entry_integrity();

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA gl, tax FROM PUBLIC;
                    GRANT USAGE ON SCHEMA gl, tax TO athera_app;

                    REVOKE ALL ON ALL TABLES IN SCHEMA gl, tax FROM PUBLIC, athera_app;

                    GRANT SELECT, INSERT, UPDATE ON TABLE
                      gl.accounts,
                      gl.financial_periods,
                      gl.account_mappings,
                      gl.journal_sequences,
                      tax.tax_codes
                    TO athera_app;

                    GRANT SELECT, INSERT ON TABLE
                      gl.journal_entries,
                      gl.journal_lines
                    TO athera_app;

                    CREATE POLICY p_gl_accounts_select ON gl.accounts FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_accounts_insert ON gl.accounts FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_accounts_update ON gl.accounts FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_periods_select ON gl.financial_periods FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_periods_insert ON gl.financial_periods FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_periods_update ON gl.financial_periods FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_mappings_select ON gl.account_mappings FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_mappings_insert ON gl.account_mappings FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_mappings_update ON gl.account_mappings FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_sequences_select ON gl.journal_sequences FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_sequences_insert ON gl.journal_sequences FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_sequences_update ON gl.journal_sequences FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_journals_select ON gl.journal_entries FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_journals_insert ON gl.journal_entries FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_gl_lines_select ON gl.journal_lines FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_gl_lines_insert ON gl.journal_lines FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_tax_codes_select ON tax.tax_codes FOR SELECT TO athera_app USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_tax_codes_insert ON tax.tax_codes FOR INSERT TO athera_app WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_tax_codes_update ON tax.tax_codes FOR UPDATE TO athera_app USING (tenant_id = platform.current_tenant_id()) WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_gl_accounts_migrator_all ON gl.accounts FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_periods_migrator_all ON gl.financial_periods FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_mappings_migrator_all ON gl.account_mappings FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_sequences_migrator_all ON gl.journal_sequences FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_journals_migrator_all ON gl.journal_entries FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_gl_lines_migrator_all ON gl.journal_lines FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_tax_codes_migrator_all ON tax.tax_codes FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_enforce_journal_entry_integrity ON gl.journal_entries;
                DROP FUNCTION IF EXISTS gl.enforce_journal_entry_integrity();
                """);

            migrationBuilder.DropTable(
                name: "account_mappings",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "journal_lines",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "journal_sequences",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "tax_codes",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "financial_periods",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "gl");

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS tax;
                DROP SCHEMA IF EXISTS gl;
                """);
        }
    }
}
