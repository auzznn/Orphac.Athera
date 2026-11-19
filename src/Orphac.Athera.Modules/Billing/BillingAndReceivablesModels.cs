using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Billing;

public static class InvoiceDocumentTypes
{
    public const string Invoice = "INVOICE";
    public const string CreditNote = "CREDIT_NOTE";

    public static readonly IReadOnlyList<string> All = [Invoice, CreditNote];
}

public static class CustomerInvoiceStatuses
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string Posted = "POSTED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, Submitted, Posted, Cancelled];
}

public static class ReceivableStatuses
{
    public const string Open = "OPEN";
    public const string PartiallySettled = "PARTIALLY_SETTLED";
    public const string Settled = "SETTLED";
    public const string Credited = "CREDITED";

    public static readonly IReadOnlyList<string> All = [Open, PartiallySettled, Settled, Credited];
}

public sealed class InvoiceSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private InvoiceSequence() { }
}

public sealed class CreditNoteSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private CreditNoteSequence() { }
}

public sealed class ReceivableSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ReceivableSequence() { }
}

public sealed class CustomerInvoice
{
    public Guid InvoiceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string DocumentType { get; private set; } = InvoiceDocumentTypes.Invoice;
    public Guid? OriginalInvoiceId { get; private set; }
    public string? OriginalInvoiceNumberSnapshot { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public string SalesOrderNumberSnapshot { get; private set; } = string.Empty;
    public Guid CustomerPartnerId { get; private set; }
    public string CustomerCodeSnapshot { get; private set; } = string.Empty;
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public string? CustomerNpwpSnapshot { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public string Status { get; private set; } = CustomerInvoiceStatuses.Draft;
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrandTotalAmount { get; private set; }
    public Guid TaxCodeId { get; private set; }
    public string TaxCodeSnapshot { get; private set; } = string.Empty;
    public string TaxTreatmentClassSnapshot { get; private set; } = string.Empty;
    public decimal TaxRatePercentSnapshot { get; private set; }
    public bool RequiresTaxEvidenceSnapshot { get; private set; }
    public string? TaxEvidenceReference { get; private set; }
    public string TaxDeterminationSummary { get; private set; } = string.Empty;
    public Guid? GlJournalId { get; private set; }
    public string? GlJournalNumber { get; private set; }
    public Guid? ArReceivableId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? PostedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private CustomerInvoice() { }

    public static CustomerInvoice CreateDraft(
        Guid invoiceId,
        Guid tenantId,
        Guid legalEntityId,
        string invoiceNumber,
        string documentType,
        Guid? originalInvoiceId,
        string? originalInvoiceNumberSnapshot,
        Guid salesOrderId,
        string salesOrderNumberSnapshot,
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerNameSnapshot,
        string? customerNpwpSnapshot,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string currencyCode,
        decimal subtotalAmount,
        decimal taxAmount,
        Guid taxCodeId,
        string taxCodeSnapshot,
        string taxTreatmentClassSnapshot,
        decimal taxRatePercentSnapshot,
        bool requiresTaxEvidenceSnapshot,
        string? taxEvidenceReference,
        string taxDeterminationSummary,
        string idempotencyKey,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        if (!InvoiceDocumentTypes.All.Contains(documentType, StringComparer.Ordinal))
        {
            throw new ArgumentException($"billing_invalid_document_type: Unsupported document type '{documentType}'.", nameof(documentType));
        }

        if (dueDate < invoiceDate)
        {
            throw new InvalidOperationException("billing_due_date_before_invoice_date: Due date cannot be earlier than invoice date.");
        }

        var sub = decimal.Round(subtotalAmount, 2, MidpointRounding.AwayFromZero);
        var tax = decimal.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
        var grand = decimal.Round(sub + tax, 2, MidpointRounding.AwayFromZero);
        if (sub <= 0m || tax < 0m || grand <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(subtotalAmount), "billing_amount_must_be_positive: Invoice subtotal and grand total must be strictly positive.");
        }

        if (documentType == InvoiceDocumentTypes.CreditNote && (!originalInvoiceId.HasValue || originalInvoiceId.Value == Guid.Empty))
        {
            throw new InvalidOperationException("billing_credit_note_requires_original_invoice: Credit note must reference a posted customer invoice.");
        }

        return new CustomerInvoice
        {
            InvoiceId = GuardValidation.RequireNonEmptyGuid(invoiceId, nameof(invoiceId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            InvoiceNumber = GuardValidation.RequireCode(invoiceNumber, nameof(invoiceNumber), 40),
            DocumentType = documentType,
            OriginalInvoiceId = originalInvoiceId,
            OriginalInvoiceNumberSnapshot = GuardValidation.NormalizeOptional(originalInvoiceNumberSnapshot, 40, nameof(originalInvoiceNumberSnapshot)),
            SalesOrderId = GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId)),
            SalesOrderNumberSnapshot = GuardValidation.RequireCode(salesOrderNumberSnapshot, nameof(salesOrderNumberSnapshot), 40),
            CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId)),
            CustomerCodeSnapshot = GuardValidation.RequireCode(customerCodeSnapshot, nameof(customerCodeSnapshot), 32),
            CustomerNameSnapshot = GuardValidation.RequireName(customerNameSnapshot, nameof(customerNameSnapshot), 200),
            CustomerNpwpSnapshot = GuardValidation.NormalizeOptional(customerNpwpSnapshot, 32, nameof(customerNpwpSnapshot)),
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            Status = CustomerInvoiceStatuses.Draft,
            SubtotalAmount = sub,
            TaxAmount = tax,
            GrandTotalAmount = grand,
            TaxCodeId = GuardValidation.RequireNonEmptyGuid(taxCodeId, nameof(taxCodeId)),
            TaxCodeSnapshot = GuardValidation.RequireCode(taxCodeSnapshot, nameof(taxCodeSnapshot), 32),
            TaxTreatmentClassSnapshot = GuardValidation.RequireCode(taxTreatmentClassSnapshot, nameof(taxTreatmentClassSnapshot), 40),
            TaxRatePercentSnapshot = decimal.Round(taxRatePercentSnapshot, 4, MidpointRounding.AwayFromZero),
            RequiresTaxEvidenceSnapshot = requiresTaxEvidenceSnapshot,
            TaxEvidenceReference = GuardValidation.NormalizeOptional(taxEvidenceReference, 120, nameof(taxEvidenceReference)),
            TaxDeterminationSummary = GuardValidation.RequireName(taxDeterminationSummary, nameof(taxDeterminationSummary), 240),
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            CreatedByUserId = GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void Submit(Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != CustomerInvoiceStatuses.Draft)
        {
            throw new InvalidOperationException($"billing_invalid_status_transition: Only DRAFT invoices can be submitted (current: '{Status}').");
        }

        Status = CustomerInvoiceStatuses.Submitted;
        SubmittedByUserId = actorUserId;
        SubmittedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        if (Status is not (CustomerInvoiceStatuses.Draft or CustomerInvoiceStatuses.Submitted))
        {
            throw new InvalidOperationException($"billing_cannot_cancel_posted: Posted or already cancelled invoices cannot be cancelled (current: '{Status}').");
        }

        Status = CustomerInvoiceStatuses.Cancelled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkPosted(
        Guid glJournalId,
        string glJournalNumber,
        Guid arReceivableId,
        string? taxEvidenceReference,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId));
        GuardValidation.RequireNonEmptyGuid(arReceivableId, nameof(arReceivableId));
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));

        if (Status is not (CustomerInvoiceStatuses.Draft or CustomerInvoiceStatuses.Submitted))
        {
            throw new InvalidOperationException($"billing_invalid_status_transition: Invoice must be DRAFT or SUBMITTED to post (current: '{Status}').");
        }

        if (!string.IsNullOrWhiteSpace(taxEvidenceReference))
        {
            TaxEvidenceReference = GuardValidation.NormalizeOptional(taxEvidenceReference, 120, nameof(taxEvidenceReference));
        }

        GlJournalId = glJournalId;
        GlJournalNumber = GuardValidation.RequireCode(glJournalNumber, nameof(glJournalNumber), 40);
        ArReceivableId = arReceivableId;
        Status = CustomerInvoiceStatuses.Posted;
        PostedByUserId = actorUserId;
        PostedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class CustomerInvoiceLine
{
    public Guid InvoiceLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string ShipmentNumberSnapshot { get; private set; } = string.Empty;
    public Guid ShipmentLineId { get; private set; }
    public Guid SoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;
    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal BilledBaseQuantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineSubtotalAmount { get; private set; }
    public Guid TaxCodeId { get; private set; }
    public string TaxCodeSnapshot { get; private set; } = string.Empty;
    public decimal TaxRatePercent { get; private set; }
    public decimal LineTaxAmount { get; private set; }
    public decimal LineTotalAmount { get; private set; }
    public Guid? OriginalInvoiceLineId { get; private set; }

    private CustomerInvoiceLine() { }

    public static CustomerInvoiceLine Create(
        Guid invoiceLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid invoiceId,
        int lineNumber,
        Guid shipmentId,
        string shipmentNumberSnapshot,
        Guid shipmentLineId,
        Guid soLineId,
        string lineageLineKey,
        Guid itemId,
        string itemCodeSnapshot,
        string itemNameSnapshot,
        Guid baseUomId,
        string baseUomCodeSnapshot,
        decimal billedBaseQuantity,
        decimal unitPrice,
        Guid taxCodeId,
        string taxCodeSnapshot,
        decimal taxRatePercent,
        decimal lineSubtotalAmount,
        decimal lineTaxAmount,
        Guid? originalInvoiceLineId = null)
    {
        var qty = decimal.Round(billedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var price = decimal.Round(unitPrice, 6, MidpointRounding.AwayFromZero);
        var sub = decimal.Round(lineSubtotalAmount, 2, MidpointRounding.AwayFromZero);
        var tax = decimal.Round(lineTaxAmount, 2, MidpointRounding.AwayFromZero);
        var total = decimal.Round(sub + tax, 2, MidpointRounding.AwayFromZero);

        if (qty <= 0m || price < 0m || sub <= 0m || tax < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(billedBaseQuantity), "billing_line_qty_and_amount_must_be_positive: Invoice line quantity and subtotal must be strictly positive.");
        }

        return new CustomerInvoiceLine
        {
            InvoiceLineId = GuardValidation.RequireNonEmptyGuid(invoiceLineId, nameof(invoiceLineId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            InvoiceId = GuardValidation.RequireNonEmptyGuid(invoiceId, nameof(invoiceId)),
            LineNumber = lineNumber,
            ShipmentId = GuardValidation.RequireNonEmptyGuid(shipmentId, nameof(shipmentId)),
            ShipmentNumberSnapshot = GuardValidation.RequireCode(shipmentNumberSnapshot, nameof(shipmentNumberSnapshot), 40),
            ShipmentLineId = GuardValidation.RequireNonEmptyGuid(shipmentLineId, nameof(shipmentLineId)),
            SoLineId = GuardValidation.RequireNonEmptyGuid(soLineId, nameof(soLineId)),
            LineageLineKey = GuardValidation.RequireCode(lineageLineKey, nameof(lineageLineKey), 32),
            ItemId = GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId)),
            ItemCodeSnapshot = GuardValidation.RequireCode(itemCodeSnapshot, nameof(itemCodeSnapshot), 40),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 160),
            BaseUomId = GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId)),
            BaseUomCodeSnapshot = GuardValidation.RequireCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 16),
            BilledBaseQuantity = qty,
            UnitPrice = price,
            LineSubtotalAmount = sub,
            TaxCodeId = GuardValidation.RequireNonEmptyGuid(taxCodeId, nameof(taxCodeId)),
            TaxCodeSnapshot = GuardValidation.RequireCode(taxCodeSnapshot, nameof(taxCodeSnapshot), 32),
            TaxRatePercent = decimal.Round(taxRatePercent, 4, MidpointRounding.AwayFromZero),
            LineTaxAmount = tax,
            LineTotalAmount = total,
            OriginalInvoiceLineId = originalInvoiceLineId
        };
    }
}

