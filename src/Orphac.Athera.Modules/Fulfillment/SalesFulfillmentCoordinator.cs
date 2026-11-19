using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;

namespace Orphac.Athera.Modules.Fulfillment;

public sealed record ReserveSalesOrderLineCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    Guid SoLineId,
    Guid LocationId,
    Guid? StockLotId,
    decimal QuantityBase,
    string IdempotencyKey,
    string? Notes,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReleaseSalesOrderReservationCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ReservationId,
    decimal? ReleaseBaseQuantity,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PickLineInput(
    Guid ReservationId,
    decimal RequestedPickBaseQuantity,
    decimal? PickedBaseQuantity = null);

public sealed record CreatePickOrderCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    bool ConfirmImmediately,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<PickLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ConfirmPickLineInput(
    Guid PickLineId,
    decimal PickedBaseQuantity);

public sealed record ConfirmPickOrderCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PickOrderId,
    IReadOnlyList<ConfirmPickLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record DispatchShipmentLineInput(
    Guid PickLineId,
    decimal DispatchedBaseQuantity);

public sealed record DispatchSalesShipmentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid PickOrderId,
    DateOnly DispatchDate,
    string DeliveryNoteReference,
    string? CarrierName,
    string? TrackingOrVehicleNumber,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<DispatchShipmentLineInput>? Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ConfirmShipmentDeliveryCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ShipmentId,
    string ConfirmedByName,
    string EvidenceReference,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SalesReservationOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    StockReservation? Reservation,
    SalesOrder? SalesOrder,
    SalesOrderLine? SalesOrderLine)
{
    public static SalesReservationOutcome Fail(int status, string code, string detail) =>
        new(false, false, status, code, detail, null, null, null);
}

public sealed record PickOrderOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    PickOrder? PickOrder,
    IReadOnlyList<PickLine> Lines)
{
    public static PickOrderOutcome Fail(int status, string code, string detail) =>
        new(false, false, status, code, detail, null, Array.Empty<PickLine>());
}

public sealed record SalesShipmentOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    SalesShipment? Shipment,
    IReadOnlyList<SalesShipmentLine> Lines,
    StockMovement? Movement,
    JournalEntry? Journal)
{
    public static SalesShipmentOutcome Fail(int status, string code, string detail) =>
        new(false, false, status, code, detail, null, Array.Empty<SalesShipmentLine>(), null, null);
}

public static class SalesFulfillmentCoordinator
{
    public static async Task<SalesReservationOutcome> ReserveSalesOrderLineAsync(
        AtheraDbContext db,
        ReserveSalesOrderLineCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return SalesReservationOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        var idempKey = cmd.IdempotencyKey.Trim();
        var existingFast = await db.StockReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.IdempotencyKey == idempKey, ct);
        if (existingFast is not null)
        {
            var replaySo = existingFast.SourceDocumentId is { } sid
                ? await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(o => o.TenantId == cmd.TenantId && o.LegalEntityId == cmd.LegalEntityId && o.SalesOrderId == sid, ct)
                : null;
            var replayLine = existingFast.SourceLineId is { } lid
                ? await db.SalesOrderLines.AsNoTracking().SingleOrDefaultAsync(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SoLineId == lid, ct)
                : null;
            return new SalesReservationOutcome(true, true, 200, null, null, existingFast, replaySo, replayLine);
        }

        var qty = decimal.Round(cmd.QuantityBase, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            return SalesReservationOutcome.Fail(400, "reservation_qty_must_be_positive", "Reserved base quantity must be strictly positive.");
        }

