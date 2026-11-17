using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orphac.Athera.Modules.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M02_AdminApprovalsAuditAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entity_access_grants_tenant_id_legal_entity_id_user_id_perm~",
                schema: "identity",
                table: "entity_access_grants");

            migrationBuilder.EnsureSchema(
                name: "approval");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "approval_policies",
                schema: "approval",
                columns: table => new
                {
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    requires_four_eyes = table.Column<bool>(type: "boolean", nullable: false),
                    requires_approver_mfa = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_policies", x => x.policy_id);
                    table.UniqueConstraint("AK_approval_policies_tenant_id_legal_entity_id_policy_id", x => new { x.tenant_id, x.legal_entity_id, x.policy_id });
                    table.ForeignKey(
                        name: "FK_approval_policies_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "audit",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.event_id);
                    table.ForeignKey(
                        name: "FK_audit_events_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "command_idempotency",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_command_idempotency", x => x.id);
                    table.ForeignKey(
                        name: "FK_command_idempotency_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "integration",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.message_id);
                    table.UniqueConstraint("AK_outbox_messages_tenant_id_message_id", x => new { x.tenant_id, x.message_id });
                    table.CheckConstraint("ck_outbox_messages_status", "status IN ('PENDING', 'PROCESSED', 'FAILED')");
                    table.ForeignKey(
                        name: "FK_outbox_messages_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_invitations",
                schema: "identity",
                columns: table => new
                {
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role_template_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invitations", x => x.invitation_id);
                    table.CheckConstraint("ck_user_invitations_expiry", "expires_at_utc > created_at_utc");
                    table.CheckConstraint("ck_user_invitations_status", "status IN ('PENDING', 'ACCEPTED', 'REVOKED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "FK_user_invitations_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_invitations_tenant_memberships_tenant_id_invited_by_us~",
                        columns: x => new { x.tenant_id, x.invited_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_cases",
                schema: "approval",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_permission_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_cases", x => x.case_id);
                    table.UniqueConstraint("AK_approval_cases_tenant_id_legal_entity_id_case_id", x => new { x.tenant_id, x.legal_entity_id, x.case_id });
                    table.CheckConstraint("ck_approval_cases_no_self_decision", "decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id");
                    table.CheckConstraint("ck_approval_cases_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_approval_cases_approval_policies_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_id },
                        principalSchema: "approval",
                        principalTable: "approval_policies",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_cases_legal_entities_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "platform",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "legal_entity_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_cases_tenant_memberships_tenant_id_requested_by_us~",
                        columns: x => new { x.tenant_id, x.requested_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inbox_receipts",
                schema: "integration",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_receipts", x => x.receipt_id);
                    table.ForeignKey(
                        name: "FK_inbox_receipts_outbox_messages_tenant_id_message_id",
                        columns: x => new { x.tenant_id, x.message_id },
                        principalSchema: "integration",
                        principalTable: "outbox_messages",
                        principalColumns: new[] { "tenant_id", "message_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                schema: "approval",
                columns: table => new
                {
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approver_mfa_verified = table.Column<bool>(type: "boolean", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_decisions", x => x.decision_id);
                    table.CheckConstraint("ck_approval_decisions_decision", "decision IN ('APPROVED', 'REJECTED')");
                    table.CheckConstraint("ck_approval_decisions_four_eyes", "decided_by_user_id <> requested_by_user_id");
                    table.ForeignKey(
                        name: "FK_approval_decisions_approval_cases_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.case_id },
                        principalSchema: "approval",
                        principalTable: "approval_cases",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "case_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_decisions_tenant_memberships_tenant_id_decided_by_~",
                        columns: x => new { x.tenant_id, x.decided_by_user_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_access_grants_tenant_id_legal_entity_id_user_id_perm~",
                schema: "identity",
                table: "entity_access_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "user_id", "permission_code" },
                unique: true,
                filter: "status <> 'REVOKED'");

            migrationBuilder.CreateIndex(
                name: "IX_approval_cases_tenant_id_legal_entity_id_policy_id",
                schema: "approval",
                table: "approval_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_cases_tenant_id_legal_entity_id_status_requested_a~",
                schema: "approval",
                table: "approval_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "status", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_cases_tenant_id_requested_by_user_id",
                schema: "approval",
                table: "approval_cases",
                columns: new[] { "tenant_id", "requested_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_decisions_tenant_id_decided_by_user_id",
                schema: "approval",
                table: "approval_decisions",
                columns: new[] { "tenant_id", "decided_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_decisions_tenant_id_legal_entity_id_case_id",
                schema: "approval",
                table: "approval_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_policies_tenant_id_legal_entity_id_policy_code",
                schema: "approval",
                table: "approval_policies",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_tenant_id_legal_entity_id_occurred_at_utc",
                schema: "audit",
                table: "audit_events",
                columns: new[] { "tenant_id", "legal_entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_command_idempotency_tenant_id_legal_entity_id_operation_cod~",
                schema: "integration",
                table: "command_idempotency",
                columns: new[] { "tenant_id", "legal_entity_id", "operation_code", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_receipts_tenant_id_consumer_name_message_id",
                schema: "integration",
                table: "inbox_receipts",
                columns: new[] { "tenant_id", "consumer_name", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_receipts_tenant_id_message_id",
                schema: "integration",
                table: "inbox_receipts",
                columns: new[] { "tenant_id", "message_id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id_legal_entity_id",
                schema: "integration",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "legal_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id_status_next_attempt_at_utc",
                schema: "integration",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id_topic_source_key",
                schema: "integration",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "topic", "source_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_tenant_id_invited_by_user_id",
                schema: "identity",
                table: "user_invitations",
                columns: new[] { "tenant_id", "invited_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_tenant_id_legal_entity_id_invited_email_st~",
                schema: "identity",
                table: "user_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "invited_email", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_token_hash",
                schema: "identity",
                table: "user_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION platform.current_invitation_token_hash()
                RETURNS text
                LANGUAGE sql
                STABLE
                SECURITY INVOKER
                SET search_path = pg_catalog
                AS $$
                  SELECT NULLIF(current_setting('app.invitation_token_hash', true), '');
                $$;

                ALTER TABLE identity.user_invitations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE identity.user_invitations FORCE ROW LEVEL SECURITY;

                ALTER TABLE approval.approval_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval.approval_policies FORCE ROW LEVEL SECURITY;

                ALTER TABLE approval.approval_cases ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval.approval_cases FORCE ROW LEVEL SECURITY;

                ALTER TABLE approval.approval_decisions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE approval.approval_decisions FORCE ROW LEVEL SECURITY;

                ALTER TABLE audit.audit_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE audit.audit_events FORCE ROW LEVEL SECURITY;

                ALTER TABLE integration.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE integration.outbox_messages FORCE ROW LEVEL SECURITY;

                ALTER TABLE integration.inbox_receipts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE integration.inbox_receipts FORCE ROW LEVEL SECURITY;

                ALTER TABLE integration.command_idempotency ENABLE ROW LEVEL SECURITY;
                ALTER TABLE integration.command_idempotency FORCE ROW LEVEL SECURITY;

                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                    REVOKE ALL ON SCHEMA approval, audit, integration FROM PUBLIC;
                    GRANT USAGE ON SCHEMA approval, audit, integration TO athera_app;
                    GRANT EXECUTE ON FUNCTION platform.current_invitation_token_hash() TO athera_app;

                    REVOKE ALL ON TABLE audit.audit_events, approval.approval_decisions FROM PUBLIC, athera_app;
                    GRANT SELECT, INSERT ON TABLE audit.audit_events, approval.approval_decisions TO athera_app;

                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                      identity.user_invitations,
                      approval.approval_policies,
                      approval.approval_cases,
                      integration.outbox_messages,
                      integration.inbox_receipts,
                      integration.command_idempotency
                    TO athera_app;

                    CREATE POLICY p_user_invitations_select
                      ON identity.user_invitations FOR SELECT TO athera_app
                      USING (
                        tenant_id = platform.current_tenant_id()
                        OR token_hash = platform.current_invitation_token_hash()
                      );
                    CREATE POLICY p_user_invitations_insert
                      ON identity.user_invitations FOR INSERT TO athera_app
                      WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_user_invitations_update
                      ON identity.user_invitations FOR UPDATE TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_user_invitations_delete
                      ON identity.user_invitations FOR DELETE TO athera_app
                      USING (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_approval_policies_tenant_isolation
                      ON approval.approval_policies FOR ALL TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_approval_cases_tenant_isolation
                      ON approval.approval_cases FOR ALL TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_approval_decisions_select
                      ON approval.approval_decisions FOR SELECT TO athera_app
                      USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_approval_decisions_insert
                      ON approval.approval_decisions FOR INSERT TO athera_app
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_audit_events_select
                      ON audit.audit_events FOR SELECT TO athera_app
                      USING (tenant_id = platform.current_tenant_id());
                    CREATE POLICY p_audit_events_insert
                      ON audit.audit_events FOR INSERT TO athera_app
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_outbox_messages_tenant_isolation
                      ON integration.outbox_messages FOR ALL TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_inbox_receipts_tenant_isolation
                      ON integration.inbox_receipts FOR ALL TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());

                    CREATE POLICY p_command_idempotency_tenant_isolation
                      ON integration.command_idempotency FOR ALL TO athera_app
                      USING (tenant_id = platform.current_tenant_id())
                      WITH CHECK (tenant_id = platform.current_tenant_id());
                  END IF;

                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                    CREATE POLICY p_user_invitations_migrator_all ON identity.user_invitations FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_approval_policies_migrator_all ON approval.approval_policies FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_approval_cases_migrator_all ON approval.approval_cases FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_approval_decisions_migrator_all ON approval.approval_decisions FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_audit_events_migrator_all ON audit.audit_events FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_outbox_messages_migrator_all ON integration.outbox_messages FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_inbox_receipts_migrator_all ON integration.inbox_receipts FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                    CREATE POLICY p_command_idempotency_migrator_all ON integration.command_idempotency FOR ALL TO athera_migrator USING (true) WITH CHECK (true);
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_decisions",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "command_idempotency",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "inbox_receipts",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "user_invitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "approval_cases",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "approval_policies",
                schema: "approval");

            migrationBuilder.DropIndex(
                name: "IX_entity_access_grants_tenant_id_legal_entity_id_user_id_perm~",
                schema: "identity",
                table: "entity_access_grants");

            migrationBuilder.CreateIndex(
                name: "IX_entity_access_grants_tenant_id_legal_entity_id_user_id_perm~",
                schema: "identity",
                table: "entity_access_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "user_id", "permission_code" },
                unique: true);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS platform.current_invitation_token_hash();
                DROP SCHEMA IF EXISTS approval;
                DROP SCHEMA IF EXISTS audit;
                DROP SCHEMA IF EXISTS integration;
                """);
        }
    }
}