public sealed class CustomerReceivable
{
    public Guid ReceivableId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string ReceivableNumber { get; private set; } = string.Empty;
    public Guid InvoiceId { get; private set; }
    public string InvoiceNumberSnapshot { get; private set; } = string.Empty;
    public Guid CustomerPartnerId { get; private set; }
    public string CustomerCodeSnapshot { get; private set; } = string.Empty;
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal OriginalAmount { get; private set; }
    public decimal CreditedAmount { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public decimal OpenAmount { get; private set; }
    public string Status { get; private set; } = ReceivableStatuses.Open;
    public Guid GlJournalId { get; private set; }
    public string GlJournalNumber { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private CustomerReceivable() { }

    public static CustomerReceivable Create(
        Guid receivableId,
        Guid tenantId,
        Guid legalEntityId,
        string receivableNumber,
        Guid invoiceId,
        string invoiceNumberSnapshot,
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerNameSnapshot,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string currencyCode,
        decimal originalAmount,
        Guid glJournalId,
        string glJournalNumber,
        DateTimeOffset nowUtc)
    {
        var amount = decimal.Round(originalAmount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "ar_amount_must_be_positive: Receivable original amount must be strictly positive.");
        }

        return new CustomerReceivable
        {
            ReceivableId = GuardValidation.RequireNonEmptyGuid(receivableId, nameof(receivableId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            ReceivableNumber = GuardValidation.RequireCode(receivableNumber, nameof(receivableNumber), 40),
            InvoiceId = GuardValidation.RequireNonEmptyGuid(invoiceId, nameof(invoiceId)),
            InvoiceNumberSnapshot = GuardValidation.RequireCode(invoiceNumberSnapshot, nameof(invoiceNumberSnapshot), 40),
            CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId)),
            CustomerCodeSnapshot = GuardValidation.RequireCode(customerCodeSnapshot, nameof(customerCodeSnapshot), 32),
            CustomerNameSnapshot = GuardValidation.RequireName(customerNameSnapshot, nameof(customerNameSnapshot), 200),
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            OriginalAmount = amount,
            CreditedAmount = 0m,
            ReceivedAmount = 0m,
            OpenAmount = amount,
            Status = ReceivableStatuses.Open,
            GlJournalId = GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId)),
            GlJournalNumber = GuardValidation.RequireCode(glJournalNumber, nameof(glJournalNumber), 40),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyCredit(decimal creditAmount, DateTimeOffset nowUtc)
    {
        var credit = decimal.Round(creditAmount, 2, MidpointRounding.AwayFromZero);
        if (credit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(creditAmount), "ar_credit_must_be_positive: Credit amount must be strictly positive.");
        }

        if (credit > OpenAmount)
        {
            throw new InvalidOperationException(
                $"ar_credit_exceeds_open_amount: Credit amount ({credit:0.00}) exceeds remaining open AR balance ({OpenAmount:0.00}).");
        }

        CreditedAmount = decimal.Round(CreditedAmount + credit, 2, MidpointRounding.AwayFromZero);
        OpenAmount = decimal.Round(OriginalAmount - CreditedAmount - ReceivedAmount, 2, MidpointRounding.AwayFromZero);
        Status = OpenAmount == 0m
            ? (ReceivedAmount > 0m ? ReceivableStatuses.Settled : ReceivableStatuses.Credited)
            : ReceivableStatuses.PartiallySettled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyReceipt(decimal receiptAllocationAmount, DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(receiptAllocationAmount, 2, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(receiptAllocationAmount), "ar_receipt_allocation_must_be_positive: Allocation amount must be strictly positive.");
        }

        if (amt > OpenAmount)
        {
            throw new InvalidOperationException(
                $"ar_allocation_exceeds_open_amount: Allocation ({amt:0.00}) exceeds remaining open AR balance ({OpenAmount:0.00}).");
        }

        ReceivedAmount = decimal.Round(ReceivedAmount + amt, 2, MidpointRounding.AwayFromZero);
        OpenAmount = decimal.Round(OriginalAmount - CreditedAmount - ReceivedAmount, 2, MidpointRounding.AwayFromZero);
        Status = OpenAmount == 0m
            ? ReceivableStatuses.Settled
            : ReceivableStatuses.PartiallySettled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ReverseReceipt(decimal reversedAllocationAmount, DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(reversedAllocationAmount, 2, MidpointRounding.AwayFromZero);
        if (amt <= 0m || amt > ReceivedAmount)
        {
            throw new InvalidOperationException(
                $"ar_reversal_exceeds_received_amount: Reversal ({amt:0.00}) exceeds received amount ({ReceivedAmount:0.00}).");
        }

        ReceivedAmount = decimal.Round(ReceivedAmount - amt, 2, MidpointRounding.AwayFromZero);
        OpenAmount = decimal.Round(OriginalAmount - CreditedAmount - ReceivedAmount, 2, MidpointRounding.AwayFromZero);
        Status = OpenAmount == 0m
            ? (ReceivedAmount > 0m ? ReceivableStatuses.Settled : ReceivableStatuses.Credited)
            : (ReceivedAmount == 0m && CreditedAmount == 0m ? ReceivableStatuses.Open : ReceivableStatuses.PartiallySettled);
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}
