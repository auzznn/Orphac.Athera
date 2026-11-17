using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
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
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.PartnersRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.PartnersManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.CatalogRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.CatalogManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ApprovalRequest, nowUtc, ct);

        // U2: Multi-entity controller & org admin across NUSA and SMDR
        foreach (var scopePair in new[] { (nusaId, nusaJktId), (nusaId, nusaSbyId), (smdrId, smdrBdgId) })
        {
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.WorkspaceView, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.FinanceRead, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.AdminRead, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.AdminManage, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.ApprovalRequest, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.ApprovalDecide, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.PartnersRead, nowUtc, ct);
            await EnsureGrantAsync(db, scopePair.Item1, scopePair.Item2, u2Id, PermissionCodes.CatalogRead, nowUtc, ct);
            await EnsureApprovalPolicyAsync(db, scopePair.Item1, scopePair.Item2, "SENSITIVE_ACCESS_CHANGE", "Sensitive Entity Access Change", requiresFourEyes: true, requiresApproverMfa: false, nowUtc, ct);
            await EnsureApprovalPolicyAsync(db, scopePair.Item1, scopePair.Item2, "HIGH_RISK_PRIVILEGE_ESCALATION", "High-Risk Privilege Escalation (MFA)", requiresFourEyes: true, requiresApproverMfa: true, nowUtc, ct);
        }

        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ProcurementRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.PartnersManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.CatalogManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.InventoryRead, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.PartnersManage, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.CatalogManage, nowUtc, ct);

        // S03 master data fixtures (NUSA)
        var nusaPcs = await EnsureUomAsync(db, nusaId, "PCS", "Pieces (Satuan Diskrit)", 0, nowUtc, ct);
        var nusaBox = await EnsureUomAsync(db, nusaId, "BOX", "Box Karton", 0, nowUtc, ct);
        await EnsureUomAsync(db, nusaId, "KG", "Kilogram", 3, nowUtc, ct);

        await EnsurePartnerAsync(
            db,
            nusaId,
            "PT-SUP-001",
            "PT Sumber Makmur Kemasan",
            "Sumber Makmur",
            isSupplier: true,
            isCustomer: false,
            npwp: "01.888.221.4-012.000",
            taxPkpStatus: TaxPkpStatuses.Pkp,
            entityScopeMode: EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            contactName: "Sari Indah",
            contactEmail: "sales@sumbermakmur.demo.local",
            nowUtc,
            ct);

        await EnsurePartnerAsync(
            db,
            nusaId,
            "PT-DUAL-001",
            "PT Mitra Niaga Nusantara",
            "Mitra Niaga Nusantara",
            isSupplier: true,
            isCustomer: true,
            npwp: "01.999.456.7-011.000",
            taxPkpStatus: TaxPkpStatuses.Pkp,
            entityScopeMode: EntityScopeModes.RestrictedEntities,
            eligibleEntityId: nusaJktId,
            contactName: "Arman Hakim",
            contactEmail: "commercial@mitraniaga.demo.local",
            nowUtc,
            ct);

        var pkgItem = await EnsureItemAsync(
            db,
            nusaId,
            "ITM-PKG-001",
            "Karton Lipat Polos 40x30x25 cm",
            ItemKinds.StockedGood,
            nusaPcs.UomId,
            nusaBox.UomId,
            nusaPcs.UomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);
        await EnsureItemConversionAsync(db, nusaId, pkgItem.ItemId, nusaBox.UomId, 24m, nowUtc, ct);

        var tapeItem = await EnsureItemAsync(
            db,
            nusaId,
            "ITM-RES-002",
            "Lakban Industri Bening 48mm",
            ItemKinds.StockedGood,
            nusaPcs.UomId,
            nusaBox.UomId,
            nusaPcs.UomId,
            EntityScopeModes.RestrictedEntities,
            eligibleEntityId: nusaJktId,
            nowUtc,
            ct);
        await EnsureItemConversionAsync(db, nusaId, tapeItem.ItemId, nusaBox.UomId, 36m, nowUtc, ct);

        // S03 master data fixtures (SMDR - foreign tenant isolation checks)
        var smdrPcs = await EnsureUomAsync(db, smdrId, "PCS", "Pieces (Samudra)", 0, nowUtc, ct);
        await EnsurePartnerAsync(
            db,
            smdrId,
            "SMDR-SUP-01",
            "CV Logistik Priangan",
            "Logistik Priangan",
            isSupplier: true,
            isCustomer: false,
            npwp: "02.444.111.2-421.000",
            taxPkpStatus: TaxPkpStatuses.NonPkp,
            entityScopeMode: EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            contactName: "Dedi Kusuma",
            contactEmail: "admin@priangan.demo.local",
            nowUtc,
            ct);
        await EnsureItemAsync(
            db,
            smdrId,
            "SMDR-ITM-01",
            "Palet Kayu Ekspor Standard",
            ItemKinds.StockedGood,
            smdrPcs.UomId,
            smdrPcs.UomId,
            smdrPcs.UomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);

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
                && g.PermissionCode == normalized
                && g.Status != GrantStatuses.Revoked,
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

    private static async Task EnsureApprovalPolicyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string policyCode,
        string name,
        bool requiresFourEyes,
        bool requiresApproverMfa,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.ApprovalPolicies.AnyAsync(
            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyCode == policyCode,
            ct);
        if (!exists)
        {
            db.ApprovalPolicies.Add(ApprovalPolicy.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                policyCode,
                name,
                requiresFourEyes,
                requiresApproverMfa,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<UnitOfMeasure> EnsureUomAsync(
        AtheraDbContext db,
        Guid tenantId,
        string code,
        string name,
        int scale,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.UnitsOfMeasure.SingleOrDefaultAsync(
            u => u.TenantId == tenantId && u.UomCode == code,
            ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, code, name, scale, nowUtc);
        db.UnitsOfMeasure.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static async Task EnsurePartnerAsync(
        AtheraDbContext db,
        Guid tenantId,
        string partnerCode,
        string legalName,
        string displayName,
        bool isSupplier,
        bool isCustomer,
        string? npwp,
        string taxPkpStatus,
        string entityScopeMode,
        Guid? eligibleEntityId,
        string contactName,
        string contactEmail,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var partner = await db.Partners.SingleOrDefaultAsync(
            p => p.TenantId == tenantId && p.PartnerCode == partnerCode,
            ct);
        if (partner is null)
        {
            partner = Partner.Create(
                Guid.NewGuid(),
                tenantId,
                partnerCode,
                legalName,
                displayName,
                isSupplier,
                isCustomer,
                npwp,
                taxPkpStatus,
                entityScopeMode,
                nowUtc);
            db.Partners.Add(partner);
            db.PartnerContacts.Add(PartnerContact.Create(
                Guid.NewGuid(),
                tenantId,
                partner.PartnerId,
                contactName,
                PartnerContactRoles.Commercial,
                contactEmail,
                "+62-21-5550-0100",
                isPrimary: true,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }

        if (eligibleEntityId is { } entityId)
        {
            var hasProfile = await db.PartnerEntityProfiles.AnyAsync(
                x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.PartnerId == partner.PartnerId,
                ct);
            if (!hasProfile)
            {
                db.PartnerEntityProfiles.Add(PartnerEntityProfile.CreateForPartner(
                    Guid.NewGuid(),
                    partner,
                    entityId,
                    supplierEligible: isSupplier,
                    customerEligible: isCustomer,
                    defaultPaymentTermsDays: 30,
                    notes: "Seeded S03 eligible entity profile",
                    nowUtc: nowUtc));
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static async Task<Item> EnsureItemAsync(
        AtheraDbContext db,
        Guid tenantId,
        string itemCode,
        string name,
        string itemKind,
        Guid baseUomId,
        Guid defaultPurchaseUomId,
        Guid defaultSalesUomId,
        string entityScopeMode,
        Guid? eligibleEntityId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(
            i => i.TenantId == tenantId && i.ItemCode == itemCode,
            ct);
        if (item is null)
        {
            item = Item.Create(
                Guid.NewGuid(),
                tenantId,
                itemCode,
                name,
                itemKind,
                baseUomId,
                defaultPurchaseUomId,
                defaultSalesUomId,
                entityScopeMode,
                nowUtc);
            db.Items.Add(item);
            await db.SaveChangesAsync(ct);
        }

        if (eligibleEntityId is { } entityId)
        {
            var hasPolicy = await db.ItemEntityPolicies.AnyAsync(
                x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.ItemId == item.ItemId,
                ct);
            if (!hasPolicy)
            {
                db.ItemEntityPolicies.Add(ItemEntityPolicy.Create(
                    Guid.NewGuid(),
                    tenantId,
                    entityId,
                    item.ItemId,
                    purchaseAllowed: true,
                    salesAllowed: true,
                    notes: "Seeded S03 eligible entity policy",
                    nowUtc: nowUtc));
                await db.SaveChangesAsync(ct);
            }
        }

        return item;
    }

    private static async Task EnsureItemConversionAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid itemId,
        Guid uomId,
        decimal factorToBase,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.ItemUomConversions.AnyAsync(
            c => c.TenantId == tenantId && c.ItemId == itemId && c.UomId == uomId,
            ct);
        if (!exists)
        {
            db.ItemUomConversions.Add(ItemUom.Create(
                Guid.NewGuid(),
                tenantId,
                itemId,
                uomId,
                factorToBase,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }
}
