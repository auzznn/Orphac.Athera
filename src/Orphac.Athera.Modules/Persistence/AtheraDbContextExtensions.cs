using Microsoft.EntityFrameworkCore;

namespace Orphac.Athera.Modules.Persistence;

public static class AtheraDbContextExtensions
{
    public static DbContextOptionsBuilder UseAtheraNpgsql(this DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"));
    }

    public static DbContextOptionsBuilder<TContext> UseAtheraNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DbContext
    {
        return builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"));
    }
}
