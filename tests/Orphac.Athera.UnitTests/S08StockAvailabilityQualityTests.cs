using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Quality;

namespace Orphac.Athera.UnitTests;

public sealed class S08StockAvailabilityQualityTests
{
    [Fact]
    public void StockPosition_StatusTransitionsAndAvailableProtection_EnforceOd10()
    {
        var now = DateTimeOffset.UtcNow;
        var pos = StockPosition.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);

        pos.ApplyInboundReceipt(10m, StockStatuses.QualityInspection, now.AddMinutes(1));
        Assert.Equal(0m, pos.UnrestrictedBaseQuantity);
        Assert.Equal(10m, pos.QualityInspectionBaseQuantity);
        Assert.Equal(10m, pos.TotalValuedOnHandBaseQuantity);
        Assert.Equal(0m, pos.AvailableBaseQuantity);

        pos.ApplyStatusTransition(StockStatuses.QualityInspection, StockStatuses.Unrestricted, 7m, now.AddMinutes(2));
        Assert.Equal(7m, pos.UnrestrictedBaseQuantity);
        Assert.Equal(3m, pos.QualityInspectionBaseQuantity);
        Assert.Equal(7m, pos.AvailableBaseQuantity);

        pos.ApplyStatusTransition(StockStatuses.QualityInspection, StockStatuses.Blocked, 3m, now.AddMinutes(3));
        Assert.Equal(3m, pos.BlockedBaseQuantity);
        Assert.Equal(0m, pos.QualityInspectionBaseQuantity);
        Assert.Equal(7m, pos.AvailableBaseQuantity);

        pos.ApplyReservationDelta(5m, now.AddMinutes(4));
        Assert.Equal(5m, pos.ReservedBaseQuantity);
        Assert.Equal(2m, pos.AvailableBaseQuantity);

        var reservedBlockEx = Assert.Throws<InvalidOperationException>(() =>
            pos.ApplyStatusTransition(StockStatuses.Unrestricted, StockStatuses.Blocked, 3m, now.AddMinutes(5)));
        Assert.Contains("od10_insufficient_available_stock", reservedBlockEx.Message, StringComparison.Ordinal);

        pos.ApplyReservationDelta(-3m, now.AddMinutes(6));
        Assert.Equal(2m, pos.ReservedBaseQuantity);
        Assert.Equal(5m, pos.AvailableBaseQuantity);

