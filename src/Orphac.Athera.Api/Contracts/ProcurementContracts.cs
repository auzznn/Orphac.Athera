namespace Orphac.Athera.Api.Contracts;

public sealed record ProcurementSupplierOptionDto(
    Guid PartnerId,
    string PartnerCode,
    string LegalName,
    string DisplayName,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    int DefaultPaymentTermsDays,
    string EntityScopeMode);

public sealed record ProcurementItemUomOptionDto(
    Guid UomId,
    string UomCode,
    string Name,
    decimal FactorToBase,
    int PrecisionScale);

public sealed record ProcurementItemOptionDto(
    Guid ItemId,
    string ItemCode,
    string Name,
    string ItemKind,
    Guid BaseUomId,
    string BaseUomCode,
    Guid DefaultPurchaseUomId,
    IReadOnlyList<ProcurementItemUomOptionDto> AllowedUoms);

public sealed record ProcurementTaxOptionDto(
    Guid TaxCodeId,
    string Code,
    string Name,
    string TaxFamily,
    string Direction,
    decimal NominalRatePercent,
    int TaxableBaseRatioNumerator,
    int TaxableBaseRatioDenominator,
    bool RequiresCounterpartyNpwp,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string ReviewStatus);

public sealed record DeliveryTolerancePolicyDto(
    Guid PolicyId,
    string Code,
    string Name,
    string Mode,
    decimal OverdeliveryTolerancePercent,
    decimal OverdeliveryToleranceBaseQty,
    bool AllowUnlimitedOverdelivery,
    string ReviewStatus,
    bool IsDefault);

public sealed record ProcurementReferenceDataResponse(
    IReadOnlyList<ProcurementSupplierOptionDto> Suppliers,
    IReadOnlyList<ProcurementItemOptionDto> Items,
    IReadOnlyList<ProcurementTaxOptionDto> TaxCodes,
    IReadOnlyList<DeliveryTolerancePolicyDto> TolerancePolicies,
    string Od01Notice,
    string Od02Notice);

public sealed record PurchaseOrderSummaryDto(
    Guid PurchaseOrderId,
    string DraftCode,
    string? PoNumber,
    string Status,
    string ReceivingProgress,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    int CurrentApprovedRevisionNumber,
    int LatestRevisionNumber,
    bool HasOpenRevision,
    Guid? PendingApprovalRevisionId,
    Guid? PendingApprovalSubmittedByUserId,
    decimal CurrentGrandTotalAmount,
    decimal CurrentOrderedBaseQuantity,
    decimal AcceptedBaseQuantityTotal,
    decimal RemainingOpenBaseQuantity,
    int DownstreamReferenceCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record PurchaseOrderListResponse(IReadOnlyList<PurchaseOrderSummaryDto> Items);

public sealed record PurchaseOrderLineDto(
    Guid PoLineId,
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
    decimal UnitPrice,
    decimal LineSubtotalAmount,
    Guid? TaxCodeId,
    string? TaxCodeSnapshot,
    string? TaxFamilySnapshot,
    decimal? TaxRatePercentSnapshot,
    decimal TaxableBaseAmount,
    decimal TaxAmount,
    decimal OverdeliveryTolerancePercent,
    decimal OverdeliveryToleranceBaseQty,
    bool AllowUnlimitedOverdelivery,
    string? LineDescription);

public sealed record DocumentApprovalDto(
    Guid ApprovalId,
    Guid PolicyId,
    string Status,
    string RequestSummary,
    Guid RequestedByUserId,
    string RequestedByEmail,
    DateTimeOffset RequestedAtUtc,
    Guid? DecidedByUserId,
    string? DecidedByEmail,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason);

public sealed record PoHeaderFieldDiffDto(
    string FieldKey,
    string? PreviousValue,
    string? CurrentValue);

public sealed record PoLineDiffItemDto(
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

public sealed record PoRevisionDiffReportDto(
    int TargetRevisionNumber,
    int? BaselineRevisionNumber,
    IReadOnlyList<PoHeaderFieldDiffDto> HeaderChanges,
    IReadOnlyList<PoLineDiffItemDto> LineChanges,
    decimal? PreviousGrandTotal,
    decimal CurrentGrandTotal,
    decimal GrandTotalDelta,
    decimal PreviousOrderedBaseQty,
    decimal CurrentOrderedBaseQty,
    decimal OrderedBaseQtyDelta);

public sealed record PurchaseOrderRevisionDto(
    Guid RevisionId,
    int RevisionNumber,
    Guid? SupersedesRevisionId,
    string Status,
    bool IsFrozen,
    Guid SupplierPartnerId,
    string SupplierCodeSnapshot,
    string SupplierLegalNameSnapshot,
    string? SupplierNpwpSnapshot,
    string SupplierPkpStatusSnapshot,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid TolerancePolicyId,
    string ToleranceModeSnapshot,
    string? ChangeReason,
    string? Notes,
    decimal SubtotalAmount,
    decimal PpnTaxTotalAmount,
    decimal WithholdingTaxTotalAmount,
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
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    DocumentApprovalDto? Approval,
    PoRevisionDiffReportDto? DiffFromBaseline);

public sealed record PurchaseOrderDetailDto(
    Guid PurchaseOrderId,
    string DraftCode,
    string? PoNumber,
    string Status,
    string ReceivingProgress,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid? CurrentApprovedRevisionId,
    int CurrentApprovedRevisionNumber,
    int LatestRevisionNumber,
    int DownstreamReferenceCount,
    decimal ApprovedOrderedBaseQuantity,
    decimal AcceptedBaseQuantityTotal,
    decimal RemainingOpenBaseQuantity,
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
    IReadOnlyList<PurchaseOrderRevisionDto> Revisions);

public sealed record PoLineInputDto(
    string? LineageLineKey,
    Guid ItemId,
    Guid OrderedUomId,
    decimal OrderedQuantity,
    decimal UnitPrice,
    Guid? TaxCodeId,
    decimal OverdeliveryTolerancePercent = 0m,
    decimal OverdeliveryToleranceBaseQty = 0m,
    bool AllowUnlimitedOverdelivery = false,
    string? LineDescription = null);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierPartnerId,
    string? CurrencyCode,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid? TolerancePolicyId,
    string? Notes,
    IReadOnlyList<PoLineInputDto> Lines);

public sealed record UpdatePurchaseOrderRevisionRequest(
    Guid SupplierPartnerId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid? TolerancePolicyId,
    string? ChangeReason,
    string? Notes,
    IReadOnlyList<PoLineInputDto> Lines);

public sealed record CreatePoAmendmentRequest(string ChangeReason);

public sealed record SubmitPoRevisionRequest(string? PoNumberOverride);

public sealed record DecidePoRevisionRequest(string Decision, string Reason);

public sealed record CloseOrCancelPoRequest(string Reason);
