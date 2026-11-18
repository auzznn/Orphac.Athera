namespace Orphac.Athera.Api.Contracts;

public sealed record SalesCustomerOptionDto(
    Guid PartnerId,
    string PartnerCode,
    string LegalName,
    string DisplayName,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    int DefaultPaymentTermsDays,
    string EntityScopeMode);

public sealed record SalesItemUomOptionDto(
    Guid UomId,
    string UomCode,
    string Name,
    decimal FactorToBase,
    int PrecisionScale);

public sealed record SalesItemAvailabilityDto(
    decimal UnrestrictedBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal AvailableBaseQuantity,
    decimal TotalValuedOnHandBaseQuantity);

public sealed record SalesItemOptionDto(
    Guid ItemId,
    string ItemCode,
    string Name,
    string ItemKind,
    Guid BaseUomId,
    string BaseUomCode,
    Guid DefaultSalesUomId,
    IReadOnlyList<SalesItemUomOptionDto> AllowedUoms,
    SalesItemAvailabilityDto StockAvailability);

public sealed record SalesTaxOptionDto(
    Guid TaxCodeId,
    string Code,
    string Name,
    string TaxFamily,
    string Direction,
    string TreatmentClass,
    decimal NominalRatePercent,
    int TaxableBaseRatioNumerator,
    int TaxableBaseRatioDenominator,
    bool RequiresCounterpartyNpwp,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string ReviewStatus);

public sealed record SalesOrdersReferenceDataResponse(
    IReadOnlyList<SalesCustomerOptionDto> Customers,
    IReadOnlyList<SalesItemOptionDto> Items,
    IReadOnlyList<SalesTaxOptionDto> OutputTaxCodes,
    string Od09Notice,
    string Od02Notice);

public sealed record SalesOrderSummaryDto(
    Guid SalesOrderId,
    string DraftCode,
    string? SoNumber,
    string Status,
    string FulfillmentProgress,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    int CurrentApprovedRevisionNumber,
    int LatestRevisionNumber,
    bool HasOpenRevision,
    Guid? PendingApprovalRevisionId,
    Guid? PendingApprovalSubmittedByUserId,
    decimal CurrentSubtotalAmount,
    decimal CurrentOutputTaxTotalAmount,
    decimal CurrentGrandTotalAmount,
    decimal CurrentOrderedBaseQuantity,
    decimal TotalReservedBaseQuantity,
    decimal TotalDispatchedBaseQuantity,
    decimal TotalBilledBaseQuantity,
    DateTimeOffset UpdatedAtUtc,
    decimal? PendingGrandTotalAmount = null);

public sealed record SalesOrderListResponse(IReadOnlyList<SalesOrderSummaryDto> Items);

public sealed record SalesOrderLineDto(
    Guid SoLineId,
    int LineNumber,
    string LineageLineKey,
    Guid ItemId,
    string ItemCodeSnapshot,
    string ItemNameSnapshot,
    string ItemKindSnapshot,
    Guid OrderedUomId,
    string OrderedUomCodeSnapshot,
    Guid BaseUomId,
    string BaseUomCodeSnapshot,
    decimal UomFactorToBaseSnapshot,
    decimal OrderedQuantity,
    decimal OrderedBaseQuantity,
    decimal ReservedBaseQuantity,
    decimal DispatchedBaseQuantity,
    decimal BilledBaseQuantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineSubtotalAmount,
    Guid? TaxCodeId,
    string? TaxCodeSnapshot,
    string? TaxTreatmentClassSnapshot,
    decimal? TaxRatePercentSnapshot,
    decimal TaxableBaseAmount,
    decimal TaxAmount,
    decimal LineTotalAmount,
    DateOnly? RequestedDeliveryDate,
    string? LineDescription);

public sealed record SoHeaderFieldDiffDto(
    string FieldKey,
    string? PreviousValue,
    string? CurrentValue);

public sealed record SoLineDiffItemDto(
    string LineageLineKey,
    string ChangeKind,
    string? PreviousItemCode,
    string? CurrentItemCode,
    decimal? PreviousQuantity,
    decimal? CurrentQuantity,
    string? PreviousUomCode,
    string? CurrentUomCode,
    decimal? PreviousUnitPrice,
    decimal? CurrentUnitPrice,
    decimal? PreviousSubtotal,
    decimal? CurrentSubtotal);

public sealed record SoRevisionDiffReportDto(
    int TargetRevisionNumber,
    int? BaselineRevisionNumber,
    IReadOnlyList<SoHeaderFieldDiffDto> HeaderChanges,
    IReadOnlyList<SoLineDiffItemDto> LineChanges,
    decimal? PreviousGrandTotal,
    decimal CurrentGrandTotal,
    decimal GrandTotalDelta,
    decimal PreviousOrderedBaseQty,
    decimal CurrentOrderedBaseQty,
    decimal OrderedBaseQtyDelta);

public sealed record SalesOrderRevisionDto(
    Guid RevisionId,
    int RevisionNumber,
    Guid? SupersedesRevisionId,
    string Status,
    bool IsFrozen,
    Guid CustomerPartnerId,
    string CustomerCodeSnapshot,
    string CustomerLegalNameSnapshot,
    string? CustomerNpwpSnapshot,
    string CustomerPkpStatusSnapshot,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    string? ChangeReason,
    string? Notes,
    decimal SubtotalAmount,
    decimal OutputTaxTotalAmount,
    decimal GrandTotalAmount,
    decimal TotalOrderedBaseQuantity,
    string SnapshotHash,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAtUtc,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason,
    long Version,
    IReadOnlyList<SalesOrderLineDto> Lines,
    DocumentApprovalDto? Approval,
    SoRevisionDiffReportDto? DiffFromBaseline);

public sealed record SalesOrderDetailDto(
    Guid SalesOrderId,
    string DraftCode,
    string? SoNumber,
    string Status,
    string FulfillmentProgress,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    Guid? CurrentApprovedRevisionId,
    int CurrentApprovedRevisionNumber,
    int LatestRevisionNumber,
    decimal ApprovedOrderedBaseQuantity,
    decimal TotalReservedBaseQuantity,
    decimal TotalDispatchedBaseQuantity,
    decimal TotalBilledBaseQuantity,
    Guid? ClosedByUserId,
    DateTimeOffset? ClosedAtUtc,
    string? ClosureReason,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version,
    IReadOnlyList<SalesOrderRevisionDto> Revisions);

public sealed record SoLineInputDto(
    string? LineageLineKey,
    Guid ItemId,
    Guid OrderedUomId,
    decimal OrderedQuantity,
    decimal UnitPrice,
    decimal DiscountAmount = 0m,
    Guid? TaxCodeId = null,
    DateOnly? RequestedDeliveryDate = null,
    string? LineDescription = null);

public sealed record CreateSalesOrderRequest(
    Guid CustomerPartnerId,
    string? CurrencyCode,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    bool CreditLimitOverrideRequested,
    string? Notes,
    IReadOnlyList<SoLineInputDto> Lines);

public sealed record UpdateSalesOrderRevisionRequest(
    Guid CustomerPartnerId,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    bool CreditLimitOverrideRequested,
    string? ChangeReason,
    string? Notes,
    IReadOnlyList<SoLineInputDto> Lines);

public sealed record CreateSoAmendmentRequest(string ChangeReason);

public sealed record DecideSoRevisionRequest(string Decision, string Reason, string? SoNumberOverride = null);

public sealed record CloseOrCancelSoRequest(string Reason);
