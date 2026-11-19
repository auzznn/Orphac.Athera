namespace Orphac.Athera.Api.Contracts;

public sealed record BillingOutputTaxCodeDto(
    Guid TaxCodeId,
    string Code,
    string Name,
    string TreatmentClass,
    decimal NominalRatePercent,
    int TaxableBaseRatioNumerator,
    int TaxableBaseRatioDenominator,
    decimal EffectiveRatePercent,
    bool RequiresDocumentaryEvidence,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record BillableShipmentLineDto(
    Guid ShipmentLineId,
    Guid ShipmentId,
    string ShipmentNumber,
    DateOnly DispatchDate,
    string ShipmentStatus,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerNpwp,
    int PaymentTermsDays,
    Guid SoLineId,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    decimal DispatchedBaseQuantity,
    decimal AlreadyBilledBaseQuantity,
    decimal RemainingBillableBaseQuantity,
    decimal UnitPricePerBase,
    Guid? DefaultTaxCodeId,
    string? DefaultTaxCodeSnapshot);

public sealed record CustomerInvoiceLineDto(
    Guid InvoiceLineId,
    int LineNumber,
    Guid ShipmentId,
    string ShipmentNumber,
    Guid ShipmentLineId,
    Guid SoLineId,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string BaseUomCode,
    decimal BilledBaseQuantity,
    decimal AlreadyCreditedBaseQuantity,
    decimal RemainingCreditableBaseQuantity,
    decimal UnitPrice,
    decimal LineSubtotalAmount,
    string TaxCodeSnapshot,
    decimal TaxRatePercent,
    decimal LineTaxAmount,
    decimal LineTotalAmount,
    Guid? OriginalInvoiceLineId);

public sealed record CustomerInvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string DocumentType,
    Guid? OriginalInvoiceId,
    string? OriginalInvoiceNumber,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerNpwp,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string CurrencyCode,
    string Status,
    decimal SubtotalAmount,
    decimal TaxAmount,
    decimal GrandTotalAmount,
    Guid TaxCodeId,
    string TaxCodeSnapshot,
    string TaxTreatmentClassSnapshot,
    decimal TaxRatePercentSnapshot,
    bool RequiresTaxEvidenceSnapshot,
    string? TaxEvidenceReference,
    string TaxDeterminationSummary,
    Guid? GlJournalId,
    string? GlJournalNumber,
    Guid? ArReceivableId,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? PostedAtUtc,
    IReadOnlyList<CustomerInvoiceLineDto> Lines);

public sealed record CustomerReceivableDto(
    Guid ReceivableId,
    string ReceivableNumber,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal CreditedAmount,
    decimal ReceivedAmount,
    decimal OpenAmount,
    string Status,
    Guid GlJournalId,
    string GlJournalNumber,
    DateTimeOffset CreatedAtUtc);

public sealed record BillingSummaryStatsDto(
    decimal UnbilledDispatchedBaseQty,
    int OpenDraftInvoicesCount,
    decimal PostedRevenueNetTotal,
    decimal OpenReceivablesTotal);

public sealed record BillingOverviewResponse(
    bool CanDraft,
    bool CanPost,
    BillingSummaryStatsDto Stats,
    IReadOnlyList<BillingOutputTaxCodeDto> OutputTaxCodes,
    IReadOnlyList<BillableShipmentLineDto> BillableShipmentLines,
    IReadOnlyList<CustomerInvoiceDto> Invoices,
    IReadOnlyList<CustomerReceivableDto> Receivables);

public sealed record CreateCustomerInvoiceLineRequest(
    Guid ShipmentLineId,
    decimal BilledBaseQuantity);

public sealed record CreateCustomerInvoiceRequest(
    Guid SalesOrderId,
    Guid TaxCodeId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? TaxEvidenceReference,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<CreateCustomerInvoiceLineRequest> Lines);

public sealed record PostCustomerInvoiceRequest(
    string? TaxEvidenceReference);

public sealed record CreateCreditNoteLineRequest(
    Guid OriginalInvoiceLineId,
    decimal CreditedBaseQuantity);

public sealed record CreateCreditNoteRequest(
    Guid OriginalInvoiceId,
    DateOnly CreditDate,
    string? TaxEvidenceReference,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<CreateCreditNoteLineRequest> Lines);
