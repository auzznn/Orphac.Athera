using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Sales;

public static class SalesOrderStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, PendingApproval, Approved, Closed, Cancelled];
}

public static class SalesOrderRevisionStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, PendingApproval, Approved, Rejected, Superseded, Cancelled];
}

public static class SalesFulfillmentProgressStatuses
{
    public const string NotApplicable = "NOT_APPLICABLE";
    public const string NotFulfilled = "NOT_FULFILLED";
    public const string PartiallyReserved = "PARTIALLY_RESERVED";
    public const string FullyReserved = "FULLY_RESERVED";
    public const string PartiallyDispatched = "PARTIALLY_DISPATCHED";
    public const string FullyDispatched = "FULLY_DISPATCHED";
    public const string ShortClosed = "SHORT_CLOSED";

    public static string Derive(
        string soStatus,
        bool hasApprovedRevision,
        decimal approvedOrderedBaseQty,
        decimal totalReservedBaseQty,
        decimal totalDispatchedBaseQty)
    {
        if (!hasApprovedRevision || soStatus == SalesOrderStatuses.Cancelled)
        {
            return NotApplicable;
        }

        if (soStatus == SalesOrderStatuses.Closed)
        {
            return approvedOrderedBaseQty > 0m && totalDispatchedBaseQty >= approvedOrderedBaseQty
                ? FullyDispatched
                : ShortClosed;
        }

        if (totalDispatchedBaseQty > 0m)
        {
            return totalDispatchedBaseQty >= approvedOrderedBaseQty ? FullyDispatched : PartiallyDispatched;
        }

        if (totalReservedBaseQty > 0m)
        {
            return totalReservedBaseQty >= approvedOrderedBaseQty ? FullyReserved : PartiallyReserved;
        }

        return NotFulfilled;
    }
}

public static class SalesPolicyDefaults
{
    public const string SalesOrderApprovalPolicyCode = "SALES_ORDER_COMMITMENT";
    public const string Od09SafePolicyCode = "POSITIVE_PRICE_ZERO_DISCOUNT_OD09_SAFE";
}

public sealed class SalesOrderSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private SalesOrderSequence()
    {
    }
}

