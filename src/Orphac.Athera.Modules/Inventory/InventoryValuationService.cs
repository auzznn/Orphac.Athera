using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

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

public sealed record PostOperationalIssueCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid ItemId,
    Guid? LocationId,
    Guid? StockLotId,
    decimal Quantity,
    DateOnly PostingDate,
    string SourceReference,
    string IdempotencyKey,
    string? Notes,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PostOperationalIssueResult(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    StockMovement? Movement,
    MovementLine? MovementLine,
    ValuationEntry? ValuationEntry,
    StockPosition? UpdatedPosition,
    ValuationPool? UpdatedPool,
    JournalEntry? GlJournal,
    IReadOnlyList<JournalLine> GlJournalLines)
{
    public static PostOperationalIssueResult Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, null, null, null, null, null, Array.Empty<JournalLine>());
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
            "procurement.return_sequences" => """
                INSERT INTO procurement.return_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = procurement.return_sequences.last_number + 1
                RETURNING last_number;
                """,
            "procurement.replacement_sequences" => """
                INSERT INTO procurement.replacement_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = procurement.replacement_sequences.last_number + 1
                RETURNING last_number;
                """,
            "ap.credit_sequences" => """
                INSERT INTO ap.credit_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = ap.credit_sequences.last_number + 1
                RETURNING last_number;
                """,
            "ap.payment_sequences" => """
                INSERT INTO ap.payment_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = ap.payment_sequences.last_number + 1
                RETURNING last_number;
                """,
            "cash.instruction_sequences" => """
                INSERT INTO cash.instruction_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = cash.instruction_sequences.last_number + 1
                RETURNING last_number;
                """,
            "cash.statement_sequences" => """
                INSERT INTO cash.statement_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = cash.statement_sequences.last_number + 1
                RETURNING last_number;
                """,
            "cash.match_sequences" => """
                INSERT INTO cash.match_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = cash.match_sequences.last_number + 1
                RETURNING last_number;
                """,
            "sales.sales_order_sequences" => """
                INSERT INTO sales.sales_order_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = sales.sales_order_sequences.last_number + 1
                RETURNING last_number;
                """,
            "fulfillment.pick_sequences" => """
                INSERT INTO fulfillment.pick_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = fulfillment.pick_sequences.last_number + 1
                RETURNING last_number;
                """,
            "fulfillment.shipment_sequences" => """
                INSERT INTO fulfillment.shipment_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, @fy, 1)
                ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
                DO UPDATE SET last_number = fulfillment.shipment_sequences.last_number + 1
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

    /// <summary>
    /// Atomically issues unrestricted inventory (e.g. sales/consumption), reducing on-hand & carrying value
    /// at current moving-average cost and posting Dr COGS / Cr Inventory Asset so later S09 invoices apply DEC-P2P-013.
    /// </summary>
    public static async Task<PostOperationalIssueResult> PostOperationalIssueAsync(
        AtheraDbContext db,
        PostOperationalIssueCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var currency = GuardValidation.RequireExactIsoCode(cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            var qty = decimal.Round(cmd.Quantity, 6, MidpointRounding.AwayFromZero);
            if (qty <= 0m)
            {
                return PostOperationalIssueResult.Fail(400, "issue_qty_must_be_positive", "Issue quantity must be strictly positive.");
            }

            var idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);
            var sourceRef = string.IsNullOrWhiteSpace(cmd.SourceReference) ? "ISS-OPS" : GuardValidation.RequireName(cmd.SourceReference, nameof(cmd.SourceReference), 40);

            var existingMovement = await db.StockMovements.AsNoTracking()
                .SingleOrDefaultAsync(m =>
                    m.TenantId == cmd.TenantId &&
                    m.LegalEntityId == cmd.LegalEntityId &&
                    m.SourceDocumentType == StockMovementTypes.OperationalIssue &&
                    m.SourceDocumentId == cmd.ItemId &&
                    m.IdempotencyKey == idempKey,
                    ct);
            if (existingMovement is not null)
            {
                var existingLine = await db.MovementLines.AsNoTracking()
                    .SingleOrDefaultAsync(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.StockMovementId == existingMovement.StockMovementId, ct);
                var existingVal = await db.ValuationEntries.AsNoTracking()
                    .SingleOrDefaultAsync(v => v.TenantId == cmd.TenantId && v.LegalEntityId == cmd.LegalEntityId && v.StockMovementId == existingMovement.StockMovementId, ct);
                var currentPos = await db.StockPositions.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.ItemId == cmd.ItemId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode, ct);
                var currentPool = await db.ValuationPools.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.ItemId == cmd.ItemId && p.CurrencyCode == currency && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode, ct);
                var existingJournal = existingVal?.GlJournalId is { } jid
                    ? await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.JournalId == jid, ct)
                    : null;
                var existingJournalLines = existingJournal is not null
                    ? await db.JournalLines.AsNoTracking().Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.JournalId == existingJournal.JournalId).OrderBy(l => l.LineNumber).ToListAsync(ct)
                    : new List<JournalLine>();
                return new PostOperationalIssueResult(true, true, 200, null, null, existingMovement, existingLine, existingVal, currentPos, currentPool, existingJournal, existingJournalLines);
            }

            var item = await db.Items.AsNoTracking()
                .SingleOrDefaultAsync(i => i.TenantId == cmd.TenantId && i.ItemId == cmd.ItemId, ct);
            if (item is null)
            {
                return PostOperationalIssueResult.Fail(404, "item_not_found", "Catalog item was not found in this tenant.");
            }

            if (item.LotTrackingPolicy == LotTrackingPolicies.Required && !cmd.StockLotId.HasValue)
            {
                return PostOperationalIssueResult.Fail(409, "lot_required_by_item_policy", $"Item '{item.ItemCode}' requires a stock lot reference for operational issues.");
            }

            if (item.LotTrackingPolicy == LotTrackingPolicies.None && cmd.StockLotId.HasValue)
            {
                return PostOperationalIssueResult.Fail(409, "lot_not_enabled_for_item", $"Item '{item.ItemCode}' does not track stock lots.");
            }

            var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
            var sourceLocation = layout.ResolveForStatus(StockStatuses.Unrestricted, cmd.LocationId);

            var locked = await EnsureAndLockItemTargetsAsync(
                db, cmd.TenantId, cmd.LegalEntityId, currency, [(item.ItemId, item.BaseUomId)], cmd.NowUtc, ct);
            var pos = locked.PositionsByItem[item.ItemId];
            var pool = locked.PoolsByItem[item.ItemId];

            var locBalance = await InventoryAvailabilityService.EnsureAndLockLocationBalanceAsync(
                db, cmd.TenantId, cmd.LegalEntityId, pos.StockPositionId, item.ItemId, item.BaseUomId,
                sourceLocation.WarehouseId, sourceLocation.LocationId, cmd.StockLotId, StockStatuses.Unrestricted, cmd.NowUtc, ct);

            if (locBalance.AvailableBaseQuantity < qty)
            {
                return PostOperationalIssueResult.Fail(
                    409,
                    "od10_insufficient_available_stock",
                    $"Location '{sourceLocation.LocationCode}' has only {locBalance.AvailableBaseQuantity:0.######} unreserved unrestricted units available to issue.");
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
                return PostOperationalIssueResult.Fail(
                    409,
                    "missing_inventory_or_cogs_mapping",
                    "COGS_DEFAULT and INVENTORY_ASSET GL account mappings are required to post valued stock issues.");
            }

            var issueStep = pool.ApplyOperationalIssue(qty, cmd.NowUtc);
            pos.ApplyOutboundIssue(qty, cmd.NowUtc);
            locBalance.AdjustOnHand(-qty, cmd.NowUtc);

            var seq = await IncrementEntityYearSequenceAsync(db, "inventory.movement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
            var movementNumber = $"MOV-{cmd.PostingDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
            var movementId = Guid.NewGuid();
            var movementLineId = Guid.NewGuid();

            var journalKey = idempKey.Length <= 64
                ? $"iss:{cmd.ItemId:N}:{idempKey}"
                : $"iss:{cmd.ItemId:N}:{movementNumber}";
            if (journalKey.Length > 80) journalKey = journalKey[..80];

            var journalOutcome = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    currency,
                    cmd.PostingDate,
                    currency,
                    $"Operational Stock Issue {movementNumber} · {item.ItemCode} (-{qty:0.######})",
                    "INVENTORY",
                    StockMovementTypes.OperationalIssue,
                    movementNumber,
                    journalKey,
                    [
                        new(cogsMap.AccountId, $"Dr COGS {item.ItemCode} (-{qty:0.######}) · {movementNumber}", DebitAmount: issueStep.IssuedCarryingAmount, CreditAmount: 0m, ItemId: item.ItemId),
                        new(invMap.AccountId, $"Cr Inventory Asset {item.ItemCode} · {movementNumber}", DebitAmount: 0m, CreditAmount: issueStep.IssuedCarryingAmount, ItemId: item.ItemId)
                    ],
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
            {
                return PostOperationalIssueResult.Fail(
                    journalOutcome.FailureHttpStatus,
                    journalOutcome.ErrorCode ?? "gl_posting_failed",
                    journalOutcome.ErrorDetail ?? "Failed posting COGS / Inventory journal.");
            }

            var movement = StockMovement.Create(
                movementId,
                cmd.TenantId,
                cmd.LegalEntityId,
                movementNumber,
                StockMovementTypes.OperationalIssue,
                cmd.PostingDate,
                "INVENTORY",
                StockMovementTypes.OperationalIssue,
                cmd.ItemId,
                sourceRef,
                idempKey,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            var movLine = MovementLine.CreateOutboundIssue(
                movementLineId,
                cmd.TenantId,
                cmd.LegalEntityId,
                movementId,
                1,
                pos.StockPositionId,
                item.ItemId,
                item.BaseUomId,
                cmd.ItemId,
                qty,
                issueStep.AppliedUnitCost,
                issueStep.IssuedCarryingAmount,
                sourceLocation.WarehouseId,
                sourceLocation.LocationId,
                cmd.StockLotId);

            var valEntry = ValuationEntry.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                pool.ValuationPoolId,
                item.ItemId,
                ValuationEntryTypes.OperationalIssue,
                cmd.PostingDate,
                currency,
                movementId,
                movementLineId,
                StockMovementTypes.OperationalIssue,
                movementId,
                movementNumber,
                -qty,
                -issueStep.IssuedCarryingAmount,
                issueStep.AppliedUnitCost,
                issueStep.ValuationStep,
                journalOutcome.Journal.JournalId,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.StockMovements.Add(movement);
            db.MovementLines.Add(movLine);
            db.ValuationEntries.Add(valEntry);

            var payloadJson = JsonSerializer.Serialize(new
            {
                movement.StockMovementId,
                movement.MovementNumber,
                item.ItemId,
                item.ItemCode,
                sourceLocation.LocationCode,
                cmd.StockLotId,
                IssuedQty = qty,
                IssuedAmount = issueStep.IssuedCarryingAmount,
                UnitCost = issueStep.AppliedUnitCost,
                journalOutcome.Journal.JournalId,
                journalOutcome.Journal.JournalNumber
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "INVENTORY_OPERATIONAL_ISSUED",
                "STOCK_MOVEMENT",
                movementId.ToString("D"),
                $"Issued {qty:0.######} {item.ItemCode} from {sourceLocation.LocationCode} (COGS {issueStep.IssuedCarryingAmount:N0} IDR)",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "inventory.stock.issued.v1",
                $"inventory:issue:{movementId:D}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new PostOperationalIssueResult(
                true,
                false,
                201,
                null,
                null,
                movement,
                movLine,
                valEntry,
                pos,
                pool,
                journalOutcome.Journal,
                journalOutcome.Lines);
        }
        catch (ArgumentException ex)
        {
            return PostOperationalIssueResult.Fail(400, "inventory_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            var code = idx > 0 ? ex.Message[..idx].Trim() : "inventory_domain_error";
            var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
            return PostOperationalIssueResult.Fail(409, code, detail);
        }
    }
}
