namespace Orphac.Athera.Api.Contracts;

public sealed record AcceptReceiptLineRequest(
    Guid GoodsReceiptLineId,
    decimal AcceptedBaseQuantity);

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
    string? BlockingReason);

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

public sealed record StockPositionOverviewDto(
    Guid StockPositionId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
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
    int TotalStockedItemsWithBalance,
    decimal TotalCarryingValueIdr,
    decimal TotalOpenGrniAccrualIdr,
    int RestrictedStockItemCount,
    string Od04Notice);

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
