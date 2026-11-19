using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M17_DocumentsAndEvidenceTrails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "business_documents",
                schema: "documents",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    file_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    sha256_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scan_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_documents", x => x.document_id);
                    table.UniqueConstraint("AK_business_documents_tenant_id_legal_entity_id_document_id", x => new { x.tenant_id, x.legal_entity_id, x.document_id });
                    table.CheckConstraint("ck_business_documents_category", "category IN ('TAX_EVIDENCE', 'DELIVERY_PROOF', 'SUPPLIER_INVOICE', 'BANK_ADVICE', 'CONTRACT', 'GENERAL')");
                    table.CheckConstraint("ck_business_documents_size", "byte_size > 0 AND byte_size <= 5242880");
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                schema: "documents",
                columns: table => new
                {
                    sequence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => x.sequence_id);
                });

            migrationBuilder.CreateTable(
                name: "document_links",
                schema: "documents",
                columns: table => new
                {
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source_document_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    source_document_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_links", x => x.link_id);
                    table.ForeignKey(
                        name: "FK_document_links_business_documents_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.document_id },
                        principalSchema: "documents",
                        principalTable: "business_documents",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "document_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_documents_created",
                schema: "documents",
                table: "business_documents",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_business_documents_number",
                schema: "documents",
                table: "business_documents",
                columns: new[] { "tenant_id", "legal_entity_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_links_document",
                schema: "documents",
                table: "document_links",
                columns: new[] { "tenant_id", "legal_entity_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_links_source",
                schema: "documents",
                table: "document_links",
                columns: new[] { "tenant_id", "legal_entity_id", "source_document_type", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_sequences_scope_year",
                schema: "documents",
                table: "document_sequences",
                columns: new[] { "tenant_id", "legal_entity_id", "fiscal_year" },
                unique: true);

            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA documents TO athera_app;
                GRANT SELECT, INSERT, UPDATE ON TABLE documents.document_sequences TO athera_app;
                GRANT SELECT, INSERT ON TABLE documents.business_documents TO athera_app;
                GRANT SELECT, INSERT ON TABLE documents.document_links TO athera_app;

                ALTER TABLE documents.document_sequences ENABLE ROW LEVEL SECURITY;
                ALTER TABLE documents.document_sequences FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_document_sequences ON documents.document_sequences
                    USING (tenant_id = platform.current_tenant_id())
                    WITH CHECK (tenant_id = platform.current_tenant_id());

                ALTER TABLE documents.business_documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE documents.business_documents FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_business_documents ON documents.business_documents
                    USING (tenant_id = platform.current_tenant_id())
                    WITH CHECK (tenant_id = platform.current_tenant_id());

                ALTER TABLE documents.document_links ENABLE ROW LEVEL SECURITY;
                ALTER TABLE documents.document_links FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_document_links ON documents.document_links
                    USING (tenant_id = platform.current_tenant_id())
                    WITH CHECK (tenant_id = platform.current_tenant_id());

                CREATE OR REPLACE FUNCTION documents.guard_business_document_immutability()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'documents.business_documents is append-only and immutable (OD-12).';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_guard_business_document_immutability
                    BEFORE UPDATE OR DELETE ON documents.business_documents
                    FOR EACH ROW EXECUTE FUNCTION documents.guard_business_document_immutability();

                CREATE OR REPLACE FUNCTION documents.guard_document_link_immutability()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'documents.document_links is append-only and immutable (OD-12).';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_guard_document_link_immutability
                    BEFORE UPDATE OR DELETE ON documents.document_links
                    FOR EACH ROW EXECUTE FUNCTION documents.guard_document_link_immutability();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_document_link_immutability ON documents.document_links;
                DROP FUNCTION IF EXISTS documents.guard_document_link_immutability();
                DROP TRIGGER IF EXISTS trg_guard_business_document_immutability ON documents.business_documents;
                DROP FUNCTION IF EXISTS documents.guard_business_document_immutability();
                """);

            migrationBuilder.DropTable(
                name: "document_links",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_sequences",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "business_documents",
                schema: "documents");
        }
    }
}
