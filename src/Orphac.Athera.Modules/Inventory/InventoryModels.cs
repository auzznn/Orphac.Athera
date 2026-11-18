using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Inventory;

public static class StockStatuses
{
    public const string Unrestricted = "UNRESTRICTED";
    public const string QualityInspection = "QUALITY_INSPECTION";
    public const string Blocked = "BLOCKED";

    public static readonly IReadOnlyList<string> All = [Unrestricted, QualityInspection, Blocked];
    private static readonly HashSet<string> KnownSet = new(All, StringComparer.Ordinal);

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Unrestricted : raw.Trim().ToUpperInvariant();
        if (!KnownSet.Contains(value))
        {
            throw new ArgumentException($"Unsupported stock status '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class StockMovementTypes
{
    public const string PurchaseReceiptAcceptance = "PURCHASE_RECEIPT_ACCEPTANCE";
    public const string LocationTransfer = "LOCATION_TRANSFER";
    public const string QualityStatusChange = "QUALITY_STATUS_CHANGE";

    public static readonly IReadOnlyList<string> All = [PurchaseReceiptAcceptance, LocationTransfer, QualityStatusChange];
}

public static class LocationZoneTypes
{
    public const string ReceivingStaging = "RECEIVING_STAGING";
    public const string Storage = "STORAGE";
    public const string Quarantine = "QUARANTINE";
    public const string BlockedHold = "BLOCKED_HOLD";
    public const string Dispatch = "DISPATCH";

    public static readonly IReadOnlyList<string> All = [ReceivingStaging, Storage, Quarantine, BlockedHold, Dispatch];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Storage : raw.Trim().ToUpperInvariant();
        if (value is not (ReceivingStaging or Storage or Quarantine or BlockedHold or Dispatch))
        {
            throw new ArgumentException($"Unsupported warehouse location zone '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReservationSourceTypes
{
    public const string SalesOrder = "SALES_ORDER";
    public const string ManualHold = "MANUAL_HOLD";
    public const string Fulfillment = "FULFILLMENT";

    public static readonly IReadOnlyList<string> All = [SalesOrder, ManualHold, Fulfillment];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? ManualHold : raw.Trim().ToUpperInvariant();
        if (value is not (SalesOrder or ManualHold or Fulfillment))
        {
            throw new ArgumentException($"Unsupported reservation source type '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReservationStatuses
{
    public const string Active = "ACTIVE";
    public const string PartiallyConsumed = "PARTIALLY_CONSUMED";
    public const string Fulfilled = "FULFILLED";
    public const string Released = "RELEASED";

    public static readonly IReadOnlyList<string> All = [Active, PartiallyConsumed, Fulfilled, Released];
}

public static class InventoryAvailabilityDefaults
{
    public const string DefaultWarehouseCode = "WH-MAIN";
    public const string DefaultMainLocationCode = "LOC-MAIN";
    public const string DefaultQuarantineLocationCode = "LOC-QC";
    public const string DefaultBlockedLocationCode = "LOC-BLK";
    public const string DefaultDispatchLocationCode = "LOC-DSP";
}

public static class ValuationPoolStatuses
{
    public const string Active = "ACTIVE";
    public const string Blocked = "BLOCKED";

    public static readonly IReadOnlyList<string> All = [Active, Blocked];
}

public static class ValuationEntryTypes
{
    public const string ReceiptAcceptance = "RECEIPT_ACCEPTANCE";

    public static readonly IReadOnlyList<string> All = [ReceiptAcceptance];
}

public static class InventoryValuationDefaults
{
    public const string DefaultValuationGroupCode = "DEFAULT";
}

public sealed class MovementSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private MovementSequence()
    {
    }
}

/// <summary>
/// Authoritative stock status balances per tenant, legal entity, item and valuation group in `inventory.stock_positions`.
/// Restricted buckets (`QUALITY_INSPECTION`, `BLOCKED`) contribute to valued on-hand quantity while keeping zero fulfillment availability.
/// </summary>
public sealed class StockPosition
{
    public Guid StockPositionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ValuationGroupCode { get; private set; } = InventoryValuationDefaults.DefaultValuationGroupCode;
    public Guid BaseUomId { get; private set; }

    public decimal UnrestrictedBaseQuantity { get; private set; }
    public decimal QualityInspectionBaseQuantity { get; private set; }
    public decimal BlockedBaseQuantity { get; private set; }
    public decimal ReservedBaseQuantity { get; private set; }
    public decimal TotalValuedOnHandBaseQuantity { get; private set; }
    public decimal AvailableBaseQuantity { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private StockPosition()
    {
    }

    public static StockPosition CreateInitial(
        Guid stockPositionId,
        Guid tenantId,
        Guid legalEntityId,
        Guid itemId,
        Guid baseUomId,
        DateTimeOffset nowUtc,
        string valuationGroupCode = InventoryValuationDefaults.DefaultValuationGroupCode)
    {
        GuardValidation.RequireNonEmptyGuid(stockPositionId, nameof(stockPositionId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));

        return new StockPosition
        {
            StockPositionId = stockPositionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ItemId = itemId,
            ValuationGroupCode = GuardValidation.RequireCode(valuationGroupCode, nameof(valuationGroupCode), 32),
            BaseUomId = baseUomId,
            UnrestrictedBaseQuantity = 0m,
            QualityInspectionBaseQuantity = 0m,
            BlockedBaseQuantity = 0m,
            ReservedBaseQuantity = 0m,
            TotalValuedOnHandBaseQuantity = 0m,
            AvailableBaseQuantity = 0m,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyInboundReceipt(decimal acceptedBaseQuantity, string stockStatus, DateTimeOffset nowUtc)
    {
        var qty = decimal.Round(acceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new InvalidOperationException("acceptance_qty_must_be_positive: Inbound valued receipt quantity must be positive.");
        }

        var normalizedStatus = StockStatuses.Normalize(stockStatus);
        AdjustStatusBucket(normalizedStatus, qty);
        RecalculateTotalsAndValidate(nowUtc);
    }

    public void ApplyStatusTransition(string fromStatus, string toStatus, decimal quantity, DateTimeOffset nowUtc)
    {
        var qty = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentException("status_transition_qty_must_be_positive: Transition quantity must be strictly positive.");
        }

        var normalizedFrom = StockStatuses.Normalize(fromStatus);
        var normalizedTo = StockStatuses.Normalize(toStatus);
        if (normalizedFrom == normalizedTo)
        {
            throw new ArgumentException("status_transition_same_status: Source and target stock statuses must differ.");
        }

        var prevUnr = UnrestrictedBaseQuantity;
        var prevQi = QualityInspectionBaseQuantity;
        var prevBlk = BlockedBaseQuantity;

        AdjustStatusBucket(normalizedFrom, -qty);
        AdjustStatusBucket(normalizedTo, qty);

        if (UnrestrictedBaseQuantity < ReservedBaseQuantity)
        {
            UnrestrictedBaseQuantity = prevUnr;
            QualityInspectionBaseQuantity = prevQi;
            BlockedBaseQuantity = prevBlk;
            throw new InvalidOperationException(
                "od10_insufficient_available_stock: Cannot restrict stock currently allocated to active reservations.");
        }

        if (UnrestrictedBaseQuantity < 0m || QualityInspectionBaseQuantity < 0m || BlockedBaseQuantity < 0m)
        {
            UnrestrictedBaseQuantity = prevUnr;
            QualityInspectionBaseQuantity = prevQi;
            BlockedBaseQuantity = prevBlk;
        }

        RecalculateTotalsAndValidate(nowUtc);
    }

    public void ApplyReservationDelta(decimal reservedDelta, DateTimeOffset nowUtc)
    {
        var delta = decimal.Round(reservedDelta, 6, MidpointRounding.AwayFromZero);
        if (delta == 0m)
        {
            throw new ArgumentException("reservation_qty_must_be_nonzero: Reservation delta must be non-zero.");
        }

        var nextReserved = decimal.Round(ReservedBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        if (nextReserved < 0m)
        {
            throw new InvalidOperationException("od10_negative_stock_forbidden: Reserved quantity cannot become negative.");
        }

        if (nextReserved > UnrestrictedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"od10_insufficient_available_stock: Reservation exceeds unreserved unrestricted quantity (Available={UnrestrictedBaseQuantity - ReservedBaseQuantity:0.######}).");
        }

        ReservedBaseQuantity = nextReserved;
        RecalculateTotalsAndValidate(nowUtc);
    }

    private void AdjustStatusBucket(string normalizedStatus, decimal signedDelta)
    {
        switch (normalizedStatus)
        {
            case StockStatuses.Unrestricted:
                UnrestrictedBaseQuantity = decimal.Round(UnrestrictedBaseQuantity + signedDelta, 6, MidpointRounding.AwayFromZero);
                break;
            case StockStatuses.QualityInspection:
                QualityInspectionBaseQuantity = decimal.Round(QualityInspectionBaseQuantity + signedDelta, 6, MidpointRounding.AwayFromZero);
                break;
            case StockStatuses.Blocked:
                BlockedBaseQuantity = decimal.Round(BlockedBaseQuantity + signedDelta, 6, MidpointRounding.AwayFromZero);
                break;
        }
    }

    private void RecalculateTotalsAndValidate(DateTimeOffset nowUtc)
    {
        TotalValuedOnHandBaseQuantity = decimal.Round(
            UnrestrictedBaseQuantity + QualityInspectionBaseQuantity + BlockedBaseQuantity,
            6,
            MidpointRounding.AwayFromZero);
        AvailableBaseQuantity = decimal.Round(
            UnrestrictedBaseQuantity - ReservedBaseQuantity,
            6,
            MidpointRounding.AwayFromZero);

        AssertConsistency();
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void AssertConsistency()
    {
        if (UnrestrictedBaseQuantity < 0m || QualityInspectionBaseQuantity < 0m || BlockedBaseQuantity < 0m ||
            ReservedBaseQuantity < 0m || TotalValuedOnHandBaseQuantity < 0m || AvailableBaseQuantity < 0m)
        {
            throw new InvalidOperationException("od10_negative_stock_forbidden: Stock position quantities cannot become negative.");
        }

        if (TotalValuedOnHandBaseQuantity != UnrestrictedBaseQuantity + QualityInspectionBaseQuantity + BlockedBaseQuantity ||
            AvailableBaseQuantity != UnrestrictedBaseQuantity - ReservedBaseQuantity)
        {
            throw new InvalidOperationException("stock_position_invariant_failed: Stock position totals and availability do not reconcile.");
        }
    }
}

public readonly record struct ValuationStepResult(
    decimal PreviousBaseQuantity,
    decimal PreviousCarryingValue,
    decimal PreviousMovingAverageCost,
    decimal NewBaseQuantity,
    decimal NewCarryingValue,
    decimal NewMovingAverageCost,
    long EntrySequence);

/// <summary>
/// Authoritative perpetual moving-average cost pool in `inventory.valuation_pools`.
/// Enforces OD-04 safe defaults against anomalous negative or orphan carrying-value pools.
/// </summary>
public sealed class ValuationPool
{
    public Guid ValuationPoolId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ItemId { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public string ValuationGroupCode { get; private set; } = InventoryValuationDefaults.DefaultValuationGroupCode;
    public Guid BaseUomId { get; private set; }
    public string Status { get; private set; } = ValuationPoolStatuses.Active;

    public decimal ValuedBaseQuantity { get; private set; }
    public decimal CarryingValueAmount { get; private set; }
    public decimal MovingAverageUnitCost { get; private set; }
    public long LastEntrySequence { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ValuationPool()
    {
    }

    public static ValuationPool CreateInitial(
        Guid valuationPoolId,
        Guid tenantId,
        Guid legalEntityId,
        Guid itemId,
        string currencyCode,
        Guid baseUomId,
        DateTimeOffset nowUtc,
        string valuationGroupCode = InventoryValuationDefaults.DefaultValuationGroupCode)
    {
        GuardValidation.RequireNonEmptyGuid(valuationPoolId, nameof(valuationPoolId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));

        return new ValuationPool
        {
            ValuationPoolId = valuationPoolId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ItemId = itemId,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            ValuationGroupCode = GuardValidation.RequireCode(valuationGroupCode, nameof(valuationGroupCode), 32),
            BaseUomId = baseUomId,
            Status = ValuationPoolStatuses.Active,
            ValuedBaseQuantity = 0m,
            CarryingValueAmount = 0m,
            MovingAverageUnitCost = 0m,
            LastEntrySequence = 0L,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public static ValuationStepResult SimulateReceiptValuation(
        string poolStatus,
        decimal previousQty,
        decimal previousCarryingValue,
        decimal previousUnitCost,
        long currentSequence,
        decimal acceptedQty,
        decimal acceptedLineAmount)
    {
        if (poolStatus != ValuationPoolStatuses.Active)
        {
            throw new InvalidOperationException("od04_anomalous_valuation_pool_blocked: Valuation pool is blocked and requires Product Owner resolution (ATH-CR-004).");
        }

        var prevQty = decimal.Round(previousQty, 6, MidpointRounding.AwayFromZero);
        var prevCarrying = decimal.Round(previousCarryingValue, 4, MidpointRounding.AwayFromZero);
        var prevCost = decimal.Round(previousUnitCost, 6, MidpointRounding.AwayFromZero);

        if (prevQty < 0m || prevCarrying < 0m || (prevQty == 0m && prevCarrying != 0m))
        {
            throw new InvalidOperationException(
                $"od04_anomalous_valuation_pool_blocked: Pool has anomalous state (Qty={prevQty:F6}, CarryingValue={prevCarrying:F4}) blocked by OD-04 (ATH-CR-004).");
        }

        var qtyDelta = decimal.Round(acceptedQty, 6, MidpointRounding.AwayFromZero);
        var valueDelta = decimal.Round(acceptedLineAmount, 4, MidpointRounding.AwayFromZero);
        if (qtyDelta <= 0m)
        {
            throw new InvalidOperationException("acceptance_qty_must_be_positive: Accepted base quantity must be positive.");
        }

        if (valueDelta <= 0m)
        {
            throw new InvalidOperationException("od04_zero_or_negative_acceptance_value_blocked: Valued acceptance requires positive monetary value under OD-04 (ATH-CR-004).");
        }

        var newQty = decimal.Round(prevQty + qtyDelta, 6, MidpointRounding.AwayFromZero);
        var newCarrying = decimal.Round(prevCarrying + valueDelta, 4, MidpointRounding.AwayFromZero);
        var newCost = decimal.Round(newCarrying / newQty, 6, MidpointRounding.AwayFromZero);

        return new ValuationStepResult(
            prevQty,
            prevCarrying,
            prevCost,
            newQty,
            newCarrying,
            newCost,
            currentSequence + 1L);
    }

    public ValuationStepResult ApplyReceiptValuation(decimal acceptedQty, decimal acceptedLineAmount, DateTimeOffset nowUtc)
    {
        var result = SimulateReceiptValuation(
            Status,
            ValuedBaseQuantity,
            CarryingValueAmount,
            MovingAverageUnitCost,
            LastEntrySequence,
            acceptedQty,
            acceptedLineAmount);

        ValuedBaseQuantity = result.NewBaseQuantity;
        CarryingValueAmount = result.NewCarryingValue;
        MovingAverageUnitCost = result.NewMovingAverageCost;
        LastEntrySequence = result.EntrySequence;
        UpdatedAtUtc = nowUtc;
        Version++;

        return result;
    }
}

/// <summary>
/// Strictly append-only inventory movement header in `inventory.stock_movements`.
/// </summary>
public sealed class StockMovement
{
    public Guid StockMovementId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string MovementNumber { get; private set; } = string.Empty;
    public string MovementType { get; private set; } = StockMovementTypes.PurchaseReceiptAcceptance;
    public DateOnly PostingDate { get; private set; }
    public string SourceModule { get; private set; } = "PROCUREMENT";
    public string SourceDocumentType { get; private set; } = "VALUED_ACCEPTANCE";
    public Guid SourceDocumentId { get; private set; }
    public string SourceDocumentNumber { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private StockMovement()
    {
    }

    public static StockMovement Create(
        Guid stockMovementId,
        Guid tenantId,
        Guid legalEntityId,
        string movementNumber,
        string movementType,
        DateOnly postingDate,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        string idempotencyKey,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(sourceDocumentId, nameof(sourceDocumentId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        return new StockMovement
        {
            StockMovementId = stockMovementId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            MovementNumber = GuardValidation.RequireCode(movementNumber, nameof(movementNumber), 40),
            MovementType = GuardValidation.RequireCode(movementType, nameof(movementType), 40),
            PostingDate = postingDate,
            SourceModule = GuardValidation.RequireCode(sourceModule, nameof(sourceModule), 32),
            SourceDocumentType = GuardValidation.RequireCode(sourceDocumentType, nameof(sourceDocumentType), 40),
            SourceDocumentId = sourceDocumentId,
            SourceDocumentNumber = GuardValidation.RequireCode(sourceDocumentNumber, nameof(sourceDocumentNumber), 40),
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}

/// <summary>
/// Strictly append-only inventory movement line in `inventory.movement_lines`.
/// </summary>
public sealed class MovementLine
{
    public Guid MovementLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid StockMovementId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid StockPositionId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public Guid SourceLineId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? StockLotId { get; private set; }
    public string ResultingStockStatus { get; private set; } = StockStatuses.Unrestricted;
    public decimal BaseQuantityDelta { get; private set; }
    public decimal UnitValuationCost { get; private set; }
    public decimal LineValuationAmount { get; private set; }

    private MovementLine()
    {
    }

    public static MovementLine Create(
        Guid movementLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid stockMovementId,
        int lineNumber,
        Guid stockPositionId,
        Guid itemId,
        Guid baseUomId,
        Guid sourceLineId,
        string resultingStockStatus,
        decimal baseQuantityDelta,
        decimal unitValuationCost,
        decimal lineValuationAmount,
        Guid? warehouseId = null,
        Guid? locationId = null,
        Guid? stockLotId = null)
    {
        ValidateCoreIds(movementLineId, tenantId, legalEntityId, stockMovementId, lineNumber, stockPositionId, itemId, baseUomId, sourceLineId);

        var qty = decimal.Round(baseQuantityDelta, 6, MidpointRounding.AwayFromZero);
        var cost = decimal.Round(unitValuationCost, 6, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(lineValuationAmount, 4, MidpointRounding.AwayFromZero);
        if (qty <= 0m || cost <= 0m || amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(baseQuantityDelta), "Inbound movement line requires positive quantity, unit cost and amount.");
        }

        return new MovementLine
        {
            MovementLineId = movementLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            StockMovementId = stockMovementId,
            LineNumber = lineNumber,
            StockPositionId = stockPositionId,
            ItemId = itemId,
            BaseUomId = baseUomId,
            SourceLineId = sourceLineId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            StockLotId = stockLotId,
            ResultingStockStatus = StockStatuses.Normalize(resultingStockStatus),
            BaseQuantityDelta = qty,
            UnitValuationCost = cost,
            LineValuationAmount = amount
        };
    }

    public static MovementLine CreateInternalLeg(
        Guid movementLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid stockMovementId,
        int lineNumber,
        Guid stockPositionId,
        Guid itemId,
        Guid baseUomId,
        Guid sourceLineId,
        string resultingStockStatus,
        decimal baseQuantityDelta,
        decimal unitValuationCost,
        Guid warehouseId,
        Guid locationId,
        Guid? stockLotId)
    {
        ValidateCoreIds(movementLineId, tenantId, legalEntityId, stockMovementId, lineNumber, stockPositionId, itemId, baseUomId, sourceLineId);
        GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId));
        GuardValidation.RequireNonEmptyGuid(locationId, nameof(locationId));

        var qty = decimal.Round(baseQuantityDelta, 6, MidpointRounding.AwayFromZero);
        var cost = decimal.Round(unitValuationCost, 6, MidpointRounding.AwayFromZero);
        if (qty == 0m || cost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(baseQuantityDelta), "Internal movement leg requires non-zero quantity and non-negative unit cost reference.");
        }

        return new MovementLine
        {
            MovementLineId = movementLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            StockMovementId = stockMovementId,
            LineNumber = lineNumber,
            StockPositionId = stockPositionId,
            ItemId = itemId,
            BaseUomId = baseUomId,
            SourceLineId = sourceLineId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            StockLotId = stockLotId,
            ResultingStockStatus = StockStatuses.Normalize(resultingStockStatus),
            BaseQuantityDelta = qty,
            UnitValuationCost = cost,
            LineValuationAmount = 0m
        };
    }

    private static void ValidateCoreIds(
        Guid movementLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid stockMovementId,
        int lineNumber,
        Guid stockPositionId,
        Guid itemId,
        Guid baseUomId,
        Guid sourceLineId)
    {
        GuardValidation.RequireNonEmptyGuid(movementLineId, nameof(movementLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(stockPositionId, nameof(stockPositionId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));
        GuardValidation.RequireNonEmptyGuid(sourceLineId, nameof(sourceLineId));

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }
    }
}

/// <summary>
/// Strictly append-only moving-average valuation ledger entry in `inventory.valuation_entries`.
/// </summary>
public sealed class ValuationEntry
{
    public Guid ValuationEntryId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ValuationPoolId { get; private set; }
    public Guid ItemId { get; private set; }
    public long PoolSequenceNumber { get; private set; }
    public string EntryType { get; private set; } = ValuationEntryTypes.ReceiptAcceptance;
    public DateOnly PostingDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public Guid StockMovementId { get; private set; }
    public Guid MovementLineId { get; private set; }
    public string SourceDocumentType { get; private set; } = "VALUED_ACCEPTANCE";
    public Guid SourceDocumentId { get; private set; }
    public string SourceDocumentNumber { get; private set; } = string.Empty;

    public decimal QuantityDelta { get; private set; }
    public decimal ValueDelta { get; private set; }
    public decimal UnitCostApplied { get; private set; }

    public decimal PreviousPoolBaseQuantity { get; private set; }
    public decimal PreviousPoolCarryingValue { get; private set; }
    public decimal PreviousMovingAverageCost { get; private set; }
    public decimal NewPoolBaseQuantity { get; private set; }
    public decimal NewPoolCarryingValue { get; private set; }
    public decimal NewMovingAverageCost { get; private set; }

    public Guid? GlJournalId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ValuationEntry()
    {
    }

    public static ValuationEntry Record(
        Guid valuationEntryId,
        Guid tenantId,
        Guid legalEntityId,
        Guid valuationPoolId,
        Guid itemId,
        string entryType,
        DateOnly postingDate,
        string currencyCode,
        Guid stockMovementId,
        Guid movementLineId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        decimal quantityDelta,
        decimal valueDelta,
        decimal unitCostApplied,
        ValuationStepResult step,
        Guid? glJournalId,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(valuationEntryId, nameof(valuationEntryId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(valuationPoolId, nameof(valuationPoolId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(movementLineId, nameof(movementLineId));
        GuardValidation.RequireNonEmptyGuid(sourceDocumentId, nameof(sourceDocumentId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        return new ValuationEntry
        {
            ValuationEntryId = valuationEntryId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ValuationPoolId = valuationPoolId,
            ItemId = itemId,
            PoolSequenceNumber = step.EntrySequence,
            EntryType = GuardValidation.RequireCode(entryType, nameof(entryType), 36),
            PostingDate = postingDate,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            StockMovementId = stockMovementId,
            MovementLineId = movementLineId,
            SourceDocumentType = GuardValidation.RequireCode(sourceDocumentType, nameof(sourceDocumentType), 40),
            SourceDocumentId = sourceDocumentId,
            SourceDocumentNumber = GuardValidation.RequireCode(sourceDocumentNumber, nameof(sourceDocumentNumber), 40),
            QuantityDelta = decimal.Round(quantityDelta, 6, MidpointRounding.AwayFromZero),
            ValueDelta = decimal.Round(valueDelta, 4, MidpointRounding.AwayFromZero),
            UnitCostApplied = decimal.Round(unitCostApplied, 6, MidpointRounding.AwayFromZero),
            PreviousPoolBaseQuantity = step.PreviousBaseQuantity,
            PreviousPoolCarryingValue = step.PreviousCarryingValue,
            PreviousMovingAverageCost = step.PreviousMovingAverageCost,
            NewPoolBaseQuantity = step.NewBaseQuantity,
            NewPoolCarryingValue = step.NewCarryingValue,
            NewMovingAverageCost = step.NewMovingAverageCost,
            GlJournalId = glJournalId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }

    public void AttachJournalId(Guid journalId)
    {
        GuardValidation.RequireNonEmptyGuid(journalId, nameof(journalId));
        GlJournalId = journalId;
    }
}

public sealed class Warehouse
{
    public Guid WarehouseId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string WarehouseCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private Warehouse()
    {
    }

    public static Warehouse Create(
        Guid warehouseId,
        Guid tenantId,
        Guid legalEntityId,
        string warehouseCode,
        string name,
        bool isDefault,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        return new Warehouse
        {
            WarehouseId = warehouseId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            WarehouseCode = GuardValidation.RequireMasterCode(warehouseCode, nameof(warehouseCode), 2, 32),
            Name = GuardValidation.RequireName(name, nameof(name), 160),
            IsDefault = isDefault,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

public sealed class WarehouseLocation
{
    public Guid LocationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string LocationCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ZoneType { get; private set; } = LocationZoneTypes.Storage;
    public bool IsDefaultReceiving { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private WarehouseLocation()
    {
    }

    public static WarehouseLocation Create(
        Guid locationId,
        Guid tenantId,
        Guid legalEntityId,
        Guid warehouseId,
        string locationCode,
        string name,
        string zoneType,
        bool isDefaultReceiving,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(locationId, nameof(locationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId));

        return new WarehouseLocation
        {
            LocationId = locationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            WarehouseId = warehouseId,
            LocationCode = GuardValidation.RequireMasterCode(locationCode, nameof(locationCode), 2, 32),
            Name = GuardValidation.RequireName(name, nameof(name), 160),
            ZoneType = LocationZoneTypes.Normalize(zoneType),
            IsDefaultReceiving = isDefaultReceiving,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

public sealed class StockLot
{
    public Guid StockLotId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ItemId { get; private set; }
    public string LotNumber { get; private set; } = string.Empty;
    public string? SupplierLotReference { get; private set; }
    public DateOnly? ManufacturedDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public Guid? SourceValuedAcceptanceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private StockLot()
    {
    }

    public static StockLot Create(
        Guid stockLotId,
        Guid tenantId,
        Guid legalEntityId,
        Guid itemId,
        string lotNumber,
        string? supplierLotReference,
        DateOnly? manufacturedDate,
        DateOnly? expiryDate,
        Guid? sourceValuedAcceptanceId,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(stockLotId, nameof(stockLotId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        if (manufacturedDate.HasValue && expiryDate.HasValue && expiryDate.Value < manufacturedDate.Value)
        {
            throw new ArgumentException("ExpiryDate cannot precede ManufacturedDate.", nameof(expiryDate));
        }

        return new StockLot
        {
            StockLotId = stockLotId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ItemId = itemId,
            LotNumber = GuardValidation.RequireMasterCode(lotNumber, nameof(lotNumber), 2, 48),
            SupplierLotReference = GuardValidation.NormalizeOptional(supplierLotReference, 64, nameof(supplierLotReference)),
            ManufacturedDate = manufacturedDate,
            ExpiryDate = expiryDate,
            SourceValuedAcceptanceId = sourceValuedAcceptanceId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void AssignSourceAcceptanceIfMissing(Guid valuedAcceptanceId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceId, nameof(valuedAcceptanceId));
        if (!SourceValuedAcceptanceId.HasValue)
        {
            SourceValuedAcceptanceId = valuedAcceptanceId;
            UpdatedAtUtc = nowUtc;
            Version++;
        }
    }
}

/// <summary>
/// Per-location, per-lot (nullable), per-status physical availability balance in `inventory.stock_location_balances`.
/// </summary>
public sealed class StockLocationBalance
{
    public Guid StockLocationBalanceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid StockPositionId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? StockLotId { get; private set; }
    public string StockStatus { get; private set; } = StockStatuses.Unrestricted;
    public decimal OnHandBaseQuantity { get; private set; }
    public decimal ReservedBaseQuantity { get; private set; }
    public decimal AvailableBaseQuantity { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private StockLocationBalance()
    {
    }

    public static StockLocationBalance CreateInitial(
        Guid stockLocationBalanceId,
        Guid tenantId,
        Guid legalEntityId,
        Guid stockPositionId,
        Guid itemId,
        Guid baseUomId,
        Guid warehouseId,
        Guid locationId,
        Guid? stockLotId,
        string stockStatus,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(stockLocationBalanceId, nameof(stockLocationBalanceId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(stockPositionId, nameof(stockPositionId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));
        GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId));
        GuardValidation.RequireNonEmptyGuid(locationId, nameof(locationId));

        return new StockLocationBalance
        {
            StockLocationBalanceId = stockLocationBalanceId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            StockPositionId = stockPositionId,
            ItemId = itemId,
            BaseUomId = baseUomId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            StockLotId = stockLotId,
            StockStatus = StockStatuses.Normalize(stockStatus),
            OnHandBaseQuantity = 0m,
            ReservedBaseQuantity = 0m,
            AvailableBaseQuantity = 0m,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void AdjustOnHand(decimal signedDelta, DateTimeOffset nowUtc)
    {
        var delta = decimal.Round(signedDelta, 6, MidpointRounding.AwayFromZero);
        if (delta == 0m)
        {
            throw new ArgumentException("balance_delta_must_be_nonzero: Quantity delta must be non-zero.");
        }

        var nextOnHand = decimal.Round(OnHandBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        if (nextOnHand < 0m)
        {
            throw new InvalidOperationException(
                $"od10_negative_stock_forbidden: Location balance cannot become negative (OnHand={OnHandBaseQuantity:0.######}, Delta={delta:0.######}).");
        }

        if (StockStatus == StockStatuses.Unrestricted && nextOnHand < ReservedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"od10_insufficient_available_stock: Location available stock ({OnHandBaseQuantity - ReservedBaseQuantity:0.######}) is less than requested movement ({Math.Abs(delta):0.######}).");
        }

        OnHandBaseQuantity = nextOnHand;
        RecalculateAvailability(nowUtc);
    }

    public void ApplyReservationDelta(decimal reservedDelta, DateTimeOffset nowUtc)
    {
        if (StockStatus != StockStatuses.Unrestricted)
        {
            throw new InvalidOperationException(
                $"od10_restricted_stock_not_reservable: Stock with status '{StockStatus}' has zero fulfillment availability.");
        }

        var delta = decimal.Round(reservedDelta, 6, MidpointRounding.AwayFromZero);
        if (delta == 0m)
        {
            throw new ArgumentException("reservation_qty_must_be_nonzero: Reservation delta must be non-zero.");
        }

        var nextReserved = decimal.Round(ReservedBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        if (nextReserved < 0m)
        {
            throw new InvalidOperationException("od10_negative_stock_forbidden: Location reserved quantity cannot become negative.");
        }

        if (nextReserved > OnHandBaseQuantity)
        {
            throw new InvalidOperationException(
                $"od10_insufficient_available_stock: Requested reservation exceeds available location stock (Available={OnHandBaseQuantity - ReservedBaseQuantity:0.######}).");
        }

        ReservedBaseQuantity = nextReserved;
        RecalculateAvailability(nowUtc);
    }

    private void RecalculateAvailability(DateTimeOffset nowUtc)
    {
        if (StockStatus == StockStatuses.Unrestricted)
        {
            AvailableBaseQuantity = decimal.Round(OnHandBaseQuantity - ReservedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        }
        else
        {
            ReservedBaseQuantity = 0m;
            AvailableBaseQuantity = 0m;
        }

        if (OnHandBaseQuantity < 0m || ReservedBaseQuantity < 0m || AvailableBaseQuantity < 0m)
        {
            throw new InvalidOperationException("od10_negative_stock_forbidden: Location stock balances cannot be negative.");
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class ReservationSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ReservationSequence()
    {
    }
}

public sealed class StockReservation
{
    public Guid StockReservationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string ReservationNumber { get; private set; } = string.Empty;
    public Guid StockPositionId { get; private set; }
    public Guid StockLocationBalanceId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? StockLotId { get; private set; }
    public string SourceType { get; private set; } = ReservationSourceTypes.ManualHold;
    public Guid? SourceDocumentId { get; private set; }
    public string SourceDocumentNumber { get; private set; } = string.Empty;
    public Guid? SourceLineId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public decimal ReservedBaseQuantity { get; private set; }
    public decimal ConsumedBaseQuantity { get; private set; }
    public decimal ReleasedBaseQuantity { get; private set; }
    public decimal RemainingReservedBaseQuantity { get; private set; }
    public string Status { get; private set; } = ReservationStatuses.Active;
    public string? Notes { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private StockReservation()
    {
    }

    public static StockReservation Create(
        Guid stockReservationId,
        Guid tenantId,
        Guid legalEntityId,
        string reservationNumber,
        StockLocationBalance balance,
        string sourceType,
        Guid? sourceDocumentId,
        string sourceDocumentNumber,
        Guid? sourceLineId,
        string idempotencyKey,
        string payloadHash,
        decimal quantity,
        string? notes,
        DateTimeOffset? expiresAtUtc,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(stockReservationId, nameof(stockReservationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        ArgumentNullException.ThrowIfNull(balance);
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        if (balance.StockStatus != StockStatuses.Unrestricted)
        {
            throw new InvalidOperationException("od10_restricted_stock_not_reservable: Only UNRESTRICTED stock may be reserved.");
        }

        var qty = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentException("reservation_qty_must_be_positive: Reserved quantity must be positive.", nameof(quantity));
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= nowUtc)
        {
            throw new ArgumentException("ExpiresAtUtc must be in the future.", nameof(expiresAtUtc));
        }

        return new StockReservation
        {
            StockReservationId = stockReservationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ReservationNumber = GuardValidation.RequireCode(reservationNumber, nameof(reservationNumber), 40),
            StockPositionId = balance.StockPositionId,
            StockLocationBalanceId = balance.StockLocationBalanceId,
            ItemId = balance.ItemId,
            BaseUomId = balance.BaseUomId,
            WarehouseId = balance.WarehouseId,
            LocationId = balance.LocationId,
            StockLotId = balance.StockLotId,
            SourceType = ReservationSourceTypes.Normalize(sourceType),
            SourceDocumentId = sourceDocumentId,
            SourceDocumentNumber = GuardValidation.RequireName(sourceDocumentNumber, nameof(sourceDocumentNumber), 64),
            SourceLineId = sourceLineId,
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            PayloadHash = GuardValidation.RequireName(payloadHash, nameof(payloadHash), 64),
            ReservedBaseQuantity = qty,
            ConsumedBaseQuantity = 0m,
            ReleasedBaseQuantity = 0m,
            RemainingReservedBaseQuantity = qty,
            Status = ReservationStatuses.Active,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            ExpiresAtUtc = expiresAtUtc,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public decimal Release(decimal? releaseQuantity, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (RemainingReservedBaseQuantity <= 0m || Status is ReservationStatuses.Released or ReservationStatuses.Fulfilled)
        {
            throw new InvalidOperationException("reservation_already_closed: Reservation has no remaining active quantity to release.");
        }

        var qtyToRelease = decimal.Round(releaseQuantity ?? RemainingReservedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (qtyToRelease <= 0m || qtyToRelease > RemainingReservedBaseQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(releaseQuantity),
                $"Release quantity must be between 0 and remaining reserved quantity ({RemainingReservedBaseQuantity:0.######}).");
        }

        ReleasedBaseQuantity = decimal.Round(ReleasedBaseQuantity + qtyToRelease, 6, MidpointRounding.AwayFromZero);
        RemainingReservedBaseQuantity = decimal.Round(
            ReservedBaseQuantity - ConsumedBaseQuantity - ReleasedBaseQuantity,
            6,
            MidpointRounding.AwayFromZero);

        if (RemainingReservedBaseQuantity == 0m)
        {
            Status = ConsumedBaseQuantity > 0m ? ReservationStatuses.Fulfilled : ReservationStatuses.Released;
            ReleasedByUserId = actorUserId;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
        return qtyToRelease;
    }
}

public static class ReservationHash
{
    public static string ComputePayloadHash(
        Guid stockLocationBalanceId,
        string sourceType,
        string sourceDocumentNumber,
        decimal quantity)
    {
        var raw = string.Join(
            '|',
            stockLocationBalanceId.ToString("D"),
            sourceType.Trim().ToUpperInvariant(),
            sourceDocumentNumber.Trim().ToUpperInvariant(),
            decimal.Round(quantity, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
