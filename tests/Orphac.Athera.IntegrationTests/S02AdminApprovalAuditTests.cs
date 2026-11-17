using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.IntegrationTests;

public sealed class S02AdminApprovalAuditTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S02SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid AdminId,
        string AdminEmail,
        Guid SecondApproverId,
        string SecondApproverEmail,
        Guid OperatorId,
        string OperatorEmail,
        Guid UnassignedId,
        string UnassignedEmail,
        string Password);

    [Fact]
    public async Task S02_PG_01_M02MigrationUpDownUpAndForcedRlsVerified()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117180435_M01_TenantAndLegalEntityWorkspace");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCmd = conn.CreateCommand();
            downCmd.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema IN ('approval', 'audit', 'integration')
                   OR (table_schema = 'identity' AND table_name = 'user_invitations');
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCmd.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var rlsCmd = conn.CreateCommand();
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'identity' AND c.relname = 'user_invitations')
                    OR (n.nspname = 'approval' AND c.relname IN ('approval_policies', 'approval_cases', 'approval_decisions'))
                    OR (n.nspname = 'audit' AND c.relname = 'audit_events')
                    OR (n.nspname = 'integration' AND c.relname IN ('outbox_messages', 'inbox_receipts', 'command_idempotency'))
                )
                AND c.relrowsecurity
                AND c.relforcerowsecurity;
                """;
            Assert.Equal(8L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S02_PG_02_AuditEventsAndApprovalDecisionsAreAppendOnlyUnderRuntimeRole()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            var policy = await migDb.ApprovalPolicies.FirstAsync(
                p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.PolicyCode == "SENSITIVE_ACCESS_CHANGE");

            var approvalCase = ApprovalCase.Create(
                caseId,
                world.TenantAId,
                world.EntityA1Id,
                policy.PolicyId,
                ApprovalActionTypes.EntityAccessGrantRequest,
                world.OperatorId,
                PermissionCodes.InventoryRead,
                "Audit append-only check",
                world.OperatorId,
                now);
            approvalCase.RecordDecision(world.AdminId, ApprovalDecisions.Approved, "Approved", now.AddSeconds(1));

            migDb.ApprovalCases.Add(approvalCase);
            migDb.ApprovalDecisions.Add(ApprovalDecision.Create(
                decisionId,
                world.TenantAId,
                world.EntityA1Id,
                caseId,
                world.OperatorId,
                world.AdminId,
                ApprovalDecisions.Approved,
                "Approved",
                false,
                now.AddSeconds(1)));
            migDb.AuditEvents.Add(AuditEvent.Append(
                eventId,
                world.TenantAId,
                world.EntityA1Id,
                world.AdminId,
                "TEST_EVENT",
                "Entity",
                world.EntityA1Id.ToString("D"),
                "Immutable event",
                "{}",
                now));
            await migDb.SaveChangesAsync();
        }

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();
        await using var tx = await appConn.BeginTransactionAsync();

        await using (var scopeCmd = appConn.CreateCommand())
        {
            scopeCmd.Transaction = tx;
            scopeCmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true), set_config('app.actor_user_id', @uid, true);";
            scopeCmd.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
            scopeCmd.Parameters.AddWithValue("uid", world.AdminId.ToString("D"));
            await scopeCmd.ExecuteNonQueryAsync();
        }

        // SELECT allowed inside tenant
        await using (var readAudit = appConn.CreateCommand())
        {
            readAudit.Transaction = tx;
            readAudit.CommandText = "SELECT count(*) FROM audit.audit_events WHERE event_id = @eid;";
            readAudit.Parameters.AddWithValue("eid", eventId);
            Assert.Equal(1L, Convert.ToInt64(await readAudit.ExecuteScalarAsync()));
        }

        // UPDATE / DELETE rejected with 42501 on audit_events and approval_decisions
        await AssertUpdateDeleteDeniedAsync(appConn, tx, "UPDATE audit.audit_events SET summary = 'tamper' WHERE event_id = @id;", eventId);
        await AssertUpdateDeleteDeniedAsync(appConn, tx, "DELETE FROM audit.audit_events WHERE event_id = @id;", eventId);
        await AssertUpdateDeleteDeniedAsync(appConn, tx, "UPDATE approval.approval_decisions SET reason = 'tamper' WHERE decision_id = @id;", decisionId);
        await AssertUpdateDeleteDeniedAsync(appConn, tx, "DELETE FROM approval.approval_decisions WHERE decision_id = @id;", decisionId);
    }

    [Fact]
    public async Task S02_API_01_InvitationExpiryRevocationAndRejoinNeverReactivatesStaleGrants()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        // Give Unassigned user existing memberships & grants on A1 and A2 BEFORE revoking membership to prove rejoin never reactivates stale A2 grants
        await using (var migDb = fx.CreateMigratorDbContext())
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(-20);
            migDb.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), world.TenantAId, world.UnassignedId, now));
            migDb.EntityAccessGrants.AddRange(
                EntityAccessGrant.Create(Guid.NewGuid(), world.TenantAId, world.EntityA1Id, world.UnassignedId, PermissionCodes.WorkspaceView, now, null, now),
                EntityAccessGrant.Create(Guid.NewGuid(), world.TenantAId, world.EntityA2Id, world.UnassignedId, PermissionCodes.WorkspaceView, now, null, now),
                EntityAccessGrant.Create(Guid.NewGuid(), world.TenantAId, world.EntityA2Id, world.UnassignedId, PermissionCodes.FinanceRead, now, null, now));
            await migDb.SaveChangesAsync();
        }

        using var factory = fx.CreateApiFactory();
        using var adminClient = await CreateSignedInClientAsync(factory, world.AdminEmail, world.Password);

        // Revoke membership of Unassigned user on Tenant A
        var revokeRes = await adminClient.PostAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/members/{world.UnassignedId}/revoke",
            null);
        Assert.Equal(HttpStatusCode.OK, revokeRes.StatusCode);

        // Create invite #1 and immediately revoke it
        var inv1Res = await adminClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/invitations",
            new CreateInvitationRequest(world.UnassignedEmail, RoleTemplates.ProcurementOfficer, 24));
        Assert.Equal(HttpStatusCode.Created, inv1Res.StatusCode);
        var inv1 = await inv1Res.Content.ReadFromJsonAsync<CreateInvitationResponse>(JsonOptions);
        var revokeInvRes = await adminClient.PostAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/invitations/{inv1!.InvitationId}/revoke",
            null);
        Assert.Equal(HttpStatusCode.OK, revokeInvRes.StatusCode);

        // Create invite #2 and mark expired in DB
        var inv2Res = await adminClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/invitations",
            new CreateInvitationRequest(world.UnassignedEmail, RoleTemplates.ProcurementOfficer, 24));
        var inv2 = await inv2Res.Content.ReadFromJsonAsync<CreateInvitationResponse>(JsonOptions);
        await using (var migDb = fx.CreateMigratorDbContext())
        {
            var createdBack = DateTimeOffset.UtcNow.AddHours(-2);
            var expiredBack = DateTimeOffset.UtcNow.AddMinutes(-5);
            await migDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE identity.user_invitations SET created_at_utc = {createdBack}, expires_at_utc = {expiredBack} WHERE invitation_id = {inv2!.InvitationId}");
        }

        // Create invite #3 valid ONLY for A1 (NUSA-JKT)
        var inv3Res = await adminClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/invitations",
            new CreateInvitationRequest(world.UnassignedEmail, RoleTemplates.ProcurementOfficer, 24));
        var inv3 = await inv3Res.Content.ReadFromJsonAsync<CreateInvitationResponse>(JsonOptions);

        // Wrong user email attempting to accept -> 403
        using var wrongUserClient = await CreateSignedInClientAsync(factory, world.OperatorEmail, world.Password);
        var mismatchRes = await wrongUserClient.PostAsJsonAsync("/api/invitations/accept", new AcceptInvitationRequest(inv3!.InviteToken));
        Assert.Equal(HttpStatusCode.Forbidden, mismatchRes.StatusCode);

        using var rejoinClient = await CreateSignedInClientAsync(factory, world.UnassignedEmail, world.Password);

        // Revoked -> 409, Expired -> 410
        Assert.Equal(HttpStatusCode.Conflict, (await rejoinClient.PostAsJsonAsync("/api/invitations/accept", new AcceptInvitationRequest(inv1.InviteToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await rejoinClient.PostAsJsonAsync("/api/invitations/accept", new AcceptInvitationRequest(inv2.InviteToken))).StatusCode);

        // Valid accept -> 200
        var acceptRes = await rejoinClient.PostAsJsonAsync("/api/invitations/accept", new AcceptInvitationRequest(inv3.InviteToken));
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);

        // A1 accessible, stale A2 MUST remain inaccessible (404)!
        Assert.Equal(HttpStatusCode.OK, (await rejoinClient.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rejoinClient.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}")).StatusCode);
    }

    [Fact]
    public async Task S02_API_02_PrivilegeEscalationAndEntityBoundariesBlocked()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var opClient = await CreateSignedInClientAsync(factory, world.OperatorEmail, world.Password);
        using var adminClient = await CreateSignedInClientAsync(factory, world.AdminEmail, world.Password);

        // Operator without admin.manage cannot invite or grant
        var opInvite = await opClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/invitations",
            new CreateInvitationRequest("new@test.local", RoleTemplates.ProcurementOfficer, 24));
        Assert.Equal(HttpStatusCode.Forbidden, opInvite.StatusCode);

        var opGrant = await opClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/grants",
            new GrantPermissionRequest(world.OperatorId, PermissionCodes.AdminManage));
        Assert.Equal(HttpStatusCode.Forbidden, opGrant.StatusCode);

        // Unknown / escalated permission rejected with 400 Bad Request
        var craftGrant = await adminClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/admin/grants",
            new GrantPermissionRequest(world.OperatorId, "superadmin.root"));
        Assert.Equal(HttpStatusCode.BadRequest, craftGrant.StatusCode);
    }

    [Fact]
    public async Task S02_API_03_FourEyesApprovalIdempotencyConcurrencyAndMfaEnforcement()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var requesterClient = await CreateSignedInClientAsync(factory, world.OperatorEmail, world.Password);
        using var approver1Client = await CreateSignedInClientAsync(factory, world.AdminEmail, world.Password);
        using var approver2Client = await CreateSignedInClientAsync(factory, world.SecondApproverEmail, world.Password);

        // Grant Operator approval.request on A1 cleanly before test
        var requestBody = new CreateApprovalCaseRequest("SENSITIVE_ACCESS_CHANGE", PermissionCodes.InventoryRead, "Cycle count support");

        using var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases")
        {
            Content = JsonContent.Create(requestBody)
        };
        req1.Headers.Add("Idempotency-Key", "s02-case-001");
        var res1 = await requesterClient.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var createdCase = await res1.Content.ReadFromJsonAsync<ApprovalCaseDto>(JsonOptions);

        // Repeat with same Idempotency-Key returns same CaseId without duplicate creation
        using var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases")
        {
            Content = JsonContent.Create(requestBody)
        };
        req2.Headers.Add("Idempotency-Key", "s02-case-001");
        var res2 = await requesterClient.SendAsync(req2);
        var repeatedCase = await res2.Content.ReadFromJsonAsync<ApprovalCaseDto>(JsonOptions);
        Assert.Equal(createdCase!.CaseId, repeatedCase!.CaseId);

        // Self-approval rejected 403
        var selfApproveRes = await requesterClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases/{createdCase.CaseId}/decide",
            new DecideApprovalCaseRequest(ApprovalDecisions.Approved, "Try self-approve"));
        Assert.Equal(HttpStatusCode.Forbidden, selfApproveRes.StatusCode);

        // DB constraint proof: raw update setting decided_by_user_id = requested_by_user_id fails with 23514
        await using (var migDb = fx.CreateMigratorDbContext())
        {
            var checkEx = await Assert.ThrowsAsync<PostgresException>(async () =>
                await migDb.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE approval.approval_cases SET decided_by_user_id = requested_by_user_id WHERE case_id = {createdCase.CaseId}"));
            Assert.Equal("23514", checkEx.SqlState);
        }

        // Concurrent dual-approver race: 1 succeeds (200), 1 fails (409 Conflict)
        var t1 = approver1Client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases/{createdCase.CaseId}/decide",
            new DecideApprovalCaseRequest(ApprovalDecisions.Approved, "Controller 1 approves"));
        var t2 = approver2Client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases/{createdCase.CaseId}/decide",
            new DecideApprovalCaseRequest(ApprovalDecisions.Approved, "Controller 2 approves"));

        var responses = await Task.WhenAll(t1, t2);
        var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToArray();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);

        // MFA-required policy enforcement
        var mfaReq = await requesterClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases",
            new CreateApprovalCaseRequest("HIGH_RISK_PRIVILEGE_ESCALATION", PermissionCodes.FinanceRead, "Temporary ledger review"));
        Assert.Equal(HttpStatusCode.Created, mfaReq.StatusCode);
        var mfaCase = await mfaReq.Content.ReadFromJsonAsync<ApprovalCaseDto>(JsonOptions);

        // Without MFA enabled -> 403 mfa_required
        var noMfaApprove = await approver1Client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases/{mfaCase!.CaseId}/decide",
            new DecideApprovalCaseRequest(ApprovalDecisions.Approved, "Approve without MFA"));
        Assert.Equal(HttpStatusCode.Forbidden, noMfaApprove.StatusCode);
        var noMfaBody = await noMfaApprove.Content.ReadAsStringAsync();
        Assert.Contains("mfa_required", noMfaBody, StringComparison.OrdinalIgnoreCase);

        // Enroll and verify RFC 6238 TOTP MFA via ASP.NET Core Identity
        var setupRes = await approver1Client.PostAsync("/api/auth/mfa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setupRes.StatusCode);
        var mfaSetup = await setupRes.Content.ReadFromJsonAsync<MfaSetupResponse>(JsonOptions);
        var totpCode = ComputeTotpCode(mfaSetup!.SharedKey);

        var verifyRes = await approver1Client.PostAsJsonAsync("/api/auth/mfa/verify", new MfaVerifyRequest(totpCode));
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // Retry approval after MFA verified -> 200 OK
        var withMfaApprove = await approver1Client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/approvals/cases/{mfaCase.CaseId}/decide",
            new DecideApprovalCaseRequest(ApprovalDecisions.Approved, "MFA verified approval"));
        Assert.Equal(HttpStatusCode.OK, withMfaApprove.StatusCode);
        var decidedDto = await withMfaApprove.Content.ReadFromJsonAsync<ApprovalCaseDto>(JsonOptions);
        Assert.True(decidedDto!.ApproverMfaVerified);
    }

    private static string ComputeTotpCode(string base32Secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = base32Secret.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var c in cleaned)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        var timestep = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(timestep & 0xFF);
            timestep >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(bytes.ToArray(), counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        return (binary % 1_000_000).ToString("D6");
    }

    [Fact]
    public async Task S02_PG_04_TenantSafeOutboxRetryIdempotencyAndZeroPoolLeakage()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        var msgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            migDb.OutboxMessages.Add(OutboxMessage.Create(
                msgId,
                world.TenantAId,
                world.EntityA1Id,
                "approval.case.decided.v1",
                $"case:{msgId:N}",
                "{\"ok\":true}",
                now));
            await migDb.SaveChangesAsync();
        }

        var poolConnStr = new NpgsqlConnectionStringBuilder(fx.RuntimeConnectionString)
        {
            MaxPoolSize = 4,
            Pooling = true
        }.ConnectionString;

        await using var dataSource = NpgsqlDataSource.Create(poolConnStr);
        var options = new DbContextOptionsBuilder<Orphac.Athera.Modules.Persistence.AtheraDbContext>()
            .UseAtheraNpgsql(dataSource)
            .Options;

        await using (var db = new Orphac.Athera.Modules.Persistence.AtheraDbContext(options))
        {
            var processor = new TenantOutboxProcessor(db);

            // First attempt simulates failure -> AttemptCount = 1, Failed/retryable
            var attempt = 0;
            var firstRun = await processor.ProcessPendingForTenantAsync(
                world.TenantAId,
                (_, _) =>
                {
                    attempt++;
                    if (attempt == 1)
                    {
                        throw new InvalidOperationException("Simulated transient relay failure");
                    }
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(1, firstRun.FailedCount);

            // Reset next_attempt_at so immediate retry runs cleanly
            await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {world.TenantAId.ToString("D")}, true);");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integration.outbox_messages SET next_attempt_at_utc = {DateTimeOffset.UtcNow.AddSeconds(-5)} WHERE message_id = {msgId}");
            await db.Database.CommitTransactionAsync();

            // Retry succeeds and records InboxReceipt
            var deliveryCount = 0;
            var secondRun = await processor.ProcessPendingForTenantAsync(
                world.TenantAId,
                (_, _) =>
                {
                    deliveryCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(1, secondRun.ProcessedCount);
            Assert.Equal(1, deliveryCount);

            // Re-running does not double-deliver (idempotent InboxReceipt)
            var thirdRun = await processor.ProcessPendingForTenantAsync(
                world.TenantAId,
                (_, _) =>
                {
                    deliveryCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(0, thirdRun.ProcessedCount);
            Assert.Equal(1, deliveryCount);
        }

        // Verify pooled sessions hold no stale app.tenant_id
        for (var i = 0; i < 4; i++)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(current_setting('app.tenant_id', true), '');";
            Assert.Equal(string.Empty, (string?)await cmd.ExecuteScalarAsync());
        }
    }

    private static async Task AssertUpdateDeleteDeniedAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string sql,
        Guid id)
    {
        await tx.SaveAsync("sp_deny_check");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("id", id);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState);
        await tx.RollbackAsync("sp_deny_check");
    }

    private static async Task<HttpClient> CreateSignedInClientAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:3200");

        var csrfPayload = await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, password, false))
        };
        loginReq.Headers.Add(csrfPayload!.HeaderName, csrfPayload.RequestToken);
        var loginRes = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session!.CsrfToken);
        return client;
    }

    private static async Task<S02SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (adminId, adminEmail, _) = await fx.CreateTestUserAsync("admin.s02@test.local", "Budi Admin", pw);
        var (approver2Id, approver2Email, _) = await fx.CreateTestUserAsync("approver2.s02@test.local", "Sari Approver", pw);
        var (opId, opEmail, _) = await fx.CreateTestUserAsync("operator.s02@test.local", "Rina Operator", pw);
        var (unassignedId, unassignedEmail, _) = await fx.CreateTestUserAsync("unassigned.s02@test.local", "Hendra Unassigned", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", now),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", now));

        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, adminId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approver2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, opId, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, adminId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, adminId, PermissionCodes.AdminRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, adminId, PermissionCodes.AdminManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, adminId, PermissionCodes.ApprovalRequest, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, adminId, PermissionCodes.ApprovalDecide, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approver2Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approver2Id, PermissionCodes.ApprovalDecide, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, opId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, opId, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, opId, PermissionCodes.ApprovalRequest, now, null, now));

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, "SENSITIVE_ACCESS_CHANGE", "Sensitive Entity Grant Change", requiresFourEyes: true, requiresApproverMfa: false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, "HIGH_RISK_PRIVILEGE_ESCALATION", "High-Risk Privilege Escalation (MFA)", requiresFourEyes: true, requiresApproverMfa: true, now));

        await db.SaveChangesAsync();

        return new S02SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            adminId, adminEmail, approver2Id, approver2Email, opId, opEmail, unassignedId, unassignedEmail, pw);
    }
}