public sealed class SalesOrder
{
    public Guid SalesOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DraftCode { get; private set; } = string.Empty;
    public string? SoNumber { get; private set; }
    public Guid CustomerPartnerId { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public DateOnly OrderDate { get; private set; }
    public DateOnly? RequestedDeliveryDate { get; private set; }
    public string DeliveryAddressSnapshot { get; private set; } = string.Empty;
    public int PaymentTermsDays { get; private set; }
    public string? CustomerReference { get; private set; }
    public string Status { get; private set; } = SalesOrderStatuses.Draft;
    public Guid? CurrentApprovedRevisionId { get; private set; }
    public int CurrentApprovedRevisionNumber { get; private set; }
    public int LatestRevisionNumber { get; private set; } = 1;
    public decimal TotalReservedBaseQuantity { get; private set; }
    public decimal TotalDispatchedBaseQuantity { get; private set; }
    public decimal TotalBilledBaseQuantity { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? ClosureReason { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SalesOrder()
    {
    }

    public static SalesOrder CreateDraft(
        Guid salesOrderId,
        Guid tenantId,
        Guid legalEntityId,
        string draftCode,
        Guid customerPartnerId,
        string currencyCode,
        string expectedFunctionalCurrencyCode,
        DateOnly orderDate,
        DateOnly? requestedDeliveryDate,
        string deliveryAddressSnapshot,
        int paymentTermsDays,
        string? customerReference,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var normalizedCurrency = GuardValidation.RequireExactIsoCode(currencyCode, expectedFunctionalCurrencyCode, 3, nameof(currencyCode));
        var normalizedExpected = GuardValidation.RequireExactIsoCode(expectedFunctionalCurrencyCode, "IDR", 3, nameof(expectedFunctionalCurrencyCode));
        if (!string.Equals(normalizedCurrency, normalizedExpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"unsupported_currency_mvp: Sales order currency must match legal entity currency '{normalizedExpected}'.");
        }

        if (requestedDeliveryDate is not null && requestedDeliveryDate.Value < orderDate)
        {
            throw new ArgumentException("RequestedDeliveryDate cannot precede OrderDate.", nameof(requestedDeliveryDate));
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");
        }

        return new SalesOrder
        {
            SalesOrderId = salesOrderId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            DraftCode = GuardValidation.RequireCode(draftCode, nameof(draftCode), 40),
            SoNumber = null,
            CustomerPartnerId = customerPartnerId,
            CurrencyCode = normalizedCurrency,
            OrderDate = orderDate,
            RequestedDeliveryDate = requestedDeliveryDate,
            DeliveryAddressSnapshot = GuardValidation.RequireName(deliveryAddressSnapshot, nameof(deliveryAddressSnapshot), 240),
            PaymentTermsDays = paymentTermsDays,
            CustomerReference = GuardValidation.NormalizeOptional(customerReference, 80, nameof(customerReference)),
            Status = SalesOrderStatuses.Draft,
            CurrentApprovedRevisionId = null,
            CurrentApprovedRevisionNumber = 0,
            LatestRevisionNumber = 1,
            TotalReservedBaseQuantity = 0m,
            TotalDispatchedBaseQuantity = 0m,
            TotalBilledBaseQuantity = 0m,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdatePreApprovalHeader(
        Guid customerPartnerId,
        DateOnly orderDate,
        DateOnly? requestedDeliveryDate,
        string deliveryAddressSnapshot,
        int paymentTermsDays,
        string? customerReference,
        DateTimeOffset nowUtc)
    {
        if (CurrentApprovedRevisionId is not null || Status != SalesOrderStatuses.Draft)
        {
            throw new InvalidOperationException("so_header_locked_by_approval: Cannot overwrite approved sales order header directly; submit an amendment revision.");
        }

        GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId));
        if (requestedDeliveryDate is not null && requestedDeliveryDate.Value < orderDate)
        {
            throw new ArgumentException("RequestedDeliveryDate cannot precede OrderDate.", nameof(requestedDeliveryDate));
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");
        }

        CustomerPartnerId = customerPartnerId;
        OrderDate = orderDate;
        RequestedDeliveryDate = requestedDeliveryDate;
        DeliveryAddressSnapshot = GuardValidation.RequireName(deliveryAddressSnapshot, nameof(deliveryAddressSnapshot), 240);
        PaymentTermsDays = paymentTermsDays;
        CustomerReference = GuardValidation.NormalizeOptional(customerReference, 80, nameof(customerReference));
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkSubmitted(DateTimeOffset nowUtc)
    {
        if (Status is SalesOrderStatuses.Closed or SalesOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"so_invalid_status_transition: Cannot submit sales order in status '{Status}'.");
        }

        if (CurrentApprovedRevisionId is null)
        {
            Status = SalesOrderStatuses.PendingApproval;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public int AllocateNextRevisionNumber(DateTimeOffset nowUtc)
    {
        var canRevise = (Status == SalesOrderStatuses.Approved && CurrentApprovedRevisionId is not null)
            || (Status == SalesOrderStatuses.Draft && CurrentApprovedRevisionId is null);

        if (!canRevise)
        {
            throw new InvalidOperationException(
                $"so_invalid_status_transition: Amendments can only be created from an APPROVED sales order or a DRAFT order recovering from rejection (current: '{Status}').");
        }

        LatestRevisionNumber++;
        UpdatedAtUtc = nowUtc;
        Version++;
        return LatestRevisionNumber;
    }

    public void ApplyRevisionDecision(
        Guid revisionId,
        int revisionNumber,
        bool approved,
        string? assignedSoNumber,
        Guid approvedCustomerPartnerId,
        DateOnly approvedOrderDate,
        DateOnly? approvedRequestedDeliveryDate,
        string approvedDeliveryAddressSnapshot,
        int approvedPaymentTermsDays,
        string? approvedCustomerReference,
        DateTimeOffset nowUtc)
    {
        if (Status is SalesOrderStatuses.Closed or SalesOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"so_invalid_status_transition: Cannot decide revision for sales order in status '{Status}'.");
        }

        if (approved)
        {
            if (string.IsNullOrWhiteSpace(SoNumber))
            {
                SoNumber = GuardValidation.RequireCode(assignedSoNumber, nameof(assignedSoNumber), 40);
            }

            Status = SalesOrderStatuses.Approved;
            CurrentApprovedRevisionId = revisionId;
            CurrentApprovedRevisionNumber = revisionNumber;
            CustomerPartnerId = approvedCustomerPartnerId;
            OrderDate = approvedOrderDate;
            RequestedDeliveryDate = approvedRequestedDeliveryDate;
            DeliveryAddressSnapshot = approvedDeliveryAddressSnapshot;
            PaymentTermsDays = approvedPaymentTermsDays;
            CustomerReference = approvedCustomerReference;
        }
        else if (CurrentApprovedRevisionId is null)
        {
            Status = SalesOrderStatuses.Draft;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Cancel(Guid actorUserId, string reason, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (TotalReservedBaseQuantity > 0m || TotalDispatchedBaseQuantity > 0m || TotalBilledBaseQuantity > 0m)
        {
            throw new InvalidOperationException(
                "so_has_downstream_fulfillment: Cannot cancel a sales order with active inventory reservations, dispatches, or billing references.");
        }

        if (Status is SalesOrderStatuses.Closed or SalesOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"so_invalid_status_transition: Cannot cancel sales order in status '{Status}'.");
        }

        Status = SalesOrderStatuses.Cancelled;
        CancelledByUserId = actorUserId;
        CancelledAtUtc = nowUtc;
        CancellationReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Close(Guid actorUserId, string reason, bool hasOpenRevision, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != SalesOrderStatuses.Approved)
        {
            throw new InvalidOperationException(
                $"so_invalid_status_transition: Only APPROVED sales orders can be closed (current: '{Status}').");
        }

        if (hasOpenRevision)
        {
            throw new InvalidOperationException(
                "so_has_open_revision_before_close: Approve, reject, or cancel open draft/pending revisions before closing the sales order.");
        }

        Status = SalesOrderStatuses.Closed;
        ClosedByUserId = actorUserId;
        ClosedAtUtc = nowUtc;
        ClosureReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyFulfillmentCounters(decimal reservedBaseQty, decimal dispatchedBaseQty, decimal billedBaseQty, DateTimeOffset nowUtc)
    {
        if (reservedBaseQty < 0m || dispatchedBaseQty < 0m || billedBaseQty < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedBaseQty), "Fulfillment counters cannot be negative.");
        }

        TotalReservedBaseQuantity = reservedBaseQty;
        TotalDispatchedBaseQuantity = dispatchedBaseQty;
        TotalBilledBaseQuantity = billedBaseQty;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class SalesOrderRevision
{
    public Guid RevisionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid? SupersedesRevisionId { get; private set; }
    public string Status { get; private set; } = SalesOrderRevisionStatuses.Draft;
    public bool IsFrozen { get; private set; }
    public Guid CustomerPartnerId { get; private set; }
    public string CustomerCodeSnapshot { get; private set; } = string.Empty;
    public string CustomerLegalNameSnapshot { get; private set; } = string.Empty;
    public string? CustomerNpwpSnapshot { get; private set; }
    public string CustomerPkpStatusSnapshot { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "IDR";
    public DateOnly OrderDate { get; private set; }
    public DateOnly? RequestedDeliveryDate { get; private set; }
    public string DeliveryAddressSnapshot { get; private set; } = string.Empty;
    public int PaymentTermsDays { get; private set; }
    public string? CustomerReference { get; private set; }
    public string? ChangeReason { get; private set; }
    public string? Notes { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal OutputTaxTotalAmount { get; private set; }
    public decimal GrandTotalAmount { get; private set; }
    public decimal TotalOrderedBaseQuantity { get; private set; }
    public string SnapshotHash { get; private set; } = "DRAFT";
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public Guid? ApprovalRecordId { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SalesOrderRevision()
    {
    }

    public static SalesOrderRevision CreateDraft(
        Guid revisionId,
        Guid tenantId,
        Guid legalEntityId,
        Guid salesOrderId,
        int revisionNumber,
        Guid? supersedesRevisionId,
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerLegalNameSnapshot,
        string? customerNpwpSnapshot,
        string customerPkpStatusSnapshot,
        string currencyCode,
        DateOnly orderDate,
        DateOnly? requestedDeliveryDate,
        string deliveryAddressSnapshot,
        int paymentTermsDays,
        string? customerReference,
        string? changeReason,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "RevisionNumber must be >= 1.");
        }

        return new SalesOrderRevision
        {
            RevisionId = GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            SalesOrderId = GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId)),
            RevisionNumber = revisionNumber,
            SupersedesRevisionId = supersedesRevisionId,
            Status = SalesOrderRevisionStatuses.Draft,
            IsFrozen = false,
            CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId)),
            CustomerCodeSnapshot = GuardValidation.RequireName(customerCodeSnapshot, nameof(customerCodeSnapshot), 32),
            CustomerLegalNameSnapshot = GuardValidation.RequireName(customerLegalNameSnapshot, nameof(customerLegalNameSnapshot), 200),
            CustomerNpwpSnapshot = GuardValidation.NormalizeOptional(customerNpwpSnapshot, 64, nameof(customerNpwpSnapshot)),
            CustomerPkpStatusSnapshot = GuardValidation.RequireCode(customerPkpStatusSnapshot, nameof(customerPkpStatusSnapshot), 16),
            CurrencyCode = GuardValidation.RequireCode(currencyCode, nameof(currencyCode), 3),
            OrderDate = orderDate,
            RequestedDeliveryDate = requestedDeliveryDate,
            DeliveryAddressSnapshot = GuardValidation.RequireName(deliveryAddressSnapshot, nameof(deliveryAddressSnapshot), 240),
            PaymentTermsDays = paymentTermsDays,
            CustomerReference = GuardValidation.NormalizeOptional(customerReference, 80, nameof(customerReference)),
            ChangeReason = GuardValidation.NormalizeOptional(changeReason, 300, nameof(changeReason)),
            Notes = GuardValidation.NormalizeOptional(notes, 500, nameof(notes)),
            SubtotalAmount = 0m,
            OutputTaxTotalAmount = 0m,
            GrandTotalAmount = 0m,
            TotalOrderedBaseQuantity = 0m,
            SnapshotHash = "DRAFT",
            CreatedByUserId = GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDraftDetailsAndTotals(
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerLegalNameSnapshot,
        string? customerNpwpSnapshot,
        string customerPkpStatusSnapshot,
        DateOnly orderDate,
        DateOnly? requestedDeliveryDate,
        string deliveryAddressSnapshot,
        int paymentTermsDays,
        string? customerReference,
        string? changeReason,
        string? notes,
        decimal subtotalAmount,
        decimal outputTaxTotalAmount,
        decimal grandTotalAmount,
        decimal totalOrderedBaseQuantity,
        IEnumerable<SalesOrderLine> lines,
        DateTimeOffset nowUtc)
    {
        EnsureMutableDraft();
        if (requestedDeliveryDate is not null && requestedDeliveryDate.Value < orderDate)
        {
            throw new ArgumentException("RequestedDeliveryDate cannot precede OrderDate.", nameof(requestedDeliveryDate));
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");
        }

        CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId));
        CustomerCodeSnapshot = GuardValidation.RequireName(customerCodeSnapshot, nameof(customerCodeSnapshot), 32);
        CustomerLegalNameSnapshot = GuardValidation.RequireName(customerLegalNameSnapshot, nameof(customerLegalNameSnapshot), 200);
        CustomerNpwpSnapshot = GuardValidation.NormalizeOptional(customerNpwpSnapshot, 64, nameof(customerNpwpSnapshot));
        CustomerPkpStatusSnapshot = GuardValidation.RequireCode(customerPkpStatusSnapshot, nameof(customerPkpStatusSnapshot), 16);
        OrderDate = orderDate;
        RequestedDeliveryDate = requestedDeliveryDate;
        DeliveryAddressSnapshot = GuardValidation.RequireName(deliveryAddressSnapshot, nameof(deliveryAddressSnapshot), 240);
        PaymentTermsDays = paymentTermsDays;
        CustomerReference = GuardValidation.NormalizeOptional(customerReference, 80, nameof(customerReference));
        ChangeReason = GuardValidation.NormalizeOptional(changeReason, 300, nameof(changeReason));
        Notes = GuardValidation.NormalizeOptional(notes, 500, nameof(notes));
        SubtotalAmount = subtotalAmount;
        OutputTaxTotalAmount = outputTaxTotalAmount;
        GrandTotalAmount = grandTotalAmount;
        TotalOrderedBaseQuantity = totalOrderedBaseQuantity;
        SnapshotHash = SalesOrderHash.ComputeRevisionHash(this, lines);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyTotalsAndHash(
        decimal subtotalAmount,
        decimal outputTaxTotalAmount,
        decimal grandTotalAmount,
        decimal totalOrderedBaseQuantity,
        IEnumerable<SalesOrderLine> lines,
        DateTimeOffset nowUtc)
    {
        EnsureMutableDraft();
        SubtotalAmount = subtotalAmount;
        OutputTaxTotalAmount = outputTaxTotalAmount;
        GrandTotalAmount = grandTotalAmount;
        TotalOrderedBaseQuantity = totalOrderedBaseQuantity;
        SnapshotHash = SalesOrderHash.ComputeRevisionHash(this, lines);
        UpdatedAtUtc = nowUtc;
    }

    public void FreezeAndMarkSubmitted(
        Guid submittedByUserId,
        Guid approvalRecordId,
        string snapshotHash,
        DateTimeOffset nowUtc)
    {
        EnsureMutableDraft();
        GuardValidation.RequireNonEmptyGuid(submittedByUserId, nameof(submittedByUserId));
        GuardValidation.RequireNonEmptyGuid(approvalRecordId, nameof(approvalRecordId));

        if (SubtotalAmount <= 0m || TotalOrderedBaseQuantity <= 0m)
        {
            throw new InvalidOperationException("so_empty_lines: Sales order revision must contain at least one positive commercial line.");
        }

        IsFrozen = true;
        Status = SalesOrderRevisionStatuses.PendingApproval;
        SubmittedByUserId = submittedByUserId;
        SubmittedAtUtc = nowUtc;
        ApprovalRecordId = approvalRecordId;
        SnapshotHash = GuardValidation.RequireName(snapshotHash, nameof(snapshotHash), 64);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyDecision(Guid decidedByUserId, bool approved, string reason, DateTimeOffset nowUtc)
    {
        if (Status != SalesOrderRevisionStatuses.PendingApproval || !IsFrozen)
        {
            throw new InvalidOperationException($"so_revision_not_pending: Revision {RevisionNumber} is '{Status}' and cannot be decided.");
        }

        GuardValidation.RequireNonEmptyGuid(decidedByUserId, nameof(decidedByUserId));
        if (SubmittedByUserId == decidedByUserId)
        {
            throw new InvalidOperationException("four_eyes_self_approval_forbidden: Submitter cannot decide their own sales order revision.");
        }

        Status = approved ? SalesOrderRevisionStatuses.Approved : SalesOrderRevisionStatuses.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = nowUtc;
        DecisionReason = GuardValidation.RequireName(reason, nameof(reason), 500);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkSuperseded(DateTimeOffset nowUtc)
    {
        if (Status != SalesOrderRevisionStatuses.Approved)
        {
            throw new InvalidOperationException($"so_invalid_status_transition: Only APPROVED revisions can be superseded (current: '{Status}').");
        }

        Status = SalesOrderRevisionStatuses.Superseded;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        if (Status is not (SalesOrderRevisionStatuses.Draft or SalesOrderRevisionStatuses.PendingApproval))
        {
            throw new InvalidOperationException($"so_invalid_status_transition: Cannot cancel revision in status '{Status}'.");
        }

        Status = SalesOrderRevisionStatuses.Cancelled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    private void EnsureMutableDraft()
    {
        if (IsFrozen || Status != SalesOrderRevisionStatuses.Draft)
        {
            throw new InvalidOperationException($"so_revision_frozen_immutable: Revision {RevisionNumber} ({Status}) is frozen and cannot be modified.");
        }
    }
}

public sealed class SalesOrderLine
{
    public Guid SoLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid RevisionId { get; private set; }
    public int LineNumber { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;
    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public string ItemKindSnapshot { get; private set; } = string.Empty;
    public Guid OrderedUomId { get; private set; }
    public string OrderedUomCodeSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal UomFactorToBaseSnapshot { get; private set; }
    public decimal OrderedQuantity { get; private set; }
    public decimal OrderedBaseQuantity { get; private set; }
    public decimal ReservedBaseQuantity { get; private set; }
    public decimal DispatchedBaseQuantity { get; private set; }
    public decimal BilledBaseQuantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal LineSubtotalAmount { get; private set; }
    public Guid? TaxCodeId { get; private set; }
    public string? TaxCodeSnapshot { get; private set; }
    public string? TaxTreatmentClassSnapshot { get; private set; }
    public decimal? TaxRatePercentSnapshot { get; private set; }
    public decimal TaxableBaseAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal LineTotalAmount { get; private set; }
    public DateOnly? RequestedDeliveryDate { get; private set; }
    public string? LineDescription { get; private set; }

    private SalesOrderLine()
    {
    }

    public static SalesOrderLine Create(
        Guid soLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid salesOrderId,
        Guid revisionId,
        int lineNumber,
        string lineageLineKey,
        Guid itemId,
        string itemCodeSnapshot,
        string itemNameSnapshot,
        string itemKindSnapshot,
        Guid orderedUomId,
        string orderedUomCodeSnapshot,
        Guid baseUomId,
        string baseUomCodeSnapshot,
        decimal uomFactorToBaseSnapshot,
        decimal orderedQuantity,
        decimal orderedBaseQuantity,
        decimal unitPrice,
        decimal discountAmount,
        Guid? taxCodeId,
        string? taxCodeSnapshot,
        string? taxTreatmentClassSnapshot,
        decimal? taxRatePercentSnapshot,
        decimal taxableBaseAmount,
        decimal taxAmount,
        DateOnly? requestedDeliveryDate,
        string? lineDescription)
    {
        if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        if (orderedQuantity <= 0m || orderedBaseQuantity <= 0m || unitPrice <= 0m)
        {
            throw new InvalidOperationException("invalid_quantity_or_price: Quantity and unit price must be strictly positive.");
        }

        if (discountAmount != 0m)
        {
            throw new InvalidOperationException(
                "od09_sales_discount_policy_unresolved: OD-09 safe default forbids unapproved sales discounts until Owner sign-off (ATH-CR-009).");
        }

        var roundedQty = decimal.Round(orderedQuantity, 6, MidpointRounding.AwayFromZero);
        var roundedPrice = decimal.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        var lineSubtotal = decimal.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);
        if (lineSubtotal <= 0m)
        {
            throw new InvalidOperationException("invalid_quantity_or_price: Line subtotal must be positive.");
        }

        var roundedTax = decimal.Round(taxAmount, 4, MidpointRounding.AwayFromZero);

        return new SalesOrderLine
        {
            SoLineId = GuardValidation.RequireNonEmptyGuid(soLineId, nameof(soLineId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            SalesOrderId = GuardValidation.RequireNonEmptyGuid(salesOrderId, nameof(salesOrderId)),
            RevisionId = GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId)),
            LineNumber = lineNumber,
            LineageLineKey = GuardValidation.RequireCode(lineageLineKey, nameof(lineageLineKey), 48),
            ItemId = GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId)),
            ItemCodeSnapshot = GuardValidation.RequireName(itemCodeSnapshot, nameof(itemCodeSnapshot), 32),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 200),
            ItemKindSnapshot = GuardValidation.RequireCode(itemKindSnapshot, nameof(itemKindSnapshot), 32),
            OrderedUomId = GuardValidation.RequireNonEmptyGuid(orderedUomId, nameof(orderedUomId)),
            OrderedUomCodeSnapshot = GuardValidation.RequireCode(orderedUomCodeSnapshot, nameof(orderedUomCodeSnapshot), 16),
            BaseUomId = GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId)),
            BaseUomCodeSnapshot = GuardValidation.RequireCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 16),
            UomFactorToBaseSnapshot = decimal.Round(uomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero),
            OrderedQuantity = roundedQty,
            OrderedBaseQuantity = decimal.Round(orderedBaseQuantity, 6, MidpointRounding.AwayFromZero),
            ReservedBaseQuantity = 0m,
            DispatchedBaseQuantity = 0m,
            BilledBaseQuantity = 0m,
            UnitPrice = roundedPrice,
            DiscountAmount = 0m,
            LineSubtotalAmount = lineSubtotal,
            TaxCodeId = taxCodeId,
            TaxCodeSnapshot = GuardValidation.NormalizeOptional(taxCodeSnapshot, 32, nameof(taxCodeSnapshot)),
            TaxTreatmentClassSnapshot = GuardValidation.NormalizeOptional(taxTreatmentClassSnapshot, 32, nameof(taxTreatmentClassSnapshot)),
            TaxRatePercentSnapshot = taxRatePercentSnapshot.HasValue ? decimal.Round(taxRatePercentSnapshot.Value, 4, MidpointRounding.AwayFromZero) : null,
            TaxableBaseAmount = decimal.Round(taxableBaseAmount, 4, MidpointRounding.AwayFromZero),
            TaxAmount = roundedTax,
            LineTotalAmount = lineSubtotal + roundedTax,
            RequestedDeliveryDate = requestedDeliveryDate,
            LineDescription = GuardValidation.NormalizeOptional(lineDescription, 240, nameof(lineDescription))
        };
    }

    public void CarryForwardFulfillmentCounters(decimal reservedBaseQty, decimal dispatchedBaseQty, decimal billedBaseQty)
    {
        ReservedBaseQuantity = decimal.Round(reservedBaseQty, 6, MidpointRounding.AwayFromZero);
        DispatchedBaseQuantity = decimal.Round(dispatchedBaseQty, 6, MidpointRounding.AwayFromZero);
        BilledBaseQuantity = decimal.Round(billedBaseQty, 6, MidpointRounding.AwayFromZero);
    }

    public void ApplyFulfillmentDelta(decimal reservedDelta, decimal dispatchedDelta, decimal billedDelta)
    {
        var nextReserved = decimal.Round(ReservedBaseQuantity + reservedDelta, 6, MidpointRounding.AwayFromZero);
        var nextDispatched = decimal.Round(DispatchedBaseQuantity + dispatchedDelta, 6, MidpointRounding.AwayFromZero);
        var nextBilled = decimal.Round(BilledBaseQuantity + billedDelta, 6, MidpointRounding.AwayFromZero);

        if (nextReserved < 0m || nextDispatched < 0m || nextBilled < 0m)
        {
            throw new InvalidOperationException("fulfillment_counter_negative: Sales order line fulfillment counters cannot become negative.");
        }

        if (decimal.Round(nextReserved + nextDispatched, 6, MidpointRounding.AwayFromZero) > OrderedBaseQuantity)
        {
            throw new InvalidOperationException(
                $"fulfillment_exceeds_ordered_quantity: Total reserved ({nextReserved:0.######}) + dispatched ({nextDispatched:0.######}) exceeds ordered base quantity ({OrderedBaseQuantity:0.######}).");
        }

        ReservedBaseQuantity = nextReserved;
        DispatchedBaseQuantity = nextDispatched;
        BilledBaseQuantity = nextBilled;
    }
}

public static class SalesOrderHash
{
    public static string ComputeRevisionHash(SalesOrderRevision rev, IEnumerable<SalesOrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(rev);
        ArgumentNullException.ThrowIfNull(lines);

        var sb = new StringBuilder();
        sb.Append(rev.SalesOrderId.ToString("N")).Append('|')
          .Append(rev.RevisionNumber.ToString(CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.CustomerCodeSnapshot).Append('|')
          .Append(rev.CustomerLegalNameSnapshot).Append('|')
          .Append(rev.CurrencyCode).Append('|')
          .Append(rev.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.RequestedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "NONE").Append('|')
          .Append(rev.DeliveryAddressSnapshot).Append('|')
          .Append(rev.PaymentTermsDays.ToString(CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.CustomerReference ?? string.Empty).Append('|')
          .Append(rev.SubtotalAmount.ToString("0.0000", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.OutputTaxTotalAmount.ToString("0.0000", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.GrandTotalAmount.ToString("0.0000", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.TotalOrderedBaseQuantity.ToString("0.000000", CultureInfo.InvariantCulture));

        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            sb.Append("\nL").Append(line.LineNumber.ToString(CultureInfo.InvariantCulture))
              .Append(':').Append(line.LineageLineKey)
              .Append(':').Append(line.ItemCodeSnapshot)
              .Append(':').Append(line.OrderedUomCodeSnapshot)
              .Append(':').Append(line.OrderedQuantity.ToString("0.000000", CultureInfo.InvariantCulture))
              .Append(':').Append(line.OrderedBaseQuantity.ToString("0.000000", CultureInfo.InvariantCulture))
              .Append(':').Append(line.UnitPrice.ToString("0.0000", CultureInfo.InvariantCulture))
              .Append(':').Append(line.DiscountAmount.ToString("0.0000", CultureInfo.InvariantCulture))
              .Append(':').Append(line.TaxCodeSnapshot ?? "NONE")
              .Append(':').Append(line.TaxAmount.ToString("0.0000", CultureInfo.InvariantCulture))
              .Append(':').Append(line.LineTotalAmount.ToString("0.0000", CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}

public static class SoDiffChangeKinds
{
    public const string Added = "ADDED";
    public const string Removed = "REMOVED";
    public const string Modified = "MODIFIED";
    public const string Unchanged = "UNCHANGED";
}

public sealed record SoHeaderFieldDiff(string Field, string? PreviousValue, string? CurrentValue);

public sealed record SoLineDiffItem(
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

public sealed record SoRevisionDiffReport(
    int TargetRevisionNumber,
    int? BaselineRevisionNumber,
    IReadOnlyList<SoHeaderFieldDiff> HeaderChanges,
    IReadOnlyList<SoLineDiffItem> LineChanges,
    decimal? PreviousGrandTotal,
    decimal CurrentGrandTotal,
    decimal GrandTotalDelta,
    decimal PreviousOrderedBaseQty,
    decimal CurrentOrderedBaseQty,
    decimal OrderedBaseQtyDelta);

public static class SalesOrderDiffCalculator
{
    public static SoRevisionDiffReport Compute(
        SalesOrderRevision target,
        IReadOnlyList<SalesOrderLine> targetLines,
        SalesOrderRevision? baseline,
        IReadOnlyList<SalesOrderLine>? baselineLines)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetLines);

        if (baseline is null)
        {
            var initialLines = targetLines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SoLineDiffItem(
                    l.LineageLineKey,
                    SoDiffChangeKinds.Added,
                    null, l.ItemCodeSnapshot,
                    null, l.OrderedQuantity,
                    null, l.OrderedUomCodeSnapshot,
                    null, l.UnitPrice,
                    null, l.LineSubtotalAmount))
                .ToList();

            return new SoRevisionDiffReport(
                target.RevisionNumber,
                null,
                Array.Empty<SoHeaderFieldDiff>(),
                initialLines,
                null,
                target.GrandTotalAmount,
                target.GrandTotalAmount,
                0m,
                target.TotalOrderedBaseQuantity,
                target.TotalOrderedBaseQuantity);
        }

        var headerChanges = new List<SoHeaderFieldDiff>();
        AddIfChanged(headerChanges, "customer", baseline.CustomerCodeSnapshot, target.CustomerCodeSnapshot);
        AddIfChanged(headerChanges, "orderDate", baseline.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), target.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfChanged(headerChanges, "requestedDeliveryDate", baseline.RequestedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), target.RequestedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfChanged(headerChanges, "deliveryAddress", baseline.DeliveryAddressSnapshot, target.DeliveryAddressSnapshot);
        AddIfChanged(headerChanges, "paymentTermsDays", baseline.PaymentTermsDays.ToString(CultureInfo.InvariantCulture), target.PaymentTermsDays.ToString(CultureInfo.InvariantCulture));
        AddIfChanged(headerChanges, "customerReference", baseline.CustomerReference, target.CustomerReference);

        var prevByKey = (baselineLines ?? Array.Empty<SalesOrderLine>()).ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);
        var currByKey = targetLines.ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);

        var orderedKeys = targetLines
            .OrderBy(l => l.LineNumber)
            .Select(l => l.LineageLineKey)
            .Concat((baselineLines ?? Array.Empty<SalesOrderLine>()).OrderBy(l => l.LineNumber).Select(l => l.LineageLineKey))
            .Distinct(StringComparer.Ordinal);

        var lineDiffs = new List<SoLineDiffItem>();
        foreach (var key in orderedKeys)
        {
            prevByKey.TryGetValue(key, out var prev);
            currByKey.TryGetValue(key, out var curr);

            if (prev is null && curr is not null)
            {
                lineDiffs.Add(new SoLineDiffItem(
                    key, SoDiffChangeKinds.Added,
                    null, curr.ItemCodeSnapshot,
                    null, curr.OrderedQuantity,
                    null, curr.OrderedUomCodeSnapshot,
                    null, curr.UnitPrice,
                    null, curr.LineSubtotalAmount));
            }
            else if (prev is not null && curr is null)
            {
                lineDiffs.Add(new SoLineDiffItem(
                    key, SoDiffChangeKinds.Removed,
                    prev.ItemCodeSnapshot, null,
                    prev.OrderedQuantity, null,
                    prev.OrderedUomCodeSnapshot, null,
                    prev.UnitPrice, null,
                    prev.LineSubtotalAmount, null));
            }
            else if (prev is not null && curr is not null)
            {
                var changed = prev.ItemId != curr.ItemId
                    || prev.OrderedUomId != curr.OrderedUomId
                    || prev.OrderedQuantity != curr.OrderedQuantity
                    || prev.UnitPrice != curr.UnitPrice
                    || prev.DiscountAmount != curr.DiscountAmount
                    || prev.TaxCodeId != curr.TaxCodeId
                    || !string.Equals(prev.LineDescription, curr.LineDescription, StringComparison.Ordinal);

                lineDiffs.Add(new SoLineDiffItem(
                    key,
                    changed ? SoDiffChangeKinds.Modified : SoDiffChangeKinds.Unchanged,
                    prev.ItemCodeSnapshot, curr.ItemCodeSnapshot,
                    prev.OrderedQuantity, curr.OrderedQuantity,
                    prev.OrderedUomCodeSnapshot, curr.OrderedUomCodeSnapshot,
                    prev.UnitPrice, curr.UnitPrice,
                    prev.LineSubtotalAmount, curr.LineSubtotalAmount));
            }
        }

        return new SoRevisionDiffReport(
            target.RevisionNumber,
            baseline.RevisionNumber,
            headerChanges,
            lineDiffs,
            baseline.GrandTotalAmount,
            target.GrandTotalAmount,
            target.GrandTotalAmount - baseline.GrandTotalAmount,
            baseline.TotalOrderedBaseQuantity,
            target.TotalOrderedBaseQuantity,
            target.TotalOrderedBaseQuantity - baseline.TotalOrderedBaseQuantity);
    }

    private static void AddIfChanged(List<SoHeaderFieldDiff> list, string key, string? previous, string? current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            list.Add(new SoHeaderFieldDiff(key, previous, current));
        }
    }
}
