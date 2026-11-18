using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Inventory;

public sealed record InventoryLineValuationInput(
    Guid PoLineId,
    Guid GoodsReceiptLineId,
    int LineNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    decimal AcceptedBaseQuantity,
    decimal UnitCostBase,
    decimal AcceptedLineAmount,
    Guid? DestinationLocationId = null,
    string? LotNumber = null,
    string? SupplierLotReference = null,
    DateOnly? ManufacturedDate = null,
    DateOnly? ExpiryDate = null);

public sealed record InventoryLineValuationPreview(
    Guid GoodsReceiptLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    decimal AcceptedBaseQuantity,
    decimal UnitCostBase,
    decimal AcceptedLineAmount,
    decimal PreviousPoolBaseQuantity,
    decimal PreviousCarryingValue,
    decimal PreviousMovingAverageCost,
    decimal ProjectedPoolBaseQuantity,
    decimal ProjectedCarryingValue,
    decimal ProjectedMovingAverageCost,
    string? BlockingReason,
    string LotTrackingPolicy = LotTrackingPolicies.None,
    string? LotNumber = null,
    Guid? DestinationLocationId = null,
    string? DestinationLocationCode = null);

public sealed record ResolvedAcceptedStockTarget(
    Guid ValuedAcceptanceLineId,
    Guid GoodsReceiptLineId,
    Guid ItemId,
    Guid BaseUomId,
    Guid WarehouseId,
    Guid LocationId,
    Guid? StockLotId,
    decimal AcceptedBaseQuantity);

public sealed record LockedInventoryTargets(
    IReadOnlyDictionary<Guid, StockPosition> PositionsByItem,
    IReadOnlyDictionary<Guid, ValuationPool> PoolsByItem);

public sealed record PostStockAcceptanceResult(
    bool Succeeded,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    StockMovement? Movement,
    IReadOnlyList<MovementLine> MovementLines,
    IReadOnlyList<ValuationEntry> ValuationEntries,
    IReadOnlyList<StockPosition> UpdatedPositions,
    IReadOnlyList<ValuationPool> UpdatedPools,
    IReadOnlyList<ResolvedAcceptedStockTarget> ResolvedTargets)
{
    public static PostStockAcceptanceResult Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, null, Array.Empty<MovementLine>(), Array.Empty<ValuationEntry>(), Array.Empty<StockPosition>(), Array.Empty<ValuationPool>(), Array.Empty<ResolvedAcceptedStockTarget>());
}

