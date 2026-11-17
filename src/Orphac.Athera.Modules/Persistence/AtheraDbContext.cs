using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Identity;

namespace Orphac.Athera.Modules.Persistence;

/// <summary>
/// Shared transactional EF Core DbContext partitioned by module-owned PostgreSQL schemas.
/// </summary>
public sealed class AtheraDbContext(DbContextOptions<AtheraDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("asp_net_users", "identity");
        builder.Entity<ApplicationRole>().ToTable("asp_net_roles", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens", "identity");

        builder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("user_accounts", "identity", t =>
            {
                t.HasCheckConstraint("ck_user_accounts_locale", "preferred_locale IN ('id-ID', 'en-US')");
            });

            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.PreferredLocale).HasColumnName("preferred_locale").HasMaxLength(16).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.LastSignedInAtUtc).HasColumnName("last_signed_in_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserAccount>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
