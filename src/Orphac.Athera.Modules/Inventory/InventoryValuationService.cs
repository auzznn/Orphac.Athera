using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    decimal AcceptedLineAmount);

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
    string? BlockingReason);

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
    IReadOnlyList<ValuationPool> UpdatedPools)
{
    public static PostStockAcceptanceResult Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, null, Array.Empty<MovementLine>(), Array.Empty<ValuationEntry>(), Array.Empty<StockPosition>(), Array.Empty<ValuationPool>());
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
        IReadOnlyList<InventoryLineValuationInput> lines,
        CancellationToken ct)
    {
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
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
                    null));
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
                    ex.Message));
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
            var seq = await IncrementMovementSequenceAsync(db, tenantId, entityId, postingDate.Year, ct);
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

                var step = pool.ApplyReceiptValuation(line.AcceptedBaseQuantity, line.AcceptedLineAmount, nowUtc);
                pos.ApplyInboundReceipt(line.AcceptedBaseQuantity, normalizedStatus, nowUtc);

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
                    line.AcceptedLineAmount);

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
                locked.PoolsByItem.Values.ToList());
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

    private static async Task<long> IncrementMovementSequenceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        int fiscalYear,
        CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = """
            INSERT INTO inventory.movement_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
            VALUES (@tid, @eid, @fy, 1)
            ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
            DO UPDATE SET last_number = inventory.movement_sequences.last_number + 1
            RETURNING last_number;
            """;

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