        var so = (await db.SalesOrders
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {cmd.SalesOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (so is null)
        {
            return SalesReservationOutcome.Fail(404, "so_not_found", "Sales order was not found in this legal entity.");
        }

        if (so.Status != SalesOrderStatuses.Approved || so.CurrentApprovedRevisionId is not { } approvedRevId || string.IsNullOrWhiteSpace(so.SoNumber))
        {
            return SalesReservationOutcome.Fail(
                409,
                "so_not_approved_for_fulfillment",
                "Only APPROVED sales orders with an official SO commitment number can reserve warehouse stock.");
        }

        var soLine = (await db.SalesOrderLines
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_order_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {so.SalesOrderId} AND revision_id = {approvedRevId} AND so_line_id = {cmd.SoLineId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (soLine is null)
        {
            return SalesReservationOutcome.Fail(
                404,
                "so_line_not_found_on_approved_revision",
                "Sales order line was not found on the current approved sales order revision.");
        }

        var remainingLineUnfulfilled = decimal.Round(
            soLine.OrderedBaseQuantity - soLine.DispatchedBaseQuantity - soLine.ReservedBaseQuantity,
            6,
            MidpointRounding.AwayFromZero);
        if (qty > remainingLineUnfulfilled)
        {
            return SalesReservationOutcome.Fail(
                409,
                "fulfillment_exceeds_ordered_quantity",
                $"Requested reservation ({qty:0.######}) exceeds remaining unreserved/undispatched line quantity ({remainingLineUnfulfilled:0.######}).");
        }

        var item = await db.Items.AsNoTracking()
            .SingleOrDefaultAsync(i => i.TenantId == cmd.TenantId && i.ItemId == soLine.ItemId, ct);
        if (item is null)
        {
            return SalesReservationOutcome.Fail(404, "item_not_found", "Catalog item was not found.");
        }

        if (item.LotTrackingPolicy == LotTrackingPolicies.Required && !cmd.StockLotId.HasValue)
        {
            return SalesReservationOutcome.Fail(
                409,
                "lot_required_by_item_policy",
                $"Item '{item.ItemCode}' requires an explicit stock lot selection for reservation and fulfillment.");
        }

        if (item.LotTrackingPolicy == LotTrackingPolicies.None && cmd.StockLotId.HasValue)
        {
            return SalesReservationOutcome.Fail(
                409,
                "lot_not_enabled_for_item",
                $"Item '{item.ItemCode}' does not use stock lot tracking.");
        }

        if (cmd.StockLotId is { } lotIdToCheck)
        {
            var lot = await db.StockLots.AsNoTracking()
                .SingleOrDefaultAsync(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.StockLotId == lotIdToCheck && l.ItemId == item.ItemId, ct);
            if (lot is null)
            {
                return SalesReservationOutcome.Fail(404, "stock_lot_not_found", "Selected stock lot was not found for this item in the legal entity.");
            }

            var today = DateOnly.FromDateTime(cmd.NowUtc.UtcDateTime);
            if (lot.ExpiryDate.HasValue && lot.ExpiryDate.Value < today)
            {
                return SalesReservationOutcome.Fail(
                    409,
                    "lot_expired_for_fulfillment",
                    $"Stock lot '{lot.LotNumber}' expired on {lot.ExpiryDate.Value:yyyy-MM-dd} and cannot be reserved for sales fulfillment.");
            }
        }

        var loc = await db.WarehouseLocations.AsNoTracking()
            .SingleOrDefaultAsync(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.LocationId == cmd.LocationId, ct);
        if (loc is null)
        {
            return SalesReservationOutcome.Fail(404, "location_not_found", "Warehouse location was not found in this legal entity.");
        }

        if (loc.ZoneType is LocationZoneTypes.Quarantine or LocationZoneTypes.BlockedHold)
        {
            return SalesReservationOutcome.Fail(
                409,
                "od10_restricted_stock_not_reservable",
                $"Location '{loc.LocationCode}' is in restricted zone '{loc.ZoneType}' and cannot be reserved for sales fulfillment.");
        }

        var position = await InventoryAvailabilityService.LockStockPositionForUpdateAsync(
            db, cmd.TenantId, cmd.LegalEntityId, soLine.ItemId, ct);
        if (position is null)
        {
            return SalesReservationOutcome.Fail(409, "od10_insufficient_available_stock", "Item has zero stock position in this legal entity.");
        }

        StockLocationBalance? balance;
        if (cmd.StockLotId is { } lotId)
        {
            balance = (await db.StockLocationBalances
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM inventory.stock_location_balances
                    WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId}
                      AND warehouse_id = {loc.WarehouseId} AND location_id = {loc.LocationId}
                      AND item_id = {soLine.ItemId} AND stock_lot_id = {lotId} AND stock_status = {StockStatuses.Unrestricted}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();
        }
        else
        {
            balance = (await db.StockLocationBalances
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM inventory.stock_location_balances
                    WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId}
                      AND warehouse_id = {loc.WarehouseId} AND location_id = {loc.LocationId}
                      AND item_id = {soLine.ItemId} AND stock_lot_id IS NULL AND stock_status = {StockStatuses.Unrestricted}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();
        }

        if (balance is null || balance.AvailableBaseQuantity < qty)
        {
            var avail = balance?.AvailableBaseQuantity ?? 0m;
            return SalesReservationOutcome.Fail(
                409,
                "od10_insufficient_available_stock",
                $"Insufficient UNRESTRICTED available stock at '{loc.LocationCode}' (Available={avail:0.######}, Requested={qty:0.######}).");
        }

        var existingLocked = await db.StockReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.IdempotencyKey == idempKey, ct);
        if (existingLocked is not null)
        {
            return new SalesReservationOutcome(true, true, 200, null, null, existingLocked, so, soLine);
        }

        try
        {
            var payloadHash = ReservationHash.ComputePayloadHash(balance.StockLocationBalanceId, ReservationSourceTypes.SalesOrder, so.SoNumber!, qty);
            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "inventory.reservation_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc.Year, ct);
            var rsvNumber = $"RSV-{cmd.NowUtc.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";

            var reservation = StockReservation.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                rsvNumber,
                balance,
                ReservationSourceTypes.SalesOrder,
                so.SalesOrderId,
                so.SoNumber!,
                soLine.SoLineId,
                idempKey,
                payloadHash,
                qty,
                cmd.Notes,
                expiresAtUtc: null,
                cmd.ActorUserId,
                cmd.NowUtc);

            balance.ApplyReservationDelta(+qty, cmd.NowUtc);
            position.ApplyReservationDelta(+qty, cmd.NowUtc);
            soLine.ApplyFulfillmentDelta(reservedDelta: +qty, dispatchedDelta: 0m, billedDelta: 0m);
            so.ApplyFulfillmentCounters(
                decimal.Round(so.TotalReservedBaseQuantity + qty, 6, MidpointRounding.AwayFromZero),
                so.TotalDispatchedBaseQuantity,
                so.TotalBilledBaseQuantity,
                cmd.NowUtc);

            db.StockReservations.Add(reservation);

            var eventPayload = JsonSerializer.Serialize(new
            {
                reservation.StockReservationId,
                reservation.ReservationNumber,
                so.SalesOrderId,
                so.SoNumber,
                soLine.SoLineId,
                soLine.LineageLineKey,
                soLine.ItemId,
                soLine.ItemCodeSnapshot,
                loc.LocationCode,
                cmd.StockLotId,
                ReservedBaseQuantity = qty
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SALES_ORDER_STOCK_RESERVED",
                "STOCK_RESERVATION",
                reservation.StockReservationId.ToString("D"),
                $"Reserved {qty:0.######} {soLine.ItemCodeSnapshot} at {loc.LocationCode} for {so.SoNumber} ({reservation.ReservationNumber})",
                eventPayload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "fulfillment.sales_order.reserved.v1",
                $"fulfillment:reservation:{reservation.StockReservationId:D}",
                eventPayload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new SalesReservationOutcome(true, false, 201, null, null, reservation, so, soLine);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapReservationFailure(ex);
        }
    }

    public static async Task<SalesReservationOutcome> ReleaseSalesOrderReservationAsync(
        AtheraDbContext db,
        ReleaseSalesOrderReservationCommand cmd,
        CancellationToken ct)
    {
        var rsvMeta = await db.StockReservations.AsNoTracking()
            .Where(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.StockReservationId == cmd.ReservationId)
            .Select(r => new { r.ItemId, r.StockLocationBalanceId, r.SourceType, r.SourceDocumentId, r.SourceLineId })
            .SingleOrDefaultAsync(ct);

        if (rsvMeta is null)
        {
            return SalesReservationOutcome.Fail(404, "reservation_not_found", "Stock reservation was not found in this legal entity.");
        }

        // Check if this reservation is locked by an active (OPEN or PICKED) pick line
        var committedInPickQty = await (
            from pl in db.PickLines.AsNoTracking()
            join po in db.PickOrders.AsNoTracking()
                on new { pl.TenantId, pl.LegalEntityId, pl.PickOrderId }
                equals new { po.TenantId, po.LegalEntityId, po.PickOrderId }
            where pl.TenantId == cmd.TenantId
                  && pl.LegalEntityId == cmd.LegalEntityId
                  && pl.StockReservationId == cmd.ReservationId
                  && (po.Status == PickOrderStatuses.Open || po.Status == PickOrderStatuses.Picked)
            select (decimal?)(po.Status == PickOrderStatuses.Open
                ? pl.RequestedPickBaseQuantity
                : pl.PickedBaseQuantity - pl.DispatchedBaseQuantity)
        ).SumAsync(ct) ?? 0m;

        SalesOrder? so = null;
        SalesOrderLine? soLine = null;
        if (rsvMeta.SourceType == ReservationSourceTypes.SalesOrder && rsvMeta.SourceDocumentId is { } soId)
        {
            so = (await db.SalesOrders
                .FromSqlInterpolated(
                    $"SELECT * FROM sales.sales_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {soId} FOR UPDATE")
                .ToListAsync(ct))
                .SingleOrDefault();

            if (so?.CurrentApprovedRevisionId is { } appRevId && rsvMeta.SourceLineId is { } lineId)
            {
                soLine = (await db.SalesOrderLines
                    .FromSqlInterpolated(
                        $"SELECT * FROM sales.sales_order_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {so.SalesOrderId} AND revision_id = {appRevId} AND so_line_id = {lineId} FOR UPDATE")
                    .ToListAsync(ct))
                    .SingleOrDefault();
            }
        }

        var position = await InventoryAvailabilityService.LockStockPositionForUpdateAsync(
            db, cmd.TenantId, cmd.LegalEntityId, rsvMeta.ItemId, ct);
        if (position is null)
        {
            return SalesReservationOutcome.Fail(409, "stock_position_missing", "Parent stock position was not found.");
        }

        var balance = (await db.StockLocationBalances
            .FromSqlInterpolated(
                $"SELECT * FROM inventory.stock_location_balances WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND stock_location_balance_id = {rsvMeta.StockLocationBalanceId} FOR UPDATE")
            .ToListAsync(ct))
            .Single();

        var reservation = (await db.StockReservations
            .FromSqlInterpolated(
                $"SELECT * FROM inventory.stock_reservations WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND stock_reservation_id = {cmd.ReservationId} FOR UPDATE")
            .ToListAsync(ct))
            .Single();

        var releasableQty = decimal.Round(reservation.RemainingReservedBaseQuantity - committedInPickQty, 6, MidpointRounding.AwayFromZero);
        var requestedRelease = decimal.Round(cmd.ReleaseBaseQuantity ?? reservation.RemainingReservedBaseQuantity, 6, MidpointRounding.AwayFromZero);

        if (requestedRelease > releasableQty)
        {
            return SalesReservationOutcome.Fail(
                409,
                "reservation_locked_by_active_pick",
                $"Cannot release {requestedRelease:0.######} units because {committedInPickQty:0.######} units are allocated to an active pick order.");
        }

        try
        {
            var released = reservation.Release(requestedRelease, cmd.ActorUserId, cmd.NowUtc);
            balance.ApplyReservationDelta(-released, cmd.NowUtc);
            position.ApplyReservationDelta(-released, cmd.NowUtc);

            if (so is not null && soLine is not null)
            {
                soLine.ApplyFulfillmentDelta(reservedDelta: -released, dispatchedDelta: 0m, billedDelta: 0m);
                so.ApplyFulfillmentCounters(
                    Math.Max(0m, decimal.Round(so.TotalReservedBaseQuantity - released, 6, MidpointRounding.AwayFromZero)),
                    so.TotalDispatchedBaseQuantity,
                    so.TotalBilledBaseQuantity,
                    cmd.NowUtc);
            }

            var eventPayload = JsonSerializer.Serialize(new
            {
                reservation.StockReservationId,
                reservation.ReservationNumber,
                so?.SalesOrderId,
                so?.SoNumber,
                ReleasedQuantity = released,
                RemainingReserved = reservation.RemainingReservedBaseQuantity,
                reservation.Status
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SALES_ORDER_RESERVATION_RELEASED",
                "STOCK_RESERVATION",
                reservation.StockReservationId.ToString("D"),
                $"Released {released:0.######} from reservation {reservation.ReservationNumber}",
                eventPayload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "fulfillment.sales_order.reservation_released.v1",
                $"fulfillment:reservation-release:{reservation.StockReservationId:D}:v{reservation.Version}",
                eventPayload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new SalesReservationOutcome(true, false, 200, null, null, reservation, so, soLine);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapReservationFailure(ex);
        }
    }

    public static async Task<PickOrderOutcome> CreatePickOrderAsync(
        AtheraDbContext db,
        CreatePickOrderCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return PickOrderOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return PickOrderOutcome.Fail(400, "pick_lines_required", "At least one reservation pick line is required.");
        }

        var idempKey = cmd.IdempotencyKey.Trim();
        var existingFast = await db.PickOrders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.IdempotencyKey == idempKey, ct);
        if (existingFast is not null)
        {
            var existingLines = await db.PickLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.PickOrderId == existingFast.PickOrderId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);
            return new PickOrderOutcome(true, true, 200, null, null, existingFast, existingLines);
        }

        var so = (await db.SalesOrders
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {cmd.SalesOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (so is null)
        {
            return PickOrderOutcome.Fail(404, "so_not_found", "Sales order was not found in this legal entity.");
        }

        if (so.Status != SalesOrderStatuses.Approved || so.CurrentApprovedRevisionId is not { } approvedRevId || string.IsNullOrWhiteSpace(so.SoNumber))
        {
            return PickOrderOutcome.Fail(409, "so_not_approved_for_fulfillment", "Pick orders can only be created for APPROVED sales orders.");
        }

        var reservationIds = cmd.Lines.Select(l => l.ReservationId).Distinct().OrderBy(x => x).ToList();
        if (reservationIds.Count != cmd.Lines.Count)
        {
            return PickOrderOutcome.Fail(400, "duplicate_reservation_in_pick", "Each reservation may appear at most once per pick order.");
        }

        var lockedReservations = new Dictionary<Guid, StockReservation>();
        foreach (var rsvId in reservationIds)
        {
            var rsv = (await db.StockReservations
                .FromSqlInterpolated(
                    $"SELECT * FROM inventory.stock_reservations WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND stock_reservation_id = {rsvId} FOR UPDATE")
                .ToListAsync(ct))
                .SingleOrDefault();

            if (rsv is null)
            {
                return PickOrderOutcome.Fail(404, "reservation_not_found", $"Stock reservation '{rsvId}' was not found.");
            }

            if (rsv.SourceType != ReservationSourceTypes.SalesOrder || rsv.SourceDocumentId != so.SalesOrderId || rsv.SourceLineId is null)
            {
                return PickOrderOutcome.Fail(
                    409,
                    "reservation_does_not_belong_to_sales_order",
                    $"Reservation '{rsv.ReservationNumber}' is not bound to sales order '{so.SoNumber}'.");
            }

            if (rsv.RemainingReservedBaseQuantity <= 0m || rsv.Status is ReservationStatuses.Released or ReservationStatuses.Fulfilled)
            {
                return PickOrderOutcome.Fail(
                    409,
                    "reservation_already_closed",
                    $"Reservation '{rsv.ReservationNumber}' has no remaining active reserved quantity.");
            }

            lockedReservations[rsvId] = rsv;
        }

        // Check already allocated pick quantities on open/picked pick orders for these reservations
        var existingOpenPickAllocations = await (
            from pl in db.PickLines.AsNoTracking()
            join po in db.PickOrders.AsNoTracking()
                on new { pl.TenantId, pl.LegalEntityId, pl.PickOrderId }
                equals new { po.TenantId, po.LegalEntityId, po.PickOrderId }
            where pl.TenantId == cmd.TenantId
                  && pl.LegalEntityId == cmd.LegalEntityId
                  && reservationIds.Contains(pl.StockReservationId)
                  && (po.Status == PickOrderStatuses.Open || po.Status == PickOrderStatuses.Picked)
            group new { pl, po } by pl.StockReservationId into g
            select new
            {
                ReservationId = g.Key,
                AllocatedQty = g.Sum(x => x.po.Status == PickOrderStatuses.Open
                    ? x.pl.RequestedPickBaseQuantity
                    : x.pl.PickedBaseQuantity - x.pl.DispatchedBaseQuantity)
            }
        ).ToDictionaryAsync(x => x.ReservationId, x => x.AllocatedQty, ct);

        var soLinesById = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SalesOrderId == so.SalesOrderId && l.RevisionId == approvedRevId)
            .ToDictionaryAsync(l => l.SoLineId, ct);

        var warehouseIds = lockedReservations.Values.Select(r => r.WarehouseId).Distinct().ToList();
        if (warehouseIds.Count != 1)
        {
            return PickOrderOutcome.Fail(400, "pick_order_single_warehouse_required", "All lines in a pick order must belong to the same warehouse.");
        }

        var warehouse = await db.Warehouses.AsNoTracking()
            .SingleAsync(w => w.TenantId == cmd.TenantId && w.LegalEntityId == cmd.LegalEntityId && w.WarehouseId == warehouseIds[0], ct);

        var locationIds = lockedReservations.Values.Select(r => r.LocationId).Distinct().ToList();
        var locationsById = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && locationIds.Contains(l.LocationId))
            .ToDictionaryAsync(l => l.LocationId, ct);

        var lotIds = lockedReservations.Values.Where(r => r.StockLotId.HasValue).Select(r => r.StockLotId!.Value).Distinct().ToList();
        var lotsById = lotIds.Count > 0
            ? await db.StockLots.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && lotIds.Contains(l.StockLotId))
                .ToDictionaryAsync(l => l.StockLotId, ct)
            : new Dictionary<Guid, StockLot>();

        try
        {
            var pickOrderId = Guid.NewGuid();
            var pickLines = new List<PickLine>(cmd.Lines.Count);
            decimal totalRequested = 0m;
            decimal totalPicked = 0m;

            for (var i = 0; i < cmd.Lines.Count; i++)
            {
                var input = cmd.Lines[i];
                var rsv = lockedReservations[input.ReservationId];
                var reqQty = decimal.Round(input.RequestedPickBaseQuantity, 6, MidpointRounding.AwayFromZero);
                if (reqQty <= 0m)
                {
                    return PickOrderOutcome.Fail(400, "pick_qty_must_be_positive", "Requested pick quantity must be strictly positive.");
                }

                var alreadyInPick = existingOpenPickAllocations.GetValueOrDefault(rsv.StockReservationId, 0m);
                var unallocatedReserved = decimal.Round(rsv.RemainingReservedBaseQuantity - alreadyInPick, 6, MidpointRounding.AwayFromZero);
                if (reqQty > unallocatedReserved)
                {
                    return PickOrderOutcome.Fail(
                        409,
                        "pick_exceeds_unallocated_reservation",
                        $"Requested pick quantity ({reqQty:0.######}) exceeds unallocated reserved quantity ({unallocatedReserved:0.######}) on reservation '{rsv.ReservationNumber}'.");
                }

                if (!soLinesById.TryGetValue(rsv.SourceLineId!.Value, out var soLine))
                {
                    return PickOrderOutcome.Fail(409, "so_line_not_found_on_approved_revision", "Reservation line is not present on the current approved sales order revision.");
                }

                var loc = locationsById[rsv.LocationId];
                var lot = rsv.StockLotId is { } lid && lotsById.TryGetValue(lid, out var sl) ? sl : null;

                var pickLine = PickLine.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    pickOrderId,
                    i + 1,
                    soLine.SoLineId,
                    soLine.LineageLineKey,
                    rsv.StockReservationId,
                    soLine.ItemId,
                    soLine.ItemCodeSnapshot,
                    soLine.ItemNameSnapshot,
                    soLine.BaseUomId,
                    soLine.BaseUomCodeSnapshot,
                    rsv.WarehouseId,
                    rsv.LocationId,
                    loc.LocationCode,
                    rsv.StockLotId,
                    lot?.LotNumber,
                    reqQty);

                if (cmd.ConfirmImmediately)
                {
                    var pickedQty = decimal.Round(input.PickedBaseQuantity ?? reqQty, 6, MidpointRounding.AwayFromZero);
                    pickLine.ConfirmPickedQuantity(pickedQty);
                    totalPicked += pickedQty;
                }

                totalRequested += reqQty;
                pickLines.Add(pickLine);
            }

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "fulfillment.pick_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc.Year, ct);
            var pickNumber = $"PCK-{cmd.NowUtc.Year:D4}-{seq:D5}";

            var pickOrder = PickOrder.Create(
                pickOrderId,
                cmd.TenantId,
                cmd.LegalEntityId,
                pickNumber,
                so.SalesOrderId,
                so.SoNumber!,
                approvedRevId,
                warehouse.WarehouseId,
                warehouse.WarehouseCode,
                totalRequested,
                idempKey,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            if (cmd.ConfirmImmediately)
            {
                pickOrder.ConfirmPicked(totalPicked, cmd.ActorUserId, cmd.NowUtc);
            }

            db.PickOrders.Add(pickOrder);
            db.PickLines.AddRange(pickLines);

            var payloadJson = JsonSerializer.Serialize(new
            {
                pickOrder.PickOrderId,
                pickOrder.PickNumber,
                pickOrder.SalesOrderId,
                pickOrder.SalesOrderNumberSnapshot,
                pickOrder.Status,
                pickOrder.TotalRequestedBaseQuantity,
                pickOrder.TotalPickedBaseQuantity,
                LineCount = pickLines.Count
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                cmd.ConfirmImmediately ? "PICK_ORDER_CREATED_AND_CONFIRMED" : "PICK_ORDER_CREATED",
                "PICK_ORDER",
                pickOrder.PickOrderId.ToString("D"),
                $"Pick order {pickOrder.PickNumber} ({pickOrder.Status}) for {so.SoNumber} ({totalRequested:0.######} requested)",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ConfirmImmediately ? "fulfillment.pick.confirmed.v1" : "fulfillment.pick.created.v1",
                $"fulfillment:pick:{pickOrder.PickOrderId:D}:v{pickOrder.Version}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new PickOrderOutcome(true, false, 201, null, null, pickOrder, pickLines);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapPickFailure(ex);
        }
    }

    public static async Task<PickOrderOutcome> ConfirmPickOrderAsync(
        AtheraDbContext db,
        ConfirmPickOrderCommand cmd,
        CancellationToken ct)
    {
        var pickOrder = (await db.PickOrders
            .FromSqlInterpolated(
                $"SELECT * FROM fulfillment.pick_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND pick_order_id = {cmd.PickOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (pickOrder is null)
        {
            return PickOrderOutcome.Fail(404, "pick_order_not_found", "Pick order was not found in this legal entity.");
        }

        var pickLines = await db.PickLines
            .FromSqlInterpolated(
                $"SELECT * FROM fulfillment.pick_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND pick_order_id = {pickOrder.PickOrderId} ORDER BY line_number FOR UPDATE")
            .ToListAsync(ct);

        if (pickOrder.Status == PickOrderStatuses.Picked)
        {
            return new PickOrderOutcome(true, true, 200, null, null, pickOrder, pickLines);
        }

        if (pickOrder.Status != PickOrderStatuses.Open)
        {
            return PickOrderOutcome.Fail(409, "pick_invalid_status_transition", $"Cannot confirm pick order in status '{pickOrder.Status}'.");
        }

        var inputsByLineId = (cmd.Lines ?? Array.Empty<ConfirmPickLineInput>())
            .ToDictionary(x => x.PickLineId, x => x.PickedBaseQuantity);

        try
        {
            decimal totalPicked = 0m;
            foreach (var line in pickLines)
            {
                var qty = inputsByLineId.TryGetValue(line.PickLineId, out var explicitQty)
                    ? explicitQty
                    : line.RequestedPickBaseQuantity;
                line.ConfirmPickedQuantity(qty);
                totalPicked += line.PickedBaseQuantity;
            }

            pickOrder.ConfirmPicked(totalPicked, cmd.ActorUserId, cmd.NowUtc);

            var payloadJson = JsonSerializer.Serialize(new
            {
                pickOrder.PickOrderId,
                pickOrder.PickNumber,
                pickOrder.SalesOrderId,
                pickOrder.SalesOrderNumberSnapshot,
                pickOrder.Status,
                pickOrder.TotalPickedBaseQuantity
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "PICK_ORDER_CONFIRMED",
                "PICK_ORDER",
                pickOrder.PickOrderId.ToString("D"),
                $"Confirmed pick order {pickOrder.PickNumber} ({totalPicked:0.######} base units picked)",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "fulfillment.pick.confirmed.v1",
                $"fulfillment:pick:{pickOrder.PickOrderId:D}:v{pickOrder.Version}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new PickOrderOutcome(true, false, 200, null, null, pickOrder, pickLines);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapPickFailure(ex);
        }
    }

    public static async Task<SalesShipmentOutcome> DispatchShipmentAsync(
        AtheraDbContext db,
        DispatchSalesShipmentCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return SalesShipmentOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(cmd.DeliveryNoteReference))
        {
            return SalesShipmentOutcome.Fail(400, "delivery_note_reference_required", "Provide a delivery note / Surat Jalan reference.");
        }

        var idempKey = cmd.IdempotencyKey.Trim();
        var existingFast = await db.SalesShipments.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == cmd.TenantId && s.LegalEntityId == cmd.LegalEntityId && s.IdempotencyKey == idempKey, ct);
        if (existingFast is not null)
        {
            return await BuildShipmentReplayAsync(db, existingFast, ct);
        }

        var pickOrder = (await db.PickOrders
            .FromSqlInterpolated(
                $"SELECT * FROM fulfillment.pick_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND pick_order_id = {cmd.PickOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (pickOrder is null)
        {
            return SalesShipmentOutcome.Fail(404, "pick_order_not_found", "Pick order was not found in this legal entity.");
        }

        var existingLocked = await db.SalesShipments.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == cmd.TenantId && s.LegalEntityId == cmd.LegalEntityId && s.IdempotencyKey == idempKey, ct);
        if (existingLocked is not null)
        {
            return await BuildShipmentReplayAsync(db, existingLocked, ct);
        }

        if (pickOrder.Status != PickOrderStatuses.Picked)
        {
            return SalesShipmentOutcome.Fail(
                409,
                "pick_not_ready_for_dispatch",
                $"Pick order '{pickOrder.PickNumber}' must be in PICKED status before dispatch (current: '{pickOrder.Status}').");
        }

        var pickLines = await db.PickLines
            .FromSqlInterpolated(
                $"SELECT * FROM fulfillment.pick_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND pick_order_id = {pickOrder.PickOrderId} ORDER BY line_number FOR UPDATE")
            .ToListAsync(ct);

        var so = (await db.SalesOrders
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {pickOrder.SalesOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (so is null || so.Status != SalesOrderStatuses.Approved || so.CurrentApprovedRevisionId is not { } approvedRevId || string.IsNullOrWhiteSpace(so.SoNumber))
        {
            return SalesShipmentOutcome.Fail(
                409,
                "so_not_approved_for_fulfillment",
                "Underlying sales order is no longer in APPROVED status.");
        }

        var approvedRev = await db.SalesOrderRevisions.AsNoTracking()
            .SingleAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.RevisionId == approvedRevId, ct);

        var soLines = await db.SalesOrderLines
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_order_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {so.SalesOrderId} AND revision_id = {approvedRevId} ORDER BY line_number FOR UPDATE")
            .ToListAsync(ct);
        var soLinesById = soLines.ToDictionary(l => l.SoLineId);
        var soLinesByLineage = soLines.ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);

        var dispatchQtyByPickLineId = new Dictionary<Guid, decimal>();
        if (cmd.Lines is { Count: > 0 })
        {
            foreach (var l in cmd.Lines)
            {
                var q = decimal.Round(l.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
                if (q > 0m)
                {
                    dispatchQtyByPickLineId[l.PickLineId] = q;
                }
            }
        }
        else
        {
            foreach (var pl in pickLines)
            {
                var rem = decimal.Round(pl.PickedBaseQuantity - pl.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
                if (rem > 0m)
                {
                    dispatchQtyByPickLineId[pl.PickLineId] = rem;
                }
            }
        }

        if (dispatchQtyByPickLineId.Count == 0)
        {
            return SalesShipmentOutcome.Fail(400, "dispatch_qty_must_be_positive", "At least one pick line with positive dispatch quantity is required.");
        }

        // Lock reservations in deterministic order
        var targetPickLines = pickLines.Where(pl => dispatchQtyByPickLineId.ContainsKey(pl.PickLineId)).ToList();
        if (targetPickLines.Count != dispatchQtyByPickLineId.Count)
        {
            return SalesShipmentOutcome.Fail(404, "pick_line_not_found", "One or more specified pick lines do not belong to this pick order.");
        }

        var lockedReservations = new Dictionary<Guid, StockReservation>();
        foreach (var rsvId in targetPickLines.Select(pl => pl.StockReservationId).Distinct().OrderBy(x => x))
        {
            var rsv = (await db.StockReservations
                .FromSqlInterpolated(
                    $"SELECT * FROM inventory.stock_reservations WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND stock_reservation_id = {rsvId} FOR UPDATE")
                .ToListAsync(ct))
                .Single();
            lockedReservations[rsvId] = rsv;
        }

        var currency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
        var itemTargets = targetPickLines
            .Select(pl => (pl.ItemId, pl.BaseUomId))
            .Distinct()
            .ToList();

        var lockedInventory = await InventoryValuationService.EnsureAndLockItemTargetsAsync(
            db, cmd.TenantId, cmd.LegalEntityId, currency, itemTargets, cmd.NowUtc, ct);

        var lockedLocationBalances = new Dictionary<Guid, StockLocationBalance>();
        foreach (var rsv in lockedReservations.Values.OrderBy(r => r.StockLocationBalanceId))
        {
            var bal = (await db.StockLocationBalances
                .FromSqlInterpolated(
                    $"SELECT * FROM inventory.stock_location_balances WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND stock_location_balance_id = {rsv.StockLocationBalanceId} FOR UPDATE")
                .ToListAsync(ct))
                .Single();
            lockedLocationBalances[bal.StockLocationBalanceId] = bal;
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m =>
                m.TenantId == cmd.TenantId &&
                m.LegalEntityId == cmd.LegalEntityId &&
                (m.PurposeCode == GlMappingPurposes.CogsDefault || m.PurposeCode == GlMappingPurposes.InventoryAsset))
            .ToDictionaryAsync(m => m.PurposeCode, ct);

        if (!mappings.TryGetValue(GlMappingPurposes.CogsDefault, out var cogsMap) ||
            !mappings.TryGetValue(GlMappingPurposes.InventoryAsset, out var invMap))
        {
            return SalesShipmentOutcome.Fail(
                409,
                "missing_inventory_or_cogs_mapping",
                "COGS_DEFAULT and INVENTORY_ASSET GL account mappings are required to post outbound sales dispatch.");
        }

        try
        {
            var shpSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "fulfillment.shipment_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.DispatchDate.Year, ct);
            var shipmentNumber = $"SHP-{cmd.DispatchDate.Year:D4}-{shpSeq:D5}";
            var shipmentId = Guid.NewGuid();

            var movSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "inventory.movement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.DispatchDate.Year, ct);
            var movementNumber = $"MOV-{cmd.DispatchDate.Year:D4}-{movSeq:D5}";
            var movementId = Guid.NewGuid();

            var journalLinesInput = new List<GlJournalLineInput>();
            var pendingLineArtifacts = new List<(
                PickLine PickLine,
                SalesOrderLine SoLine,
                StockReservation Reservation,
                decimal DispatchQty,
                OperationalIssueStepResult IssueStep,
                decimal RoundedCogsAmount,
                Guid MovementLineId,
                Guid ValuationEntryId)>();

            decimal totalDispatchedQty = 0m;
            decimal totalCogsAmount = 0m;

            foreach (var pl in targetPickLines)
            {
                var dispatchQty = dispatchQtyByPickLineId[pl.PickLineId];
                var remainingPicked = decimal.Round(pl.PickedBaseQuantity - pl.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
                if (dispatchQty > remainingPicked)
                {
                    return SalesShipmentOutcome.Fail(
                        409,
                        "dispatch_exceeds_picked_quantity",
                        $"Dispatch quantity ({dispatchQty:0.######}) on line {pl.LineNumber} exceeds remaining picked quantity ({remainingPicked:0.######}).");
                }

                var soLine = soLinesById.TryGetValue(pl.SoLineId, out var byId)
                    ? byId
                    : soLinesByLineage.GetValueOrDefault(pl.LineageLineKey);
                if (soLine is null)
                {
                    return SalesShipmentOutcome.Fail(409, "so_line_not_found_on_approved_revision", "Pick line is no longer present on the approved sales order revision.");
                }

                var remainingOrderableToDispatch = decimal.Round(soLine.OrderedBaseQuantity - soLine.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
                if (dispatchQty > remainingOrderableToDispatch)
                {
                    return SalesShipmentOutcome.Fail(
                        409,
                        "dispatch_exceeds_unfulfilled_order_quantity",
                        $"Dispatch quantity ({dispatchQty:0.######}) exceeds remaining unfulfilled sales order quantity ({remainingOrderableToDispatch:0.######}).");
                }

                var rsv = lockedReservations[pl.StockReservationId];
                var locBalance = lockedLocationBalances[rsv.StockLocationBalanceId];
                var pos = lockedInventory.PositionsByItem[pl.ItemId];
                var pool = lockedInventory.PoolsByItem[pl.ItemId];

                rsv.Consume(dispatchQty, cmd.ActorUserId, cmd.NowUtc);
                locBalance.ConsumeReservedOnHand(dispatchQty, cmd.NowUtc);
                pos.ConsumeReservedOutboundIssue(dispatchQty, cmd.NowUtc);

                var issueStep = pool.ApplyOperationalIssue(dispatchQty, cmd.NowUtc);
                var roundedCogs = decimal.Round(issueStep.IssuedCarryingAmount, 2, MidpointRounding.AwayFromZero);

                pl.RecordDispatchedQuantity(dispatchQty);
                soLine.ApplyFulfillmentDelta(reservedDelta: -dispatchQty, dispatchedDelta: +dispatchQty, billedDelta: 0m);

                totalDispatchedQty = decimal.Round(totalDispatchedQty + dispatchQty, 6, MidpointRounding.AwayFromZero);
                totalCogsAmount = decimal.Round(totalCogsAmount + roundedCogs, 2, MidpointRounding.AwayFromZero);

                journalLinesInput.Add(new GlJournalLineInput(
                    cogsMap.AccountId,
                    $"Dr COGS {pl.ItemCodeSnapshot} (-{dispatchQty:0.######} {pl.BaseUomCodeSnapshot}) · {shipmentNumber} / {so.SoNumber}",
                    DebitAmount: roundedCogs,
                    CreditAmount: 0m,
                    PartnerId: so.CustomerPartnerId,
                    ItemId: pl.ItemId));

                journalLinesInput.Add(new GlJournalLineInput(
                    invMap.AccountId,
                    $"Cr Inventory Asset {pl.ItemCodeSnapshot} · {shipmentNumber} / {movementNumber}",
                    DebitAmount: 0m,
                    CreditAmount: roundedCogs,
                    PartnerId: so.CustomerPartnerId,
                    ItemId: pl.ItemId));

                pendingLineArtifacts.Add((
                    pl,
                    soLine,
                    rsv,
                    dispatchQty,
                    issueStep,
                    roundedCogs,
                    Guid.NewGuid(),
                    Guid.NewGuid()));
            }

            pickOrder.ApplyDispatchedQuantity(totalDispatchedQty, cmd.NowUtc);
            so.ApplyFulfillmentCounters(
                Math.Max(0m, decimal.Round(so.TotalReservedBaseQuantity - totalDispatchedQty, 6, MidpointRounding.AwayFromZero)),
                decimal.Round(so.TotalDispatchedBaseQuantity + totalDispatchedQty, 6, MidpointRounding.AwayFromZero),
                so.TotalBilledBaseQuantity,
                cmd.NowUtc);

            var journalKey = $"shp:{shipmentId:N}";
            var journalOutcome = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    currency,
                    cmd.DispatchDate,
                    currency,
                    $"Sales Shipment Dispatch {shipmentNumber} ({so.SoNumber} · {cmd.DeliveryNoteReference.Trim()})",
                    "FULFILLMENT",
                    "SALES_SHIPMENT",
                    shipmentNumber,
                    journalKey,
                    journalLinesInput,
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
            {
                return SalesShipmentOutcome.Fail(
                    journalOutcome.FailureHttpStatus,
                    journalOutcome.ErrorCode ?? "gl_posting_failed",
                    journalOutcome.ErrorDetail ?? "Failed posting sales dispatch COGS / Inventory Asset journal.");
            }

            var movement = StockMovement.Create(
                movementId,
                cmd.TenantId,
                cmd.LegalEntityId,
                movementNumber,
                StockMovementTypes.SalesDispatchOutbound,
                cmd.DispatchDate,
                "FULFILLMENT",
                "SALES_SHIPMENT",
                shipmentId,
                shipmentNumber,
                idempKey,
                cmd.Notes ?? $"Sales dispatch {shipmentNumber} for {so.SoNumber}",
                cmd.ActorUserId,
                cmd.NowUtc);

            var movementLines = new List<MovementLine>(pendingLineArtifacts.Count);
            var valuationEntries = new List<ValuationEntry>(pendingLineArtifacts.Count);
            var shipmentLines = new List<SalesShipmentLine>(pendingLineArtifacts.Count);

            for (var idx = 0; idx < pendingLineArtifacts.Count; idx++)
            {
                var itemArt = pendingLineArtifacts[idx];
                var pos = lockedInventory.PositionsByItem[itemArt.PickLine.ItemId];
                var pool = lockedInventory.PoolsByItem[itemArt.PickLine.ItemId];

                var movLine = MovementLine.CreateOutboundIssue(
                    itemArt.MovementLineId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    movementId,
                    idx + 1,
                    pos.StockPositionId,
                    itemArt.PickLine.ItemId,
                    itemArt.PickLine.BaseUomId,
                    itemArt.PickLine.PickLineId,
                    itemArt.DispatchQty,
                    itemArt.IssueStep.AppliedUnitCost,
                    itemArt.IssueStep.IssuedCarryingAmount,
                    itemArt.PickLine.WarehouseId,
                    itemArt.PickLine.LocationId,
                    itemArt.PickLine.StockLotId);

                var valEntry = ValuationEntry.Record(
                    itemArt.ValuationEntryId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    pool.ValuationPoolId,
                    itemArt.PickLine.ItemId,
                    ValuationEntryTypes.SalesDispatchOutbound,
                    cmd.DispatchDate,
                    currency,
                    movementId,
                    itemArt.MovementLineId,
                    "SALES_SHIPMENT",
                    shipmentId,
                    shipmentNumber,
                    -itemArt.DispatchQty,
                    -itemArt.IssueStep.IssuedCarryingAmount,
                    itemArt.IssueStep.AppliedUnitCost,
                    itemArt.IssueStep.ValuationStep,
                    journalOutcome.Journal.JournalId,
                    cmd.ActorUserId,
                    cmd.NowUtc);

                var shpLine = SalesShipmentLine.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    shipmentId,
                    idx + 1,
                    itemArt.PickLine.PickLineId,
                    itemArt.SoLine.SoLineId,
                    itemArt.SoLine.LineageLineKey,
                    itemArt.Reservation.StockReservationId,
                    itemArt.PickLine.ItemId,
                    itemArt.PickLine.ItemCodeSnapshot,
                    itemArt.PickLine.ItemNameSnapshot,
                    itemArt.PickLine.BaseUomId,
                    itemArt.PickLine.BaseUomCodeSnapshot,
                    itemArt.PickLine.WarehouseId,
                    itemArt.PickLine.LocationId,
                    itemArt.PickLine.LocationCodeSnapshot,
                    itemArt.PickLine.StockLotId,
                    itemArt.PickLine.LotNumberSnapshot,
                    itemArt.DispatchQty,
                    itemArt.IssueStep.AppliedUnitCost,
                    itemArt.RoundedCogsAmount,
                    itemArt.MovementLineId,
                    itemArt.ValuationEntryId);

                movementLines.Add(movLine);
                valuationEntries.Add(valEntry);
                shipmentLines.Add(shpLine);
            }

            var shipment = SalesShipment.CreateDispatched(
                shipmentId,
                cmd.TenantId,
                cmd.LegalEntityId,
                shipmentNumber,
                pickOrder.PickOrderId,
                so.SalesOrderId,
                so.SoNumber!,
                approvedRevId,
                so.CustomerPartnerId,
                approvedRev.CustomerCodeSnapshot,
                approvedRev.CustomerLegalNameSnapshot,
                approvedRev.DeliveryAddressSnapshot,
                cmd.DispatchDate,
                cmd.CarrierName,
                cmd.TrackingOrVehicleNumber,
                cmd.DeliveryNoteReference.Trim(),
                movementId,
                movementNumber,
                journalOutcome.Journal.JournalId,
                journalOutcome.Journal.JournalNumber,
                currency,
                totalDispatchedQty,
                totalCogsAmount,
                idempKey,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.StockMovements.Add(movement);
            db.MovementLines.AddRange(movementLines);
            db.ValuationEntries.AddRange(valuationEntries);
            db.SalesShipments.Add(shipment);
            db.SalesShipmentLines.AddRange(shipmentLines);

            var payloadJson = JsonSerializer.Serialize(new
            {
                shipment.ShipmentId,
                shipment.ShipmentNumber,
                shipment.PickOrderId,
                pickOrder.PickNumber,
                shipment.SalesOrderId,
                shipment.SalesOrderNumberSnapshot,
                shipment.DeliveryNoteReference,
                shipment.TotalDispatchedBaseQuantity,
                shipment.TotalCogsAmount,
                shipment.StockMovementId,
                shipment.StockMovementNumber,
                shipment.GlJournalId,
                shipment.GlJournalNumber
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SALES_SHIPMENT_DISPATCHED",
                "SALES_SHIPMENT",
                shipment.ShipmentId.ToString("D"),
                $"Dispatched {shipment.ShipmentNumber} for {so.SoNumber} ({totalDispatchedQty:0.######} units, COGS Rp {totalCogsAmount:N0}, {movementNumber}, {journalOutcome.Journal.JournalNumber})",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "fulfillment.shipment.dispatched.v1",
                $"fulfillment:shipment:{shipment.ShipmentId:D}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new SalesShipmentOutcome(true, false, 201, null, null, shipment, shipmentLines, movement, journalOutcome.Journal);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapShipmentFailure(ex);
        }
    }

    public static async Task<SalesShipmentOutcome> ConfirmShipmentDeliveryAsync(
        AtheraDbContext db,
        ConfirmShipmentDeliveryCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.ConfirmedByName) || string.IsNullOrWhiteSpace(cmd.EvidenceReference))
        {
            return SalesShipmentOutcome.Fail(400, "delivery_evidence_required", "Provide both receiver name and delivery evidence reference (POD / Surat Jalan).");
        }

        var shipment = (await db.SalesShipments
            .FromSqlInterpolated(
                $"SELECT * FROM fulfillment.shipments WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND shipment_id = {cmd.ShipmentId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (shipment is null)
        {
            return SalesShipmentOutcome.Fail(404, "shipment_not_found", "Shipment was not found in this legal entity.");
        }

        if (shipment.Status == ShipmentStatuses.Delivered)
        {
            return await BuildShipmentReplayAsync(db, shipment, ct);
        }

        try
        {
            shipment.ConfirmDelivery(cmd.ConfirmedByName.Trim(), cmd.EvidenceReference.Trim(), cmd.ActorUserId, cmd.NowUtc);

            var payloadJson = JsonSerializer.Serialize(new
            {
                shipment.ShipmentId,
                shipment.ShipmentNumber,
                shipment.SalesOrderId,
                shipment.SalesOrderNumberSnapshot,
                shipment.DeliveryConfirmedByName,
                shipment.DeliveryEvidenceReference,
                shipment.DeliveredAtUtc
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SALES_SHIPMENT_DELIVERY_CONFIRMED",
                "SALES_SHIPMENT",
                shipment.ShipmentId.ToString("D"),
                $"Confirmed delivery for {shipment.ShipmentNumber} ({shipment.DeliveryConfirmedByName} · {shipment.DeliveryEvidenceReference})",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "fulfillment.shipment.delivered.v1",
                $"fulfillment:shipment-delivered:{shipment.ShipmentId:D}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return await BuildShipmentReplayAsync(db, shipment, ct, isReplay: false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapShipmentFailure(ex);
        }
    }

    private static async Task<SalesShipmentOutcome> BuildShipmentReplayAsync(
        AtheraDbContext db,
        SalesShipment shipment,
        CancellationToken ct,
        bool isReplay = true)
    {
        var lines = await db.SalesShipmentLines.AsNoTracking()
            .Where(l => l.TenantId == shipment.TenantId && l.LegalEntityId == shipment.LegalEntityId && l.ShipmentId == shipment.ShipmentId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);
        var mov = await db.StockMovements.AsNoTracking()
            .SingleOrDefaultAsync(m => m.TenantId == shipment.TenantId && m.LegalEntityId == shipment.LegalEntityId && m.StockMovementId == shipment.StockMovementId, ct);
        var jrn = await db.JournalEntries.AsNoTracking()
            .SingleOrDefaultAsync(j => j.TenantId == shipment.TenantId && j.LegalEntityId == shipment.LegalEntityId && j.JournalId == shipment.GlJournalId, ct);
        return new SalesShipmentOutcome(true, isReplay, 200, null, null, shipment, lines, mov, jrn);
    }

    private static SalesReservationOutcome MapReservationFailure(Exception ex)
    {
        var (code, detail, status) = ParseDomainException(ex, "fulfillment_reservation_error");
        return SalesReservationOutcome.Fail(status, code, detail);
    }

    private static PickOrderOutcome MapPickFailure(Exception ex)
    {
        var (code, detail, status) = ParseDomainException(ex, "fulfillment_pick_error");
        return PickOrderOutcome.Fail(status, code, detail);
    }

    private static SalesShipmentOutcome MapShipmentFailure(Exception ex)
    {
        var (code, detail, status) = ParseDomainException(ex, "fulfillment_shipment_error");
        return SalesShipmentOutcome.Fail(status, code, detail);
    }

    private static (string Code, string Detail, int Status) ParseDomainException(Exception ex, string defaultCode)
    {
        var msg = ex.Message;
        var idx = msg.IndexOf(':', StringComparison.Ordinal);
        var code = idx > 0 ? msg[..idx].Trim() : defaultCode;
        var detail = idx > 0 ? msg[(idx + 1)..].Trim() : msg;
        var status = ex is ArgumentException && idx <= 0 ? 400 : 409;
        return (code, detail, status);
    }
}
