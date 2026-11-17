using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.IntegrationTests;

public sealed class S01TenantIsolationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset SeedNow = DateTimeOffset.UtcNow.AddMinutes(-10);

    private sealed record SeededTenancyWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid U1Id,
        string U1Email,
        Guid U2Id,
        string U2Email,
        Guid U3Id,
        string U3Email,
        string Password);

    [Fact]
    public async Task S01_PG_01_M01MigrationUpDownAndReapplySucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117173251_M00_InitialIdentityFoundation");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var checkDown = conn.CreateCommand();
            checkDown.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE (table_schema = 'platform')
                   OR (table_schema = 'identity' AND table_name IN ('tenant_memberships', 'entity_access_grants'));
                """;
            Assert.Equal(0L, Convert.ToInt64(await checkDown.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var checkUp = conn.CreateCommand();
            checkUp.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE (table_schema = 'platform' AND table_name IN ('tenants', 'legal_entities'))
                   OR (table_schema = 'identity' AND table_name IN ('tenant_memberships', 'entity_access_grants'));
                """;
            Assert.Equal(4L, Convert.ToInt64(await checkUp.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S01_PG_02_RuntimeRolePrivilegesAndForcedRlsVerified()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var roleCmd = conn.CreateCommand())
        {
            roleCmd.CommandText = """
                SELECT rolsuper, rolbypassrls, rolcreatedb, rolcreaterole
                FROM pg_roles WHERE rolname = current_user;
                """;
            await using var reader = await roleCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
        }

        await using (var ownerCmd = conn.CreateCommand())
        {
            ownerCmd.CommandText = """
                SELECT count(*) FROM pg_tables
                WHERE schemaname IN ('identity', 'platform') AND tableowner = current_user;
                """;
            Assert.Equal(0L, Convert.ToInt64(await ownerCmd.ExecuteScalarAsync()));
        }

        await using (var privCmd = conn.CreateCommand())
        {
            privCmd.CommandText = """
                SELECT
                    has_table_privilege('athera_app', 'platform.tenants', 'SELECT'),
                    has_table_privilege('athera_app', 'platform.tenants', 'INSERT'),
                    has_table_privilege('athera_app', 'platform.tenants', 'UPDATE'),
                    has_table_privilege('athera_app', 'platform.tenants', 'DELETE');
                """;
            await using var reader = await privCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
        }

        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (n.nspname = 'platform' AND c.relname IN ('tenants', 'legal_entities'))
                   OR (n.nspname = 'identity' AND c.relname IN ('tenant_memberships', 'entity_access_grants'))
                HAVING bool_and(c.relrowsecurity AND c.relforcerowsecurity);
                """;
            var count = Convert.ToInt64(await rlsCmd.ExecuteScalarAsync());
            Assert.Equal(4L, count);
        }
    }

    [Fact]
    public async Task S01_PG_03_MissingTenantContextDeniesReadsAndRejectsWrites()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        foreach (var table in new[]
        {
            "platform.tenants",
            "platform.legal_entities",
            "identity.tenant_memberships",
            "identity.entity_access_grants"
        })
        {
            await using var cmd = appConn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM {table};";
            Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
        }

        await using var insertCmd = appConn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO platform.legal_entities
                (legal_entity_id, tenant_id, code, name, functional_currency_code, country_code, time_zone_id, status, created_at_utc, updated_at_utc, version)
            VALUES
                (@eid, @tid, 'UNAUTH', 'Unauthorized Insert', 'IDR', 'ID', 'Asia/Jakarta', 'ACTIVE', now(), now(), 1);
            """;
        insertCmd.Parameters.AddWithValue("eid", Guid.NewGuid());
        insertCmd.Parameters.AddWithValue("tid", world.TenantAId);

        var pgEx = await Assert.ThrowsAsync<PostgresException>(() => insertCmd.ExecuteNonQueryAsync());
        Assert.Equal("42501", pgEx.SqlState);
    }

    [Fact]
    public async Task S01_PG_04_TenantScopedAppConnectionBlocksCrossTenantReadsAndWrites()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();
        await using var tx = await appConn.BeginTransactionAsync();

        await using (var setCmd = appConn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true), set_config('app.actor_user_id', @uid, true);";
            setCmd.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
            setCmd.Parameters.AddWithValue("uid", world.U2Id.ToString("D"));
            await setCmd.ExecuteNonQueryAsync();
        }

        await using (var readB = appConn.CreateCommand())
        {
            readB.Transaction = tx;
            readB.CommandText = "SELECT count(*) FROM platform.legal_entities WHERE tenant_id = @tb;";
            readB.Parameters.AddWithValue("tb", world.TenantBId);
            Assert.Equal(0L, Convert.ToInt64(await readB.ExecuteScalarAsync()));
        }

        await using (var updateB = appConn.CreateCommand())
        {
            updateB.Transaction = tx;
            updateB.CommandText = "UPDATE platform.legal_entities SET name = 'Tampered' WHERE legal_entity_id = @eb;";
            updateB.Parameters.AddWithValue("eb", world.EntityB1Id);
            Assert.Equal(0, await updateB.ExecuteNonQueryAsync());
        }

        await using (var deleteB = appConn.CreateCommand())
        {
            deleteB.Transaction = tx;
            deleteB.CommandText = "DELETE FROM platform.legal_entities WHERE legal_entity_id = @eb;";
            deleteB.Parameters.AddWithValue("eb", world.EntityB1Id);
            Assert.Equal(0, await deleteB.ExecuteNonQueryAsync());
        }

        await using (var insertB = appConn.CreateCommand())
        {
            insertB.Transaction = tx;
            insertB.CommandText = """
                INSERT INTO platform.legal_entities
                    (legal_entity_id, tenant_id, code, name, functional_currency_code, country_code, time_zone_id, status, created_at_utc, updated_at_utc, version)
                VALUES
                    (@eid, @tb, 'SMDR-ROGUE', 'Rogue Entity', 'IDR', 'ID', 'Asia/Jakarta', 'ACTIVE', now(), now(), 1);
                """;
            insertB.Parameters.AddWithValue("eid", Guid.NewGuid());
            insertB.Parameters.AddWithValue("tb", world.TenantBId);
            var pgEx = await Assert.ThrowsAsync<PostgresException>(() => insertB.ExecuteNonQueryAsync());
            Assert.Equal("42501", pgEx.SqlState);
        }
    }

    [Fact]
    public async Task S01_PG_05_CompositeForeignKeysBlockCrossTenantLinksEvenUnderMigrator()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var migratorConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migratorConn.OpenAsync();

        // Cross-tenant (TenantA, EntityB1) FK violation
        await using (var crossEntityCmd = migratorConn.CreateCommand())
        {
            crossEntityCmd.CommandText = """
                INSERT INTO identity.entity_access_grants
                    (grant_id, tenant_id, legal_entity_id, user_id, permission_code, status, valid_from_utc, created_at_utc, updated_at_utc, version)
                VALUES
                    (@gid, @ta, @eb1, @u1, 'workspace.view', 'ACTIVE', now(), now(), now(), 1);
                """;
            crossEntityCmd.Parameters.AddWithValue("gid", Guid.NewGuid());
            crossEntityCmd.Parameters.AddWithValue("ta", world.TenantAId);
            crossEntityCmd.Parameters.AddWithValue("eb1", world.EntityB1Id);
            crossEntityCmd.Parameters.AddWithValue("u1", world.U1Id);

            var pgEx = await Assert.ThrowsAsync<PostgresException>(() => crossEntityCmd.ExecuteNonQueryAsync());
            Assert.Equal("23503", pgEx.SqlState);
        }

        // User without TenantMembership in TenantB cannot receive EntityAccessGrant in (TenantB, EntityB1)
        await using (var missingMembershipCmd = migratorConn.CreateCommand())
        {
            missingMembershipCmd.CommandText = """
                INSERT INTO identity.entity_access_grants
                    (grant_id, tenant_id, legal_entity_id, user_id, permission_code, status, valid_from_utc, created_at_utc, updated_at_utc, version)
                VALUES
                    (@gid, @tb, @eb1, @u1, 'workspace.view', 'ACTIVE', now(), now(), now(), 1);
                """;
            missingMembershipCmd.Parameters.AddWithValue("gid", Guid.NewGuid());
            missingMembershipCmd.Parameters.AddWithValue("tb", world.TenantBId);
            missingMembershipCmd.Parameters.AddWithValue("eb1", world.EntityB1Id);
            missingMembershipCmd.Parameters.AddWithValue("u1", world.U1Id);

            var pgEx = await Assert.ThrowsAsync<PostgresException>(() => missingMembershipCmd.ExecuteNonQueryAsync());
            Assert.Equal("23503", pgEx.SqlState);
        }
    }

    [Fact]
    public async Task S01_API_01_02_03_AuthorizedWorkspacesHostileProbesSpoofedHeadersAndMultiTenantSwitch()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();

        // Anonymous request rejected
        using var anonClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/workspaces")).StatusCode);

        // U1: single tenant, single entity (NUSA-JKT only)
        using var u1Client = await CreateSignedInClientAsync(factory, world.U1Email, world.Password);

        var u1Me = await (await u1Client.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        Assert.NotNull(u1Me);
        Assert.Equal(1, u1Me.AuthorizedWorkspaceCount);
        Assert.Equal("ACTIVE_WORKSPACES", u1Me.WorkspaceAccessStatus);

        var u1WorkspacesRes = await u1Client.GetAsync("/api/workspaces");
        Assert.Equal(HttpStatusCode.OK, u1WorkspacesRes.StatusCode);
        var u1Workspaces = await u1WorkspacesRes.Content.ReadFromJsonAsync<WorkspacesResponse>(JsonOptions);
        Assert.NotNull(u1Workspaces);
        var onlyTenant = Assert.Single(u1Workspaces.Tenants);
        Assert.Equal("NUSA", onlyTenant.Code);
        var onlyEntity = Assert.Single(onlyTenant.Entities);
        Assert.Equal("NUSA-JKT", onlyEntity.Code);

        // Authorized entity with spoofed X-Tenant-Id header -> header ignored, NUSA-JKT returned
        using var spoofReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}");
        spoofReq.Headers.Add("X-Tenant-Id", world.TenantBId.ToString("D"));
        var a1Res = await u1Client.SendAsync(spoofReq);
        Assert.Equal(HttpStatusCode.OK, a1Res.StatusCode);
        var a1Detail = await a1Res.Content.ReadFromJsonAsync<EntityWorkspaceDetailResponse>(JsonOptions);
        Assert.NotNull(a1Detail);
        Assert.Equal("NUSA", a1Detail.Tenant.Code);
        Assert.Equal("NUSA-JKT", a1Detail.Entity.Code);

        // Unassigned sibling entity A2 -> 404 without leaking entity metadata
        var a2Res = await u1Client.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}");
        Assert.Equal(HttpStatusCode.NotFound, a2Res.StatusCode);
        var a2Body = await a2Res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("NUSA-SBY", a2Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Surabaya", a2Body, StringComparison.OrdinalIgnoreCase);

        // Unassigned foreign Tenant B -> 404 without leaking tenant/entity metadata
        var b1Res = await u1Client.GetAsync($"/api/workspaces/{world.TenantBId}/entities/{world.EntityB1Id}");
        Assert.Equal(HttpStatusCode.NotFound, b1Res.StatusCode);
        var b1Body = await b1Res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SMDR", b1Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Samudra", b1Body, StringComparison.OrdinalIgnoreCase);

        // Missing granular permission inside authorized entity -> 403 Forbidden
        var forbiddenRes = await u1Client.GetAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}?requirePermission=finance.journal.post");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // U2: multi-tenant controller sees 2 tenants, 3 entities, switches seamlessly
        using var u2Client = await CreateSignedInClientAsync(factory, world.U2Email, world.Password);
        var u2Workspaces = await (await u2Client.GetAsync("/api/workspaces")).Content.ReadFromJsonAsync<WorkspacesResponse>(JsonOptions);
        Assert.NotNull(u2Workspaces);
        Assert.Equal(2, u2Workspaces.Tenants.Count);
        Assert.Equal(3, u2Workspaces.Tenants.Sum(t => t.Entities.Count));

        var sby = await (await u2Client.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}"))
            .Content.ReadFromJsonAsync<EntityWorkspaceDetailResponse>(JsonOptions);
        var bdg = await (await u2Client.GetAsync($"/api/workspaces/{world.TenantBId}/entities/{world.EntityB1Id}"))
            .Content.ReadFromJsonAsync<EntityWorkspaceDetailResponse>(JsonOptions);
        Assert.Equal("NUSA-SBY", sby!.Entity.Code);
        Assert.Equal("SMDR-BDG", bdg!.Entity.Code);

        // U3: authenticated user with zero memberships
        using var u3Client = await CreateSignedInClientAsync(factory, world.U3Email, world.Password);
        var u3Me = await (await u3Client.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        Assert.NotNull(u3Me);
        Assert.Equal(0, u3Me.AuthorizedWorkspaceCount);
        Assert.Equal("NO_ACTIVE_WORKSPACE", u3Me.WorkspaceAccessStatus);

        var u3Workspaces = await (await u3Client.GetAsync("/api/workspaces")).Content.ReadFromJsonAsync<WorkspacesResponse>(JsonOptions);
        Assert.NotNull(u3Workspaces);
        Assert.Empty(u3Workspaces.Tenants);
        Assert.Equal(HttpStatusCode.NotFound, (await u3Client.GetAsync($"/api/workspaces/{world.TenantAId}/entities")).StatusCode);
    }

    [Fact]
    public async Task S01_API_04_MembershipRevocationTakesImmediateEffectDespiteActiveGrant()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var u1Client = await CreateSignedInClientAsync(factory, world.U1Email, world.Password);

        Assert.Equal(
            HttpStatusCode.OK,
            (await u1Client.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}")).StatusCode);

        // Revoke membership ONLY; leave EntityAccessGrant ACTIVE
        await using (var db = fx.CreateMigratorDbContext())
        {
            var membership = await db.TenantMemberships.SingleAsync(m => m.TenantId == world.TenantAId && m.UserId == world.U1Id);
            membership.Revoke(SeedNow.AddHours(1));
            await db.SaveChangesAsync();
        }

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await u1Client.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}")).StatusCode);
        var afterRevoke = await (await u1Client.GetAsync("/api/workspaces")).Content.ReadFromJsonAsync<WorkspacesResponse>(JsonOptions);
        Assert.Empty(afterRevoke!.Tenants);
    }

    [Fact]
    public async Task S01_PG_06_ConcurrentPoolAndRollbackExecutionHasZeroTenantLeakage()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        var smallPoolConnStr = new NpgsqlConnectionStringBuilder(fx.RuntimeConnectionString)
        {
            MaxPoolSize = 4,
            Pooling = true
        }.ConnectionString;

        await using var dataSource = NpgsqlDataSource.Create(smallPoolConnStr);
        var options = new DbContextOptionsBuilder<Orphac.Athera.Modules.Persistence.AtheraDbContext>()
            .UseAtheraNpgsql(dataSource)
            .Options;

        var tasks = Enumerable.Range(0, 60).Select(async i =>
        {
            await using var db = new Orphac.Athera.Modules.Persistence.AtheraDbContext(options);
            var executor = new TenantScopeExecutor(db);
            var isTenantA = i % 2 == 0;
            var targetTenant = isTenantA ? world.TenantAId : world.TenantBId;
            var expectedCodePrefix = isTenantA ? "NUSA" : "SMDR";

            if (i % 7 == 0)
            {
                // Mid-transaction exception must roll back cleanly without leaking app.tenant_id into the pool
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await executor.ExecuteInTenantAsync<int>(
                        world.U2Id,
                        targetTenant,
                        async (_, _) =>
                        {
                            var count = await db.LegalEntities.CountAsync();
                            Assert.True(count >= 1);
                            throw new InvalidOperationException("Simulated mid-tx failure");
                        },
                        CancellationToken.None);
                });
                return;
            }

            var result = await executor.ExecuteInTenantAsync(
                world.U2Id,
                targetTenant,
                async (scope, ct) =>
                {
                    var visibleEntities = await db.LegalEntities.AsNoTracking().ToListAsync(ct);
                    Assert.NotEmpty(visibleEntities);
                    Assert.All(visibleEntities, e =>
                    {
                        Assert.Equal(targetTenant, e.TenantId);
                        Assert.StartsWith(expectedCodePrefix, e.Code, StringComparison.Ordinal);
                    });

                    // Explicit host probe for foreign tenant must always return 0 rows under forced RLS
                    var foreignTenant = isTenantA ? world.TenantBId : world.TenantAId;
                    var foreignCount = await db.LegalEntities.CountAsync(e => e.TenantId == foreignTenant, ct);
                    Assert.Equal(0, foreignCount);

                    return scope.Tenant.Code;
                },
                CancellationToken.None);

            Assert.True(result.IsAllowed);
            Assert.Equal(expectedCodePrefix, result.Value);
        });

        await Task.WhenAll(tasks);

        // Verify no pooled session retains any app.tenant_id or app.actor_user_id after transactions conclude
        for (var i = 0; i < 6; i++)
        {
            await using var checkConn = await dataSource.OpenConnectionAsync();
            await using var cmd = checkConn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(current_setting('app.tenant_id', true), ''), COALESCE(current_setting('app.actor_user_id', true), '');";
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(string.Empty, reader.GetString(0));
            Assert.Equal(string.Empty, reader.GetString(1));
        }
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

    private static async Task<SeededTenancyWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (u1Id, u1Email, _) = await fx.CreateTestUserAsync("rina.u1@test.local", "Rina U1", pw);
        var (u2Id, u2Email, _) = await fx.CreateTestUserAsync("budi.u2@test.local", "Budi U2", pw);
        var (u3Id, u3Email, _) = await fx.CreateTestUserAsync("hendra.u3@test.local", "Hendra U3", pw);

        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", SeedNow),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", SeedNow));

        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", SeedNow),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", SeedNow),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", SeedNow));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, u1Id, SeedNow),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, u2Id, SeedNow),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, u2Id, SeedNow));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, u1Id, PermissionCodes.WorkspaceView, SeedNow, null, SeedNow),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, u1Id, PermissionCodes.ProcurementRead, SeedNow, null, SeedNow),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, u2Id, PermissionCodes.WorkspaceView, SeedNow, null, SeedNow),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, u2Id, PermissionCodes.WorkspaceView, SeedNow, null, SeedNow),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, u2Id, PermissionCodes.WorkspaceView, SeedNow, null, SeedNow));

        await db.SaveChangesAsync();

        return new SeededTenancyWorld(
            tenantAId, entityA1Id, entityA2Id,
            tenantBId, entityB1Id,
            u1Id, u1Email,
            u2Id, u2Email,
            u3Id, u3Email,
            pw);
    }
}
