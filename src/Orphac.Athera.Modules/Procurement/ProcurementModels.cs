using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Procurement;

public static class PurchaseOrderStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, PendingApproval, Approved, Closed, Cancelled];
}

public static class PurchaseOrderRevisionStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, PendingApproval, Approved, Rejected, Superseded, Cancelled];
}

public static class ReceivingProgressStatuses
{
    public const string NotApplicable = "NOT_APPLICABLE";
    public const string NotReceived = "NOT_RECEIVED";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string FullyReceived = "FULLY_RECEIVED";
    public const string ShortClosed = "SHORT_CLOSED";

    public static string Derive(
        string poStatus,
        bool hasApprovedRevision,
        decimal approvedOrderedBaseQty,
        decimal acceptedBaseQty)
    {
        if (!hasApprovedRevision || poStatus == PurchaseOrderStatuses.Cancelled)
        {
            return NotApplicable;
        }

        if (poStatus == PurchaseOrderStatuses.Closed)
        {
            return approvedOrderedBaseQty > 0m && acceptedBaseQty >= approvedOrderedBaseQty
                ? FullyReceived
                : ShortClosed;
        }

        if (acceptedBaseQty <= 0m)
        {
            return NotReceived;
        }

        if (acceptedBaseQty < approvedOrderedBaseQty)
        {
            return PartiallyReceived;
        }

        return FullyReceived;
    }
}

public static class DeliveryToleranceModes
{
    public const string ExactApprovedQtyOnly = "EXACT_APPROVED_QTY_ONLY";
    public const string UnreviewedSafeDefault = "UNREVIEWED_SAFE_DEFAULT";
}

public sealed class PoSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private PoSequence()
    {
    }
}

/// <summary>
/// Legal-entity overdelivery tolerance policy governed by OD-01.
/// Safe default until PO approval allows exact approved quantity only (0%, 0 qty, no unlimited flag).
/// </summary>
public sealed class DeliveryTolerancePolicy
{
    public Guid PolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Mode { get; private set; } = DeliveryToleranceModes.ExactApprovedQtyOnly;
    public decimal OverdeliveryTolerancePercent { get; private set; }
    public decimal OverdeliveryToleranceBaseQty { get; private set; }
    public bool AllowUnlimitedOverdelivery { get; private set; }
    public string ReviewStatus { get; private set; } = DeliveryToleranceModes.UnreviewedSafeDefault;
    public bool IsDefault { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private DeliveryTolerancePolicy()
    {
    }

    public static void EnsureSafeToleranceValues(
        decimal overdeliveryTolerancePercent,
        decimal overdeliveryToleranceBaseQty,
        bool allowUnlimitedOverdelivery)
    {
        if (overdeliveryTolerancePercent < 0m || overdeliveryToleranceBaseQty < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overdeliveryTolerancePercent),
                "Tolerance values cannot be negative.");
        }

