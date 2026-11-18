using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Cli;

/// <summary>
/// Idempotent development/staging seed data for S00/S01 verification. Never auto-runs in production.
/// </summary>
public static partial class DemoDataSeeder
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
        // Anchor seed timestamps 3 hours in the past so sequential demo fixtures (+1..+67+ min)
        // never outrank live interactive/E2E audit events sorted newest-first.
        var nowUtc = DateTimeOffset.UtcNow.AddHours(-3);

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

        var u4Id = await EnsureUserAsync(
            userManager,
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111104"),
            "siti.rahayu@demo.athera.local",
            "Siti Rahayu (AP Officer)",
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
        await EnsureMembershipAsync(db, nusaId, u4Id, nowUtc, ct);
        await EnsureMembershipAsync(db, smdrId, u2Id, nowUtc, ct);

        // U1: NUSA-JKT only (NUSA-SBY and SMDR-BDG remain inaccessible; SoD: no approval, quality.inspect, or finance grants)
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.WorkspaceView, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ProcurementRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ProcurementManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ReceivingRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ReceivingManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.ReceivingAccept, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.InventoryRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u1Id, PermissionCodes.InventoryManage, nowUtc, ct);
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
            await EnsureApprovalPolicyAsync(db, scopePair.Item1, scopePair.Item2, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "Purchase Order Commitment Four-Eyes Approval", requiresFourEyes: true, requiresApproverMfa: false, nowUtc, ct);
            await EnsureApprovalPolicyAsync(db, scopePair.Item1, scopePair.Item2, ApPolicyDefaults.SupplierBillApprovalPolicyCode, "Supplier Bill Variance & Posting Four-Eyes Approval", requiresFourEyes: true, requiresApproverMfa: false, nowUtc, ct);
            await EnsureApprovalPolicyAsync(db, scopePair.Item1, scopePair.Item2, ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode, "Supplier Return & Financial Correction Four-Eyes Authorization", requiresFourEyes: true, requiresApproverMfa: false, nowUtc, ct);
            await EnsureTolerancePolicyAsync(db, scopePair.Item1, scopePair.Item2, "EXACT_QTY_OD01_SAFE", "Exact Approved Quantity Only (OD-01 Safe Default)", nowUtc, ct);
            await SupplierBillCoordinator.EnsureDefaultMatchPolicyAsync(db, scopePair.Item1, scopePair.Item2, nowUtc, ct);
        }

        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ProcurementRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ProcurementManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ProcurementApprove, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ReceivingRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ReceivingManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ReceivingAccept, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.InventoryRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.InventoryManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.QualityInspect, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.PartnersManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.CatalogManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.FinanceConfigManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.FinanceJournalPost, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ApRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ApManage, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ApApprove, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaJktId, u2Id, PermissionCodes.ApPost, nowUtc, ct);
        foreach (var apClerkPerm in RoleTemplates.GetPermissions(RoleTemplates.ApClerk))
        {
            await EnsureGrantAsync(db, nusaId, nusaJktId, u4Id, apClerkPerm, nowUtc, ct);
        }
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.ProcurementRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.ReceivingRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.InventoryRead, nowUtc, ct);
        await EnsureGrantAsync(db, nusaId, nusaSbyId, u2Id, PermissionCodes.ApRead, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.PartnersManage, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.CatalogManage, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.FinanceConfigManage, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.FinanceJournalPost, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.ApRead, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.ApManage, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.ApApprove, nowUtc, ct);
        await EnsureGrantAsync(db, smdrId, smdrBdgId, u2Id, PermissionCodes.ApPost, nowUtc, ct);

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

        // S04 financial configuration fixtures (CoA, periods, mappings, OD-07 configurable tax rules, sample journal)
        await EnsureFinanceConfigurationAsync(db, nusaId, nusaJktId, u2Id, "IDR", seedInitialJournal: true, nowUtc, ct);
        await EnsureFinanceConfigurationAsync(db, nusaId, nusaSbyId, u2Id, "IDR", seedInitialJournal: true, nowUtc, ct);
        await EnsureFinanceConfigurationAsync(db, smdrId, smdrBdgId, u2Id, "IDR", seedInitialJournal: true, nowUtc, ct);

        await db.SaveChangesAsync(ct);

        // S05 procurement PO fixtures
        var supNusa = await db.Partners.AsNoTracking().SingleAsync(p => p.TenantId == nusaId && p.PartnerCode == "PT-SUP-001", ct);
        var supSmdr = await db.Partners.AsNoTracking().SingleAsync(p => p.TenantId == smdrId && p.PartnerCode == "SMDR-SUP-01", ct);
        var smdrItem = await db.Items.AsNoTracking().SingleAsync(i => i.TenantId == smdrId && i.ItemCode == "SMDR-ITM-01", ct);

        await EnsureApprovedSeedPoAsync(db, nusaId, nusaJktId, supNusa.PartnerId, pkgItem.ItemId, nusaBox.UomId, 2m, 480_000m, "Gudang Pusat Jakarta", u1Id, u2Id, approve: true, nowUtc, ct);
        await EnsureApprovedSeedPoAsync(db, nusaId, nusaSbyId, supNusa.PartnerId, pkgItem.ItemId, nusaBox.UomId, 3m, 485_000m, "Gudang Regional Surabaya", u1Id, u2Id, approve: true, nowUtc, ct);
        await EnsureApprovedSeedPoAsync(db, smdrId, smdrBdgId, supSmdr.PartnerId, smdrItem.ItemId, smdrPcs.UomId, 50m, 120_000m, "Gudang Samudra Bandung", u2Id, u2Id, approve: false, nowUtc, ct);

        await db.SaveChangesAsync(ct);

        // S06 Gate 1 dock custody seed on NUSA-SBY (read-only showcase without altering NUSA-JKT test baseline)
        await EnsureSeedGoodsReceiptAsync(db, nusaId, nusaSbyId, nusaBox.UomId, u2Id, nowUtc, ct);

        // S07 moving-average inventory & GRNI seed on NUSA-JKT (dedicated S07 item so S05/S06 assertions remain unchanged)
        var steelPlateItem = await EnsureItemAsync(
            db,
            nusaId,
            "ITM-STL-PLATE-10",
            "Pelat Baja 10mm Grade SS400",
            ItemKinds.StockedGood,
            nusaPcs.UomId,
            nusaPcs.UomId,
            nusaPcs.UomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct);
        await db.SaveChangesAsync(ct);

        await EnsureS07InventoryDemoDataAsync(
            db,
            nusaId,
            nusaJktId,
            supNusa.PartnerId,
            steelPlateItem.ItemId,
            nusaPcs.UomId,
            u1Id,
            u2Id,
            nowUtc,
            ct);

        // S08 warehouse locations, lot-required chemical solvent item, and pending QUALITY_INSPECTION queue on NUSA-JKT
        await EnsureS08WarehouseAndQualityDataAsync(
            db,
            nusaId,
            nusaJktId,
            nusaSbyId,
            smdrId,
            smdrBdgId,
            supNusa.PartnerId,
            nusaPcs.UomId,
            u1Id,
            u2Id,
            nowUtc,
            ct);

        // S09 valve item with 100 PCS open unbilled GRNI @ Rp 50.000 and 60 PCS operational issue leaving 40 PCS on-hand
        await EnsureS09PayablesDemoDataAsync(
            db,
            nusaId,
            nusaJktId,
            supNusa.PartnerId,
            nusaPcs.UomId,
            u1Id,
            u2Id,
            nowUtc,
            ct);

        // S10 seal (6 dock-rejected for M1 + 14 billed unpaid for M3) & gasket (30 unbilled accepted for M2) on NUSA-JKT only
        await EnsureS10ReturnsDemoDataAsync(
            db,
            nusaId,
            nusaJktId,
            supNusa.PartnerId,
            nusaPcs.UomId,
            u1Id,
            u2Id,
            u4Id,
            nowUtc,
            ct);

        // S11 bank accounts, simulated instruction pending four-eyes release, outbound clearing batch & unmatched bank statement line
        await EnsureS11PaymentsDemoDataAsync(
            db,
            nusaId,
            nusaJktId,
            nusaSbyId,
            smdrId,
            smdrBdgId,
            supNusa.PartnerId,
            nusaPcs.UomId,
            u1Id,
            u2Id,
            u4Id,
            nowUtc,
            ct);
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
        CancellationToken ct,
        string? lotTrackingPolicy = null)
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
                nowUtc,
                lotTrackingPolicy);
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

    private static async Task EnsureFinanceConfigurationAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid actorUserId,
        string currencyCode,
        bool seedInitialJournal,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var hAsset = await EnsureGlAccountAsync(db, tenantId, entityId, "1000", "Aset (Header)", GlAccountTypes.Asset, GlAccountCategories.HeaderGroup, null, false, false, nowUtc, ct);
        var hLiab = await EnsureGlAccountAsync(db, tenantId, entityId, "2000", "Liabilitas (Header)", GlAccountTypes.Liability, GlAccountCategories.HeaderGroup, null, false, false, nowUtc, ct);
        var hEquity = await EnsureGlAccountAsync(db, tenantId, entityId, "3000", "Ekuitas (Header)", GlAccountTypes.Equity, GlAccountCategories.HeaderGroup, null, false, false, nowUtc, ct);
        var hRev = await EnsureGlAccountAsync(db, tenantId, entityId, "4000", "Pendapatan (Header)", GlAccountTypes.Revenue, GlAccountCategories.HeaderGroup, null, false, false, nowUtc, ct);
        var hExp = await EnsureGlAccountAsync(db, tenantId, entityId, "5000", "Beban & HPP (Header)", GlAccountTypes.Expense, GlAccountCategories.HeaderGroup, null, false, false, nowUtc, ct);

        var cash = await EnsureGlAccountAsync(db, tenantId, entityId, "1110", "Kas & Bank Operasional Utama", GlAccountTypes.Asset, GlAccountCategories.CashBank, hAsset.AccountId, true, false, nowUtc, ct);
        var bankClearing = await EnsureGlAccountAsync(db, tenantId, entityId, "1120", "Kliring Pembayaran Bank Keluar", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, hAsset.AccountId, true, true, nowUtc, ct);
        var ar = await EnsureGlAccountAsync(db, tenantId, entityId, "1200", "Piutang Usaha (AR Control)", GlAccountTypes.Asset, GlAccountCategories.AccountsReceivable, hAsset.AccountId, true, true, nowUtc, ct);
        var inv = await EnsureGlAccountAsync(db, tenantId, entityId, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, hAsset.AccountId, true, true, nowUtc, ct);
        var ppnIn = await EnsureGlAccountAsync(db, tenantId, entityId, "1410", "PPN Masukan", GlAccountTypes.Asset, GlAccountCategories.TaxReceivable, hAsset.AccountId, true, false, nowUtc, ct);

        var ap = await EnsureGlAccountAsync(db, tenantId, entityId, "2100", "Utang Usaha (AP Control)", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, hLiab.AccountId, true, true, nowUtc, ct);
        var grni = await EnsureGlAccountAsync(db, tenantId, entityId, "2110", "Akrual Barang Diterima Belum Ditagih (GRNI)", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, hLiab.AccountId, true, true, nowUtc, ct);
        var ppnOut = await EnsureGlAccountAsync(db, tenantId, entityId, "2210", "PPN Keluaran", GlAccountTypes.Liability, GlAccountCategories.TaxPayable, hLiab.AccountId, true, false, nowUtc, ct);
        var pph23 = await EnsureGlAccountAsync(db, tenantId, entityId, "2220", "Utang PPh 23 Dipotong", GlAccountTypes.Liability, GlAccountCategories.WithholdingTaxPayable, hLiab.AccountId, true, false, nowUtc, ct);

        var capital = await EnsureGlAccountAsync(db, tenantId, entityId, "3100", "Modal Disetor", GlAccountTypes.Equity, GlAccountCategories.EquityCapital, hEquity.AccountId, true, false, nowUtc, ct);
        var salesRev = await EnsureGlAccountAsync(db, tenantId, entityId, "4100", "Pendapatan Penjualan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, hRev.AccountId, true, false, nowUtc, ct);
        var cogs = await EnsureGlAccountAsync(db, tenantId, entityId, "5100", "Beban Pokok Penjualan (COGS)", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, hExp.AccountId, true, false, nowUtc, ct);
        var ppv = await EnsureGlAccountAsync(db, tenantId, entityId, "5200", "Selisih Harga Pembelian (PPV)", GlAccountTypes.Expense, GlAccountCategories.PurchasePriceVariance, hExp.AccountId, true, false, nowUtc, ct);
        _ = await EnsureGlAccountAsync(db, tenantId, entityId, "5300", "Beban Operasional Umum", GlAccountTypes.Expense, GlAccountCategories.OperatingExpense, hExp.AccountId, true, false, nowUtc, ct);

        await EnsurePeriodAsync(db, tenantId, entityId, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, nowUtc, ct);
        await EnsurePeriodAsync(db, tenantId, entityId, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, nowUtc, ct);

        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.InventoryAsset, inv, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.GrniClearing, grni, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.ApTradeControl, ap, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.ArTradeControl, ar, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.PurchasePriceVariance, ppv, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.PpnInputTax, ppnIn, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.PpnOutputTax, ppnOut, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.PphWithholdingPayable, pph23, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.BankOutboundClearing, bankClearing, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.BankMainOperating, cash, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.SalesRevenueDefault, salesRev, actorUserId, nowUtc, ct);
        await EnsureMappingAsync(db, tenantId, entityId, GlMappingPurposes.CogsDefault, cogs, actorUserId, nowUtc, ct);

        var effFrom = new DateOnly(2025, 1, 1);
        await EnsureTaxCodeAsync(db, tenantId, entityId, "PPN-IN-11", "PPN Masukan Standar 11%", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, true, false, ppnIn.AccountId, effFrom, "Configurable PPN Input 11% (OD-07)", nowUtc, ct);
        await EnsureTaxCodeAsync(db, tenantId, entityId, "PPN-12-DPP1112", "PPN Masukan 12% DPP 11/12 (Efektif 11%)", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.SpecialDpp1112, 12m, 11, 12, true, false, ppnIn.AccountId, effFrom, "Configurable nominal 12% x 11/12 DPP template (OD-07)", nowUtc, ct);
        await EnsureTaxCodeAsync(db, tenantId, entityId, "PPN-OUT-11", "PPN Keluaran Standar 11%", TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, true, false, ppnOut.AccountId, effFrom, "Configurable PPN Output 11% (OD-07)", nowUtc, ct);
        await EnsureTaxCodeAsync(db, tenantId, entityId, "PPN-NOT-COLLECTED", "PPN Tidak Dipungut / Fasilitas Khusus", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.NotCollectedFacility, 0m, 1, 1, true, false, null, effFrom, "Memerlukan bukti fasilitas perpajakan / FTZ (OD-07)", nowUtc, ct);
        await EnsureTaxCodeAsync(db, tenantId, entityId, "PPH23-SVC-2", "PPh 23 Jasa 2% (Wajib NPWP)", TaxFamilies.Pph, TaxDirections.Withholding, TaxTreatmentClasses.WithholdingDeduction, 2m, 1, 1, true, true, pph23.AccountId, effFrom, "Configurable PPh 23 2% withholding pending OD-07", nowUtc, ct);

        if (seedInitialJournal)
        {
            var hasSeedJournal = await db.JournalEntries.AnyAsync(
                j => j.TenantId == tenantId
                    && j.LegalEntityId == entityId
                    && j.SourceModule == "DEMO_SEED"
                    && j.SourceEventKey == "seed:opening-capital",
                ct);
            if (!hasSeedJournal)
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var outcome = await GlPostingKernel.PostJournalAsync(
                    db,
                    new GlPostJournalCommand(
                        tenantId,
                        entityId,
                        currencyCode,
                        new DateOnly(2026, 11, 1),
                        currencyCode,
                        "Setoran awal modal operasional entitas (Demo S04)",
                        "DEMO_SEED",
                        "OPENING_SEED",
                        "seed-opening-01",
                        "seed:opening-capital",
                        [
                            new(cash.AccountId, "Kas bank operasional", 100_000_000m, 0m),
                            new(capital.AccountId, "Modal disetor awal", 0m, 100_000_000m)
                        ],
                        actorUserId,
                        nowUtc),
                    ct);
                if (!outcome.Succeeded)
                {
                    throw new InvalidOperationException($"Failed seeding sample S04 journal: {outcome.ErrorCode} {outcome.ErrorDetail}");
                }

                await tx.CommitAsync(ct);
            }
        }
    }

    private static async Task<GlAccount> EnsureGlAccountAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string code,
        string name,
        string type,
        string category,
        Guid? parentAccountId,
        bool allowPosting,
        bool isControlAccount,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.GlAccounts.SingleOrDefaultAsync(
            a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountCode == code,
            ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = GlAccount.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            code,
            name,
            type,
            category,
            parentAccountId,
            allowPosting,
            isControlAccount,
            nowUtc);
        db.GlAccounts.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static async Task EnsurePeriodAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string periodCode,
        int fiscalYear,
        int periodNumber,
        DateOnly startDate,
        DateOnly endDate,
        string status,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.FinancialPeriods.AnyAsync(
            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PeriodCode == periodCode,
            ct);
        if (!exists)
        {
            db.FinancialPeriods.Add(FinancialPeriod.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                periodCode,
                fiscalYear,
                periodNumber,
                startDate,
                endDate,
                status,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureMappingAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string purposeCode,
        GlAccount account,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.AccountMappings.AnyAsync(
            m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.PurposeCode == purposeCode,
            ct);
        if (!exists)
        {
            db.AccountMappings.Add(AccountMapping.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                purposeCode,
                account,
                "Seeded S04 standard mapping",
                actorUserId,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureTaxCodeAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string code,
        string name,
        string family,
        string direction,
        string treatment,
        decimal rate,
        int dppNum,
        int dppDen,
        bool reqEvidence,
        bool reqNpwp,
        Guid? accountId,
        DateOnly effectiveFrom,
        string note,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.TaxCodes.AnyAsync(
            t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.Code == code && t.EffectiveFrom == effectiveFrom,
            ct);
        if (!exists)
        {
            db.TaxCodes.Add(TaxCode.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                code,
                name,
                family,
                direction,
                treatment,
                rate,
                dppNum,
                dppDen,
                reqEvidence,
                reqNpwp,
                accountId,
                effectiveFrom,
                null,
                TaxReviewStatuses.UnreviewedConfigurable,
                note,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureTolerancePolicyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string code,
        string name,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var exists = await db.DeliveryTolerancePolicies.AnyAsync(
            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.Code == code,
            ct);
        if (!exists)
        {
            db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(
                Guid.NewGuid(),
                tenantId,
                entityId,
                code,
                name,
                0m,
                0m,
                allowUnlimitedOverdelivery: false,
                isDefault: true,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureApprovedSeedPoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierPartnerId,
        Guid itemId,
        Guid orderedUomId,
        decimal orderedQty,
        decimal unitPrice,
        string destination,
        Guid requesterUserId,
        Guid approverUserId,
        bool approve,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var anyPo = await db.PurchaseOrders.AnyAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId, ct);
        if (anyPo)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CreateAndOptionallyApprovePoAsync(
            db,
            tenantId,
            entityId,
            supplierPartnerId,
            itemId,
            orderedUomId,
            orderedQty,
            unitPrice,
            destination,
            new DateOnly(2026, 11, 1),
            "Seeded S05 purchase commitment",
            "Pengadaan kemasan awal (Demo S05)",
            requesterUserId,
            approverUserId,
            approve,
            nowUtc,
            ct);
        await tx.CommitAsync(ct);
    }

    private static async Task<(Guid PurchaseOrderId, Guid PoLineId)> CreateAndOptionallyApprovePoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierPartnerId,
        Guid itemId,
        Guid orderedUomId,
        decimal orderedQty,
        decimal unitPrice,
        string destination,
        DateOnly orderDate,
        string poNotes,
        string lineDescription,
        Guid requesterUserId,
        Guid approverUserId,
        bool approve,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var taxId = await db.TaxCodes.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.Code == "PPN-IN-11" && t.Status == MasterStatuses.Active)
            .Select(t => (Guid?)t.TaxCodeId)
            .FirstOrDefaultAsync(ct);

        var draftOutcome = await PurchaseOrderWorkflowService.CreateDraftAsync(
            db,
            new CreatePoDraftCommand(
                tenantId,
                entityId,
                "IDR",
                "IDR",
                supplierPartnerId,
                orderDate,
                orderDate.AddDays(14),
                destination,
                30,
                null,
                poNotes,
                [new PoLineInput("L001", itemId, orderedUomId, orderedQty, unitPrice, taxId, 0m, 0m, false, lineDescription)],
                requesterUserId,
                nowUtc),
            ct);

        if (!draftOutcome.Succeeded || draftOutcome.PurchaseOrder is null || draftOutcome.Revision is null)
        {
            throw new InvalidOperationException($"Failed seeding PO draft: {draftOutcome.ErrorCode} {draftOutcome.ErrorDetail}");
        }

        if (approve)
        {
            var submitOutcome = await PurchaseOrderWorkflowService.SubmitRevisionAsync(
                db,
                new SubmitPoRevisionCommand(
                    tenantId,
                    entityId,
                    draftOutcome.PurchaseOrder.PurchaseOrderId,
                    draftOutcome.Revision.RevisionId,
                    null,
                    requesterUserId,
                    nowUtc.AddMinutes(1)),
                ct);
            if (!submitOutcome.Succeeded)
            {
                throw new InvalidOperationException($"Failed seeding PO submit: {submitOutcome.ErrorCode} {submitOutcome.ErrorDetail}");
            }

            var decideOutcome = await PurchaseOrderWorkflowService.DecideRevisionAsync(
                db,
                new DecidePoRevisionCommand(
                    tenantId,
                    entityId,
                    draftOutcome.PurchaseOrder.PurchaseOrderId,
                    draftOutcome.Revision.RevisionId,
                    ApprovalDecisions.Approved,
                    "Disetujui sebagai baseline komitmen PO Demo",
                    approverUserId,
                    nowUtc.AddMinutes(2)),
                ct);
            if (!decideOutcome.Succeeded)
            {
                throw new InvalidOperationException($"Failed seeding PO approval: {decideOutcome.ErrorCode} {decideOutcome.ErrorDetail}");
            }
        }

        var seededLineId = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.RevisionId == draftOutcome.Revision.RevisionId)
            .Select(l => l.PoLineId)
            .SingleAsync(ct);

        return (draftOutcome.PurchaseOrder.PurchaseOrderId, seededLineId);
    }

    private static async Task EnsureSeedGoodsReceiptAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid boxUomId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var anyGrn = await db.GoodsReceipts.AnyAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId, ct);
        if (anyGrn)
        {
            return;
        }

        var po = await db.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.LegalEntityId == entityId &&
                p.Status == PurchaseOrderStatuses.Approved &&
                p.CurrentApprovedRevisionId != null,
                ct);
        if (po?.CurrentApprovedRevisionId is not { } revId)
        {
            return;
        }

        var line = await db.PurchaseOrderLines.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.RevisionId == revId, ct);
        if (line is null)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var arrivalOutcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                tenantId,
                entityId,
                po.PurchaseOrderId,
                "SJ-SBY-DEMO-001",
                "seed:sby-gate1-01",
                new DateOnly(2026, 11, 2),
                "Dermaga Barat Surabaya",
                "L 9042 UB · Segel SBY-11",
                "DO-SBY-20261102.pdf",
                "Penerimaan parsial 1 BOX menunggu inspeksi fisik Gate 1 (tanpa GRNI)",
                [new ArrivalLineInput(line.PoLineId, boxUomId, 1m, true, DiscrepancyReasonCodes.None, "Segel palet utama utuh, menunggu verifikasi QA", null)],
                actorUserId,
                nowUtc),
            ct);
        if (!arrivalOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S06 Gate 1 receipt: {arrivalOutcome.ErrorCode} {arrivalOutcome.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }

    private static async Task EnsureS07InventoryDemoDataAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierPartnerId,
        Guid steelItemId,
        Guid pcsUomId,
        Guid requesterUserId,
        Guid approverUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var alreadySeeded = await db.ValuedAcceptances.AnyAsync(a => a.TenantId == tenantId && a.LegalEntityId == entityId, ct);
        if (alreadySeeded)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Batch 1 (10 PCS @ Rp 850.000) -> already valued-accepted as opening moving-average pool & GRNI accrual
        var (po1Id, po1LineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            tenantId,
            entityId,
            supplierPartnerId,
            steelItemId,
            pcsUomId,
            10m,
            850_000m,
            "Gudang Pusat Jakarta",
            new DateOnly(2026, 11, 5),
            "Komitmen pengadaan pelat baja batch 1 (Demo S07)",
            "Pelat Baja 10mm Grade SS400 — Batch 1",
            requesterUserId,
            approverUserId,
            approve: true,
            nowUtc,
            ct);

        var gr1Outcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                tenantId,
                entityId,
                po1Id,
                "SJ-JKT-DEMO-S07-01",
                "seed:jkt-s07-gr1",
                new DateOnly(2026, 11, 5),
                "Dermaga Utama Jakarta",
                "B 8821 KTR · Segel JKT-07",
                "DO-JKT-20261105.pdf",
                "Batch 1 pelat baja lolos cek fisik awal menuju Gate 2",
                [new ArrivalLineInput(po1LineId, pcsUomId, 10m, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                nowUtc.AddMinutes(3)),
            ct);

        if (!gr1Outcome.Succeeded || gr1Outcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S07 Gate 1 batch 1: {gr1Outcome.ErrorCode} {gr1Outcome.ErrorDetail}");
        }

        var gr1LineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.GoodsReceiptId == gr1Outcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dock1Outcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                tenantId,
                entityId,
                gr1Outcome.Receipt.GoodsReceiptId,
                gr1LineId,
                "seed:jkt-s07-dc1",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 10m,
                DockRejectedBaseQuantity: 0m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.None,
                InspectionNotes: "Lolos cek fisik lengkap 10 PCS untuk masuk ke Gate 2",
                EvidenceReference: "QC-JKT-20261105-01",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(3).AddSeconds(30)),
            ct);

        if (!dock1Outcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S07 Gate 1 release 1: {dock1Outcome.ErrorCode} {dock1Outcome.ErrorDetail}");
        }

        var acc1Outcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                tenantId,
                entityId,
                "IDR",
                gr1Outcome.Receipt.GoodsReceiptId,
                "seed:jkt-s07-acc1",
                new DateOnly(2026, 11, 5),
                StockStatuses.Unrestricted,
                "Penerimaan bernilai Batch 1 pelat baja untuk baseline biaya rata-rata bergerak",
                [new AcceptReceiptLineInput(gr1LineId, 10m)],
                approverUserId,
                nowUtc.AddMinutes(4)),
            ct);

        if (!acc1Outcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S07 Gate 2 acceptance 1: {acc1Outcome.ErrorCode} {acc1Outcome.ErrorDetail}");
        }

        // Batch 2 (10 PCS @ Rp 950.000) -> released at Gate 1 and ready for operator Gate 2 preview & acceptance
        var (po2Id, po2LineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            tenantId,
            entityId,
            supplierPartnerId,
            steelItemId,
            pcsUomId,
            10m,
            950_000m,
            "Gudang Pusat Jakarta",
            new DateOnly(2026, 11, 6),
            "Komitmen pengadaan pelat baja batch 2 (Demo S07)",
            "Pelat Baja 10mm Grade SS400 — Batch 2",
            requesterUserId,
            approverUserId,
            approve: true,
            nowUtc.AddMinutes(5),
            ct);

        var gr2Outcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                tenantId,
                entityId,
                po2Id,
                "SJ-JKT-DEMO-S07-02",
                "seed:jkt-s07-gr2",
                new DateOnly(2026, 11, 6),
                "Dermaga Utama Jakarta",
                "B 9104 JKT · Segel JKT-08",
                "DO-JKT-20261106.pdf",
                "Batch 2 pelat baja dirilis Gate 1, siap ditinjau dan diterima di Gate 2",
                [new ArrivalLineInput(po2LineId, pcsUomId, 10m, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                nowUtc.AddMinutes(8)),
            ct);

        if (!gr2Outcome.Succeeded || gr2Outcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S07 Gate 1 batch 2: {gr2Outcome.ErrorCode} {gr2Outcome.ErrorDetail}");
        }

        var gr2LineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.GoodsReceiptId == gr2Outcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dock2Outcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                tenantId,
                entityId,
                gr2Outcome.Receipt.GoodsReceiptId,
                gr2LineId,
                "seed:jkt-s07-dc2",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 10m,
                DockRejectedBaseQuantity: 0m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.None,
                InspectionNotes: "Lolos cek fisik lengkap 10 PCS, siap preview dan penerimaan Gate 2",
                EvidenceReference: "QC-JKT-20261106-02",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(9)),
            ct);

        if (!dock2Outcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S07 Gate 1 release 2: {dock2Outcome.ErrorCode} {dock2Outcome.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }

    private static async Task EnsureS08WarehouseAndQualityDataAsync(
        AtheraDbContext db,
        Guid nusaTenantId,
        Guid nusaJktId,
        Guid nusaSbyId,
        Guid smdrTenantId,
        Guid smdrBdgId,
        Guid nusaSupplierId,
        Guid pcsUomId,
        Guid requesterUserId,
        Guid inspectorApproverUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        // Initialize WH-MAIN + default locations across all demo entities cleanly (zero stock on NUSA-SBY).
        foreach (var (tid, eid) in new[] { (nusaTenantId, nusaJktId), (nusaTenantId, nusaSbyId), (smdrTenantId, smdrBdgId) })
        {
            await using var whTx = await db.Database.BeginTransactionAsync(ct);
            await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tid, eid, nowUtc, ct);
            await whTx.CommitAsync(ct);
        }

        // Seed secondary rack LOC-RACK-B2 on NUSA-JKT for location transfer demonstrations.
        var jktLayout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, nusaTenantId, nusaJktId, nowUtc, ct);
        if (!jktLayout.LocationsByCode.ContainsKey("LOC-RACK-B2"))
        {
            db.WarehouseLocations.Add(WarehouseLocation.Create(
                Guid.NewGuid(),
                nusaTenantId,
                nusaJktId,
                jktLayout.DefaultWarehouse.WarehouseId,
                "LOC-RACK-B2",
                "Rak Penyimpanan Sekunder B2",
                LocationZoneTypes.Storage,
                isDefaultReceiving: false,
                nowUtc));
            await db.SaveChangesAsync(ct);
        }

        var chemItem = await EnsureItemAsync(
            db,
            nusaTenantId,
            "ITM-CHEM-LOT-01",
            "Pelarut Industri Grade A",
            ItemKinds.StockedGood,
            pcsUomId,
            pcsUomId,
            pcsUomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct,
            LotTrackingPolicies.Required);

        var alreadySeededLot = await db.StockLots.AnyAsync(
            l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.ItemId == chemItem.ItemId && l.LotNumber == "LOT-JKT-2611-01",
            ct);
        if (alreadySeededLot)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (poId, poLineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            nusaTenantId,
            nusaJktId,
            nusaSupplierId,
            chemItem.ItemId,
            pcsUomId,
            20m,
            320_000m,
            "Gudang Pusat Jakarta",
            new DateOnly(2026, 11, 7),
            "Pengadaan pelarut wajib lot untuk karantina mutu (Demo S08)",
            "Pelarut Industri Grade A — Batch LOT-JKT-2611-01",
            requesterUserId,
            inspectorApproverUserId,
            approve: true,
            nowUtc.AddMinutes(10),
            ct);

        var grOutcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                nusaTenantId,
                nusaJktId,
                poId,
                "SJ-JKT-DEMO-S08-01",
                "seed:jkt-s08-gr1",
                new DateOnly(2026, 11, 7),
                "Dermaga Utama Jakarta",
                "B 7742 JKT · Segel JKT-08B",
                "DO-JKT-20261107-COA.pdf",
                "Batch pelarut kimia lolos cek fisik, menunggu pemeriksaan mutu di Gate 2",
                [new ArrivalLineInput(poLineId, pcsUomId, 20m, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                nowUtc.AddMinutes(11)),
            ct);

        if (!grOutcome.Succeeded || grOutcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S08 Gate 1 receipt: {grOutcome.ErrorCode} {grOutcome.ErrorDetail}");
        }

        var grLineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.GoodsReceiptId == grOutcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dockOutcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                nusaTenantId,
                nusaJktId,
                grOutcome.Receipt.GoodsReceiptId,
                grLineId,
                "seed:jkt-s08-dc1",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 20m,
                DockRejectedBaseQuantity: 0m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.None,
                InspectionNotes: "Kemasan drum utuh, dirilis ke karantina mutu Gate 2",
                EvidenceReference: "QC-JKT-20261107-COA",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(12)),
            ct);

        if (!dockOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S08 Gate 1 release: {dockOutcome.ErrorCode} {dockOutcome.ErrorDetail}");
        }

        var accOutcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                grOutcome.Receipt.GoodsReceiptId,
                "seed:jkt-s08-acc1",
                new DateOnly(2026, 11, 7),
                StockStatuses.QualityInspection,
                "Penerimaan bernilai ke Zona Karantina (QUALITY_INSPECTION) menunggu inspeksi QC",
                [new AcceptReceiptLineInput(grLineId, 20m, null, "LOT-JKT-2611-01", "SUP-CHEM-2611-A", new DateOnly(2026, 11, 1), new DateOnly(2027, 11, 30))],
                inspectorApproverUserId,
                nowUtc.AddMinutes(13)),
            ct);

        if (!accOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S08 Gate 2 quarantine acceptance: {accOutcome.ErrorCode} {accOutcome.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }

    private static async Task EnsureS09PayablesDemoDataAsync(
        AtheraDbContext db,
        Guid nusaTenantId,
        Guid nusaJktId,
        Guid nusaSupplierId,
        Guid pcsUomId,
        Guid requesterUserId,
        Guid approverUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var valveItem = await EnsureItemAsync(
            db,
            nusaTenantId,
            "ITM-S09-VALVE-01",
            "Katup Industri PN16 50mm",
            ItemKinds.StockedGood,
            pcsUomId,
            pcsUomId,
            pcsUomId,
            EntityScopeModes.AllEntities,
            eligibleEntityId: null,
            nowUtc,
            ct,
            LotTrackingPolicies.None);

        var alreadySeededAccrual = await db.ReceiptAccrualLines.AnyAsync(
            l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.ItemId == valveItem.ItemId,
            ct);
        if (alreadySeededAccrual)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (poId, poLineId) = await CreateAndOptionallyApprovePoAsync(
            db,
            nusaTenantId,
            nusaJktId,
            nusaSupplierId,
            valveItem.ItemId,
            pcsUomId,
            100m,
            50_000m,
            "Gudang Pusat Jakarta",
            new DateOnly(2026, 11, 8),
            "Pengadaan katup industri PN16 untuk stok & demonstrasi 3-way match (Demo S09)",
            "Katup Industri PN16 50mm — Batch Penerimaan S09",
            requesterUserId,
            approverUserId,
            approve: true,
            nowUtc.AddMinutes(15),
            ct);

        var grOutcome = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                nusaTenantId,
                nusaJktId,
                poId,
                "SJ-JKT-DEMO-S09-01",
                "seed:jkt-s09-gr1",
                new DateOnly(2026, 11, 8),
                "Dermaga Utama Jakarta",
                "B 8890 JKT · Segel JKT-09A",
                "DO-JKT-20261108-VLV.pdf",
                "Kedatangan 100 PCS Katup Industri PN16 50mm (Demo S09)",
                [new ArrivalLineInput(poLineId, pcsUomId, 100m, false, DiscrepancyReasonCodes.None, null, null)],
                requesterUserId,
                nowUtc.AddMinutes(16)),
            ct);

        if (!grOutcome.Succeeded || grOutcome.Receipt is null)
        {
            throw new InvalidOperationException($"Failed seeding S09 Gate 1 receipt: {grOutcome.ErrorCode} {grOutcome.ErrorDetail}");
        }

        var grLineId = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == nusaTenantId && l.LegalEntityId == nusaJktId && l.GoodsReceiptId == grOutcome.Receipt.GoodsReceiptId)
            .Select(l => l.GoodsReceiptLineId)
            .SingleAsync(ct);

        var dockOutcome = await GoodsReceiptWorkflowService.RecordDockCheckAsync(
            db,
            new RecordDockCheckCommand(
                nusaTenantId,
                nusaJktId,
                grOutcome.Receipt.GoodsReceiptId,
                grLineId,
                "seed:jkt-s09-dc1",
                PendingCheckBaseQuantity: 0m,
                DockHeldBaseQuantity: 0m,
                DockReleasedBaseQuantity: 100m,
                DockRejectedBaseQuantity: 0m,
                DiscrepancyReasonCode: DiscrepancyReasonCodes.None,
                InspectionNotes: "100 PCS utuh dan siap diterima ke persediaan bebas",
                EvidenceReference: "QC-JKT-20261108-VLV",
                ActorUserId: requesterUserId,
                NowUtc: nowUtc.AddMinutes(17)),
            ct);

        if (!dockOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S09 Gate 1 release: {dockOutcome.ErrorCode} {dockOutcome.ErrorDetail}");
        }

        var accOutcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
            db,
            new AcceptReceiptCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                grOutcome.Receipt.GoodsReceiptId,
                "seed:jkt-s09-acc1",
                new DateOnly(2026, 11, 8),
                StockStatuses.Unrestricted,
                "Penerimaan bernilai 100 PCS @ Rp 50.000 (Rp 5.000.000) ke persediaan bebas",
                [new AcceptReceiptLineInput(grLineId, 100m, null, null, null, null, null)],
                approverUserId,
                nowUtc.AddMinutes(18)),
            ct);

        if (!accOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S09 Gate 2 acceptance: {accOutcome.ErrorCode} {accOutcome.ErrorDetail}");
        }

        var issueOutcome = await InventoryValuationService.PostOperationalIssueAsync(
            db,
            new PostOperationalIssueCommand(
                nusaTenantId,
                nusaJktId,
                "IDR",
                valveItem.ItemId,
                LocationId: null,
                StockLotId: null,
                Quantity: 60m,
                PostingDate: new DateOnly(2026, 11, 9),
                SourceReference: "ISS-JKT-DEMO-S09-01",
                IdempotencyKey: "seed:jkt-s09-iss1",
                Notes: "Pemakaian operasional 60 PCS (menyisakan 40 PCS on-hand vs 100 PCS GRNI terbuka)",
                ActorUserId: approverUserId,
                NowUtc: nowUtc.AddMinutes(19)),
            ct);

        if (!issueOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Failed seeding S09 operational issue: {issueOutcome.ErrorCode} {issueOutcome.ErrorDetail}");
        }

        await tx.CommitAsync(ct);
    }
}
