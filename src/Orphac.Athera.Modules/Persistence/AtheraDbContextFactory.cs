using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orphac.Athera.Modules.Persistence;

/// <summary>
/// Design-time factory used by dotnet-ef for generating PostgreSQL migrations without runtime startup.
/// </summary>
public sealed class AtheraDbContextFactory : IDesignTimeDbContextFactory<AtheraDbContext>
{
    public AtheraDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Migrator")
            ?? "Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_migrator;Password=athera_dev_local_migrator_pw";

        var options = new DbContextOptionsBuilder<AtheraDbContext>()
            .UseAtheraNpgsql(conn)
            .Options;

        return new AtheraDbContext(options);
    }
}
