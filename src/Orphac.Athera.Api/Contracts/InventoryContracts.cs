namespace Orphac.Athera.Api.Contracts;

public sealed record AcceptReceiptLineRequest(
    Guid GoodsReceiptLineId,
    decimal AcceptedBaseQuantity,
    Guid? DestinationLocationId = null,
    string? LotNumber = null,
    string? SupplierLotReference = null,
    DateOnly? ManufacturedDate = null,
    DateOnly? ExpiryDate = null);

public sealed record PreviewReceiptAcceptanceRequest(
    DateOnly AcceptanceDate,
    string StockStatus,
    IReadOnlyList<AcceptReceiptLineRequest> Lines);

public sealed record AcceptReceiptRequest(
    string IdempotencyKey,
    DateOnly AcceptanceDate,
    string StockStatus,
    string? Notes,
    IReadOnlyList<AcceptReceiptLineRequest> Lines);

public sealed record AcceptancePreviewLineDto(
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
    string LotTrackingPolicy,
    string? LotNumber,
    Guid? DestinationLocationId,
    string? DestinationLocationCode);

public sealed record PreviewJournalSuggestionDto(
    string Side,
    string AccountCode,
    string AccountName,
    decimal Amount,
    string Description);

public sealed record AcceptancePreviewDto(
    Guid GoodsReceiptId,
    string ReceiptNumber,
    string CurrencyCode,
    DateOnly AcceptanceDate,
    string StockStatus,
    bool CanPost,
    decimal TotalAcceptedBaseQuantity,
    decimal TotalAcceptedAmount,
    string Od04Notice,
    IReadOnlyList<AcceptancePreviewLineDto> Lines,
    IReadOnlyList<PreviewJournalSuggestionDto> JournalPreview,
    IReadOnlyList<string> BlockingReasons);

public sealed record ValuedAcceptanceLineDto(
    Guid ValuedAcceptanceLineId,
    int LineNumber,
    Guid GoodsReceiptLineId,
    Guid PoLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    decimal AcceptedBaseQuantity,
    decimal UnitPriceBaseSnapshot,
    decimal AcceptedLineAmount);

public sealed record ValuedAcceptanceSummaryDto(
    Guid ValuedAcceptanceId,
    string AcceptanceNumber,
    Guid GoodsReceiptId,
    string ReceiptNumber,
    Guid PurchaseOrderId,
    string PoNumber,
    string SupplierCode,
    string SupplierName,
    DateOnly AcceptanceDate,
    string StockStatus,
    string CurrencyCode,
    decimal TotalAcceptedBaseQuantity,
    decimal TotalAcceptedAmount,
    Guid? StockMovementId,
    string? MovementNumber,
    Guid? ReceiptAccrualId,
    string? AccrualNumber,
    Guid? GlJournalId,
    string? GlJournalNumber,
    string AcceptedByEmail,
    DateTimeOffset CreatedAtUtc);

public sealed record ValuedAcceptanceListResponse(
    IReadOnlyList<ValuedAcceptanceSummaryDto> Items);

public sealed record ValuedAcceptanceDetailDto(
    Guid ValuedAcceptanceId,
    string AcceptanceNumber,
    Guid GoodsReceiptId,
    string ReceiptNumber,
    string ReceiptStatus,
    Guid PurchaseOrderId,
    string PoNumber,
    int PoRevisionNumber,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierName,
    DateOnly AcceptanceDate,
    string StockStatus,
    string CurrencyCode,
    decimal TotalAcceptedBaseQuantity,
    decimal TotalAcceptedAmount,
    Guid? StockMovementId,
    string? MovementNumber,
    Guid? ReceiptAccrualId,
    string? AccrualNumber,
    Guid? GlJournalId,
    string? GlJournalNumber,
    string? Notes,
    Guid AcceptedByUserId,
    string AcceptedByEmail,
    DateTimeOffset CreatedAtUtc,
    bool WasIdempotentReplay,
    IReadOnlyList<ValuedAcceptanceLineDto> Lines);