        if (overdeliveryTolerancePercent != 0m || overdeliveryToleranceBaseQty != 0m || allowUnlimitedOverdelivery)
        {
            throw new InvalidOperationException(
                "od01_overdelivery_tolerance_blocked: OD-01 requires Product Owner sign-off before enabling non-zero overdelivery tolerance or unlimited delivery flags (ATH-CR-003).");
        }
    }

    public static DeliveryTolerancePolicy CreateSafeDefault(
        Guid policyId,
        Guid tenantId,
        Guid legalEntityId,
        string code,
        string name,
        decimal overdeliveryTolerancePercent,
        decimal overdeliveryToleranceBaseQty,
        bool allowUnlimitedOverdelivery,
        bool isDefault,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(policyId, nameof(policyId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        EnsureSafeToleranceValues(overdeliveryTolerancePercent, overdeliveryToleranceBaseQty, allowUnlimitedOverdelivery);

        return new DeliveryTolerancePolicy
        {
            PolicyId = policyId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = GuardValidation.RequireMasterCode(code, nameof(code), 2, 48),
            Name = GuardValidation.RequireName(name, nameof(name), 160),
            Mode = DeliveryToleranceModes.ExactApprovedQtyOnly,
            OverdeliveryTolerancePercent = 0m,
            OverdeliveryToleranceBaseQty = 0m,
            AllowUnlimitedOverdelivery = false,
            ReviewStatus = DeliveryToleranceModes.UnreviewedSafeDefault,
            IsDefault = isDefault,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

public sealed class PurchaseOrder
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DraftCode { get; private set; } = string.Empty;
    public string? PoNumber { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDeliveryDate { get; private set; }
    public string DeliveryDestination { get; private set; } = string.Empty;
    public int PaymentTermsDays { get; private set; }
    public string Status { get; private set; } = PurchaseOrderStatuses.Draft;
    public Guid? CurrentApprovedRevisionId { get; private set; }
    public int CurrentApprovedRevisionNumber { get; private set; }
    public int LatestRevisionNumber { get; private set; } = 1;
    public int DownstreamReferenceCount { get; private set; }
    public decimal AcceptedBaseQuantityTotal { get; private set; }
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

    private PurchaseOrder()
    {
    }

    public static PurchaseOrder CreateDraft(
        Guid purchaseOrderId,
        Guid tenantId,
        Guid legalEntityId,
        string draftCode,
        Guid supplierPartnerId,
        string currencyCode,
        string expectedFunctionalCurrencyCode,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string deliveryDestination,
        int paymentTermsDays,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var normalizedCurrency = GuardValidation.RequireExactIsoCode(currencyCode, expectedFunctionalCurrencyCode, 3, nameof(currencyCode));
        var normalizedExpected = GuardValidation.RequireExactIsoCode(expectedFunctionalCurrencyCode, "IDR", 3, nameof(expectedFunctionalCurrencyCode));
        if (!string.Equals(normalizedCurrency, normalizedExpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"unsupported_currency_mvp: PO currency must match legal entity currency '{normalizedExpected}'.");
        }

        if (expectedDeliveryDate is not null && expectedDeliveryDate.Value < orderDate)
        {
            throw new ArgumentException("ExpectedDeliveryDate cannot precede OrderDate.", nameof(expectedDeliveryDate));
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");
        }

        return new PurchaseOrder
        {
            PurchaseOrderId = purchaseOrderId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            DraftCode = GuardValidation.RequireCode(draftCode, nameof(draftCode), 40),
            PoNumber = null,
            SupplierPartnerId = supplierPartnerId,
            CurrencyCode = normalizedCurrency,
            OrderDate = orderDate,
            ExpectedDeliveryDate = expectedDeliveryDate,
            DeliveryDestination = GuardValidation.RequireName(deliveryDestination, nameof(deliveryDestination), 200),
            PaymentTermsDays = paymentTermsDays,
            Status = PurchaseOrderStatuses.Draft,
            CurrentApprovedRevisionId = null,
            CurrentApprovedRevisionNumber = 0,
            LatestRevisionNumber = 1,
            DownstreamReferenceCount = 0,
            AcceptedBaseQuantityTotal = 0m,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdatePreApprovalHeader(
        Guid supplierPartnerId,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string deliveryDestination,
        int paymentTermsDays,
        DateTimeOffset nowUtc)
    {
        if (CurrentApprovedRevisionId is not null || Status != PurchaseOrderStatuses.Draft)
        {
            throw new InvalidOperationException("po_header_locked_by_approval: Cannot overwrite approved PO header directly; submit an amendment revision.");
        }

        GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        if (expectedDeliveryDate is not null && expectedDeliveryDate.Value < orderDate)
        {
            throw new ArgumentException("ExpectedDeliveryDate cannot precede OrderDate.", nameof(expectedDeliveryDate));
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");
        }

        SupplierPartnerId = supplierPartnerId;
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        DeliveryDestination = GuardValidation.RequireName(deliveryDestination, nameof(deliveryDestination), 200);
        PaymentTermsDays = paymentTermsDays;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkSubmitted(string? assignedPoNumber, DateTimeOffset nowUtc)
    {
        if (Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Cannot submit PO in status '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(PoNumber))
        {
            PoNumber = GuardValidation.RequireCode(assignedPoNumber, nameof(assignedPoNumber), 40);
        }

        if (CurrentApprovedRevisionId is null)
        {
            Status = PurchaseOrderStatuses.PendingApproval;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public int AllocateNextRevisionNumber(DateTimeOffset nowUtc)
    {
        var canRevise = (Status == PurchaseOrderStatuses.Approved && CurrentApprovedRevisionId is not null)
            || (Status == PurchaseOrderStatuses.Draft && CurrentApprovedRevisionId is null);

        if (!canRevise)
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Revisions can only be created from an APPROVED PO or a DRAFT PO recovering from rejection (current: '{Status}').");
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
        Guid approvedSupplierPartnerId,
        DateOnly approvedOrderDate,
        DateOnly? approvedExpectedDeliveryDate,
        string approvedDeliveryDestination,
        int approvedPaymentTermsDays,
        DateTimeOffset nowUtc)
    {
        if (Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Cannot decide revision for PO in status '{Status}'.");
        }

        if (approved)
        {
            Status = PurchaseOrderStatuses.Approved;
            CurrentApprovedRevisionId = revisionId;
            CurrentApprovedRevisionNumber = revisionNumber;
            SupplierPartnerId = approvedSupplierPartnerId;
            OrderDate = approvedOrderDate;
            ExpectedDeliveryDate = approvedExpectedDeliveryDate;
            DeliveryDestination = approvedDeliveryDestination;
            PaymentTermsDays = approvedPaymentTermsDays;
        }
        else if (CurrentApprovedRevisionId is null)
        {
            Status = PurchaseOrderStatuses.Draft;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RevertPendingFirstSubmissionOnAllRejectedOrCancelled(DateTimeOffset nowUtc)
    {
        if (CurrentApprovedRevisionId is null && Status == PurchaseOrderStatuses.PendingApproval)
        {
            Status = PurchaseOrderStatuses.Draft;
            UpdatedAtUtc = nowUtc;
            Version++;
        }
    }

    public void Cancel(Guid actorUserId, string reason, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (DownstreamReferenceCount > 0 || AcceptedBaseQuantityTotal > 0m)
        {
            throw new InvalidOperationException(
                "po_has_downstream_references: Cannot cancel a purchase order with downstream receipts or invoice references; use authorized short-close instead.");
        }

        if (Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Cannot cancel PO in status '{Status}'.");
        }

        Status = PurchaseOrderStatuses.Cancelled;
        CancelledByUserId = actorUserId;
        CancelledAtUtc = nowUtc;
        CancellationReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ShortClose(Guid actorUserId, string reason, bool hasOpenRevision, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != PurchaseOrderStatuses.Approved)
        {
            throw new InvalidOperationException(
                $"po_invalid_status_transition: Only APPROVED purchase orders can be short-closed (current: '{Status}').");
        }

        if (hasOpenRevision)
        {
            throw new InvalidOperationException(
                "po_has_open_revision_before_close: Approve, reject, or discard open draft/pending revisions before short-closing the PO.");
        }

        Status = PurchaseOrderStatuses.Closed;
        ClosedByUserId = actorUserId;
        ClosedAtUtc = nowUtc;
        ClosureReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    /// <summary>
    /// Invoked by downstream S06-S09 workflows (or integration tests simulating receiving references)
    /// inside an entity transaction so approved PO deletion/cancellation stays blocked.
    /// </summary>
    public void RegisterDownstreamReference(decimal acceptedBaseDelta, DateTimeOffset nowUtc)
    {
        if (Status != PurchaseOrderStatuses.Approved)
        {
            throw new InvalidOperationException($"po_not_approved_for_downstream: PO '{PoNumber ?? DraftCode}' is in status '{Status}'.");
        }

        if (acceptedBaseDelta < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedBaseDelta), "AcceptedBaseDelta cannot be negative.");
        }

        DownstreamReferenceCount++;
        AcceptedBaseQuantityTotal += acceptedBaseDelta;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RecordAcceptedQuantity(decimal acceptedBaseDelta, DateTimeOffset nowUtc)
    {
        if (Status != PurchaseOrderStatuses.Approved)
        {
            throw new InvalidOperationException($"po_not_approved_for_downstream: PO '{PoNumber ?? DraftCode}' is in status '{Status}'.");
        }

        var delta = decimal.Round(acceptedBaseDelta, 6, MidpointRounding.AwayFromZero);
        if (delta <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedBaseDelta), "Accepted quantity delta must be positive.");
        }

        AcceptedBaseQuantityTotal = decimal.Round(AcceptedBaseQuantityTotal + delta, 6, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ReverseAcceptedQuantityForReturn(decimal returnedBaseQty, DateTimeOffset nowUtc)
    {
        if (Status is not (PurchaseOrderStatuses.Approved or PurchaseOrderStatuses.Closed))
        {
            throw new InvalidOperationException($"po_invalid_status_for_return: PO '{PoNumber ?? DraftCode}' is in status '{Status}'.");
        }

        var delta = decimal.Round(returnedBaseQty, 6, MidpointRounding.AwayFromZero);
        if (delta <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(returnedBaseQty), "Returned base quantity must be positive.");
        }

        if (delta > AcceptedBaseQuantityTotal)
        {
            throw new InvalidOperationException(
                $"po_return_exceeds_accepted_total: Return delta {delta:0.######} exceeds accepted PO total {AcceptedBaseQuantityTotal:0.######}.");
        }

        AcceptedBaseQuantityTotal = decimal.Round(AcceptedBaseQuantityTotal - delta, 6, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class PurchaseOrderRevision
{
    public Guid RevisionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid? SupersedesRevisionId { get; private set; }
    public string Status { get; private set; } = PurchaseOrderRevisionStatuses.Draft;
    public bool IsFrozen { get; private set; }

    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string? SupplierNpwpSnapshot { get; private set; }
    public string SupplierPkpStatusSnapshot { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = "IDR";
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDeliveryDate { get; private set; }
    public string DeliveryDestination { get; private set; } = string.Empty;
    public int PaymentTermsDays { get; private set; }
    public Guid TolerancePolicyId { get; private set; }
    public string ToleranceModeSnapshot { get; private set; } = DeliveryToleranceModes.ExactApprovedQtyOnly;
    public string? ChangeReason { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubtotalAmount { get; private set; }
    public decimal PpnTaxTotalAmount { get; private set; }
    public decimal WithholdingTaxTotalAmount { get; private set; }
    public decimal GrandTotalAmount { get; private set; }
    public decimal TotalOrderedBaseQuantity { get; private set; }
    public string SnapshotHash { get; private set; } = string.Empty;

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

    private PurchaseOrderRevision()
    {
    }

    public static PurchaseOrderRevision CreateDraft(
        Guid revisionId,
        Guid tenantId,
        Guid legalEntityId,
        Guid purchaseOrderId,
        int revisionNumber,
        Guid? supersedesRevisionId,
        Guid supplierPartnerId,
        string supplierCodeSnapshot,
        string supplierLegalNameSnapshot,
        string? supplierNpwpSnapshot,
        string supplierPkpStatusSnapshot,
        string currencyCode,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string deliveryDestination,
        int paymentTermsDays,
        Guid tolerancePolicyId,
        string toleranceModeSnapshot,
        string? changeReason,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        GuardValidation.RequireNonEmptyGuid(tolerancePolicyId, nameof(tolerancePolicyId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber), "RevisionNumber must be >= 1.");
        if (expectedDeliveryDate is not null && expectedDeliveryDate.Value < orderDate)
            throw new ArgumentException("ExpectedDeliveryDate cannot precede OrderDate.", nameof(expectedDeliveryDate));
        if (paymentTermsDays < 0 || paymentTermsDays > 365)
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");

        return new PurchaseOrderRevision
        {
            RevisionId = revisionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PurchaseOrderId = purchaseOrderId,
            RevisionNumber = revisionNumber,
            SupersedesRevisionId = supersedesRevisionId,
            Status = PurchaseOrderRevisionStatuses.Draft,
            IsFrozen = false,
            SupplierPartnerId = supplierPartnerId,
            SupplierCodeSnapshot = GuardValidation.RequireName(supplierCodeSnapshot, nameof(supplierCodeSnapshot), 32),
            SupplierLegalNameSnapshot = GuardValidation.RequireName(supplierLegalNameSnapshot, nameof(supplierLegalNameSnapshot), 200),
            SupplierNpwpSnapshot = GuardValidation.NormalizeOptional(supplierNpwpSnapshot, 64, nameof(supplierNpwpSnapshot)),
            SupplierPkpStatusSnapshot = GuardValidation.RequireCode(supplierPkpStatusSnapshot, nameof(supplierPkpStatusSnapshot), 16),
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            OrderDate = orderDate,
            ExpectedDeliveryDate = expectedDeliveryDate,
            DeliveryDestination = GuardValidation.RequireName(deliveryDestination, nameof(deliveryDestination), 200),
            PaymentTermsDays = paymentTermsDays,
            TolerancePolicyId = tolerancePolicyId,
            ToleranceModeSnapshot = GuardValidation.RequireCode(toleranceModeSnapshot, nameof(toleranceModeSnapshot), 40),
            ChangeReason = GuardValidation.NormalizeOptional(changeReason, 300, nameof(changeReason)),
            Notes = GuardValidation.NormalizeOptional(notes, 500, nameof(notes)),
            SubtotalAmount = 0m,
            PpnTaxTotalAmount = 0m,
            WithholdingTaxTotalAmount = 0m,
            GrandTotalAmount = 0m,
            TotalOrderedBaseQuantity = 0m,
            SnapshotHash = "DRAFT",
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDraftDetailsAndTotals(
        Guid supplierPartnerId,
        string supplierCodeSnapshot,
        string supplierLegalNameSnapshot,
        string? supplierNpwpSnapshot,
        string supplierPkpStatusSnapshot,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string deliveryDestination,
        int paymentTermsDays,
        Guid tolerancePolicyId,
        string toleranceModeSnapshot,
        string? changeReason,
        string? notes,
        decimal subtotalAmount,
        decimal ppnTaxTotalAmount,
        decimal withholdingTaxTotalAmount,
        decimal grandTotalAmount,
        decimal totalOrderedBaseQuantity,
        IEnumerable<PurchaseOrderLine> lines,
        DateTimeOffset nowUtc)
    {
        EnsureMutableDraft();
        if (expectedDeliveryDate is not null && expectedDeliveryDate.Value < orderDate)
            throw new ArgumentException("ExpectedDeliveryDate cannot precede OrderDate.", nameof(expectedDeliveryDate));
        if (paymentTermsDays < 0 || paymentTermsDays > 365)
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "PaymentTermsDays must be between 0 and 365.");

        SupplierPartnerId = GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        SupplierCodeSnapshot = GuardValidation.RequireName(supplierCodeSnapshot, nameof(supplierCodeSnapshot), 32);
        SupplierLegalNameSnapshot = GuardValidation.RequireName(supplierLegalNameSnapshot, nameof(supplierLegalNameSnapshot), 200);
        SupplierNpwpSnapshot = GuardValidation.NormalizeOptional(supplierNpwpSnapshot, 64, nameof(supplierNpwpSnapshot));
        SupplierPkpStatusSnapshot = GuardValidation.RequireCode(supplierPkpStatusSnapshot, nameof(supplierPkpStatusSnapshot), 16);
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        DeliveryDestination = GuardValidation.RequireName(deliveryDestination, nameof(deliveryDestination), 200);
        PaymentTermsDays = paymentTermsDays;
        TolerancePolicyId = GuardValidation.RequireNonEmptyGuid(tolerancePolicyId, nameof(tolerancePolicyId));
        ToleranceModeSnapshot = GuardValidation.RequireCode(toleranceModeSnapshot, nameof(toleranceModeSnapshot), 40);
        ChangeReason = GuardValidation.NormalizeOptional(changeReason, 300, nameof(changeReason));
        Notes = GuardValidation.NormalizeOptional(notes, 500, nameof(notes));
        SubtotalAmount = subtotalAmount;
        PpnTaxTotalAmount = ppnTaxTotalAmount;
        WithholdingTaxTotalAmount = withholdingTaxTotalAmount;
        GrandTotalAmount = grandTotalAmount;
        TotalOrderedBaseQuantity = totalOrderedBaseQuantity;
        SnapshotHash = PurchaseOrderHash.ComputeRevisionHash(this, lines);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyTotalsAndHash(
        decimal subtotalAmount,
        decimal ppnTaxTotalAmount,
        decimal withholdingTaxTotalAmount,
        decimal grandTotalAmount,
        decimal totalOrderedBaseQuantity,
        IEnumerable<PurchaseOrderLine> lines,
        DateTimeOffset nowUtc)
    {
        EnsureMutableDraft();
        SubtotalAmount = subtotalAmount;
        PpnTaxTotalAmount = ppnTaxTotalAmount;
        WithholdingTaxTotalAmount = withholdingTaxTotalAmount;
        GrandTotalAmount = grandTotalAmount;
        TotalOrderedBaseQuantity = totalOrderedBaseQuantity;
        SnapshotHash = PurchaseOrderHash.ComputeRevisionHash(this, lines);
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
            throw new InvalidOperationException("po_lines_required: PO revision requires at least one positive line item before submission.");
        }

        IsFrozen = true;
        Status = PurchaseOrderRevisionStatuses.PendingApproval;
        SubmittedByUserId = submittedByUserId;
        SubmittedAtUtc = nowUtc;
        ApprovalRecordId = approvalRecordId;
        SnapshotHash = GuardValidation.RequireName(snapshotHash, nameof(snapshotHash), 64);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyDecision(Guid decidedByUserId, bool approved, string reason, DateTimeOffset nowUtc)
    {
        if (Status != PurchaseOrderRevisionStatuses.PendingApproval || !IsFrozen)
        {
            throw new InvalidOperationException($"approval_already_decided: Revision '{RevisionNumber}' is in status '{Status}'.");
        }

        if (SubmittedByUserId == decidedByUserId)
        {
            throw new InvalidOperationException("four_eyes_self_approval_forbidden: Submitter cannot decide their own PO revision.");
        }

        Status = approved ? PurchaseOrderRevisionStatuses.Approved : PurchaseOrderRevisionStatuses.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = nowUtc;
        DecisionReason = GuardValidation.RequireName(reason, nameof(reason), 500);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkSuperseded(DateTimeOffset nowUtc)
    {
        if (Status != PurchaseOrderRevisionStatuses.Approved)
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Only APPROVED revision can be superseded (current: '{Status}').");
        }

        Status = PurchaseOrderRevisionStatuses.Superseded;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkCancelled(DateTimeOffset nowUtc)
    {
        if (Status is not (PurchaseOrderRevisionStatuses.Draft or PurchaseOrderRevisionStatuses.PendingApproval))
        {
            throw new InvalidOperationException($"po_invalid_status_transition: Cannot cancel revision in status '{Status}'.");
        }

        Status = PurchaseOrderRevisionStatuses.Cancelled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    private void EnsureMutableDraft()
    {
        if (IsFrozen || Status != PurchaseOrderRevisionStatuses.Draft)
        {
            throw new InvalidOperationException(
                $"po_revision_frozen_immutable: Revision {RevisionNumber} is frozen ({Status}) and cannot be modified; create a new revision.");
        }
    }
}

public sealed class PurchaseOrderLine
{
    public Guid PoLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
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

    public decimal UnitPrice { get; private set; }
    public decimal LineSubtotalAmount { get; private set; }

    public Guid? TaxCodeId { get; private set; }
    public string? TaxCodeSnapshot { get; private set; }
    public string? TaxFamilySnapshot { get; private set; }
    public decimal? TaxRatePercentSnapshot { get; private set; }
    public decimal TaxableBaseAmount { get; private set; }
    public decimal TaxAmount { get; private set; }

    public decimal OverdeliveryTolerancePercent { get; private set; }
    public decimal OverdeliveryToleranceBaseQty { get; private set; }
    public bool AllowUnlimitedOverdelivery { get; private set; }
    public string? LineDescription { get; private set; }

    private PurchaseOrderLine()
    {
    }

    public static PurchaseOrderLine Create(
        Guid poLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid purchaseOrderId,
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
        Guid? taxCodeId,
        string? taxCodeSnapshot,
        string? taxFamilySnapshot,
        decimal? taxRatePercentSnapshot,
        decimal taxableBaseAmount,
        decimal taxAmount,
        decimal overdeliveryTolerancePercent,
        decimal overdeliveryToleranceBaseQty,
        bool allowUnlimitedOverdelivery,
        string? lineDescription)
    {
        GuardValidation.RequireNonEmptyGuid(poLineId, nameof(poLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(revisionId, nameof(revisionId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(orderedUomId, nameof(orderedUomId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));
        DeliveryTolerancePolicy.EnsureSafeToleranceValues(overdeliveryTolerancePercent, overdeliveryToleranceBaseQty, allowUnlimitedOverdelivery);

        if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        if (orderedQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(orderedQuantity), "OrderedQuantity must be positive.");
        if (orderedBaseQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(orderedBaseQuantity), "OrderedBaseQuantity must be positive.");
        if (unitPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(unitPrice), "UnitPrice must be positive.");
        if (uomFactorToBaseSnapshot <= 0m) throw new ArgumentOutOfRangeException(nameof(uomFactorToBaseSnapshot), "UomFactorToBase must be positive.");

        var normalizedQty = decimal.Round(orderedQuantity, 6, MidpointRounding.AwayFromZero);
        var normalizedPrice = decimal.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        var lineSubtotal = decimal.Round(normalizedQty * normalizedPrice, 4, MidpointRounding.AwayFromZero);

        return new PurchaseOrderLine
        {
            PoLineId = poLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PurchaseOrderId = purchaseOrderId,
            RevisionId = revisionId,
            LineNumber = lineNumber,
            LineageLineKey = GuardValidation.RequireMasterCode(lineageLineKey, nameof(lineageLineKey), 2, 48),
            ItemId = itemId,
            ItemCodeSnapshot = GuardValidation.RequireName(itemCodeSnapshot, nameof(itemCodeSnapshot), 32),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 200),
            ItemKindSnapshot = GuardValidation.RequireCode(itemKindSnapshot, nameof(itemKindSnapshot), 32),
            OrderedUomId = orderedUomId,
            OrderedUomCodeSnapshot = GuardValidation.RequireCode(orderedUomCodeSnapshot, nameof(orderedUomCodeSnapshot), 16),
            BaseUomId = baseUomId,
            BaseUomCodeSnapshot = GuardValidation.RequireCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 16),
            UomFactorToBaseSnapshot = decimal.Round(uomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero),
            OrderedQuantity = normalizedQty,
            OrderedBaseQuantity = decimal.Round(orderedBaseQuantity, 6, MidpointRounding.AwayFromZero),
            UnitPrice = normalizedPrice,
            LineSubtotalAmount = lineSubtotal,
            TaxCodeId = taxCodeId,
            TaxCodeSnapshot = GuardValidation.NormalizeOptional(taxCodeSnapshot, 32, nameof(taxCodeSnapshot)),
            TaxFamilySnapshot = GuardValidation.NormalizeOptional(taxFamilySnapshot, 16, nameof(taxFamilySnapshot)),
            TaxRatePercentSnapshot = taxRatePercentSnapshot is not null
                ? decimal.Round(taxRatePercentSnapshot.Value, 4, MidpointRounding.AwayFromZero)
                : null,
            TaxableBaseAmount = decimal.Round(taxableBaseAmount, 4, MidpointRounding.AwayFromZero),
            TaxAmount = decimal.Round(taxAmount, 4, MidpointRounding.AwayFromZero),
            OverdeliveryTolerancePercent = 0m,
            OverdeliveryToleranceBaseQty = 0m,
            AllowUnlimitedOverdelivery = false,
            LineDescription = GuardValidation.NormalizeOptional(lineDescription, 240, nameof(lineDescription))
        };
    }
}

public static class PurchaseOrderHash
{
    public static string ComputeRevisionHash(PurchaseOrderRevision rev, IEnumerable<PurchaseOrderLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append(rev.TenantId.ToString("D")).Append('|')
          .Append(rev.LegalEntityId.ToString("D")).Append('|')
          .Append(rev.PurchaseOrderId.ToString("D")).Append('|')
          .Append(rev.RevisionNumber.ToString(CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.SupplierPartnerId.ToString("D")).Append('|')
          .Append(rev.CurrencyCode).Append('|')
          .Append(rev.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.ExpectedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "").Append('|')
          .Append(rev.DeliveryDestination).Append('|')
          .Append(rev.PaymentTermsDays.ToString(CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.SubtotalAmount.ToString("F4", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.PpnTaxTotalAmount.ToString("F4", CultureInfo.InvariantCulture)).Append('|')
          .Append(rev.GrandTotalAmount.ToString("F4", CultureInfo.InvariantCulture));

        foreach (var line in lines.OrderBy(l => l.LineNumber).ThenBy(l => l.LineageLineKey, StringComparer.Ordinal))
        {
            sb.Append("\nL|")
              .Append(line.LineageLineKey).Append('|')
              .Append(line.ItemId.ToString("D")).Append('|')
              .Append(line.OrderedUomId.ToString("D")).Append('|')
              .Append(line.OrderedQuantity.ToString("F6", CultureInfo.InvariantCulture)).Append('|')
              .Append(line.OrderedBaseQuantity.ToString("F6", CultureInfo.InvariantCulture)).Append('|')
              .Append(line.UnitPrice.ToString("F4", CultureInfo.InvariantCulture)).Append('|')
              .Append(line.TaxCodeId?.ToString("D") ?? "").Append('|')
              .Append(line.TaxAmount.ToString("F4", CultureInfo.InvariantCulture));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class PoDiffChangeKinds
{
    public const string Added = "ADDED";
    public const string Modified = "MODIFIED";
    public const string Removed = "REMOVED";
    public const string Unchanged = "UNCHANGED";
}

public sealed record PoHeaderFieldDiff(
    string FieldKey,
    string? PreviousValue,
    string? CurrentValue);

public sealed record PoLineDiffItem(
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

public sealed record PoRevisionDiffReport(
    int TargetRevisionNumber,
    int? BaselineRevisionNumber,
    IReadOnlyList<PoHeaderFieldDiff> HeaderChanges,
    IReadOnlyList<PoLineDiffItem> LineChanges,
    decimal? PreviousGrandTotal,
    decimal CurrentGrandTotal,
    decimal GrandTotalDelta,
    decimal PreviousOrderedBaseQty,
    decimal CurrentOrderedBaseQty,
    decimal OrderedBaseQtyDelta);

public static class PurchaseOrderDiffCalculator
{
    public static PoRevisionDiffReport Compute(
        PurchaseOrderRevision target,
        IReadOnlyList<PurchaseOrderLine> targetLines,
        PurchaseOrderRevision? baseline,
        IReadOnlyList<PurchaseOrderLine>? baselineLines)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetLines);

        if (baseline is null)
        {
            var initialLines = targetLines
                .OrderBy(l => l.LineNumber)
                .Select(l => new PoLineDiffItem(
                    l.LineageLineKey,
                    PoDiffChangeKinds.Added,
                    null,
                    l.ItemCodeSnapshot,
                    null,
                    l.OrderedQuantity,
                    null,
                    l.OrderedUomCodeSnapshot,
                    null,
                    l.UnitPrice,
                    null,
                    l.LineSubtotalAmount))
                .ToList();

            return new PoRevisionDiffReport(
                target.RevisionNumber,
                null,
                Array.Empty<PoHeaderFieldDiff>(),
                initialLines,
                null,
                target.GrandTotalAmount,
                target.GrandTotalAmount,
                0m,
                target.TotalOrderedBaseQuantity,
                target.TotalOrderedBaseQuantity);
        }

        var headerChanges = new List<PoHeaderFieldDiff>();
        AddIfChanged(headerChanges, "supplier", baseline.SupplierCodeSnapshot, target.SupplierCodeSnapshot);
        AddIfChanged(headerChanges, "orderDate", baseline.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), target.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfChanged(headerChanges, "expectedDeliveryDate", baseline.ExpectedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), target.ExpectedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfChanged(headerChanges, "deliveryDestination", baseline.DeliveryDestination, target.DeliveryDestination);
        AddIfChanged(headerChanges, "paymentTermsDays", baseline.PaymentTermsDays.ToString(CultureInfo.InvariantCulture), target.PaymentTermsDays.ToString(CultureInfo.InvariantCulture));

        var prevByKey = (baselineLines ?? Array.Empty<PurchaseOrderLine>())
            .ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);
        var currByKey = targetLines.ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);

        var orderedKeys = targetLines
            .OrderBy(l => l.LineNumber)
            .Select(l => l.LineageLineKey)
            .Concat((baselineLines ?? Array.Empty<PurchaseOrderLine>()).OrderBy(l => l.LineNumber).Select(l => l.LineageLineKey))
            .Distinct(StringComparer.Ordinal);

        var lineDiffs = new List<PoLineDiffItem>();
        foreach (var key in orderedKeys)
        {
            prevByKey.TryGetValue(key, out var prev);
            currByKey.TryGetValue(key, out var curr);

            if (prev is null && curr is not null)
            {
                lineDiffs.Add(new PoLineDiffItem(
                    key, PoDiffChangeKinds.Added,
                    null, curr.ItemCodeSnapshot,
                    null, curr.OrderedQuantity,
                    null, curr.OrderedUomCodeSnapshot,
                    null, curr.UnitPrice,
                    null, curr.LineSubtotalAmount));
            }
            else if (prev is not null && curr is null)
            {
                lineDiffs.Add(new PoLineDiffItem(
                    key, PoDiffChangeKinds.Removed,
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
                    || prev.TaxCodeId != curr.TaxCodeId
                    || !string.Equals(prev.LineDescription, curr.LineDescription, StringComparison.Ordinal);

                lineDiffs.Add(new PoLineDiffItem(
                    key,
                    changed ? PoDiffChangeKinds.Modified : PoDiffChangeKinds.Unchanged,
                    prev.ItemCodeSnapshot, curr.ItemCodeSnapshot,
                    prev.OrderedQuantity, curr.OrderedQuantity,
                    prev.OrderedUomCodeSnapshot, curr.OrderedUomCodeSnapshot,
                    prev.UnitPrice, curr.UnitPrice,
                    prev.LineSubtotalAmount, curr.LineSubtotalAmount));
            }
        }

        return new PoRevisionDiffReport(
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

    private static void AddIfChanged(List<PoHeaderFieldDiff> list, string key, string? previous, string? current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            list.Add(new PoHeaderFieldDiff(key, previous, current));
        }
    }
}
