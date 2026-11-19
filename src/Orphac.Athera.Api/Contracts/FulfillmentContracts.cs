namespace Orphac.Athera.Api.Contracts;

public sealed record FulfillmentEligibleBalanceDto(
    Guid StockLocationBalanceId,
    Guid WarehouseId,
    string WarehouseCode,
    Guid LocationId,
    string LocationCode,
    string ZoneType,
    Guid? StockLotId,
    string? LotNumber,
    DateOnly? ExpiryDate,
    decimal OnHandBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal AvailableBaseQuantity);

public sealed record FulfillableSalesOrderLineDto(
    Guid SoLineId,
    int LineNumber,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string LotTrackingPolicy,
    string OrderedUomCode,
    decimal OrderedQuantity,
    string BaseUomCode,
    decimal OrderedBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal DispatchedBaseQuantity,
    decimal RemainingToReserveBaseQuantity,
    decimal RemainingToDispatchBaseQuantity,
    decimal MovingAverageUnitCost,
    IReadOnlyList<FulfillmentEligibleBalanceDto> EligibleLocations);

public sealed record FulfillableSalesOrderDto(
    Guid SalesOrderId,
    string SoNumber,
    string Status,
    string FulfillmentProgress,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string DeliveryAddressSnapshot,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    Guid CurrentApprovedRevisionId,
    int CurrentApprovedRevisionNumber,
    decimal TotalOrderedBaseQuantity,
    decimal TotalReservedBaseQuantity,
    decimal TotalDispatchedBaseQuantity,
    decimal RemainingUnfulfilledBaseQuantity,
    IReadOnlyList<FulfillableSalesOrderLineDto> Lines);

public sealed record SalesReservationDto(
    Guid StockReservationId,
    string ReservationNumber,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid SoLineId,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    Guid WarehouseId,
    string WarehouseCode,
    Guid LocationId,
    string LocationCode,
    Guid? StockLotId,
    string? LotNumber,
    decimal ReservedBaseQuantity,
    decimal ConsumedBaseQuantity,
    decimal ReleasedBaseQuantity,
    decimal RemainingReservedBaseQuantity,
    decimal AllocatedToOpenPickBaseQuantity,
    decimal UnallocatedReservedBaseQuantity,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PickLineDto(
    Guid PickLineId,
    int LineNumber,
    Guid SoLineId,
    string LineageLineKey,
    Guid StockReservationId,
    string ReservationNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    Guid WarehouseId,
    Guid LocationId,
    string LocationCode,
    Guid? StockLotId,
    string? LotNumber,
    decimal RequestedPickBaseQuantity,
    decimal PickedBaseQuantity,
    decimal DispatchedBaseQuantity,
    decimal RemainingToDispatchBaseQuantity);

public sealed record PickOrderDto(
    Guid PickOrderId,
    string PickNumber,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid RevisionId,
    Guid WarehouseId,
    string WarehouseCode,
    string Status,
    decimal TotalRequestedBaseQuantity,
    decimal TotalPickedBaseQuantity,
    decimal TotalDispatchedBaseQuantity,
    decimal RemainingToDispatchBaseQuantity,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PickedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PickLineDto> Lines);

public sealed record SalesShipmentLineDto(
    Guid ShipmentLineId,
    int LineNumber,
    Guid PickLineId,
    Guid SoLineId,
    string LineageLineKey,
    Guid StockReservationId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    Guid LocationId,
    string LocationCode,
    Guid? StockLotId,
    string? LotNumber,
    decimal DispatchedBaseQuantity,
    decimal UnitCostApplied,
    decimal LineCogsAmount,
    Guid MovementLineId,
    Guid ValuationEntryId);

public sealed record SalesShipmentDto(
    Guid ShipmentId,
    string ShipmentNumber,
    Guid PickOrderId,
    string PickNumber,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string DeliveryAddressSnapshot,
    DateOnly DispatchDate,
    string Status,
    string? CarrierName,
    string? TrackingOrVehicleNumber,
    string DeliveryNoteReference,
    string? DeliveryConfirmedByName,
    string? DeliveryEvidenceReference,
    DateTimeOffset? DeliveredAtUtc,
    Guid StockMovementId,
    string StockMovementNumber,
    Guid GlJournalId,
    string GlJournalNumber,
    string CurrencyCode,
    decimal TotalDispatchedBaseQuantity,
    decimal TotalCogsAmount,
    string? Notes,
    DateTimeOffset DispatchedAtUtc,
    IReadOnlyList<SalesShipmentLineDto> Lines);

public sealed record FulfillmentOverviewResponse(
    IReadOnlyList<FulfillableSalesOrderDto> FulfillableOrders,
    IReadOnlyList<SalesReservationDto> Reservations,
    IReadOnlyList<PickOrderDto> PickOrders,
    IReadOnlyList<SalesShipmentDto> Shipments,
    string Od10Notice,
    string Od09Notice);

public sealed record ReserveSalesOrderLineRequest(
    Guid SalesOrderId,
    Guid SoLineId,
    Guid LocationId,
    Guid? StockLotId,
    decimal QuantityBase,
    string IdempotencyKey,
    string? Notes = null);

public sealed record ReleaseSalesReservationRequest(
    decimal? ReleaseBaseQuantity = null);

public sealed record PickLineInputDto(
    Guid ReservationId,
    decimal RequestedPickBaseQuantity,
    decimal? PickedBaseQuantity = null);

public sealed record CreatePickOrderRequest(
    Guid SalesOrderId,
    bool ConfirmImmediately,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<PickLineInputDto> Lines);

public sealed record ConfirmPickLineInputDto(
    Guid PickLineId,
    decimal PickedBaseQuantity);

public sealed record ConfirmPickOrderRequest(
    IReadOnlyList<ConfirmPickLineInputDto> Lines);

public sealed record DispatchShipmentLineInputDto(
    Guid PickLineId,
    decimal DispatchedBaseQuantity);

public sealed record DispatchSalesShipmentRequest(
    Guid PickOrderId,
    DateOnly DispatchDate,
    string DeliveryNoteReference,
    string? CarrierName,
    string? TrackingOrVehicleNumber,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<DispatchShipmentLineInputDto>? Lines = null);

public sealed record ConfirmShipmentDeliveryRequest(
    string ConfirmedByName,
    string EvidenceReference);