/// <summary>
/// Inventory module owner service maintaining `inventory.*` positions, movements, and OD-04 weighted-average cost pools.
/// Must be called inside the caller's PostgreSQL transaction so stock, valuation and GL journals commit atomically.
/// </summary>
public static class InventoryValuationService
{
    public static async Task<IReadOnlyList<InventoryLineValuationPreview>> PreviewReceiptValuationAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string currencyCode,
        string normalizedStockStatus,
        IReadOnlyList<InventoryLineValuationInput> lines,
        CancellationToken ct)
    {
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var itemPolicies = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, i => i.LotTrackingPolicy, ct);

        var activeLocations = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.Status == Platform.MasterStatuses.Active)
            .ToDictionaryAsync(l => l.LocationId, ct);

        var defaultLocCode = InventoryAvailabilityService.ResolveDefaultLocationCodeForStatus(normalizedStockStatus);
        var defaultLoc = activeLocations.Values.FirstOrDefault(l => l.LocationCode == defaultLocCode);

        var pools = await db.ValuationPools
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId &&
                p.LegalEntityId == entityId &&
                p.CurrencyCode == currencyCode &&
                p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode &&
                itemIds.Contains(p.ItemId))
            .ToDictionaryAsync(p => p.ItemId, ct);

        // Simulate cumulatively across lines so multi-line acceptances for the same item accumulate accurately.
        var runningState = new Dictionary<Guid, (string Status, decimal Qty, decimal Carrying, decimal Cost, long Seq)>();
        foreach (var itemId in itemIds)
        {
            if (pools.TryGetValue(itemId, out var p))
            {
                runningState[itemId] = (p.Status, p.ValuedBaseQuantity, p.CarryingValueAmount, p.MovingAverageUnitCost, p.LastEntrySequence);
            }
            else
            {
                runningState[itemId] = (ValuationPoolStatuses.Active, 0m, 0m, 0m, 0L);
            }
        }

        var previews = new List<InventoryLineValuationPreview>(lines.Count);
        foreach (var line in lines)
        {
            var cur = runningState[line.ItemId];
            var lotPolicy = itemPolicies.GetValueOrDefault(line.ItemId, LotTrackingPolicies.None);
            var trimmedLot = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim().ToUpperInvariant();
            var resolvedLoc = line.DestinationLocationId is { } explicitLocId && activeLocations.TryGetValue(explicitLocId, out var foundLoc)
                ? foundLoc
                : defaultLoc;
            var locCode = resolvedLoc?.LocationCode ?? defaultLocCode;
            var locId = resolvedLoc?.LocationId;

            string? policyOrLocationError = null;
            if (line.DestinationLocationId is { } expId)
            {
                if (!activeLocations.TryGetValue(expId, out var expLoc))
                {
                    policyOrLocationError = "location_not_found: Selected warehouse location does not exist or is inactive.";
                }
                else if (!InventoryAvailabilityService.IsZoneCompatibleWithStatus(expLoc.ZoneType, normalizedStockStatus))
                {
                    policyOrLocationError = $"location_zone_mismatch: Location '{expLoc.LocationCode}' ({expLoc.ZoneType}) cannot hold '{normalizedStockStatus}' stock.";
                }
            }

            if (policyOrLocationError is null)
            {
                if (lotPolicy == LotTrackingPolicies.Required && trimmedLot is null)
                {
                    policyOrLocationError = $"lot_required_by_item_policy: Item '{line.ItemCode}' requires a stock lot number for Gate 2 acceptance.";
                }
                else if (lotPolicy == LotTrackingPolicies.None && trimmedLot is not null)
                {
                    policyOrLocationError = $"lot_not_enabled_for_item: Item '{line.ItemCode}' does not track stock lots.";
                }
            }

            try
            {
                var step = ValuationPool.SimulateReceiptValuation(
                    cur.Status,
                    cur.Qty,
                    cur.Carrying,
                    cur.Cost,
                    cur.Seq,
                    line.AcceptedBaseQuantity,
                    line.AcceptedLineAmount);

                runningState[line.ItemId] = (
                    cur.Status,
                    step.NewBaseQuantity,
                    step.NewCarryingValue,
                    step.NewMovingAverageCost,
                    step.EntrySequence);

                previews.Add(new InventoryLineValuationPreview(
                    line.GoodsReceiptLineId,
                    line.ItemId,
                    line.ItemCode,
                    line.ItemName,
                    line.BaseUomCode,
                    line.AcceptedBaseQuantity,
                    line.UnitCostBase,
                    line.AcceptedLineAmount,
                    step.PreviousBaseQuantity,
                    step.PreviousCarryingValue,
                    step.PreviousMovingAverageCost,
                    step.NewBaseQuantity,
                    step.NewCarryingValue,
                    step.NewMovingAverageCost,
                    policyOrLocationError,
                    lotPolicy,
                    trimmedLot,
                    locId,
                    locCode));
            }
            catch (InvalidOperationException ex)
            {
                previews.Add(new InventoryLineValuationPreview(
                    line.GoodsReceiptLineId,
                    line.ItemId,
                    line.ItemCode,
                    line.ItemName,
                    line.BaseUomCode,
                    line.AcceptedBaseQuantity,
                    line.UnitCostBase,
                    line.AcceptedLineAmount,
                    cur.Qty,
                    cur.Carrying,
                    cur.Cost,
                    cur.Qty,
                    cur.Carrying,
                    cur.Cost,
                    ex.Message,
                    lotPolicy,
                    trimmedLot,
                    locId,
                    locCode));
            }
        }

        return previews;
    }

    public static async Task<LockedInventoryTargets> EnsureAndLockItemTargetsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string currencyCode,
        IEnumerable<(Guid ItemId, Guid BaseUomId)> items,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var distinctItems = items
            .GroupBy(x => x.ItemId)
            .Select(g => g.First())
            .OrderBy(x => x.ItemId)
            .ToList();

        var positions = new Dictionary<Guid, StockPosition>();
        var pools = new Dictionary<Guid, ValuationPool>();

        foreach (var (itemId, baseUomId) in distinctItems)
        {
            var defaultGroup = InventoryValuationDefaults.DefaultValuationGroupCode;
            var newPosId = Guid.NewGuid();
            var newPoolId = Guid.NewGuid();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO inventory.stock_positions (
                    stock_position_id, tenant_id, legal_entity_id, item_id, valuation_group_code, base_uom_id,
                    unrestricted_base_quantity, quality_inspection_base_quantity, blocked_base_quantity,
                    reserved_base_quantity, total_valued_on_hand_base_quantity, available_base_quantity,
                    updated_at_utc, version
                )
                VALUES (
                    {newPosId}, {tenantId}, {entityId}, {itemId}, {defaultGroup}, {baseUomId},
                    0, 0, 0, 0, 0, 0, {nowUtc}, 1
                )
                ON CONFLICT (tenant_id, legal_entity_id, item_id, valuation_group_code) DO NOTHING
                """,
                ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO inventory.valuation_pools (
                    valuation_pool_id, tenant_id, legal_entity_id, item_id, currency_code,
                    valuation_group_code, base_uom_id, status, valued_base_quantity,
                    carrying_value_amount, moving_average_unit_cost, last_entry_sequence,
                    updated_at_utc, version
                )
                VALUES (
                    {newPoolId}, {tenantId}, {entityId}, {itemId}, {currencyCode},
                    {defaultGroup}, {baseUomId}, {ValuationPoolStatuses.Active}, 0, 0, 0, 0,
                    {nowUtc}, 1
                )
                ON CONFLICT (tenant_id, legal_entity_id, item_id, currency_code, valuation_group_code) DO NOTHING
                """,
                ct);

            var pos = (await db.StockPositions
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM inventory.stock_positions
                    WHERE tenant_id = {tenantId}
                      AND legal_entity_id = {entityId}
                      AND item_id = {itemId}
                      AND valuation_group_code = {defaultGroup}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();

            var pool = (await db.ValuationPools
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM inventory.valuation_pools
                    WHERE tenant_id = {tenantId}
                      AND legal_entity_id = {entityId}
                      AND item_id = {itemId}
                      AND currency_code = {currencyCode}
                      AND valuation_group_code = {defaultGroup}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();

            positions[itemId] = pos;
            pools[itemId] = pool;
        }

        return new LockedInventoryTargets(positions, pools);
    }

    public static async Task<PostStockAcceptanceResult> PostAcceptedStockAndValuationAsync(
        AtheraDbContext db,
        LockedInventoryTargets locked,
        Guid tenantId,
        Guid entityId,
        string currencyCode,
        DateOnly postingDate,
        string stockStatus,
        Guid valuedAcceptanceId,
        string acceptanceNumber,
        string idempotencyKey,
        string? notes,
        IReadOnlyList<(InventoryLineValuationInput Line, Guid ValuedAcceptanceLineId)> lines,
        Guid? glJournalId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        try
        {
            var normalizedStatus = StockStatuses.Normalize(stockStatus);
            var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantId, entityId, nowUtc, ct);
            var itemIds = lines.Select(l => l.Line.ItemId).Distinct().ToList();
            var itemLotPolicies = await db.Items.AsNoTracking()
                .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
                .ToDictionaryAsync(i => i.ItemId, i => i.LotTrackingPolicy, ct);

            var seq = await IncrementEntityYearSequenceAsync(db, "inventory.movement_sequences", tenantId, entityId, postingDate.Year, ct);
            var movementNumber = $"MOV-{postingDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
            var movementId = Guid.NewGuid();

            var movement = StockMovement.Create(
                movementId,
                tenantId,
                entityId,
                movementNumber,
                StockMovementTypes.PurchaseReceiptAcceptance,
                postingDate,
                "PROCUREMENT",
                "VALUED_ACCEPTANCE",
                valuedAcceptanceId,
                acceptanceNumber,
                idempotencyKey,
                notes,
                actorUserId,
                nowUtc);

            db.StockMovements.Add(movement);

            var movementLines = new List<MovementLine>(lines.Count);
            var valuationEntries = new List<ValuationEntry>(lines.Count);
            var resolvedTargets = new List<ResolvedAcceptedStockTarget>(lines.Count);

            for (var movLineIdx = 0; movLineIdx < lines.Count; movLineIdx++)
            {
                var (line, acceptanceLineId) = lines[movLineIdx];
                var pos = locked.PositionsByItem[line.ItemId];
                var pool = locked.PoolsByItem[line.ItemId];

                if (pos.BaseUomId != line.BaseUomId || pool.BaseUomId != line.BaseUomId)
                {
                    return PostStockAcceptanceResult.Fail(
                        409,
                        "od04_anomalous_valuation_pool_blocked",
                        $"Base UoM mismatch between accepted item '{line.ItemCode}' and inventory pool.");
                }

                var lotPolicy = itemLotPolicies.GetValueOrDefault(line.ItemId, LotTrackingPolicies.None);
                var trimmedLot = string.IsNullOrWhiteSpace(line.LotNumber) ? null : line.LotNumber.Trim();
                if (lotPolicy == LotTrackingPolicies.Required && trimmedLot is null)
                {
                    return PostStockAcceptanceResult.Fail(
                        409,
                        "lot_required_by_item_policy",
                        $"Item '{line.ItemCode}' requires a stock lot number for Gate 2 acceptance.");
                }

                if (lotPolicy == LotTrackingPolicies.None && trimmedLot is not null)
                {
                    return PostStockAcceptanceResult.Fail(
                        409,
                        "lot_not_enabled_for_item",
                        $"Item '{line.ItemCode}' does not track stock lots.");
                }

                var targetLocation = layout.ResolveForStatus(normalizedStatus, line.DestinationLocationId);
                StockLot? lot = null;
                if (trimmedLot is not null)
                {
                    lot = await InventoryAvailabilityService.UpsertStockLotAsync(
                        db,
                        tenantId,
                        entityId,
                        line.ItemId,
                        trimmedLot,
                        line.SupplierLotReference,
                        line.ManufacturedDate,
                        line.ExpiryDate,
                        valuedAcceptanceId,
                        actorUserId,
                        nowUtc,
                        ct);
                }

                var locBalance = await InventoryAvailabilityService.EnsureAndLockLocationBalanceAsync(
                    db,
                    tenantId,
                    entityId,
                    pos.StockPositionId,
                    line.ItemId,
                    line.BaseUomId,
                    targetLocation.WarehouseId,
                    targetLocation.LocationId,
                    lot?.StockLotId,
                    normalizedStatus,
                    nowUtc,
                    ct);

                var step = pool.ApplyReceiptValuation(line.AcceptedBaseQuantity, line.AcceptedLineAmount, nowUtc);
                pos.ApplyInboundReceipt(line.AcceptedBaseQuantity, normalizedStatus, nowUtc);
                locBalance.AdjustOnHand(line.AcceptedBaseQuantity, nowUtc);

                var movLineId = Guid.NewGuid();
                var movLine = MovementLine.Create(
                    movLineId,
                    tenantId,
                    entityId,
                    movementId,
                    movLineIdx + 1,
                    pos.StockPositionId,
                    line.ItemId,
                    line.BaseUomId,
                    acceptanceLineId,
                    normalizedStatus,
                    line.AcceptedBaseQuantity,
                    line.UnitCostBase,
                    line.AcceptedLineAmount,
                    targetLocation.WarehouseId,
                    targetLocation.LocationId,
                    lot?.StockLotId);

                var valEntry = ValuationEntry.Record(
                    Guid.NewGuid(),
                    tenantId,
                    entityId,
                    pool.ValuationPoolId,
                    line.ItemId,
                    ValuationEntryTypes.ReceiptAcceptance,
                    postingDate,
                    currencyCode,
                    movementId,
                    movLineId,
                    "VALUED_ACCEPTANCE",
                    valuedAcceptanceId,
                    acceptanceNumber,
                    line.AcceptedBaseQuantity,
                    line.AcceptedLineAmount,
                    line.UnitCostBase,
                    step,
                    glJournalId,
                    actorUserId,
                    nowUtc);

                movementLines.Add(movLine);
                valuationEntries.Add(valEntry);
                resolvedTargets.Add(new ResolvedAcceptedStockTarget(
                    acceptanceLineId,
                    line.GoodsReceiptLineId,
                    line.ItemId,
                    line.BaseUomId,
                    targetLocation.WarehouseId,
                    targetLocation.LocationId,
                    lot?.StockLotId,
                    line.AcceptedBaseQuantity));
            }

            db.MovementLines.AddRange(movementLines);
            db.ValuationEntries.AddRange(valuationEntries);

            return new PostStockAcceptanceResult(
                true,
                201,
                null,
                null,
                movement,
                movementLines,
                valuationEntries,
                locked.PositionsByItem.Values.ToList(),
                locked.PoolsByItem.Values.ToList(),
                resolvedTargets);
        }
        catch (ArgumentException ex)
        {
            return PostStockAcceptanceResult.Fail(400, "inventory_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            var code = idx > 0 ? ex.Message[..idx].Trim() : "inventory_domain_error";
            var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
            return PostStockAcceptanceResult.Fail(409, code, detail);
        }
    }

    internal static async Task<long> IncrementEntityYearSequenceAsync(
        AtheraDbContext db,
        string qualifiedTableName,
        Guid tenantId,
        Guid entityId,
        int fiscalYear,
        CancellationToken ct)
    {
        var sql = qualifiedTableName switch
        {
            "inventory.movement_sequences" => """
                INSERT INTO inventory.movement_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = inventory.movement_sequences.last_number + 1
                RETURNING last_number;
                """,
            "inventory.reservation_sequences" => """
                INSERT INTO inventory.reservation_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = inventory.reservation_sequences.last_number + 1
                RETURNING last_number;
                """,
            "quality.inspection_sequences" => """
                INSERT INTO quality.inspection_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = quality.inspection_sequences.last_number + 1
                RETURNING last_number;
                """,
            _ => throw new ArgumentException($"Unsupported sequence table '{qualifiedTableName}'.", nameof(qualifiedTableName))
        };

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = sql;

        var pTid = cmd.CreateParameter();
        pTid.ParameterName = "tid";
        pTid.Value = tenantId;
        cmd.Parameters.Add(pTid);

        var pEid = cmd.CreateParameter();
        pEid.ParameterName = "eid";
        pEid.Value = entityId;
        cmd.Parameters.Add(pEid);

        var pFy = cmd.CreateParameter();
        pFy.ParameterName = "fy";
        pFy.Value = fiscalYear;
        cmd.Parameters.Add(pFy);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
