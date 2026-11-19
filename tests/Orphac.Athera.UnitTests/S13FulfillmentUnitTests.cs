using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Sales;

namespace Orphac.Athera.UnitTests;

public sealed class S13FulfillmentUnitTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    private static readonly Guid EntityId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa101");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S13_UNIT_01_ReservationConsumeAndLocationPositionConservation()
    {
        var itemId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var position = StockPosition.CreateInitial(Guid.NewGuid(), TenantId, EntityId, itemId, uomId, Now);
        position.ApplyInboundReceipt(40m, StockStatuses.Unrestricted, Now);

        var balance = StockLocationBalance.CreateInitial(
            Guid.NewGuid(), TenantId, EntityId, position.StockPositionId, itemId, uomId, whId, locId, null, StockStatuses.Unrestricted, Now);
        balance.AdjustOnHand(40m, Now);

        var reservation = StockReservation.Create(
            Guid.NewGuid(), TenantId, EntityId, "RSV-2026-00001", balance,
            ReservationSourceTypes.SalesOrder, Guid.NewGuid(), "SO-2026-00001", Guid.NewGuid(),
            "rsv-key-1", "hash1234", 10m, "Reserve 10 PCS", null, UserId, Now);

        balance.ApplyReservationDelta(10m, Now);
        position.ApplyReservationDelta(10m, Now);

        Assert.Equal(40m, balance.OnHandBaseQuantity);
        Assert.Equal(10m, balance.ReservedBaseQuantity);
        Assert.Equal(30m, balance.AvailableBaseQuantity);
        Assert.Equal(30m, position.AvailableBaseQuantity);

        // Partial dispatch consume of 4 PCS
        var consumed = reservation.Consume(4m, UserId, Now.AddMinutes(5));
        balance.ConsumeReservedOnHand(4m, Now.AddMinutes(5));
        position.ConsumeReservedOutboundIssue(4m, Now.AddMinutes(5));

        Assert.Equal(4m, consumed);
        Assert.Equal(4m, reservation.ConsumedBaseQuantity);
        Assert.Equal(6m, reservation.RemainingReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.PartiallyConsumed, reservation.Status);

        // On-hand drops by 4 (40 -> 36), reserved drops by 4 (10 -> 6), available stays conserved at 30!
        Assert.Equal(36m, balance.OnHandBaseQuantity);
        Assert.Equal(6m, balance.ReservedBaseQuantity);
        Assert.Equal(30m, balance.AvailableBaseQuantity);
        Assert.Equal(36m, position.UnrestrictedBaseQuantity);
        Assert.Equal(6m, position.ReservedBaseQuantity);
        Assert.Equal(30m, position.AvailableBaseQuantity);

        // Attempting to consume more than remaining 6 PCS fails
        var ex = Assert.Throws<InvalidOperationException>(() => reservation.Consume(7m, UserId, Now.AddMinutes(6)));
        Assert.StartsWith("dispatch_exceeds_reservation:", ex.Message, StringComparison.Ordinal);

        // Consuming remaining 6 PCS fulfills reservation
        reservation.Consume(6m, UserId, Now.AddMinutes(7));
        balance.ConsumeReservedOnHand(6m, Now.AddMinutes(7));
        position.ConsumeReservedOutboundIssue(6m, Now.AddMinutes(7));

        Assert.Equal(ReservationStatuses.Fulfilled, reservation.Status);
        Assert.Equal(0m, reservation.RemainingReservedBaseQuantity);
        Assert.Equal(30m, balance.OnHandBaseQuantity);
        Assert.Equal(0m, balance.ReservedBaseQuantity);
        Assert.Equal(30m, balance.AvailableBaseQuantity);
    }

    [Fact]
    public void S13_UNIT_02_PickOrderAndSalesShipmentStateMachine()
    {
        var pickOrderId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var pickOrder = PickOrder.Create(
            pickOrderId, TenantId, EntityId, "PCK-2026-00001", soId, "SO-2026-00001",
            revId, whId, "WH-MAIN", 10m, "pck-key-1", null, UserId, Now);

        var pickLine = PickLine.Create(
            Guid.NewGuid(), TenantId, EntityId, pickOrderId, 1, Guid.NewGuid(), "SO-L001",
            Guid.NewGuid(), Guid.NewGuid(), "ITM-S09-VALVE-01", "Katup Industri", Guid.NewGuid(), "PCS",
            whId, Guid.NewGuid(), "LOC-MAIN", null, null, 10m);

        // Cannot dispatch an OPEN pick order before confirmation
        var notPickedEx = Assert.Throws<InvalidOperationException>(() => pickOrder.ApplyDispatchedQuantity(4m, Now));
        Assert.StartsWith("pick_not_ready_for_dispatch:", notPickedEx.Message, StringComparison.Ordinal);

        pickLine.ConfirmPickedQuantity(6m);
        pickOrder.ConfirmPicked(6m, UserId, Now.AddMinutes(2));
        Assert.Equal(PickOrderStatuses.Picked, pickOrder.Status);
        Assert.Equal(6m, pickOrder.TotalPickedBaseQuantity);

        // Partial dispatch 4 of 6 keeps status PICKED
        pickLine.RecordDispatchedQuantity(4m);
        pickOrder.ApplyDispatchedQuantity(4m, Now.AddMinutes(4));
        Assert.Equal(PickOrderStatuses.Picked, pickOrder.Status);
        Assert.Equal(4m, pickOrder.TotalDispatchedBaseQuantity);

        // Dispatching more than remaining 2 fails
        var overDispEx = Assert.Throws<InvalidOperationException>(() => pickLine.RecordDispatchedQuantity(3m));
        Assert.StartsWith("dispatch_exceeds_picked_quantity:", overDispEx.Message, StringComparison.Ordinal);

        // Dispatching remaining 2 transitions pick order to DISPATCHED
        pickLine.RecordDispatchedQuantity(2m);
        pickOrder.ApplyDispatchedQuantity(2m, Now.AddMinutes(5));
        Assert.Equal(PickOrderStatuses.Dispatched, pickOrder.Status);

        var shipment = SalesShipment.CreateDispatched(
            Guid.NewGuid(), TenantId, EntityId, "SHP-2026-00001", pickOrderId, soId, "SO-2026-00001",
            revId, Guid.NewGuid(), "PT-CUST-001", "PT Graha Ritel Nusantara", "Bekasi",
            new DateOnly(2026, 11, 19), "JNE Logistics", "B 9124 TCD", "SJ-JKT-2026-001",
            Guid.NewGuid(), "MOV-2026-00001", Guid.NewGuid(), "JRN-2026-11-00010", "IDR",
            4m, 160_000m, "shp-key-1", null, UserId, Now.AddMinutes(4));

        Assert.Equal(ShipmentStatuses.Dispatched, shipment.Status);
        shipment.ConfirmDelivery("Tari Wibowo", "POD-GRN-2026-001", UserId, Now.AddMinutes(30));
        Assert.Equal(ShipmentStatuses.Delivered, shipment.Status);
        Assert.Equal("Tari Wibowo", shipment.DeliveryConfirmedByName);
        Assert.Equal("POD-GRN-2026-001", shipment.DeliveryEvidenceReference);

        var dupEx = Assert.Throws<InvalidOperationException>(() =>
            shipment.ConfirmDelivery("Tari Wibowo", "POD-GRN-2026-001", UserId, Now.AddMinutes(31)));
        Assert.StartsWith("shipment_already_delivered:", dupEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void S13_UNIT_03_SalesOrderLineApplyFulfillmentDeltaAndProgress()
    {
        var line = SalesOrderLine.Create(
            Guid.NewGuid(), TenantId, EntityId, Guid.NewGuid(), Guid.NewGuid(),
            1, "SO-L001", Guid.NewGuid(), "ITM-S09-VALVE-01", "Katup Industri", "STOCKED_GOOD",
            Guid.NewGuid(), "PCS", Guid.NewGuid(), "PCS", 1m, 10m, 10m, 95_000m, 0m,
            null, null, null, null, 950_000m, 0m, null, null);

        line.ApplyFulfillmentDelta(reservedDelta: 6m, dispatchedDelta: 0m, billedDelta: 0m);
        Assert.Equal(6m, line.ReservedBaseQuantity);
        Assert.Equal(
            SalesFulfillmentProgressStatuses.PartiallyReserved,
            SalesFulfillmentProgressStatuses.Derive(SalesOrderStatuses.Approved, true, 10m, 6m, 0m));

        // Exceeding ordered base quantity (6 + 5 > 10) fails
        var ex = Assert.Throws<InvalidOperationException>(() =>
            line.ApplyFulfillmentDelta(reservedDelta: 5m, dispatchedDelta: 0m, billedDelta: 0m));
        Assert.StartsWith("fulfillment_exceeds_ordered_quantity:", ex.Message, StringComparison.Ordinal);

        // Partial dispatch of 4 PCS shifts 4 from reserved to dispatched
        line.ApplyFulfillmentDelta(reservedDelta: -4m, dispatchedDelta: +4m, billedDelta: 0m);
        Assert.Equal(2m, line.ReservedBaseQuantity);
        Assert.Equal(4m, line.DispatchedBaseQuantity);
        Assert.Equal(
            SalesFulfillmentProgressStatuses.PartiallyDispatched,
            SalesFulfillmentProgressStatuses.Derive(SalesOrderStatuses.Approved, true, 10m, 2m, 4m));
    }
}
