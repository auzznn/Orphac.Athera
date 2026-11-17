using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Cli;

/// <summary>
/// Idempotent development/staging seed data for S00/S01 verification. Never auto-runs in production.
/// </summary>
public static class DemoDataSeeder
{
    public static readonly Guid TenantNusaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    public static readonly Guid EntityNusaJakartaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101");
    public static readonly Guid EntityNusaSurabayaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa102");

    public static readonly Guid TenantSamudraId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01");
    public static readonly Guid EntitySamudraBandungId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb101");

    public static async Task SeedAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        string demoPassword,
        CancellationToken ct)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException("Refusing to seed demo fixtures in Production environment.");
        }

        if (string.IsNullOrWhiteSpace(demoPassword) || demoPassword.Length < 12)
        {
            throw new InvalidOperationException("Set ATHERA_DEMO_PASSWORD (min 12 chars) before running --seed-demo.");
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AtheraDbContext>();
        var nowUtc = DateTimeOffset.UtcNow;

        var u1Id = await EnsureUserAsync(
            userManager,
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111101"),
            "rina.utami@demo.athera.local",
            "Rina Utami (Demo Procurement)",
            SupportedLocales.Indonesian,
            demoPassword,
            nowUtc,
            ct);

        var u2Id = await EnsureUserAsync(
            userManager,
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111102"),
            "budi.santoso@demo.athera.local",
            "Budi Santoso (Group Controller)",
            SupportedLocales.Indonesian,
            demoPassword,
            nowUtc,
            ct);

        await EnsureUserAsync(
            userManager,
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111103"),
            "hendra.wijaya@demo.athera.local",
            "Hendra Wijaya (Unassigned Staff)",
            SupportedLocales.Indonesian,
            demoPassword,
            nowUtc,
            ct);

        var nusaId = await EnsureTenantAsync(db, TenantNusaId, "NUSA", "PT Nusantara Niaga Utama", nowUtc, ct);
        var smdrId = await EnsureTenantAsync(db, TenantSamudraId, "SMDR", "CV Samudra Teknik Mandiri", nowUtc, ct);

        var nusaJktId = await EnsureEntityAsync(
            db,
            EntityNusaJakartaId,
            nusaId,
            "NUSA-JKT",
            "PT Nusantara Distribusi Jakarta",
            "01.234.567.8-011.000",
            nowUtc,
            ct);

        var nusaSbyId = await EnsureEntityAsync(
            db,
            EntityNusaSurabayaId,
            nusaId,
            "NUSA-SBY",
            "PT Nusantara Niaga Surabaya",
            "01.234.567.8-601.000",
            nowUtc,
            ct);

        var smdrBdgId = await EnsureEntityAsync(
            db,
            EntitySamudraBandungId,
            smdrId,
            "SMDR-BDG",
            "CV Samudra Teknik Bandung",
            "02.345.678.9-421.000",
            nowUtc,
            ct);

        await EnsureMembershipAsync(db, nusaId, u1Id, nowUtc, ct);
        await EnsureMembershipAsync(db, nusaId, u2Id, nowUtc, ct);
        await EnsureMembershipAsync(db, smdrId, u2Id, nowUtc, ct);

        // U1: NUSA-JKT only (NUSA-SBY and SMDR-BDG remain inaccessible)
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.WorkspaceView, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ProcurementRead, nowUtc, ct);

        // U2: Multi-entity across NUSA and SMDR
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.WorkspaceView, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ProcurementRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.FinanceRead, nowUtc, ct);

        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.WorkspaceView, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.InventoryRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.FinanceRead, nowUtc, ct);

        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.WorkspaceView, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.FinanceRead, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.AdminRead, nowUtc, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        AtheraDbContext db,
        Guid defaultUserId,
        string email,
        string displayName,
        string preferredLocale,
        string password,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = defaultUserId,
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        var existingAccount = await db.UserAccounts.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (existingAccount is null)
        {
            db.UserAccounts.Add(UserAccount.Create(user.Id, displayName, preferredLocale, nowUtc));
            await db.SaveChangesAsync(ct);
        }

        return user.Id;
    }

    private static async Task<Guid> EnsureTenantAsync(
        AtheraDbContext db,
        Guid defaultTenantId,
        string code,
        string name,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.Tenants.SingleOrDefaultAsync(t => t.Code == code, ct);
        if (existing is not null)
        {
            return existing.TenantId;
        }

        var tenant = Tenant.Create(defaultTenantId, code, name, nowUtc);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant.TenantId;
    }

    private static async Task<Guid> EnsureEntityAsync(
        AtheraDbContext db,
        Guid defaultEntityId,
        Guid tenantId,
        string code,
        string name,
        string npwp,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.LegalEntities.SingleOrDefaultAsync(
            e => e.TenantId == tenantId && e.Code == code,
            ct);
        if (existing is not null)
        {
            return existing.LegalEntityId;
        }

        var entity = LegalEntity.Create(
            defaultEntityId,
            tenantId,
            code,
            name,
            npwp,
            "IDR",
            "ID",
            "Asia/Jakarta",
            nowUtc);
        db.LegalEntities.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.LegalEntityId;
    }

    private static async Task EnsureMembershipAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid userId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.TenantMemberships.AnyAsync(
            m => m.TenantId == tenantId && m.UserId == userId,
            ct);
        if (!exists)
        {
            db.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), tenantId, userId, nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureGrantAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid userId,
        string permissionCode,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var normalized = PermissionCodes.Normalize(permissionCode);
        var exists = await db.EntityAccessGrants.AnyAsync(
            g => g.TenantId == tenantId
                && g.LegalEntityId == entityId
                && g.UserId == userId
                && g.PermissionCode == normalized,
            ct);
        if (!exists)
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                userId,
                normalized,
                nowUtc.AddMinutes(-1),
                null,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }
}
