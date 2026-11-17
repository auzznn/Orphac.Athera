using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.UnitTests;

public sealed class S03PartnersCatalogueUomTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Partner_NormalizesCodeRequiresRoleAndEnforcesEntityScopeAndDeactivation()
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var entityJkt = Guid.NewGuid();
        var entitySby = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            Partner.Create(partnerId, tenantId, "pt-none", "PT Invalid", "Invalid", false, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, Now));

        var partner = Partner.Create(
            partnerId,
            tenantId,
            " pt-dual-01 ",
            "PT Mitra Niaga Nusantara",
            "Mitra Niaga",
            isSupplier: true,
            isCustomer: true,
            npwp: "01.234.567.8-011.000",
            taxPkpStatus: TaxPkpStatuses.Pkp,
            entityScopeMode: EntityScopeModes.RestrictedEntities,
            nowUtc: Now);

        Assert.Equal("PT-DUAL-01", partner.PartnerCode);
        Assert.Equal(PartnerStatuses.Active, partner.Status);

        var (supNoProfile, cusNoProfile) = PartnerEligibilityEvaluator.Evaluate(partner, null);
        Assert.False(supNoProfile);
        Assert.False(cusNoProfile);

        var jktProfile = PartnerEntityProfile.CreateForPartner(
            Guid.NewGuid(),
            partner,
            entityJkt,
            supplierEligible: true,
            customerEligible: false,
            defaultPaymentTermsDays: 30,
            notes: "Jakarta supply only",
            nowUtc: Now);

        var (supJkt, cusJkt) = PartnerEligibilityEvaluator.Evaluate(partner, jktProfile);
        Assert.True(supJkt);
        Assert.False(cusJkt);

        var supplierOnly = Partner.Create(
            Guid.NewGuid(),
            tenantId,
            "SUP-ONLY",
            "CV Supplier Only",
            "Supplier Only",
            isSupplier: true,
            isCustomer: false,
            npwp: null,
            taxPkpStatus: TaxPkpStatuses.NonPkp,
            entityScopeMode: EntityScopeModes.AllEntities,
            nowUtc: Now);

        Assert.Throws<InvalidOperationException>(() =>
            PartnerEntityProfile.CreateForPartner(Guid.NewGuid(), supplierOnly, entitySby, true, true, 14, null, Now));

        var (supAllDefault, cusAllDefault) = PartnerEligibilityEvaluator.Evaluate(supplierOnly, null);
        Assert.True(supAllDefault);
        Assert.False(cusAllDefault);

        partner.Deactivate(Now.AddMinutes(5));
        Assert.Equal(PartnerStatuses.Inactive, partner.Status);
        var (supDeactivated, _) = PartnerEligibilityEvaluator.Evaluate(partner, jktProfile);
        Assert.False(supDeactivated);
    }

    [Fact]
    public void UomConverter_PerformsExactRoundTripRejectsFractionalDiscreteAndCapturesImmutableSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, " pcs ", "Pieces", 0, Now);
        var box = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, "box", "Box Karton", 0, Now);
        var kg = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, "kg", "Kilogram", 3, Now);
        var unregisteredPallet = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, "plt", "Pallet", 0, Now);

        Assert.Equal("PCS", pcs.UomCode);
        Assert.Throws<ArgumentException>(() => UnitOfMeasure.Create(Guid.NewGuid(), tenantId, "bad", "Bad", 7, Now));

        var item = Item.Create(
            Guid.NewGuid(),
            tenantId,
            " itm-pkg-01 ",
            "Karton Lipat Polos 40x30x25 cm",
            ItemKinds.StockedGood,
            pcs.UomId,
            box.UomId,
            pcs.UomId,
            EntityScopeModes.AllEntities,
            Now);

        Assert.Equal("ITM-PKG-01", item.ItemCode);

        var boxConv = ItemUom.Create(Guid.NewGuid(), tenantId, item.ItemId, box.UomId, 24m, Now);
        Assert.Throws<ArgumentException>(() => ItemUom.Create(Guid.NewGuid(), tenantId, item.ItemId, box.UomId, 0m, Now));

        // 3 BOX -> 72 PCS -> 3 BOX exact round trip
        var toBase = UomConverter.ConvertToBase(item, pcs, box, boxConv, 3m);
        Assert.Equal(72m, toBase.ConvertedBaseQuantity);
        Assert.Equal(0m, toBase.RoundingResidual);
        Assert.Equal("PCS", toBase.BaseUomCode);

        var backToBox = UomConverter.ConvertFromBase(item, pcs, box, boxConv, toBase.ConvertedBaseQuantity);
        Assert.Equal(3m, backToBox);

        // Discrete base (scale 0) must reject 0.1 BOX * 24 = 2.4 PCS
        var fracEx = Assert.Throws<InvalidOperationException>(() => UomConverter.ConvertToBase(item, pcs, box, boxConv, 0.1m));
        Assert.Contains("fractional_discrete_base_quantity", fracEx.Message);

        // Unconfigured UoM must fail without guessing
        var missingEx = Assert.Throws<InvalidOperationException>(() => UomConverter.ConvertToBase(item, pcs, unregisteredPallet, null, 1m));
        Assert.Contains("unsupported_uom_conversion", missingEx.Message);

        // Continuous base unit (KG scale 3) rounds AwayFromZero deterministically
        var kgItem = Item.Create(Guid.NewGuid(), tenantId, "ITM-BULK", "Resin Industri Netral", ItemKinds.StockedGood, kg.UomId, null, null, EntityScopeModes.AllEntities, Now);
        var gram = UnitOfMeasure.Create(Guid.NewGuid(), tenantId, "g", "Gram", 0, Now);
        var gramConv = ItemUom.Create(Guid.NewGuid(), tenantId, kgItem.ItemId, gram.UomId, 0.001m, Now);
        var continuous = UomConverter.ConvertToBase(kgItem, kg, gram, gramConv, 12.5m);
        Assert.Equal(0.013m, continuous.ConvertedBaseQuantity);
        Assert.Equal(-0.0005m, continuous.RoundingResidual);

        // Immutable transaction snapshot stays intact even after item rename and conversion change
        var snapshot = UomConverter.CaptureSnapshot(item, pcs, box, boxConv, 3m, Now);
        item.UpdateDetails("Karton Nama Baru", ItemKinds.StockedGood, box.UomId, pcs.UomId, EntityScopeModes.RestrictedEntities, Now.AddMinutes(10));
        boxConv.UpdateFactor(12m, CatalogStatuses.Active, Now.AddMinutes(10));

        Assert.Equal("Karton Lipat Polos 40x30x25 cm", snapshot.ItemName);
        Assert.Equal(24m, snapshot.FactorToBase);
        Assert.Equal(72m, snapshot.ConvertedBaseQuantity);
    }

    [Fact]
    public void S03PermissionCodesAndRoleTemplates_ExposeMasterDataAndSalesOfficerSafely()
    {
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.PartnersRead));
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.PartnersManage));
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.CatalogRead));
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.CatalogManage));
        Assert.True(RoleTemplates.IsKnown(RoleTemplates.SalesOfficer));

        var procurement = RoleTemplates.GetPermissions(RoleTemplates.ProcurementOfficer);
        Assert.Contains(PermissionCodes.PartnersManage, procurement);
        Assert.Contains(PermissionCodes.CatalogManage, procurement);

        var sales = RoleTemplates.GetPermissions(RoleTemplates.SalesOfficer);
        Assert.Contains(PermissionCodes.SalesRead, sales);
        Assert.Contains(PermissionCodes.PartnersRead, sales);
        Assert.Contains(PermissionCodes.CatalogRead, sales);
        Assert.DoesNotContain(PermissionCodes.AdminManage, sales);
    }
}
