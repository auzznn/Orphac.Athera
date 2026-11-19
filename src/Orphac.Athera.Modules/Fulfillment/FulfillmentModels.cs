using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Fulfillment;

public static class PickOrderStatuses
{
    public const string Open = "OPEN";
    public const string Picked = "PICKED";
    public const string Dispatched = "DISPATCHED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Open, Picked, Dispatched, Cancelled];
}

public static class ShipmentStatuses
{
    public const string Dispatched = "DISPATCHED";
    public const string Delivered = "DELIVERED";

    public static readonly IReadOnlyList<string> All = [Dispatched, Delivered];
}

public sealed class PickSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private PickSequence()
    {
    }
}

public sealed class ShipmentSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ShipmentSequence()
    {
    }
}

public sealed class PickOrder
{
    public Guid PickOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string PickNumber { get; private set; } = string.Empty;
    public Guid SalesOrderId { get; private set; }
    public string SalesOrderNumberSnapshot { get; private set; } = string.Empty;
    public Guid RevisionId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string WarehouseCodeSnapshot { get; private set; } = string.Empty;
    public string Status { get; private set; } = PickOrderStatuses.Open;
    public decimal TotalRequestedBaseQuantity { get; private set; }
    public decimal TotalPickedBaseQuantity { get; private set; }
    public decimal TotalDispatchedBaseQuantity { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? PickedByUserId { get; private set; }
    public DateTimeOffset? PickedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private PickOrder()
    {
    }

    public static PickOrder Create(
        Guid pickOrderId,
        Guid tenantId,
        Guid legalEntityId,
        string pickNumber,
        Guid salesOrderId,
        string salesOrderNumberSnapshot,
        Guid revisionId,
        Guid warehouseId,
        string warehouseCodeSnapshot,
        decimal totalRequestedBaseQuantity,
        string idempotencyKey,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        var reqQty = decimal.Round(totalRequestedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (reqQty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRequestedBaseQuantity), "pick_qty_must_be_positive: Total requested pick quantity must be strictly positive.");
        }

        return new PickOrder
        {
            PickOrderId = GuardValidation.RequireNonEmptyGuid(pickOrderId, nameof(pickOrderId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            PickNumber = GuardValidation.RequireCode(pickNumber, nameof(pickNumber), 40),
            SalesOrderId = GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId)),
            SalesOrderNumberSnapshot = GuardValidation.RequireCode(salesOrderNumberSnapshot, nameof(salesOrderNumberSnapshot), 40),
            RevisionId = GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId)),
            WarehouseId = GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId)),
            WarehouseCodeSnapshot = GuardValidation.RequireCode(warehouseCodeSnapshot, nameof(warehouseCodeSnapshot), 32),
            Status = PickOrderStatuses.Open,
            TotalRequestedBaseQuantity = reqQty,
            TotalPickedBaseQuantity = 0m,
            TotalDispatchedBaseQuantity = 0m,
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            CreatedByUserId = GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ConfirmPicked(decimal totalPickedBaseQuantity, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != PickOrderStatuses.Open)
        {
            throw new InvalidOperationException($"pick_invalid_status_transition: Only OPEN pick orders can be confirmed (current: '{Status}').");
        }

        var picked = decimal.Round(totalPickedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (picked <= 0m || picked > TotalRequestedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"pick_qty_invalid: Total picked quantity ({picked:0.######}) must be positive and not exceed requested quantity ({TotalRequestedBaseQuantity:0.######}).");
        }

        TotalPickedBaseQuantity = picked;
        Status = PickOrderStatuses.Picked;
        PickedByUserId = actorUserId;
        PickedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyDispatchedQuantity(decimal dispatchedDelta, DateTimeOffset nowUtc)
    {
        if (Status is not (PickOrderStatuses.Picked or PickOrderStatuses.Dispatched))
        {
            throw new InvalidOperationException(
                $"pick_not_ready_for_dispatch: Pick order must be PICKED before dispatch (current: '{Status}').");
        }

        var delta = decimal.Round(dispatchedDelta, 6, MidpointRounding.AwayFromZero);
        if (delta <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchedDelta), "dispatch_qty_must_be_positive: Dispatched quantity must be strictly positive.");
        }

        var nextDispatched = decimal.Round(TotalDispatchedBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        if (nextDispatched > TotalPickedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"dispatch_exceeds_picked_quantity: Dispatched total ({nextDispatched:0.######}) cannot exceed picked quantity ({TotalPickedBaseQuantity:0.######}).");
        }

        TotalDispatchedBaseQuantity = nextDispatched;
        if (TotalDispatchedBaseQuantity >= TotalPickedBaseQuantity)
        {
            Status = PickOrderStatuses.Dispatched;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class PickLine
{
    public Guid PickLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PickOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid SoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;
    public Guid StockReservationId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public Guid WarehouseId { get; private set; }
    public Guid LocationId { get; private set; }
    public string LocationCodeSnapshot { get; private set; } = string.Empty;
    public Guid? StockLotId { get; private set; }
    public string? LotNumberSnapshot { get; private set; }
    public decimal RequestedPickBaseQuantity { get; private set; }
    public decimal PickedBaseQuantity { get; private set; }
    public decimal DispatchedBaseQuantity { get; private set; }

    private PickLine()
    {
    }

    public static PickLine Create(
        Guid pickLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid pickOrderId,
        int lineNumber,
        Guid soLineId,
        string lineageLineKey,
        Guid stockReservationId,
        Guid itemId,
        string itemCodeSnapshot,
        string itemNameSnapshot,
        Guid baseUomId,
        string baseUomCodeSnapshot,
        Guid warehouseId,
        Guid locationId,
        string locationCodeSnapshot,
        Guid? stockLotId,
        string? lotNumberSnapshot,
        decimal requestedPickBaseQuantity)
    {
        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        var reqQty = decimal.Round(requestedPickBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (reqQty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedPickBaseQuantity), "pick_qty_must_be_positive: Requested pick base quantity must be positive.");
        }

        return new PickLine
        {
            PickLineId = GuardValidation.RequireNonEmptyGuid(pickLineId, nameof(pickLineId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            PickOrderId = GuardValidation.RequireNonEmptyGuid(pickOrderId, nameof(pickOrderId)),
            LineNumber = lineNumber,
            SoLineId = GuardValidation.RequireNonEmptyGuid(soLineId, nameof(soLineId)),
            LineageLineKey = GuardValidation.RequireCode(lineageLineKey, nameof(lineageLineKey), 32),
            StockReservationId = GuardValidation.RequireNonEmptyGuid(stockReservationId, nameof(stockReservationId)),
            ItemId = GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId)),
            ItemCodeSnapshot = GuardValidation.RequireCode(itemCodeSnapshot, nameof(itemCodeSnapshot), 40),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 160),
            BaseUomId = GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId)),
            BaseUomCodeSnapshot = GuardValidation.RequireCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 16),
            WarehouseId = GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId)),
            LocationId = GuardValidation.RequireNonEmptyGuid(locationId, nameof(locationId)),
            LocationCodeSnapshot = GuardValidation.RequireCode(locationCodeSnapshot, nameof(locationCodeSnapshot), 32),
            StockLotId = stockLotId,
            LotNumberSnapshot = GuardValidation.NormalizeOptional(lotNumberSnapshot, 64, nameof(lotNumberSnapshot)),
            RequestedPickBaseQuantity = reqQty,
            PickedBaseQuantity = 0m,
            DispatchedBaseQuantity = 0m
        };
    }

    public void ConfirmPickedQuantity(decimal pickedBaseQuantity)
    {
        var qty = decimal.Round(pickedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m || qty > RequestedPickBaseQuantity)
        {
            throw new InvalidOperationException(
                $"pick_qty_invalid: Picked quantity ({qty:0.######}) on line {LineNumber} must be between 0 and requested pick quantity ({RequestedPickBaseQuantity:0.######}).");
        }

        PickedBaseQuantity = qty;
    }

    public void RecordDispatchedQuantity(decimal dispatchedDelta)
    {
        var delta = decimal.Round(dispatchedDelta, 6, MidpointRounding.AwayFromZero);
        if (delta <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchedDelta), "dispatch_qty_must_be_positive: Dispatched line quantity must be strictly positive.");
        }

        var nextDispatched = decimal.Round(DispatchedBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        if (nextDispatched > PickedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"dispatch_exceeds_picked_quantity: Dispatched quantity ({nextDispatched:0.######}) on line {LineNumber} exceeds picked quantity ({PickedBaseQuantity:0.######}).");
        }

        DispatchedBaseQuantity = nextDispatched;
    }
}