        pos.ApplyStatusTransition(StockStatuses.Blocked, StockStatuses.Unrestricted, 2m, now.AddMinutes(7));
        Assert.Equal(9m, pos.UnrestrictedBaseQuantity);
        Assert.Equal(1m, pos.BlockedBaseQuantity);
        Assert.Equal(2m, pos.ReservedBaseQuantity);
        Assert.Equal(7m, pos.AvailableBaseQuantity);
        Assert.Equal(10m, pos.TotalValuedOnHandBaseQuantity);
    }

    [Fact]
    public void LocationBalanceAndReservation_Od10NoNegativeAndNoOverReserve_Enforced()
    {
        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var posId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var unrestricted = StockLocationBalance.CreateInitial(
            Guid.NewGuid(), tenantId, entityId, posId, itemId, uomId, whId, locId, null, StockStatuses.Unrestricted, now);
        unrestricted.AdjustOnHand(10m, now.AddMinutes(1));
        Assert.Equal(10m, unrestricted.OnHandBaseQuantity);
        Assert.Equal(10m, unrestricted.AvailableBaseQuantity);

        var payloadHash = ReservationHash.ComputePayloadHash(unrestricted.StockLocationBalanceId, ReservationSourceTypes.SalesOrder, "SO-2026-1001", 7m);
        var reservation = StockReservation.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            "RSV-2026-00001",
            unrestricted,
            ReservationSourceTypes.SalesOrder,
            null,
            "SO-2026-1001",
            null,
            "idem-rsv-1",
            payloadHash,
            7m,
            "Hold for SO-1001",
            now.AddDays(5),
            actorId,
            now.AddMinutes(2));

        unrestricted.ApplyReservationDelta(7m, now.AddMinutes(2));
        Assert.Equal(7m, unrestricted.ReservedBaseQuantity);
        Assert.Equal(3m, unrestricted.AvailableBaseQuantity);

        var overReserveEx = Assert.Throws<InvalidOperationException>(() =>
            unrestricted.ApplyReservationDelta(4m, now.AddMinutes(3)));
        Assert.Contains("od10_insufficient_available_stock", overReserveEx.Message, StringComparison.Ordinal);

        var underflowReserveEx = Assert.Throws<InvalidOperationException>(() =>
            unrestricted.AdjustOnHand(-4m, now.AddMinutes(3)));
        Assert.Contains("od10_insufficient_available_stock", underflowReserveEx.Message, StringComparison.Ordinal);

        var negativeStockEx = Assert.Throws<InvalidOperationException>(() =>
            unrestricted.AdjustOnHand(-12m, now.AddMinutes(3)));
        Assert.Contains("od10_negative_stock_forbidden", negativeStockEx.Message, StringComparison.Ordinal);

        var quarantine = StockLocationBalance.CreateInitial(
            Guid.NewGuid(), tenantId, entityId, posId, itemId, uomId, whId, Guid.NewGuid(), null, StockStatuses.QualityInspection, now);
        quarantine.AdjustOnHand(5m, now.AddMinutes(1));
        var restrictedResEx = Assert.Throws<InvalidOperationException>(() =>
            StockReservation.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                "RSV-2026-00002",
                quarantine,
                ReservationSourceTypes.ManualHold,
                null,
                "HOLD-01",
                null,
                "idem-rsv-2",
                "hash-2",
                2m,
                null,
                null,
                actorId,
                now.AddMinutes(2)));
        Assert.Contains("od10_restricted_stock_not_reservable", restrictedResEx.Message, StringComparison.Ordinal);

        var releasedPart = reservation.Release(3m, actorId, now.AddMinutes(4));
        Assert.Equal(3m, releasedPart);
        unrestricted.ApplyReservationDelta(-releasedPart, now.AddMinutes(4));
        Assert.Equal(4m, reservation.RemainingReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.Active, reservation.Status);
        Assert.Equal(6m, unrestricted.AvailableBaseQuantity);

        var releasedRest = reservation.Release(null, actorId, now.AddMinutes(5));
        Assert.Equal(4m, releasedRest);
        unrestricted.ApplyReservationDelta(-releasedRest, now.AddMinutes(5));
        Assert.Equal(0m, reservation.RemainingReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.Released, reservation.Status);
        Assert.Equal(10m, unrestricted.AvailableBaseQuantity);

        var closedEx = Assert.Throws<InvalidOperationException>(() =>
            reservation.Release(1m, actorId, now.AddMinutes(6)));
        Assert.Contains("reservation_already_closed", closedEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogItem_LotTrackingPolicies_AllowStockedAndRejectNonStockService()
    {
        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        var uomId = Guid.NewGuid();

        var stocked = Item.Create(
            Guid.NewGuid(),
            tenantId,
            "ITM-LOT-TST",
            "Reagen Kimia S08",
            ItemKinds.StockedGood,
            uomId,
            uomId,
            uomId,
            EntityScopeModes.AllEntities,
            now,
            LotTrackingPolicies.Optional);
        Assert.Equal(LotTrackingPolicies.Optional, stocked.LotTrackingPolicy);

        stocked.UpdateDetails("Reagen Kimia S08", ItemKinds.StockedGood, uomId, uomId, EntityScopeModes.AllEntities, now.AddMinutes(1), LotTrackingPolicies.Required);
        Assert.Equal(LotTrackingPolicies.Required, stocked.LotTrackingPolicy);

        Assert.Throws<ArgumentException>(() =>
            stocked.UpdateDetails("Reagen Kimia S08", ItemKinds.NonStockService, uomId, uomId, EntityScopeModes.AllEntities, now.AddMinutes(2)));

        Assert.Throws<ArgumentException>(() =>
            Item.Create(
                Guid.NewGuid(),
                tenantId,
                "SVC-LOT-BAD",
                "Jasa Kalibrasi",
                ItemKinds.NonStockService,
                uomId,
                uomId,
                uomId,
                EntityScopeModes.AllEntities,
                now,
                LotTrackingPolicies.Required));
    }

    [Fact]
    public void ReceiptInspection_PartialAndFinalDisposition_ConservesQuantitiesAndBlocksOverDisposition()
    {
        var now = DateTimeOffset.UtcNow;
        var insp = ReceiptInspection.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "QCI-2026-00001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            20m,
            Guid.NewGuid(),
            now);

        Assert.Equal(ReceiptInspectionStatuses.Pending, insp.Status);
        Assert.Equal(20m, insp.PendingBaseQuantity);

        insp.ApplyDisposition(12m, 3m, now.AddMinutes(1));
        Assert.Equal(ReceiptInspectionStatuses.PartiallyDecided, insp.Status);
        Assert.Equal(12m, insp.ReleasedBaseQuantity);
        Assert.Equal(3m, insp.BlockedBaseQuantity);
        Assert.Equal(5m, insp.PendingBaseQuantity);
        Assert.Equal(insp.InspectedTotalBaseQuantity, insp.ReleasedBaseQuantity + insp.BlockedBaseQuantity + insp.PendingBaseQuantity);

        var overEx = Assert.Throws<InvalidOperationException>(() =>
            insp.ApplyDisposition(4m, 2m, now.AddMinutes(2)));
        Assert.Contains("disposition_exceeds_pending_quantity", overEx.Message, StringComparison.Ordinal);

        insp.ApplyDisposition(5m, 0m, now.AddMinutes(3));
        Assert.Equal(ReceiptInspectionStatuses.Completed, insp.Status);
        Assert.Equal(17m, insp.ReleasedBaseQuantity);
        Assert.Equal(3m, insp.BlockedBaseQuantity);
        Assert.Equal(0m, insp.PendingBaseQuantity);
    }

    [Fact]
    public void RoleTemplates_SegregateWarehouseClerkAndQualityInspectorDuties()
    {
        var clerkPerms = RoleTemplates.GetPermissions(RoleTemplates.InventoryClerk);
        Assert.Contains(PermissionCodes.InventoryManage, clerkPerms);
        Assert.Contains(PermissionCodes.InventoryRead, clerkPerms);
        Assert.DoesNotContain(PermissionCodes.QualityInspect, clerkPerms);

        var qcPerms = RoleTemplates.GetPermissions(RoleTemplates.QualityInspector);
        Assert.Contains(PermissionCodes.InventoryRead, qcPerms);
        Assert.Contains(PermissionCodes.QualityInspect, qcPerms);
        Assert.DoesNotContain(PermissionCodes.InventoryManage, qcPerms);
    }
}
