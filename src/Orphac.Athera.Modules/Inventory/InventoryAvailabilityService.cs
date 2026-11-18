using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Quality;

namespace Orphac.Athera.Modules.Inventory;

public sealed record DefaultWarehouseLayout(
    Warehouse DefaultWarehouse,
    IReadOnlyDictionary<Guid, WarehouseLocation> LocationsById,
    IReadOnlyDictionary<string, WarehouseLocation> LocationsByCode)
{
    public WarehouseLocation ResolveForStatus(string stockStatus, Guid? explicitLocationId = null)
    {
        var normalizedStatus = StockStatuses.Normalize(stockStatus);
        if (explicitLocationId.HasValue && explicitLocationId.Value != Guid.Empty)
        {
            if (!LocationsById.TryGetValue(explicitLocationId.Value, out var explicitLoc))
            {
                throw new InvalidOperationException("location_not_found: Selected warehouse location does not exist in this legal entity.");
            }

            if (explicitLoc.Status != Platform.MasterStatuses.Active)
            {
                throw new InvalidOperationException("location_inactive: Selected warehouse location is inactive.");
            }

            if (!InventoryAvailabilityService.IsZoneCompatibleWithStatus(explicitLoc.ZoneType, normalizedStatus))
            {
                throw new InvalidOperationException(
                    $"location_zone_mismatch: Location '{explicitLoc.LocationCode}' ({explicitLoc.ZoneType}) cannot hold stock with status '{normalizedStatus}'.");
            }

            return explicitLoc;
        }

        var defaultCode = InventoryAvailabilityService.ResolveDefaultLocationCodeForStatus(normalizedStatus);
        return LocationsByCode[defaultCode];
    }
}

public sealed record TransferStockLocationCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    Guid? StockLotId,
    string StockStatus,
    decimal Quantity,
    DateOnly PostingDate,
    string IdempotencyKey,
    string? Notes,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record StockStatusTransitionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ItemId,
    Guid FromLocationId,
    Guid? ToLocationId,
    Guid? StockLotId,
    string FromStatus,
    string ToStatus,
    decimal Quantity,
    DateOnly PostingDate,
    string ReasonCode,
    string? EvidenceReference,
    string? Notes,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CreateStockReservationCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ItemId,
    Guid LocationId,
    Guid? StockLotId,
    string SourceType,
    Guid? SourceDocumentId,
    string SourceDocumentNumber,
    Guid? SourceLineId,
    decimal Quantity,
    DateTimeOffset? ExpiresAtUtc,
    string IdempotencyKey,
    string? Notes,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record InventoryMovementOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    StockMovement? Movement,
    IReadOnlyList<MovementLine> MovementLines,
    StockPosition? UpdatedPosition)
{
    public static InventoryMovementOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, Array.Empty<MovementLine>(), null);
}

public sealed record StockReservationOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    StockReservation? Reservation,
    StockPosition? UpdatedPosition,
    StockLocationBalance? UpdatedLocationBalance)
{
    public static StockReservationOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, null, null);
}

public sealed record ItemAvailabilitySummary(
    Guid ItemId,
    decimal UnrestrictedBaseQuantity,
    decimal QualityInspectionBaseQuantity,
    decimal BlockedBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal TotalValuedOnHandBaseQuantity,
    decimal AvailableBaseQuantity);

public sealed record ItemReconciliationResult(
    bool IsReconciled,
    decimal PositionOnHandTotal,
    decimal PositionUnrestricted,
    decimal PositionQualityInspection,
    decimal PositionBlocked,
    decimal PositionReserved,
    decimal PositionAvailable,
    decimal LocationBalancesOnHandTotal,
    decimal LocationBalancesUnrestricted,
    decimal LocationBalancesQualityInspection,
    decimal LocationBalancesBlocked,
    decimal LocationBalancesReserved,
    decimal MovementsNetTotal,
    decimal ActiveReservationsRemaining);