public sealed class SalesShipment
{
    public Guid ShipmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string ShipmentNumber { get; private set; } = string.Empty;
    public Guid PickOrderId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public string SalesOrderNumberSnapshot { get; private set; } = string.Empty;
    public Guid RevisionId { get; private set; }
    public Guid CustomerPartnerId { get; private set; }
    public string CustomerCodeSnapshot { get; private set; } = string.Empty;
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public string DeliveryAddressSnapshot { get; private set; } = string.Empty;
    public DateOnly DispatchDate { get; private set; }
    public string Status { get; private set; } = ShipmentStatuses.Dispatched;
    public string? CarrierName { get; private set; }
    public string? TrackingOrVehicleNumber { get; private set; }
    public string DeliveryNoteReference { get; private set; } = string.Empty;
    public string? DeliveryConfirmedByName { get; private set; }
    public string? DeliveryEvidenceReference { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public Guid? DeliveryConfirmedByUserId { get; private set; }
    public Guid StockMovementId { get; private set; }
    public string StockMovementNumber { get; private set; } = string.Empty;
    public Guid GlJournalId { get; private set; }
    public string GlJournalNumber { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal TotalDispatchedBaseQuantity { get; private set; }
    public decimal TotalCogsAmount { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid DispatchedByUserId { get; private set; }
    public DateTimeOffset DispatchedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SalesShipment()
    {
    }

    public static SalesShipment CreateDispatched(
        Guid shipmentId,
        Guid tenantId,
        Guid legalEntityId,
        string shipmentNumber,
        Guid pickOrderId,
        Guid salesOrderId,
        string salesOrderNumberSnapshot,
        Guid revisionId,
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerNameSnapshot,
        string deliveryAddressSnapshot,
        DateOnly dispatchDate,
        string? carrierName,
        string? trackingOrVehicleNumber,
        string deliveryNoteReference,
        Guid stockMovementId,
        string stockMovementNumber,
        Guid glJournalId,
        string glJournalNumber,
        string currencyCode,
        decimal totalDispatchedBaseQuantity,
        decimal totalCogsAmount,
        string idempotencyKey,
        string? notes,
        Guid dispatchedByUserId,
        DateTimeOffset nowUtc)
    {
        var qty = decimal.Round(totalDispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var cogs = decimal.Round(totalCogsAmount, 2, MidpointRounding.AwayFromZero);
        if (qty <= 0m || cogs < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDispatchedBaseQuantity), "dispatch_qty_must_be_positive: Dispatched shipment requires positive quantity and non-negative COGS.");
        }

        return new SalesShipment
        {
            ShipmentId = GuardValidation.RequireNonEmptyGuid(shipmentId, nameof(shipmentId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            ShipmentNumber = GuardValidation.RequireCode(shipmentNumber, nameof(shipmentNumber), 40),
            PickOrderId = GuardValidation.RequireNonEmptyGuid(pickOrderId, nameof(pickOrderId)),
            SalesOrderId = GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId)),
            SalesOrderNumberSnapshot = GuardValidation.RequireCode(salesOrderNumberSnapshot, nameof(salesOrderNumberSnapshot), 40),
            RevisionId = GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId)),
            CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId)),
            CustomerCodeSnapshot = GuardValidation.RequireCode(customerCodeSnapshot, nameof(customerCodeSnapshot), 32),
            CustomerNameSnapshot = GuardValidation.RequireName(customerNameSnapshot, nameof(customerNameSnapshot), 200),
            DeliveryAddressSnapshot = GuardValidation.RequireName(deliveryAddressSnapshot, nameof(deliveryAddressSnapshot), 240),
            DispatchDate = dispatchDate,
            Status = ShipmentStatuses.Dispatched,
            CarrierName = GuardValidation.NormalizeOptional(carrierName, 120, nameof(carrierName)),
            TrackingOrVehicleNumber = GuardValidation.NormalizeOptional(trackingOrVehicleNumber, 80, nameof(trackingOrVehicleNumber)),
            DeliveryNoteReference = GuardValidation.RequireName(deliveryNoteReference, nameof(deliveryNoteReference), 80),
            StockMovementId = GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId)),
            StockMovementNumber = GuardValidation.RequireCode(stockMovementNumber, nameof(stockMovementNumber), 40),
            GlJournalId = GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId)),
            GlJournalNumber = GuardValidation.RequireCode(glJournalNumber, nameof(glJournalNumber), 40),
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            TotalDispatchedBaseQuantity = qty,
            TotalCogsAmount = cogs,
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            DispatchedByUserId = GuardValidation.RequireNonEmptyGuid(dispatchedByUserId, nameof(dispatchedByUserId)),
            DispatchedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ConfirmDelivery(
        string confirmedByName,
        string evidenceReference,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status == ShipmentStatuses.Delivered)
        {
            throw new InvalidOperationException("shipment_already_delivered: Delivery evidence has already been confirmed for this shipment.");
        }

        if (Status != ShipmentStatuses.Dispatched)
        {
            throw new InvalidOperationException($"shipment_invalid_status_transition: Cannot confirm delivery for shipment in status '{Status}'.");
        }

        DeliveryConfirmedByName = GuardValidation.RequireName(confirmedByName, nameof(confirmedByName), 120);
        DeliveryEvidenceReference = GuardValidation.RequireName(evidenceReference, nameof(evidenceReference), 120);
        DeliveredAtUtc = nowUtc;
        DeliveryConfirmedByUserId = actorUserId;
        Status = ShipmentStatuses.Delivered;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class SalesShipmentLine
{
    public Guid ShipmentLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid PickLineId { get; private set; }
    public Guid SoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;
    public Guid StockReservationId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public Guid WarehouseId { get; private set; }
    public Guid LocationId { get; private set; }
    public string LocationCodeSnapshot { get; private set; } = string.Empty;
    public Guid? StockLotId { get; private set; }
    public string? LotNumberSnapshot { get; private set; }
    public decimal DispatchedBaseQuantity { get; private set; }
    public decimal UnitCostApplied { get; private set; }
    public decimal LineCogsAmount { get; private set; }
    public Guid MovementLineId { get; private set; }
    public Guid ValuationEntryId { get; private set; }

    private SalesShipmentLine()
    {
    }

    public static SalesShipmentLine Create(
        Guid shipmentLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid shipmentId,
        int lineNumber,
        Guid pickLineId,
        Guid soLineId,
        string lineageLineKey,
        Guid stockReservationId,
        Guid itemId,
        string itemCodeSnapshot,
        string itemNameSnapshot,
        Guid baseUomId,
        string baseUomCodeSnapshot,
        Guid warehouseId,
        Guid locationId,
        string locationCodeSnapshot,
        Guid? stockLotId,
        string? lotNumberSnapshot,
        decimal dispatchedBaseQuantity,
        decimal unitCostApplied,
        decimal lineCogsAmount,
        Guid movementLineId,
        Guid valuationEntryId)
    {
        var qty = decimal.Round(dispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var cost = decimal.Round(unitCostApplied, 6, MidpointRounding.AwayFromZero);
        var cogs = decimal.Round(lineCogsAmount, 2, MidpointRounding.AwayFromZero);
        if (qty <= 0m || cost < 0m || cogs < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchedBaseQuantity), "dispatch_qty_must_be_positive: Dispatched shipment line requires positive quantity and non-negative cost.");
        }

        return new SalesShipmentLine
        {
            ShipmentLineId = GuardValidation.RequireNonEmptyGuid(shipmentLineId, nameof(shipmentLineId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            ShipmentId = GuardValidation.RequireNonEmptyGuid(shipmentId, nameof(shipmentId)),
            LineNumber = lineNumber,
            PickLineId = GuardValidation.RequireNonEmptyGuid(pickLineId, nameof(pickLineId)),
            SoLineId = GuardValidation.RequireNonEmptyGuid(soLineId, nameof(soLineId)),
            LineageLineKey = GuardValidation.RequireCode(lineageLineKey, nameof(lineageLineKey), 32),
            StockReservationId = GuardValidation.RequireNonEmptyGuid(stockReservationId, nameof(stockReservationId)),
            ItemId = GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId)),
            ItemCodeSnapshot = GuardValidation.RequireCode(itemCodeSnapshot, nameof(itemCodeSnapshot), 40),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 160),
            BaseUomId = GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId)),
            BaseUomCodeSnapshot = GuardValidation.RequireCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 16),
            WarehouseId = GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId)),
            LocationId = GuardValidation.RequireNonEmptyGuid(locationId, nameof(locationId)),
            LocationCodeSnapshot = GuardValidation.RequireCode(locationCodeSnapshot, nameof(locationCodeSnapshot), 32),
            StockLotId = stockLotId,
            LotNumberSnapshot = GuardValidation.NormalizeOptional(lotNumberSnapshot, 64, nameof(lotNumberSnapshot)),
            DispatchedBaseQuantity = qty,
            UnitCostApplied = cost,
            LineCogsAmount = cogs,
            MovementLineId = GuardValidation.RequireNonEmptyGuid(movementLineId, nameof(movementLineId)),
            ValuationEntryId = GuardValidation.RequireNonEmptyGuid(valuationEntryId, nameof(valuationEntryId))
        };
    }
}
