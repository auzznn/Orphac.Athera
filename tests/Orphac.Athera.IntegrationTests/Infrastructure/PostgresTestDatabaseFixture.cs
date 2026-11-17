using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.IntegrationTests.Infrastructure;

public sealed class PostgresTestDatabaseFixture : IAsyncLifetime
{
    public const string DefaultMigratorPassword = "athera_dev_local_migrator_pw";
    public const string DefaultAppPassword = "athera_dev_local_app_pw";

    private readonly string _adminConnectionString;

    public string DatabaseName { get; } = $"athera_test_{Guid.NewGuid():N}";
    public string MigratorConnectionString { get; private set; } = string.Empty;
    public string RuntimeConnectionString { get; private set; } = string.Empty;

    public PostgresTestDatabaseFixture()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("ATHERA_TEST_PG_ADMIN")
            ?? "Host=127.0.0.1;Port=55433;Username=athera_dev_admin;Password=athera_dev_local_admin_pw;Database=athera_dev";
    }

    public async Task InitializeAsync()
    {
        await using var adminConn = new NpgsqlConnection(_adminConnectionString);
        await adminConn.OpenAsync();

        await EnsureRolesExistAsync(adminConn);

        await using (var createDb = adminConn.CreateCommand())
        {
            createDb.CommandText = $"""
                CREATE DATABASE "{DatabaseName}" OWNER athera_migrator;
                REVOKE ALL ON DATABASE "{DatabaseName}" FROM PUBLIC;
                GRANT CONNECT, CREATE ON DATABASE "{DatabaseName}" TO athera_migrator;
                GRANT CONNECT ON DATABASE "{DatabaseName}" TO athera_app;
                """;
            await createDb.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = DatabaseName,
            Username = "athera_migrator",
            Password = DefaultMigratorPassword,
            Pooling = true
        };
        MigratorConnectionString = builder.ConnectionString;

        var appBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = DatabaseName,
            Username = "athera_app",
            Password = DefaultAppPassword,
            Pooling = true,
            MaxPoolSize = 8
        };
        RuntimeConnectionString = appBuilder.ConnectionString;

        await using var db = CreateMigratorDbContext();
        await db.Database.MigrateAsync();
    }

    public AtheraDbContext CreateMigratorDbContext()
    {
        var options = new DbContextOptionsBuilder<AtheraDbContext>()
            .UseAtheraNpgsql(MigratorConnectionString)
            .Options;
        return new AtheraDbContext(options);
    }

    public WebApplicationFactory<Program> CreateApiFactory(Action<IServiceCollection>? configureServices = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Runtime"] = RuntimeConnectionString,
                    ["ConnectionStrings:Migrator"] = MigratorConnectionString,
                    ["Security:AllowedBrowserOrigins:0"] = "http://127.0.0.1:3200",
                    ["Security:AllowedBrowserOrigins:1"] = "http://localhost"
                });
            });

            if (configureServices is not null)
            {
                builder.ConfigureServices(configureServices);
            }
        });
    }

    public async Task<(Guid UserId, string Email, string Password)> CreateTestUserAsync(
        string email,
        string displayName,
        string password,
        bool isActive = true)
    {
        using var factory = CreateApiFactory();
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var db = CreateMigratorDbContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var identityResult = await userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", identityResult.Errors.Select(e => e.Description)));
        }

        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create(user.Id, displayName, "id-ID", now);
        if (!isActive)
        {
            account.Deactivate(now);
        }

        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();

        return (user.Id, email, password);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var adminConn = new NpgsqlConnection(_adminConnectionString);
        await adminConn.OpenAsync();

        await using var terminate = adminConn.CreateCommand();
        terminate.CommandText = """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @db AND pid <> pg_backend_pid();
            """;
        terminate.Parameters.AddWithValue("db", DatabaseName);
        await terminate.ExecuteNonQueryAsync();

        await using var dropDb = adminConn.CreateCommand();
        dropDb.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\";";
        await dropDb.ExecuteNonQueryAsync();
    }

    private static async Task EnsureRolesExistAsync(NpgsqlConnection adminConn)
    {
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"""
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_migrator') THEN
                CREATE ROLE athera_migrator LOGIN PASSWORD '{DefaultMigratorPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
              END IF;
              IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'athera_app') THEN
                CREATE ROLE athera_app LOGIN PASSWORD '{DefaultAppPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
              END IF;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
