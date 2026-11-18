using System.Globalization;
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

    public static readonly IReadOnlyList<string> All = [PurchaseReceiptAcceptance];
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
        switch (normalizedStatus)
        {
            case StockStatuses.Unrestricted:
                UnrestrictedBaseQuantity = decimal.Round(UnrestrictedBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
                break;
            case StockStatuses.QualityInspection:
                QualityInspectionBaseQuantity = decimal.Round(QualityInspectionBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
                break;
            case StockStatuses.Blocked:
                BlockedBaseQuantity = decimal.Round(BlockedBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
                break;
        }

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
        decimal lineValuationAmount)
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
            ResultingStockStatus = StockStatuses.Normalize(resultingStockStatus),
            BaseQuantityDelta = qty,
            UnitValuationCost = cost,
            LineValuationAmount = amount
        };
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
