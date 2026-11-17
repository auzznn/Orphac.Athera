using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Persistence;

/// <summary>
/// Shared transactional EF Core DbContext partitioned by module-owned PostgreSQL schemas.
/// </summary>
public sealed class AtheraDbContext(DbContextOptions<AtheraDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<EntityAccessGrant> EntityAccessGrants => Set<EntityAccessGrant>();

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

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", "platform", t =>
            {
                t.HasCheckConstraint("ck_tenants_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
            });

            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<LegalEntity>(entity =>
        {
            entity.ToTable("legal_entities", "platform", t =>
            {
                t.HasCheckConstraint("ck_legal_entities_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
            });

            entity.HasKey(x => x.LegalEntityId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId });

            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.TaxRegistrationNpwp).HasColumnName("tax_registration_npwp").HasMaxLength(64);
            entity.Property(x => x.FunctionalCurrencyCode).HasColumnName("functional_currency_code").HasColumnType("char(3)").HasDefaultValue("IDR").IsRequired();
            entity.Property(x => x.CountryCode).HasColumnName("country_code").HasColumnType("char(2)").HasDefaultValue("ID").IsRequired();
            entity.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).HasDefaultValue("Asia/Jakarta").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("tenant_memberships", "identity", t =>
            {
                t.HasCheckConstraint("ck_tenant_memberships_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
            });

            entity.HasKey(x => x.MembershipId);
            entity.HasAlternateKey(x => new { x.TenantId, x.UserId });

            entity.Property(x => x.MembershipId).HasColumnName("membership_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.UserId, x.MembershipId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EntityAccessGrant>(entity =>
        {
            entity.ToTable("entity_access_grants", "identity", t =>
            {
                t.HasCheckConstraint("ck_entity_access_grants_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
                t.HasCheckConstraint("ck_entity_access_grants_validity_window", "valid_until_utc IS NULL OR valid_until_utc > valid_from_utc");
            });

            entity.HasKey(x => x.GrantId);

            entity.Property(x => x.GrantId).HasColumnName("grant_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.PermissionCode).HasColumnName("permission_code").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ValidFromUtc).HasColumnName("valid_from_utc").IsRequired();
            entity.Property(x => x.ValidUntilUtc).HasColumnName("valid_until_utc");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UserId, x.PermissionCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.LegalEntityId, x.Status });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.UserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
