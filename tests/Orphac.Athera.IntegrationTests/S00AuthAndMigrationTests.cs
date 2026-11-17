using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;

namespace Orphac.Athera.IntegrationTests;

public sealed class S00AuthAndMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task S00_PG_01_MigrationUpDownUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("0");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name <> '__ef_migrations_history';";
            var remaining = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            Assert.Equal(0L, remaining);
        }

        await db.Database.MigrateAsync("20261117173251_M00_InitialIdentityFoundation");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name <> '__ef_migrations_history';";
            var restored = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            Assert.Equal(8L, restored);
        }
    }

    [Fact]
    public async Task S00_PG_02_RuntimeRoleLacksSuperuserBypassAndTableOwnership()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var roleCmd = conn.CreateCommand())
        {
            roleCmd.CommandText = """
                SELECT rolsuper, rolbypassrls, rolcreatedb, rolcreaterole
                FROM pg_roles
                WHERE rolname = current_user;
                """;
            await using var reader = await roleCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
        }

        await using var ownerCmd = conn.CreateCommand();
        ownerCmd.CommandText = """
            SELECT count(*)
            FROM pg_tables
            WHERE schemaname = 'identity' AND tableowner = current_user;
            """;
        var ownedCount = Convert.ToInt64(await ownerCmd.ExecuteScalarAsync());
        Assert.Equal(0L, ownedCount);
    }

    [Fact]
    public async Task S00_API_01_HealthEndpointVerifiesPostgreSqlAsRuntimeRole()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        using var factory = fx.CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }

    [Fact]
    public async Task S00_API_02_04_05_CsrfLoginMeAndLogoutEndToEnd()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var user = await fx.CreateTestUserAsync("rina.utami@demo.athera.local", "Rina Utami", "AtheraDemo!2026");

        using var factory = fx.CreateApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Unauthenticated /api/auth/me returns 401 JSON (never redirect)
        var unauth = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // Missing CSRF token on login fails with 403
        var missingCsrf = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, user.Password, false));
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);

        // Fetch CSRF token
        var csrfRes = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf", JsonOptions);
        Assert.NotNull(csrfRes);
        Assert.False(string.IsNullOrWhiteSpace(csrfRes.RequestToken));

        // Login with bad origin fails 403
        using (var badOriginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(user.Email, user.Password, false))
        })
        {
            badOriginReq.Headers.Add(csrfRes.HeaderName, csrfRes.RequestToken);
            badOriginReq.Headers.Add("Origin", "https://attacker.invalid");
            var badOriginRes = await client.SendAsync(badOriginReq);
            Assert.Equal(HttpStatusCode.Forbidden, badOriginRes.StatusCode);
        }

        // Valid login succeeds and returns rotated CSRF token + session cookie
        string rotatedCsrf;
        using (var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(user.Email, user.Password, false))
        })
        {
            loginReq.Headers.Add(csrfRes.HeaderName, csrfRes.RequestToken);
            var loginRes = await client.SendAsync(loginReq);
            Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
            var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
            Assert.NotNull(session);
            Assert.Equal(user.UserId, session.User.UserId);
            Assert.Equal("Rina Utami", session.User.DisplayName);
            rotatedCsrf = session.CsrfToken;
            Assert.False(string.IsNullOrWhiteSpace(rotatedCsrf));
        }

        // Authenticated /api/auth/me reads PostgreSQL user account and enforces no-store cache headers
        var meRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        Assert.True(meRes.Headers.CacheControl?.NoStore);
        var me = await meRes.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        Assert.NotNull(me);
        Assert.Equal("Rina Utami", me.DisplayName);
        Assert.NotNull(me.LastSignedInAtUtc);

        // Logout with rotated CSRF clears session
        using (var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
        {
            logoutReq.Headers.Add("X-CSRF-Token", rotatedCsrf);
            var logoutRes = await client.SendAsync(logoutReq);
            Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);
        }

        var postLogoutMe = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, postLogoutMe.StatusCode);
    }

    [Fact]
    public async Task S00_API_03_InvalidPasswordAndLockoutEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var user = await fx.CreateTestUserAsync("lockout@demo.athera.local", "Lockout User", "AtheraDemo!2026");

        using var factory = fx.CreateApiFactory();
        using var client = factory.CreateClient();

        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf", JsonOptions);
        Assert.NotNull(csrf);

        for (var i = 0; i < 4; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(user.Email, "WrongPassword!99", false))
            };
            req.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        using var lockReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(user.Email, "WrongPassword!99", false))
        };
        lockReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        var lockRes = await client.SendAsync(lockReq);
        Assert.Equal((HttpStatusCode)423, lockRes.StatusCode);
    }

    [Fact]
    public async Task S00_API_05b_DeactivatedUserAccountRejectedImmediately()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var user = await fx.CreateTestUserAsync("deact@demo.athera.local", "Deact User", "AtheraDemo!2026");

        using var factory = fx.CreateApiFactory();
        using var client = factory.CreateClient();

        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf", JsonOptions);
        Assert.NotNull(csrf);

        using (var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(user.Email, user.Password, false))
        })
        {
            loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            var loginRes = await client.SendAsync(loginReq);
            Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        }

        await using (var db = fx.CreateMigratorDbContext())
        {
            var account = await db.UserAccounts.SingleAsync(x => x.UserId == user.UserId);
            account.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var meRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meRes.StatusCode);
    }
}