public sealed record WarehouseDto(
    Guid WarehouseId,
    string WarehouseCode,
    string Name,
    bool IsDefault,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record WarehouseLocationDto(
    Guid LocationId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string LocationCode,
    string Name,
    string ZoneType,
    bool IsDefaultReceiving,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record WarehouseListResponse(
    IReadOnlyList<WarehouseDto> Warehouses,
    IReadOnlyList<WarehouseLocationDto> Locations);

public sealed record CreateWarehouseRequest(
    string WarehouseCode,
    string Name);

public sealed record CreateWarehouseLocationRequest(
    string LocationCode,
    string Name,
    string ZoneType,
    bool IsDefaultReceiving);

public sealed record StockLotSummaryDto(
    Guid StockLotId,
    Guid ItemId,
    string LotNumber,
    string? SupplierLotReference,
    DateOnly? ManufacturedDate,
    DateOnly? ExpiryDate,
    Guid? SourceValuedAcceptanceId,
    string? SourceAcceptanceNumber,
    DateTimeOffset CreatedAtUtc);

public sealed record StockLocationBalanceDto(
    Guid StockLocationBalanceId,
    Guid WarehouseId,
    string WarehouseCode,
    Guid LocationId,
    string LocationCode,
    string LocationName,
    string ZoneType,
    Guid? StockLotId,
    string? LotNumber,
    DateOnly? LotExpiryDate,
    string StockStatus,
    decimal OnHandBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal AvailableBaseQuantity,
    DateTimeOffset UpdatedAtUtc);

public sealed record StockReservationDto(
    Guid StockReservationId,
    string ReservationNumber,
    Guid StockLocationBalanceId,
    Guid ItemId,
    string ItemCode,
    Guid WarehouseId,
    Guid LocationId,
    string LocationCode,
    Guid? StockLotId,
    string? LotNumber,
    string SourceType,
    Guid? SourceDocumentId,
    string SourceDocumentNumber,
    decimal ReservedBaseQuantity,
    decimal ConsumedBaseQuantity,
    decimal ReleasedBaseQuantity,
    decimal RemainingReservedBaseQuantity,
    string Status,
    string? Notes,
    DateTimeOffset? ExpiresAtUtc,
    string CreatedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InspectionDispositionDto(
    Guid InspectionDispositionId,
    Guid ReceiptInspectionId,
    Guid StockMovementId,
    string MovementNumber,
    DateOnly DispositionDate,
    decimal ReleasedBaseQuantity,
    Guid? TargetReleaseLocationId,
    string? TargetReleaseLocationCode,
    decimal BlockedBaseQuantity,
    Guid? TargetBlockLocationId,
    string? TargetBlockLocationCode,
    string ReasonCode,
    string? EvidenceReference,
    string? Notes,
    string DecidedByEmail,
    DateTimeOffset DecidedAtUtc);

public sealed record ReceiptInspectionSummaryDto(
    Guid ReceiptInspectionId,
    string InspectionNumber,
    Guid ValuedAcceptanceId,
    string AcceptanceNumber,
    Guid GoodsReceiptId,
    string ReceiptNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    Guid WarehouseId,
    Guid QuarantineLocationId,
    string QuarantineLocationCode,
    Guid? StockLotId,
    string? LotNumber,
    decimal InspectedTotalBaseQuantity,
    decimal PendingBaseQuantity,
    decimal ReleasedBaseQuantity,
    decimal BlockedBaseQuantity,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<InspectionDispositionDto> Dispositions);

public sealed record ItemReconciliationDto(
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

public sealed record ItemMovementTraceDto(
    Guid StockMovementId,
    Guid MovementLineId,
    string MovementNumber,
    string MovementType,
    DateOnly PostingDate,
    string SourceDocumentType,
    string SourceDocumentNumber,
    string? LocationCode,
    string? ZoneType,
    string? LotNumber,
    string ResultingStockStatus,
    decimal BaseQuantityDelta,
    decimal UnitValuationCost,
    decimal LineValuationAmount,
    string? Notes,
    string CreatedByEmail,
    DateTimeOffset CreatedAtUtc);

public sealed record ItemStockDetailResponse(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    string LotTrackingPolicy,
    string CurrencyCode,
    string PoolStatus,
    decimal CarryingValueAmount,
    decimal MovingAverageUnitCost,
    ItemReconciliationDto Reconciliation,
    IReadOnlyList<StockLocationBalanceDto> LocationBalances,
    IReadOnlyList<StockLotSummaryDto> Lots,
    IReadOnlyList<ReceiptInspectionSummaryDto> Inspections,
    IReadOnlyList<StockReservationDto> Reservations,
    IReadOnlyList<ItemMovementTraceDto> MovementTimeline);

public sealed record TransferStockLocationRequest(
    Guid ItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    Guid? StockLotId,
    string StockStatus,
    decimal Quantity,
    DateOnly PostingDate,
    string IdempotencyKey,
    string? Notes);

public sealed record StockStatusTransitionRequest(
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
    string IdempotencyKey);

public sealed record RecordInspectionDispositionRequest(
    DateOnly DispositionDate,
    decimal ReleasedBaseQuantity,
    Guid? TargetReleaseLocationId,
    decimal BlockedBaseQuantity,
    Guid? TargetBlockLocationId,
    string ReasonCode,
    string? EvidenceReference,
    string? Notes,
    string IdempotencyKey);

public sealed record InspectionDispositionOutcomeDto(
    ReceiptInspectionSummaryDto Inspection,
    InspectionDispositionDto Disposition,
    string MovementNumber,
    bool WasIdempotentReplay);

public sealed record CreateStockReservationRequest(
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
    string? Notes);

public sealed record ReleaseStockReservationRequest(
    decimal? ReleaseBaseQuantity);

public sealed record StockReservationOutcomeDto(
    StockReservationDto Reservation,
    bool WasIdempotentReplay);

public sealed record InventoryMovementResultDto(
    Guid StockMovementId,
    string MovementNumber,
    string MovementType,
    DateOnly PostingDate,
    bool WasIdempotentReplay);

public sealed record StockPositionOverviewDto(
    Guid StockPositionId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string LotTrackingPolicy,
    Guid BaseUomId,
    string BaseUomCode,
    string ValuationGroupCode,
    string CurrencyCode,
    decimal UnrestrictedBaseQuantity,
    decimal QualityInspectionBaseQuantity,
    decimal BlockedBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal TotalValuedOnHandBaseQuantity,
    decimal AvailableBaseQuantity,
    decimal CarryingValueAmount,
    decimal MovingAverageUnitCost,
    string PoolStatus,
    long LastEntrySequence,
    int LocationCount,
    int ActiveLotCount,
    int PendingInspectionCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record StockMovementSummaryDto(
    Guid StockMovementId,
    string MovementNumber,
    string MovementType,
    DateOnly PostingDate,
    string SourceDocumentNumber,
    int LineCount,
    decimal TotalBaseQuantity,
    decimal TotalValuationAmount,
    string CreatedByEmail,
    DateTimeOffset CreatedAtUtc);

public sealed record ReceiptAccrualOverviewDto(
    Guid ReceiptAccrualId,
    string AccrualNumber,
    Guid ValuedAcceptanceId,
    string AcceptanceNumber,
    Guid GoodsReceiptId,
    string ReceiptNumber,
    Guid PurchaseOrderId,
    string PoNumber,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierName,
    Guid JournalId,
    string JournalNumber,
    DateOnly PostingDate,
    string CurrencyCode,
    decimal AccruedAmount,
    decimal ClearedAmount,
    decimal RemainingOpenAmount,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record InventoryOverviewResponse(
    IReadOnlyList<StockPositionOverviewDto> Positions,
    IReadOnlyList<StockMovementSummaryDto> RecentMovements,
    IReadOnlyList<ReceiptAccrualOverviewDto> OpenAccruals,
    IReadOnlyList<WarehouseLocationDto> Locations,
    IReadOnlyList<ReceiptInspectionSummaryDto> Inspections,
    int TotalStockedItemsWithBalance,
    decimal TotalCarryingValueIdr,
    decimal TotalOpenGrniAccrualIdr,
    int RestrictedStockItemCount,
    string Od04Notice,
    string Od10Notice);

public sealed record ValuationEntryLedgerDto(
    Guid ValuationEntryId,
    long PoolSequenceNumber,
    DateOnly PostingDate,
    string EntryType,
    string SourceDocumentType,
    Guid SourceDocumentId,
    string SourceDocumentNumber,
    string MovementNumber,
    Guid? GlJournalId,
    string? JournalNumber,
    decimal QuantityDelta,
    decimal UnitCostApplied,
    decimal ValueDelta,
    decimal PreviousPoolBaseQuantity,
    decimal PreviousPoolCarryingValue,
    decimal PreviousMovingAverageCost,
    decimal NewPoolBaseQuantity,
    decimal NewPoolCarryingValue,
    decimal NewMovingAverageCost,
    string CreatedByEmail,
    DateTimeOffset CreatedAtUtc);

public sealed record ItemValuationLedgerResponse(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    string CurrencyCode,
    string ValuationGroupCode,
    string PoolStatus,
    decimal CurrentValuedBaseQuantity,
    decimal CurrentCarryingValueAmount,
    decimal CurrentMovingAverageUnitCost,
    decimal UnrestrictedBaseQuantity,
    decimal QualityInspectionBaseQuantity,
    decimal BlockedBaseQuantity,
    decimal AvailableBaseQuantity,
    IReadOnlyList<ValuationEntryLedgerDto> Entries);