/// <summary>
/// Inventory owner service for S08 warehouses/locations, lots, physical availability, quality holds, and reservations.
/// Preserves valuation carrying value and GL ledgers untouched across internal stock state and location transitions.
/// </summary>
public static class InventoryAvailabilityService
{
    public static async Task<IReadOnlyDictionary<Guid, ItemAvailabilitySummary>> GetItemAvailabilityMapAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken ct)
    {
        var positions = await db.StockPositions.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == legalEntityId)
            .ToListAsync(ct);

        return positions.ToDictionary(
            p => p.ItemId,
            p => new ItemAvailabilitySummary(
                p.ItemId,
                p.UnrestrictedBaseQuantity,
                p.QualityInspectionBaseQuantity,
                p.BlockedBaseQuantity,
                p.ReservedBaseQuantity,
                p.TotalValuedOnHandBaseQuantity,
                p.AvailableBaseQuantity));
    }

    public static string ResolveDefaultLocationCodeForStatus(string normalizedStatus) =>
        normalizedStatus switch
        {
            StockStatuses.QualityInspection => InventoryAvailabilityDefaults.DefaultQuarantineLocationCode,
            StockStatuses.Blocked => InventoryAvailabilityDefaults.DefaultBlockedLocationCode,
            _ => InventoryAvailabilityDefaults.DefaultMainLocationCode
        };

    public static bool IsZoneCompatibleWithStatus(string zoneType, string normalizedStatus) =>
        normalizedStatus switch
        {
            StockStatuses.Unrestricted => zoneType is LocationZoneTypes.Storage or LocationZoneTypes.ReceivingStaging or LocationZoneTypes.Dispatch,
            StockStatuses.QualityInspection => zoneType == LocationZoneTypes.Quarantine,
            StockStatuses.Blocked => zoneType == LocationZoneTypes.BlockedHold,
            _ => false
        };

    public static async Task<DefaultWarehouseLayout> EnsureDefaultWarehouseAndLocationsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var defaultWhCode = InventoryAvailabilityDefaults.DefaultWarehouseCode;
        var newWhId = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inventory.warehouses (
                warehouse_id, tenant_id, legal_entity_id, warehouse_code, name, is_default, status, created_at_utc, updated_at_utc, version
            )
            VALUES (
                {newWhId}, {tenantId}, {entityId}, {defaultWhCode}, {"Gudang Distribusi Utama"}, {true}, {"ACTIVE"}, {nowUtc}, {nowUtc}, {1L}
            )
            ON CONFLICT (tenant_id, legal_entity_id, warehouse_code) DO NOTHING
            """,
            ct);

        var defaultWh = await db.Warehouses
            .SingleAsync(w => w.TenantId == tenantId && w.LegalEntityId == entityId && w.WarehouseCode == defaultWhCode, ct);

        var defaults = new (string Code, string Name, string Zone, bool IsDefaultRcv)[]
        {
            (InventoryAvailabilityDefaults.DefaultMainLocationCode, "Rak Penyimpanan Utama", LocationZoneTypes.Storage, true),
            (InventoryAvailabilityDefaults.DefaultQuarantineLocationCode, "Zona Karantina Inspeksi Mutu", LocationZoneTypes.Quarantine, false),
            (InventoryAvailabilityDefaults.DefaultBlockedLocationCode, "Area Barang Diblokir", LocationZoneTypes.BlockedHold, false),
            (InventoryAvailabilityDefaults.DefaultDispatchLocationCode, "Area Persiapan Pengiriman", LocationZoneTypes.Dispatch, false)
        };

        foreach (var loc in defaults)
        {
            var locId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO inventory.locations (
                    location_id, tenant_id, legal_entity_id, warehouse_id, location_code, name, zone_type, is_default_receiving, status, created_at_utc, updated_at_utc, version
                )
                VALUES (
                    {locId}, {tenantId}, {entityId}, {defaultWh.WarehouseId}, {loc.Code}, {loc.Name}, {loc.Zone}, {loc.IsDefaultRcv}, {"ACTIVE"}, {nowUtc}, {nowUtc}, {1L}
                )
                ON CONFLICT (tenant_id, legal_entity_id, location_code) DO NOTHING
                """,
                ct);
        }

        var allLocs = await db.WarehouseLocations
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .OrderBy(l => l.LocationCode)
            .ToListAsync(ct);

        return new DefaultWarehouseLayout(
            defaultWh,
            allLocs.ToDictionary(l => l.LocationId),
            allLocs.ToDictionary(l => l.LocationCode, StringComparer.OrdinalIgnoreCase));
    }

    public static async Task<StockLot> UpsertStockLotAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid itemId,
        string lotNumber,
        string? supplierLotReference,
        DateOnly? manufacturedDate,
        DateOnly? expiryDate,
        Guid? sourceValuedAcceptanceId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var candidate = StockLot.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            itemId,
            lotNumber,
            supplierLotReference,
            manufacturedDate,
            expiryDate,
            sourceValuedAcceptanceId,
            actorUserId,
            nowUtc);

        // Defer source_valued_acceptance_id until SaveChanges so uncommitted ValuedAcceptance inserts satisfy FK ordering.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inventory.stock_lots (
                stock_lot_id, tenant_id, legal_entity_id, item_id, lot_number, supplier_lot_reference,
                manufactured_date, expiry_date, source_valued_acceptance_id, created_by_user_id, created_at_utc, updated_at_utc, version
            )
            VALUES (
                {candidate.StockLotId}, {tenantId}, {entityId}, {itemId}, {candidate.LotNumber}, {candidate.SupplierLotReference},
                {candidate.ManufacturedDate}, {candidate.ExpiryDate}, {null}, {actorUserId}, {nowUtc}, {nowUtc}, {1L}
            )
            ON CONFLICT (tenant_id, legal_entity_id, item_id, lot_number) DO NOTHING
            """,
            ct);

        var lot = (await db.StockLots
            .FromSqlInterpolated(
                $"""
                SELECT * FROM inventory.stock_lots
                WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId} AND item_id = {itemId} AND lot_number = {candidate.LotNumber}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .Single();

        if (sourceValuedAcceptanceId.HasValue && lot.SourceValuedAcceptanceId is null)
        {
            lot.AssignSourceAcceptanceIfMissing(sourceValuedAcceptanceId.Value, nowUtc);
        }

        return lot;
    }

    public static async Task<StockLocationBalance> EnsureAndLockLocationBalanceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid stockPositionId,
        Guid itemId,
        Guid baseUomId,
        Guid warehouseId,
        Guid locationId,
        Guid? stockLotId,
        string stockStatus,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var normStatus = StockStatuses.Normalize(stockStatus);
        var newId = Guid.NewGuid();

        if (stockLotId is { } lotId)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO inventory.stock_location_balances (
                    stock_location_balance_id, tenant_id, legal_entity_id, stock_position_id, item_id, base_uom_id,
                    warehouse_id, location_id, stock_lot_id, stock_status, on_hand_base_quantity, reserved_base_quantity,
                    available_base_quantity, updated_at_utc, version
                )
                VALUES (
                    {newId}, {tenantId}, {entityId}, {stockPositionId}, {itemId}, {baseUomId},
                    {warehouseId}, {locationId}, {lotId}, {normStatus}, {0m}, {0m}, {0m}, {nowUtc}, {1L}
                )
                ON CONFLICT (tenant_id, legal_entity_id, warehouse_id, location_id, item_id, stock_lot_id, stock_status)
                WHERE stock_lot_id IS NOT NULL
                DO NOTHING
                """,
                ct);

            return (await db.StockLocationBalances
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM inventory.stock_location_balances
                    WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId}
                      AND warehouse_id = {warehouseId} AND location_id = {locationId}
                      AND item_id = {itemId} AND stock_lot_id = {lotId} AND stock_status = {normStatus}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inventory.stock_location_balances (
                stock_location_balance_id, tenant_id, legal_entity_id, stock_position_id, item_id, base_uom_id,
                warehouse_id, location_id, stock_lot_id, stock_status, on_hand_base_quantity, reserved_base_quantity,
                available_base_quantity, updated_at_utc, version
            )
            VALUES (
                {newId}, {tenantId}, {entityId}, {stockPositionId}, {itemId}, {baseUomId},
                {warehouseId}, {locationId}, {null}, {normStatus}, {0m}, {0m}, {0m}, {nowUtc}, {1L}
            )
            ON CONFLICT (tenant_id, legal_entity_id, warehouse_id, location_id, item_id, stock_status)
            WHERE stock_lot_id IS NULL
            DO NOTHING
            """,
            ct);

        return (await db.StockLocationBalances
            .FromSqlInterpolated(
                $"""
                SELECT * FROM inventory.stock_location_balances
                WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId}
                  AND warehouse_id = {warehouseId} AND location_id = {locationId}
                  AND item_id = {itemId} AND stock_lot_id IS NULL AND stock_status = {normStatus}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .Single();
    }

    public static async Task<StockPosition?> LockStockPositionForUpdateAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid itemId,
        CancellationToken ct)
    {
        return (await db.StockPositions
            .FromSqlInterpolated(
                $"""
                SELECT * FROM inventory.stock_positions
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {entityId}
                  AND item_id = {itemId}
                  AND valuation_group_code = {InventoryValuationDefaults.DefaultValuationGroupCode}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .SingleOrDefault();
    }

    public static async Task<InventoryMovementOutcome> TransferLocationAsync(
        AtheraDbContext db,
        TransferStockLocationCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return InventoryMovementOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        if (cmd.FromLocationId == Guid.Empty || cmd.ToLocationId == Guid.Empty || cmd.FromLocationId == cmd.ToLocationId)
        {
            return InventoryMovementOutcome.Fail(400, "invalid_transfer_locations", "Source and destination warehouse locations must be distinct.");
        }

        var qty = decimal.Round(cmd.Quantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            return InventoryMovementOutcome.Fail(400, "transfer_qty_must_be_positive", "Transfer quantity must be positive.");
        }

        string normalizedStatus;
        try
        {
            normalizedStatus = StockStatuses.Normalize(cmd.StockStatus);
        }
        catch (ArgumentException ex)
        {
            return InventoryMovementOutcome.Fail(400, "invalid_stock_status", ex.Message);
        }

        var idempotencyKey = cmd.IdempotencyKey.Trim();
        var replay = await FindMovementReplayAsync(db, cmd.TenantId, cmd.LegalEntityId, StockMovementTypes.LocationTransfer, cmd.ItemId, idempotencyKey, ct);
        if (replay is not null)
        {
            var matchesPayload = replay.Lines.Count == 2
                && replay.Lines.Any(l => l.LocationId == cmd.FromLocationId && l.StockLotId == cmd.StockLotId && l.ResultingStockStatus == normalizedStatus && l.BaseQuantityDelta == -qty)
                && replay.Lines.Any(l => l.LocationId == cmd.ToLocationId && l.StockLotId == cmd.StockLotId && l.ResultingStockStatus == normalizedStatus && l.BaseQuantityDelta == qty);

            if (!matchesPayload)
            {
                return InventoryMovementOutcome.Fail(409, "duplicate_transfer_idempotency_conflict", "Idempotency key already belongs to a different stock location transfer.");
            }

            var currentPos = await db.StockPositions.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.ItemId == cmd.ItemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode, ct);
            return new InventoryMovementOutcome(true, true, 200, null, null, replay.Movement, replay.Lines, currentPos);
        }

        var item = await db.Items.AsNoTracking()
            .SingleOrDefaultAsync(i => i.TenantId == cmd.TenantId && i.ItemId == cmd.ItemId, ct);
        if (item is null)
        {
            return InventoryMovementOutcome.Fail(404, "item_not_found", "Catalog item was not found in this tenant.");
        }

        if (item.LotTrackingPolicy == LotTrackingPolicies.Required && !cmd.StockLotId.HasValue)
        {
            return InventoryMovementOutcome.Fail(409, "lot_required_by_item_policy", $"Item '{item.ItemCode}' requires a stock lot reference for transfers.");
        }

        if (item.LotTrackingPolicy == LotTrackingPolicies.None && cmd.StockLotId.HasValue)
        {
            return InventoryMovementOutcome.Fail(409, "lot_not_enabled_for_item", $"Item '{item.ItemCode}' does not track stock lots.");
        }

        if (cmd.StockLotId is { } lotId)
        {
            var validLot = await db.StockLots.AsNoTracking().AnyAsync(
                l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.ItemId == cmd.ItemId && l.StockLotId == lotId,
                ct);
            if (!validLot)
            {
                return InventoryMovementOutcome.Fail(404, "stock_lot_not_found", "Specified stock lot does not belong to this item in this legal entity.");
            }
        }

        var position = await LockStockPositionForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.ItemId, ct);
        if (position is null)
        {
            return InventoryMovementOutcome.Fail(409, "insufficient_location_stock", "Item has zero stock position in this legal entity.");
        }

        var locs = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && (l.LocationId == cmd.FromLocationId || l.LocationId == cmd.ToLocationId))
            .ToDictionaryAsync(l => l.LocationId, ct);
        if (!locs.TryGetValue(cmd.FromLocationId, out var fromLoc) || !locs.TryGetValue(cmd.ToLocationId, out var toLoc))
        {
            return InventoryMovementOutcome.Fail(404, "location_not_found", "Source or destination warehouse location was not found in this legal entity.");
        }

        if (fromLoc.Status != Platform.MasterStatuses.Active || toLoc.Status != Platform.MasterStatuses.Active)
        {
            return InventoryMovementOutcome.Fail(409, "location_inactive", "Source and destination locations must be active.");
        }

        if (fromLoc.WarehouseId != toLoc.WarehouseId)
        {
            return InventoryMovementOutcome.Fail(409, "cross_warehouse_transfer_not_in_scope", "Location transfers must remain within the same warehouse.");
        }

        if (!IsZoneCompatibleWithStatus(toLoc.ZoneType, normalizedStatus))
        {
            return InventoryMovementOutcome.Fail(409, "location_zone_mismatch", $"Target location '{toLoc.LocationCode}' ({toLoc.ZoneType}) cannot hold '{normalizedStatus}' stock.");
        }

        var fromBalance = await EnsureAndLockLocationBalanceAsync(
            db, cmd.TenantId, cmd.LegalEntityId, position.StockPositionId, cmd.ItemId, position.BaseUomId,
            fromLoc.WarehouseId, fromLoc.LocationId, cmd.StockLotId, normalizedStatus, cmd.NowUtc, ct);
        var toBalance = await EnsureAndLockLocationBalanceAsync(
            db, cmd.TenantId, cmd.LegalEntityId, position.StockPositionId, cmd.ItemId, position.BaseUomId,
            toLoc.WarehouseId, toLoc.LocationId, cmd.StockLotId, normalizedStatus, cmd.NowUtc, ct);

        if (fromBalance.OnHandBaseQuantity < qty)
        {
            return InventoryMovementOutcome.Fail(409, "od10_negative_stock_forbidden", $"Location '{fromLoc.LocationCode}' holds only {fromBalance.OnHandBaseQuantity:0.######} {normalizedStatus} units.");
        }

        if (normalizedStatus == StockStatuses.Unrestricted && fromBalance.AvailableBaseQuantity < qty)
        {
            return InventoryMovementOutcome.Fail(409, "od10_insufficient_available_stock", $"Location '{fromLoc.LocationCode}' has only {fromBalance.AvailableBaseQuantity:0.######} unreserved available units.");
        }

        if (normalizedStatus == StockStatuses.QualityInspection)
        {
            var queuedPending = await db.ReceiptInspections.AsNoTracking()
                .Where(q =>
                    q.TenantId == cmd.TenantId &&
                    q.LegalEntityId == cmd.LegalEntityId &&
                    q.ItemId == cmd.ItemId &&
                    q.QuarantineLocationId == fromLoc.LocationId &&
                    q.StockLotId == cmd.StockLotId &&
                    q.Status != ReceiptInspectionStatuses.Completed)
                .SumAsync(q => q.PendingBaseQuantity, ct);
            var movableQi = decimal.Round(fromBalance.OnHandBaseQuantity - queuedPending, 6, MidpointRounding.AwayFromZero);
            if (qty > movableQi)
            {
                return InventoryMovementOutcome.Fail(
                    409,
                    "pending_receipt_inspection_location_locked",
                    $"Pending receipt inspection queue locks {queuedPending:0.######} units at '{fromLoc.LocationCode}'; decide inspection disposition instead.");
            }
        }

        var poolCost = await db.ValuationPools.AsNoTracking()
            .Where(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.ItemId == cmd.ItemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode)
            .Select(p => p.MovingAverageUnitCost)
            .SingleOrDefaultAsync(ct);

        fromBalance.AdjustOnHand(-qty, cmd.NowUtc);
        toBalance.AdjustOnHand(+qty, cmd.NowUtc);

        var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(db, "inventory.movement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
        var movementNumber = $"MOV-{cmd.PostingDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
        var movementId = Guid.NewGuid();
        var transferActionId = Guid.NewGuid();

        var movement = StockMovement.Create(
            movementId,
            cmd.TenantId,
            cmd.LegalEntityId,
            movementNumber,
            StockMovementTypes.LocationTransfer,
            cmd.PostingDate,
            "INVENTORY",
            StockMovementTypes.LocationTransfer,
            cmd.ItemId,
            item.ItemCode,
            idempotencyKey,
            cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        var legOut = MovementLine.CreateInternalLeg(
            Guid.NewGuid(), cmd.TenantId, cmd.LegalEntityId, movementId, 1,
            position.StockPositionId, cmd.ItemId, position.BaseUomId, transferActionId,
            normalizedStatus, -qty, poolCost, fromLoc.WarehouseId, fromLoc.LocationId, cmd.StockLotId);
        var legIn = MovementLine.CreateInternalLeg(
            Guid.NewGuid(), cmd.TenantId, cmd.LegalEntityId, movementId, 2,
            position.StockPositionId, cmd.ItemId, position.BaseUomId, transferActionId,
            normalizedStatus, +qty, poolCost, toLoc.WarehouseId, toLoc.LocationId, cmd.StockLotId);

        db.StockMovements.Add(movement);
        db.MovementLines.AddRange(legOut, legIn);

        var payloadJson = JsonSerializer.Serialize(new
        {
            movement.StockMovementId,
            movement.MovementNumber,
            cmd.ItemId,
            item.ItemCode,
            FromLocationCode = fromLoc.LocationCode,
            ToLocationCode = toLoc.LocationCode,
            cmd.StockLotId,
            StockStatus = normalizedStatus,
            Quantity = qty
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "INVENTORY_LOCATION_TRANSFERRED",
            "STOCK_MOVEMENT",
            movement.StockMovementId.ToString("D"),
            $"Transferred {qty:0.######} {item.ItemCode} ({normalizedStatus}) {fromLoc.LocationCode} -> {toLoc.LocationCode}",
            payloadJson,
            cmd.NowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "inventory.stock.transferred.v1",
            $"inventory:transfer:{movement.StockMovementId:D}",
            payloadJson,
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return new InventoryMovementOutcome(true, false, 201, null, null, movement, [legOut, legIn], position);
    }

    public static async Task<InventoryMovementOutcome> TransitionManualStockStatusAsync(
        AtheraDbContext db,
        StockStatusTransitionCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return InventoryMovementOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        var qty = decimal.Round(cmd.Quantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            return InventoryMovementOutcome.Fail(400, "status_transition_qty_must_be_positive", "Quantity must be strictly positive.");
        }

        string fromStatus;
        string toStatus;
        string normalizedReason;
        try
        {
            fromStatus = StockStatuses.Normalize(cmd.FromStatus);
            toStatus = StockStatuses.Normalize(cmd.ToStatus);
            normalizedReason = QualityReasonCodes.Normalize(cmd.ReasonCode);
        }
        catch (ArgumentException ex)
        {
            return InventoryMovementOutcome.Fail(400, "invalid_quality_transition_input", ex.Message);
        }

        if (fromStatus == toStatus)
        {
            return InventoryMovementOutcome.Fail(400, "status_transition_same_status", "Source and target stock statuses must differ.");
        }

        var normNotes = Platform.GuardValidation.NormalizeOptional(cmd.Notes, 300, nameof(cmd.Notes));
        var normEvidence = Platform.GuardValidation.NormalizeOptional(cmd.EvidenceReference, 120, nameof(cmd.EvidenceReference));
        if ((toStatus is StockStatuses.Blocked or StockStatuses.QualityInspection)
            && normalizedReason == QualityReasonCodes.PassedSpecification
            && normNotes is null && normEvidence is null)
        {
            return InventoryMovementOutcome.Fail(400, "quality_reason_required", "Restricting or blocking stock requires a failure reason, evidence reference, or inspection note.");
        }

        var idempotencyKey = cmd.IdempotencyKey.Trim();
        var replay = await FindMovementReplayAsync(db, cmd.TenantId, cmd.LegalEntityId, StockMovementTypes.QualityStatusChange, cmd.ItemId, idempotencyKey, ct);
        if (replay is not null)
        {
            var outLeg = replay.Lines.FirstOrDefault(l => l.BaseQuantityDelta < 0m);
            var inLeg = replay.Lines.FirstOrDefault(l => l.BaseQuantityDelta > 0m);
            if (outLeg is null || inLeg is null || outLeg.LocationId != cmd.FromLocationId || outLeg.ResultingStockStatus != fromStatus || inLeg.ResultingStockStatus != toStatus || inLeg.BaseQuantityDelta != qty || outLeg.StockLotId != cmd.StockLotId)
            {
                return InventoryMovementOutcome.Fail(409, "duplicate_status_transition_idempotency_conflict", "Idempotency key already belongs to a different quality status transition.");
            }

            var currentPos = await db.StockPositions.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.ItemId == cmd.ItemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode, ct);
            return new InventoryMovementOutcome(true, true, 200, null, null, replay.Movement, replay.Lines, currentPos);
        }

        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == cmd.TenantId && i.ItemId == cmd.ItemId, ct);
        if (item is null)
        {
            return InventoryMovementOutcome.Fail(404, "item_not_found", "Catalog item was not found in this tenant.");
        }

        if (item.LotTrackingPolicy == LotTrackingPolicies.Required && !cmd.StockLotId.HasValue)
        {
            return InventoryMovementOutcome.Fail(409, "lot_required_by_item_policy", $"Item '{item.ItemCode}' requires a stock lot reference.");
        }

        if (cmd.StockLotId is { } statusLotId)
        {
            var validLot = await db.StockLots.AsNoTracking().AnyAsync(
                l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.ItemId == cmd.ItemId && l.StockLotId == statusLotId,
                ct);
            if (!validLot)
            {
                return InventoryMovementOutcome.Fail(404, "stock_lot_not_found", "Specified stock lot does not belong to this item in this legal entity.");
            }
        }

        if (fromStatus == StockStatuses.QualityInspection)
        {
            var openPending = await db.ReceiptInspections.AsNoTracking()
                .Where(q =>
                    q.TenantId == cmd.TenantId &&
                    q.LegalEntityId == cmd.LegalEntityId &&
                    q.ItemId == cmd.ItemId &&
                    q.QuarantineLocationId == cmd.FromLocationId &&
                    q.StockLotId == cmd.StockLotId &&
                    q.Status != ReceiptInspectionStatuses.Completed)
                .SumAsync(q => q.PendingBaseQuantity, ct);
            var posBal = await db.StockLocationBalances.AsNoTracking()
                .Where(b =>
                    b.TenantId == cmd.TenantId &&
                    b.LegalEntityId == cmd.LegalEntityId &&
                    b.LocationId == cmd.FromLocationId &&
                    b.ItemId == cmd.ItemId &&
                    b.StockLotId == cmd.StockLotId &&
                    b.StockStatus == StockStatuses.QualityInspection)
                .Select(b => b.OnHandBaseQuantity)
                .SingleOrDefaultAsync(ct);

            if (qty > decimal.Round(posBal - openPending, 6, MidpointRounding.AwayFromZero))
            {
                return InventoryMovementOutcome.Fail(409, "use_receipt_inspection_disposition", "Record quality inspection disposition in the Receipt Inspection queue for Gate 2 quarantined lots.");
            }
        }

        var layout = await EnsureDefaultWarehouseAndLocationsAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
        if (!layout.LocationsById.TryGetValue(cmd.FromLocationId, out var fromLoc))
        {
            return InventoryMovementOutcome.Fail(404, "location_not_found", "Source location was not found in this legal entity.");
        }

        WarehouseLocation toLoc;
        try
        {
            toLoc = layout.ResolveForStatus(toStatus, cmd.ToLocationId);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            return InventoryMovementOutcome.Fail(409, idx > 0 ? ex.Message[..idx] : "location_invalid", idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message);
        }

        var movement = await PostQualitySplitMovementAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            item,
            fromLoc,
            fromStatus,
            cmd.StockLotId,
            [(toLoc, toStatus, qty)],
            cmd.PostingDate,
            StockMovementTypes.QualityStatusChange,
            cmd.ItemId,
            item.ItemCode,
            Guid.NewGuid(),
            idempotencyKey,
            normNotes ?? $"{normalizedReason} {(normEvidence ?? string.Empty)}".Trim(),
            cmd.ActorUserId,
            cmd.NowUtc,
            ct);

        if (!movement.Succeeded)
        {
            return movement;
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            movement.Movement!.StockMovementId,
            movement.Movement.MovementNumber,
            cmd.ItemId,
            item.ItemCode,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Quantity = qty,
            ReasonCode = normalizedReason,
            EvidenceReference = normEvidence
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "QUALITY_STATUS_CHANGED",
            "STOCK_MOVEMENT",
            movement.Movement.StockMovementId.ToString("D"),
            $"Changed {qty:0.######} {item.ItemCode} from {fromStatus} ({fromLoc.LocationCode}) to {toStatus} ({toLoc.LocationCode}) [{normalizedReason}]",
            payloadJson,
            cmd.NowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "inventory.stock.status_changed.v1",
            $"inventory:status:{movement.Movement.StockMovementId:D}",
            payloadJson,
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return movement;
    }

    public static async Task<InventoryMovementOutcome> PostQualitySplitMovementAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Item item,
        WarehouseLocation fromLocation,
        string fromStatus,
        Guid? stockLotId,
        IReadOnlyList<(WarehouseLocation TargetLocation, string TargetStatus, decimal Quantity)> targets,
        DateOnly postingDate,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        Guid sourceLineId,
        string idempotencyKey,
        string? notes,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var totalQty = decimal.Round(targets.Sum(t => t.Quantity), 6, MidpointRounding.AwayFromZero);
        if (totalQty <= 0m)
        {
            return InventoryMovementOutcome.Fail(400, "disposition_qty_must_be_positive", "Total moved quantity must be positive.");
        }

        var position = await LockStockPositionForUpdateAsync(db, tenantId, entityId, item.ItemId, ct);
        if (position is null)
        {
            return InventoryMovementOutcome.Fail(409, "insufficient_location_stock", "Item has zero stock position in this legal entity.");
        }

        try
        {
            var fromBalance = await EnsureAndLockLocationBalanceAsync(
                db, tenantId, entityId, position.StockPositionId, item.ItemId, position.BaseUomId,
                fromLocation.WarehouseId, fromLocation.LocationId, stockLotId, fromStatus, nowUtc, ct);

            if (fromBalance.OnHandBaseQuantity < totalQty)
            {
                return InventoryMovementOutcome.Fail(
                    409,
                    "od10_negative_stock_forbidden",
                    $"Source location '{fromLocation.LocationCode}' holds {fromBalance.OnHandBaseQuantity:0.######} {fromStatus} units, less than requested {totalQty:0.######}.");
            }

            if (fromStatus == StockStatuses.Unrestricted && fromBalance.AvailableBaseQuantity < totalQty)
            {
                return InventoryMovementOutcome.Fail(
                    409,
                    "od10_insufficient_available_stock",
                    $"Source location '{fromLocation.LocationCode}' has only {fromBalance.AvailableBaseQuantity:0.######} unreserved available units.");
            }

            fromBalance.AdjustOnHand(-totalQty, nowUtc);

            var poolCost = await db.ValuationPools.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.ItemId == item.ItemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode)
                .Select(p => p.MovingAverageUnitCost)
                .SingleOrDefaultAsync(ct);

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(db, "inventory.movement_sequences", tenantId, entityId, postingDate.Year, ct);
            var movementNumber = $"MOV-{postingDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
            var movementId = Guid.NewGuid();

            var movement = StockMovement.Create(
                movementId,
                tenantId,
                entityId,
                movementNumber,
                StockMovementTypes.QualityStatusChange,
                postingDate,
                sourceDocumentType == "QUALITY_DISPOSITION" ? "QUALITY" : "INVENTORY",
                sourceDocumentType,
                sourceDocumentId,
                sourceDocumentNumber,
                idempotencyKey,
                notes,
                actorUserId,
                nowUtc);

            var movLines = new List<MovementLine>(1 + targets.Count)
            {
                MovementLine.CreateInternalLeg(
                    Guid.NewGuid(),
                    tenantId,
                    entityId,
                    movementId,
                    1,
                    position.StockPositionId,
                    item.ItemId,
                    position.BaseUomId,
                    sourceLineId,
                    fromStatus,
                    -totalQty,
                    poolCost,
                    fromLocation.WarehouseId,
                    fromLocation.LocationId,
                    stockLotId)
            };

            var lineNum = 2;
            foreach (var target in targets)
            {
                var targetQty = decimal.Round(target.Quantity, 6, MidpointRounding.AwayFromZero);
                if (targetQty <= 0m) continue;

                var toBalance = await EnsureAndLockLocationBalanceAsync(
                    db, tenantId, entityId, position.StockPositionId, item.ItemId, position.BaseUomId,
                    target.TargetLocation.WarehouseId, target.TargetLocation.LocationId, stockLotId, target.TargetStatus, nowUtc, ct);

                toBalance.AdjustOnHand(+targetQty, nowUtc);
                position.ApplyStatusTransition(fromStatus, target.TargetStatus, targetQty, nowUtc);

                movLines.Add(MovementLine.CreateInternalLeg(
                    Guid.NewGuid(),
                    tenantId,
                    entityId,
                    movementId,
                    lineNum++,
                    position.StockPositionId,
                    item.ItemId,
                    position.BaseUomId,
                    sourceLineId,
                    target.TargetStatus,
                    +targetQty,
                    poolCost,
                    target.TargetLocation.WarehouseId,
                    target.TargetLocation.LocationId,
                    stockLotId));
            }

            db.StockMovements.Add(movement);
            db.MovementLines.AddRange(movLines);
            return new InventoryMovementOutcome(true, false, 201, null, null, movement, movLines, position);
        }
        catch (ArgumentException ex)
        {
            return InventoryMovementOutcome.Fail(400, "inventory_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            return InventoryMovementOutcome.Fail(
                409,
                idx > 0 ? ex.Message[..idx].Trim() : "inventory_domain_error",
                idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message);
        }
    }

    public static async Task<StockReservationOutcome> CreateReservationAsync(
        AtheraDbContext db,
        CreateStockReservationCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return StockReservationOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        var idempotencyKey = cmd.IdempotencyKey.Trim();
        var existingFast = await db.StockReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.IdempotencyKey == idempotencyKey, ct);
        if (existingFast is not null)
        {
            return await BuildReservationReplayAsync(db, cmd, existingFast, ct);
        }

        var qty = decimal.Round(cmd.Quantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            return StockReservationOutcome.Fail(400, "reservation_qty_must_be_positive", "Reserved quantity must be positive.");
        }

        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == cmd.TenantId && i.ItemId == cmd.ItemId, ct);
        if (item is null)
        {
            return StockReservationOutcome.Fail(404, "item_not_found", "Catalog item was not found in this tenant.");
        }

        var loc = await db.WarehouseLocations.AsNoTracking()
            .SingleOrDefaultAsync(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.LocationId == cmd.LocationId, ct);
        if (loc is null)
        {
            return StockReservationOutcome.Fail(404, "location_not_found", "Warehouse location was not found in this legal entity.");
        }

        var position = await LockStockPositionForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.ItemId, ct);
        if (position is null)
        {
            return StockReservationOutcome.Fail(409, "od10_insufficient_available_stock", "Item has zero stock position in this legal entity.");
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
                      AND item_id = {cmd.ItemId} AND stock_lot_id = {lotId} AND stock_status = {StockStatuses.Unrestricted}
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
                      AND item_id = {cmd.ItemId} AND stock_lot_id IS NULL AND stock_status = {StockStatuses.Unrestricted}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();
        }

        if (balance is null)
        {
            return StockReservationOutcome.Fail(
                409,
                "od10_insufficient_available_stock",
                $"No UNRESTRICTED stock balance exists at '{loc.LocationCode}' matching this lot selection.");
        }

        var existingLocked = await db.StockReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.IdempotencyKey == idempotencyKey, ct);
        if (existingLocked is not null)
        {
            return await BuildReservationReplayAsync(db, cmd, existingLocked, ct);
        }

        try
        {
            var payloadHash = ReservationHash.ComputePayloadHash(balance.StockLocationBalanceId, cmd.SourceType, cmd.SourceDocumentNumber, qty);
            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(db, "inventory.reservation_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc.Year, ct);
            var rsvNumber = $"RSV-{cmd.NowUtc.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";

            var reservation = StockReservation.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                rsvNumber,
                balance,
                cmd.SourceType,
                cmd.SourceDocumentId,
                cmd.SourceDocumentNumber,
                cmd.SourceLineId,
                idempotencyKey,
                payloadHash,
                qty,
                cmd.Notes,
                cmd.ExpiresAtUtc,
                cmd.ActorUserId,
                cmd.NowUtc);

            balance.ApplyReservationDelta(+qty, cmd.NowUtc);
            position.ApplyReservationDelta(+qty, cmd.NowUtc);
            db.StockReservations.Add(reservation);

            var eventPayload = JsonSerializer.Serialize(new
            {
                reservation.StockReservationId,
                reservation.ReservationNumber,
                cmd.ItemId,
                item.ItemCode,
                loc.LocationCode,
                cmd.StockLotId,
                reservation.SourceType,
                reservation.SourceDocumentNumber,
                ReservedBaseQuantity = qty
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "STOCK_RESERVED",
                "STOCK_RESERVATION",
                reservation.StockReservationId.ToString("D"),
                $"Reserved {qty:0.######} {item.ItemCode} at {loc.LocationCode} ({reservation.ReservationNumber})",
                eventPayload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "inventory.stock.reserved.v1",
                $"inventory:reservation:{reservation.StockReservationId:D}",
                eventPayload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return new StockReservationOutcome(true, false, 201, null, null, reservation, position, balance);
        }
        catch (ArgumentException ex)
        {
            return StockReservationOutcome.Fail(400, "invalid_reservation", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            return StockReservationOutcome.Fail(
                409,
                idx > 0 ? ex.Message[..idx].Trim() : "reservation_domain_error",
                idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message);
        }
    }

    public static async Task<StockReservationOutcome> ReleaseReservationAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid reservationId,
        decimal? releaseQuantity,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var rsvRef = await db.StockReservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.StockReservationId == reservationId)
            .Select(r => new { r.ItemId, r.StockLocationBalanceId })
            .SingleOrDefaultAsync(ct);

        if (rsvRef is null)
        {
            return StockReservationOutcome.Fail(404, "reservation_not_found", "Stock reservation was not found in this legal entity.");
        }

        var position = await LockStockPositionForUpdateAsync(db, tenantId, entityId, rsvRef.ItemId, ct);
        if (position is null)
        {
            return StockReservationOutcome.Fail(409, "stock_position_missing", "Parent stock position was not found.");
        }

        var balance = (await db.StockLocationBalances
            .FromSqlInterpolated(
                $"SELECT * FROM inventory.stock_location_balances WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId} AND stock_location_balance_id = {rsvRef.StockLocationBalanceId} FOR UPDATE")
            .ToListAsync(ct))
            .Single();

        var reservation = (await db.StockReservations
            .FromSqlInterpolated(
                $"SELECT * FROM inventory.stock_reservations WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId} AND stock_reservation_id = {reservationId} FOR UPDATE")
            .ToListAsync(ct))
            .Single();

        try
        {
            var released = reservation.Release(releaseQuantity, actorUserId, nowUtc);
            balance.ApplyReservationDelta(-released, nowUtc);
            position.ApplyReservationDelta(-released, nowUtc);

            var eventPayload = JsonSerializer.Serialize(new
            {
                reservation.StockReservationId,
                reservation.ReservationNumber,
                ReleasedQuantity = released,
                RemainingReserved = reservation.RemainingReservedBaseQuantity,
                reservation.Status
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                tenantId,
                entityId,
                actorUserId,
                "STOCK_RESERVATION_RELEASED",
                "STOCK_RESERVATION",
                reservation.StockReservationId.ToString("D"),
                $"Released {released:0.######} from reservation {reservation.ReservationNumber} (Remaining={reservation.RemainingReservedBaseQuantity:0.######})",
                eventPayload,
                nowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                "inventory.stock.reservation_released.v1",
                $"inventory:reservation-release:{reservation.StockReservationId:D}:v{reservation.Version}",
                eventPayload,
                nowUtc));

            await db.SaveChangesAsync(ct);
            return new StockReservationOutcome(true, false, 200, null, null, reservation, position, balance);
        }
        catch (ArgumentException ex)
        {
            return StockReservationOutcome.Fail(400, "invalid_release_quantity", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            return StockReservationOutcome.Fail(
                409,
                idx > 0 ? ex.Message[..idx].Trim() : "reservation_release_error",
                idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message);
        }
    }

    public static async Task<ItemReconciliationResult> VerifyItemReconciliationAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid itemId,
        CancellationToken ct)
    {
        var pos = await db.StockPositions.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.ItemId == itemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode, ct);

        var balances = await db.StockLocationBalances.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.ItemId == itemId)
            .ToListAsync(ct);

        var movementsNet = await db.MovementLines.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.ItemId == itemId)
            .SumAsync(m => (decimal?)m.BaseQuantityDelta, ct) ?? 0m;

        var activeReservations = await db.StockReservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.ItemId == itemId && r.RemainingReservedBaseQuantity > 0m)
            .SumAsync(r => (decimal?)r.RemainingReservedBaseQuantity, ct) ?? 0m;

        var posOnHand = pos?.TotalValuedOnHandBaseQuantity ?? 0m;
        var posUnrest = pos?.UnrestrictedBaseQuantity ?? 0m;
        var posQi = pos?.QualityInspectionBaseQuantity ?? 0m;
        var posBlk = pos?.BlockedBaseQuantity ?? 0m;
        var posRes = pos?.ReservedBaseQuantity ?? 0m;
        var posAvail = pos?.AvailableBaseQuantity ?? 0m;

        var locOnHand = decimal.Round(balances.Sum(b => b.OnHandBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var locUnrest = decimal.Round(balances.Where(b => b.StockStatus == StockStatuses.Unrestricted).Sum(b => b.OnHandBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var locQi = decimal.Round(balances.Where(b => b.StockStatus == StockStatuses.QualityInspection).Sum(b => b.OnHandBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var locBlk = decimal.Round(balances.Where(b => b.StockStatus == StockStatuses.Blocked).Sum(b => b.OnHandBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var locRes = decimal.Round(balances.Sum(b => b.ReservedBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var movNet = decimal.Round(movementsNet, 6, MidpointRounding.AwayFromZero);
        var rsvRem = decimal.Round(activeReservations, 6, MidpointRounding.AwayFromZero);

        var reconciled =
            posOnHand == locOnHand &&
            posOnHand == movNet &&
            posUnrest == locUnrest &&
            posQi == locQi &&
            posBlk == locBlk &&
            posRes == locRes &&
            posRes == rsvRem &&
            posAvail == posUnrest - posRes;

        return new ItemReconciliationResult(
            reconciled,
            posOnHand,
            posUnrest,
            posQi,
            posBlk,
            posRes,
            posAvail,
            locOnHand,
            locUnrest,
            locQi,
            locBlk,
            locRes,
            movNet,
            rsvRem);
    }

    private sealed record MovementReplayData(StockMovement Movement, List<MovementLine> Lines);

    private static async Task<MovementReplayData?> FindMovementReplayAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string docType,
        Guid itemId,
        string idempotencyKey,
        CancellationToken ct)
    {
        var mov = await db.StockMovements.AsNoTracking()
            .SingleOrDefaultAsync(m =>
                m.TenantId == tenantId &&
                m.LegalEntityId == entityId &&
                m.SourceDocumentType == docType &&
                m.SourceDocumentId == itemId &&
                m.IdempotencyKey == idempotencyKey,
                ct);
        if (mov is null) return null;

        var lines = await db.MovementLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.StockMovementId == mov.StockMovementId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        return new MovementReplayData(mov, lines);
    }

    private static async Task<StockReservationOutcome> BuildReservationReplayAsync(
        AtheraDbContext db,
        CreateStockReservationCommand cmd,
        StockReservation existing,
        CancellationToken ct)
    {
        var expectedHash = ReservationHash.ComputePayloadHash(
            existing.StockLocationBalanceId,
            cmd.SourceType,
            cmd.SourceDocumentNumber,
            cmd.Quantity);

        if (!string.Equals(existing.PayloadHash, expectedHash, StringComparison.Ordinal) ||
            existing.ItemId != cmd.ItemId ||
            existing.LocationId != cmd.LocationId ||
            existing.StockLotId != cmd.StockLotId)
        {
            return StockReservationOutcome.Fail(
                409,
                "duplicate_reservation_idempotency_conflict",
                "Idempotency key already belongs to a different stock reservation payload.");
        }

        var pos = await db.StockPositions.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.StockPositionId == existing.StockPositionId, ct);
        var bal = await db.StockLocationBalances.AsNoTracking()
            .SingleOrDefaultAsync(b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.StockLocationBalanceId == existing.StockLocationBalanceId, ct);

        return new StockReservationOutcome(true, true, 200, null, null, existing, pos, bal);
    }
}
