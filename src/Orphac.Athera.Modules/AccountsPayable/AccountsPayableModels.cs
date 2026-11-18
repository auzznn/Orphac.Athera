using System.Text.Json;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.AccountsPayable;

public static class SupplierBillTypes
{
    public const string PoThreeWayMatch = "PO_THREE_WAY_MATCH";
    public const string NonPoExpense = "NON_PO_EXPENSE";
    public const string SupplierAdvance = "SUPPLIER_ADVANCE";

    public static readonly IReadOnlyList<string> All = [PoThreeWayMatch, NonPoExpense, SupplierAdvance];

    public static string EnsureSupported(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? PoThreeWayMatch : raw.Trim().ToUpperInvariant();
        if (value is NonPoExpense or SupplierAdvance)
        {
            throw new InvalidOperationException(
                $"od08_non_po_bill_not_supported: OD-08 forbids '{value}' bills until Product Owner policy & account rules are approved (ATH-CR-006).");
        }

        if (value != PoThreeWayMatch)
        {
            throw new ArgumentException($"Unsupported supplier bill type '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class SupplierBillStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Posted = "POSTED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Draft, PendingApproval, Approved, Posted, Cancelled];
}

public static class InvoiceMatchStatuses
{
    public const string Matched = "MATCHED";
    public const string WithinTolerance = "WITHIN_TOLERANCE";
    public const string ApprovalRequired = "APPROVAL_REQUIRED";
    public const string PostingBlocked = "POSTING_BLOCKED";

    public static readonly IReadOnlyList<string> All = [Matched, WithinTolerance, ApprovalRequired, PostingBlocked];
}

public static class MatchDimensionOutcomes
{
    public const string Matched = "MATCHED";
    public const string WithinTolerance = "WITHIN_TOLERANCE";
    public const string VarianceApprovalRequired = "VARIANCE_APPROVAL_REQUIRED";
    public const string Blocked = "BLOCKED";

    public static readonly IReadOnlyList<string> All = [Matched, WithinTolerance, VarianceApprovalRequired, Blocked];
}

public static class PaymentBlockStatuses
{
    public const string None = "NONE";
    public const string PaymentBlocked = "PAYMENT_BLOCKED";
    public const string Released = "RELEASED";

    public static readonly IReadOnlyList<string> All = [None, PaymentBlocked, Released];
}

public static class PayableStatuses
{
    public const string Open = "OPEN";
    public const string PartiallyPaid = "PARTIALLY_PAID";
    public const string Paid = "PAID";
    public const string Credited = "CREDITED";
    public const string Reversed = "REVERSED";

    public static readonly IReadOnlyList<string> All = [Open, PartiallyPaid, Paid, Credited, Reversed];
}

public static class MatchEvaluationStages
{
    public const string DraftPreview = "DRAFT_PREVIEW";
    public const string Submit = "SUBMIT";
    public const string PostCommit = "POST_COMMIT";

    public static readonly IReadOnlyList<string> All = [DraftPreview, Submit, PostCommit];
}

public static class InvoiceMatchPolicyReviewStatuses
{
    public const string UnreviewedSafeDefault = "UNREVIEWED_SAFE_DEFAULT";
    public const string ReviewedInternal = "REVIEWED_INTERNAL";

    public static readonly IReadOnlyList<string> All = [UnreviewedSafeDefault, ReviewedInternal];
}

public static class ApPolicyDefaults
{
    public const string SupplierBillApprovalPolicyCode = "SUPPLIER_BILL_POSTING";
    public const string DefaultMatchPolicyCode = "EXACT_MATCH_OD03_SAFE";
}

public sealed class BillSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private BillSequence()
    {
    }
}

public sealed class PayableSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private PayableSequence()
    {
    }
}

/// <summary>
/// Legal-entity three-way match tolerance policy (`OD-03`).
/// Public safe default fixes all automatic tolerances and small write-offs to zero until PO sign-off (`ATH-CR-006`).
/// </summary>
public sealed class InvoiceMatchPolicy
{
    public Guid PolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ReviewStatus { get; private set; } = InvoiceMatchPolicyReviewStatuses.UnreviewedSafeDefault;
    public decimal PpTolerancePercent { get; private set; }
    public decimal PpToleranceAmount { get; private set; }
    public decimal DqTolerancePercent { get; private set; }
    public decimal DqToleranceBaseQty { get; private set; }
    public decimal BdWriteOffLimitAmount { get; private set; }
    public Guid? BdWriteOffAccountId { get; private set; }
    public bool IsDefault { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private InvoiceMatchPolicy()
    {
    }

    public static void EnsurePublicSafeValues(
        decimal ppPct,
        decimal ppAmt,
        decimal dqPct,
        decimal dqQty,
        decimal bdLimit,
        Guid? bdAccountId)
    {
        if (ppPct < 0m || ppAmt < 0m || dqPct < 0m || dqQty < 0m || bdLimit < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(ppPct), "Tolerance limits cannot be negative.");
        }

        if (ppPct != 0m || ppAmt != 0m || dqPct != 0m || dqQty != 0m || bdLimit != 0m || bdAccountId.HasValue)
        {
            throw new InvalidOperationException(
                "od03_unreviewed_tolerance_policy_blocked: Non-zero automatic PP/DQ/BD tolerances and BD write-off accounts require Product Owner approval under OD-03 (ATH-CR-006).");
        }
    }

    public static InvoiceMatchPolicy CreateSafeDefault(
        Guid policyId,
        Guid tenantId,
        Guid legalEntityId,
        string code,
        string name,
        bool isDefault,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(policyId, nameof(policyId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        return new InvoiceMatchPolicy
        {
            PolicyId = policyId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = GuardValidation.RequireMasterCode(code, nameof(code), 2, 48),
            Name = GuardValidation.RequireName(name, nameof(name), 160),
            ReviewStatus = InvoiceMatchPolicyReviewStatuses.UnreviewedSafeDefault,
            PpTolerancePercent = 0m,
            PpToleranceAmount = 0m,
            DqTolerancePercent = 0m,
            DqToleranceBaseQty = 0m,
            BdWriteOffLimitAmount = 0m,
            BdWriteOffAccountId = null,
            IsDefault = isDefault,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public static InvoiceMatchPolicy CreateReviewedForVerification(
        Guid policyId,
        Guid tenantId,
        Guid legalEntityId,
        string code,
        string name,
        decimal ppTolerancePercent,
        decimal ppToleranceAmount,
        decimal bdWriteOffLimitAmount,
        Guid? bdWriteOffAccountId,
        bool isDefault,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(policyId, nameof(policyId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        if (ppTolerancePercent < 0m || ppToleranceAmount < 0m || bdWriteOffLimitAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(ppTolerancePercent), "Tolerance limits cannot be negative.");
        }

        if (bdWriteOffLimitAmount > 0m && (!bdWriteOffAccountId.HasValue || bdWriteOffAccountId.Value == Guid.Empty))
        {
            throw new ArgumentException("Non-zero BD write-off limit requires a BdWriteOffAccountId.", nameof(bdWriteOffAccountId));
        }

        return new InvoiceMatchPolicy
        {
            PolicyId = policyId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = GuardValidation.RequireMasterCode(code, nameof(code), 2, 48),
            Name = GuardValidation.RequireName(name, nameof(name), 160),
            ReviewStatus = InvoiceMatchPolicyReviewStatuses.ReviewedInternal,
            PpTolerancePercent = decimal.Round(ppTolerancePercent, 4, MidpointRounding.AwayFromZero),
            PpToleranceAmount = decimal.Round(ppToleranceAmount, 4, MidpointRounding.AwayFromZero),
            DqTolerancePercent = 0m,
            DqToleranceBaseQty = 0m,
            BdWriteOffLimitAmount = decimal.Round(bdWriteOffLimitAmount, 4, MidpointRounding.AwayFromZero),
            BdWriteOffAccountId = bdWriteOffAccountId,
            IsDefault = isDefault,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

public sealed class SupplierBill
{
    public Guid SupplierBillId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DraftCode { get; private set; } = string.Empty;
    public string? BillNumber { get; private set; }
    public string BillType { get; private set; } = SupplierBillTypes.PoThreeWayMatch;
    public Guid PurchaseOrderId { get; private set; }
    public Guid PurchaseOrderRevisionId { get; private set; }
    public string PoNumberSnapshot { get; private set; } = string.Empty;
    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string? SupplierNpwpSnapshot { get; private set; }
    public string SupplierInvoiceNumber { get; private set; } = string.Empty;
    public string? TaxInvoiceNumber { get; private set; }
    public string? WithholdingSlipReference { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal DeclaredGrossAmount { get; private set; }
    public decimal LinesNetSubtotal { get; private set; }
    public decimal TotalGrniClearAmount { get; private set; }
    public decimal TotalPriceVarianceAmount { get; private set; }
    public decimal TotalInventoryAdjAmount { get; private set; }
    public decimal TotalPnlVarianceAmount { get; private set; }
    public decimal TotalBdWriteOffAmount { get; private set; }
    public decimal PpnTaxTotal { get; private set; }
    public decimal WithholdingTaxTotal { get; private set; }
    public decimal ComputedGrossTotal { get; private set; }
    public decimal NetPayableAmount { get; private set; }
    public string Status { get; private set; } = SupplierBillStatuses.Draft;
    public string MatchStatus { get; private set; } = InvoiceMatchStatuses.Matched;
    public string PaymentBlockStatus { get; private set; } = PaymentBlockStatuses.None;
    public string? PaymentBlockReason { get; private set; }
    public string ApprovalPolicyCode { get; private set; } = ApPolicyDefaults.SupplierBillApprovalPolicyCode;
    public string? LastApprovalDecision { get; private set; }
    public string? LastRejectionReason { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public bool VarianceApproved { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DateTimeOffset? PostedAtUtc { get; private set; }
    public Guid? PostedJournalId { get; private set; }
    public string? PostIdempotencyKey { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SupplierBill()
    {
    }

    public static SupplierBill CreateDraft(
        Guid supplierBillId,
        Guid tenantId,
        Guid legalEntityId,
        string draftCode,
        string billType,
        Guid purchaseOrderId,
        Guid purchaseOrderRevisionId,
        string poNumberSnapshot,
        Guid supplierPartnerId,
        string supplierCodeSnapshot,
        string supplierLegalNameSnapshot,
        string? supplierNpwpSnapshot,
        string supplierInvoiceNumber,
        string? taxInvoiceNumber,
        string? withholdingSlipReference,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string currencyCode,
        decimal declaredGrossAmount,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(supplierBillId, nameof(supplierBillId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderRevisionId, nameof(purchaseOrderRevisionId));
        GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var normalizedType = SupplierBillTypes.EnsureSupported(billType);
        if (dueDate < invoiceDate)
        {
            throw new ArgumentException("DueDate cannot precede InvoiceDate.", nameof(dueDate));
        }

        var gross = decimal.Round(declaredGrossAmount, 4, MidpointRounding.AwayFromZero);
        if (gross <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredGrossAmount), "DeclaredGrossAmount must be strictly positive.");
        }

        return new SupplierBill
        {
            SupplierBillId = supplierBillId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            DraftCode = GuardValidation.RequireCode(draftCode, nameof(draftCode), 40),
            BillType = normalizedType,
            PurchaseOrderId = purchaseOrderId,
            PurchaseOrderRevisionId = purchaseOrderRevisionId,
            PoNumberSnapshot = GuardValidation.RequireCode(poNumberSnapshot, nameof(poNumberSnapshot), 40),
            SupplierPartnerId = supplierPartnerId,
            SupplierCodeSnapshot = GuardValidation.RequireMasterCode(supplierCodeSnapshot, nameof(supplierCodeSnapshot), 2, 48),
            SupplierLegalNameSnapshot = GuardValidation.RequireName(supplierLegalNameSnapshot, nameof(supplierLegalNameSnapshot), 200),
            SupplierNpwpSnapshot = GuardValidation.NormalizeOptional(supplierNpwpSnapshot, 64, nameof(supplierNpwpSnapshot)),
            SupplierInvoiceNumber = GuardValidation.RequireName(supplierInvoiceNumber, nameof(supplierInvoiceNumber), 64),
            TaxInvoiceNumber = GuardValidation.NormalizeOptional(taxInvoiceNumber, 80, nameof(taxInvoiceNumber)),
            WithholdingSlipReference = GuardValidation.NormalizeOptional(withholdingSlipReference, 80, nameof(withholdingSlipReference)),
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            DeclaredGrossAmount = gross,
            Status = SupplierBillStatuses.Draft,
            MatchStatus = InvoiceMatchStatuses.Matched,
            PaymentBlockStatus = PaymentBlockStatuses.None,
            ApprovalPolicyCode = ApPolicyDefaults.SupplierBillApprovalPolicyCode,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDraftHeader(
        string supplierInvoiceNumber,
        string? taxInvoiceNumber,
        string? withholdingSlipReference,
        DateOnly invoiceDate,
        DateOnly dueDate,
        decimal declaredGrossAmount,
        string? notes,
        DateTimeOffset nowUtc)
    {
        if (Status != SupplierBillStatuses.Draft)
        {
            throw new InvalidOperationException($"bill_not_editable: Supplier bill in status '{Status}' cannot be edited.");
        }

        if (dueDate < invoiceDate)
        {
            throw new ArgumentException("DueDate cannot precede InvoiceDate.", nameof(dueDate));
        }

        var gross = decimal.Round(declaredGrossAmount, 4, MidpointRounding.AwayFromZero);
        if (gross <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredGrossAmount), "DeclaredGrossAmount must be strictly positive.");
        }

        SupplierInvoiceNumber = GuardValidation.RequireName(supplierInvoiceNumber, nameof(supplierInvoiceNumber), 64);
        TaxInvoiceNumber = GuardValidation.NormalizeOptional(taxInvoiceNumber, 80, nameof(taxInvoiceNumber));
        WithholdingSlipReference = GuardValidation.NormalizeOptional(withholdingSlipReference, 80, nameof(withholdingSlipReference));
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        DeclaredGrossAmount = gross;
        Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes));
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyMatchComputation(InvoiceMatchComputation comp, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(comp);
        LinesNetSubtotal = comp.LinesNetSubtotal;
        TotalGrniClearAmount = comp.TotalGrniClearAmount;
        TotalPriceVarianceAmount = comp.TotalPriceVarianceAmount;
        TotalInventoryAdjAmount = comp.TotalInventoryAdjAmount;
        TotalPnlVarianceAmount = comp.TotalPnlVarianceAmount;
        TotalBdWriteOffAmount = comp.BdWriteOffAmount;
        PpnTaxTotal = comp.PpnTaxTotal;
        WithholdingTaxTotal = comp.WithholdingTaxTotal;
        ComputedGrossTotal = comp.ComputedGrossTotal;
        NetPayableAmount = comp.NetPayableAmount;
        MatchStatus = comp.OverallMatchStatus;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkSubmitted(string assignedBillNumber, Guid submittedByUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(submittedByUserId, nameof(submittedByUserId));
        if (Status != SupplierBillStatuses.Draft)
        {
            throw new InvalidOperationException($"bill_invalid_transition: Cannot submit bill in status '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(BillNumber))
        {
            BillNumber = GuardValidation.RequireCode(assignedBillNumber, nameof(assignedBillNumber), 40);
        }

        Status = SupplierBillStatuses.PendingApproval;
        SubmittedByUserId = submittedByUserId;
        SubmittedAtUtc = nowUtc;
        ApprovedByUserId = null;
        ApprovedAtUtc = null;
        VarianceApproved = false;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RecordApprovalDecision(
        Guid decidedByUserId,
        string decision,
        string reason,
        bool approvePriceVariance,
        bool applyPaymentBlock,
        string? paymentBlockReason,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(decidedByUserId, nameof(decidedByUserId));
        if (Status != SupplierBillStatuses.PendingApproval)
        {
            throw new InvalidOperationException($"bill_not_pending_approval: Supplier bill in status '{Status}' is not awaiting approval.");
        }

        if (SubmittedByUserId == decidedByUserId)
        {
            throw new InvalidOperationException(
                "four_eyes_self_approval_forbidden: Submitter cannot approve or reject their own supplier bill under OD-02.");
        }

        var normDecision = decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normDecision is not ("APPROVED" or "REJECTED"))
        {
            throw new ArgumentException("Decision must be APPROVED or REJECTED.", nameof(decision));
        }

        var cleanReason = GuardValidation.RequireName(reason, nameof(reason), 500);
        LastApprovalDecision = normDecision;

        if (normDecision == "REJECTED")
        {
            Status = SupplierBillStatuses.Draft;
            LastRejectionReason = cleanReason;
            VarianceApproved = false;
            ApprovedByUserId = null;
            ApprovedAtUtc = null;
            UpdatedAtUtc = nowUtc;
            Version++;
            return;
        }

        if (MatchStatus == InvoiceMatchStatuses.ApprovalRequired && !approvePriceVariance)
        {
            throw new InvalidOperationException(
                "variance_approval_acknowledgement_required: Approver must explicitly acknowledge PP unit-price variance (approvePriceVariance=true).");
        }

        Status = SupplierBillStatuses.Approved;
        LastRejectionReason = null;
        ApprovedByUserId = decidedByUserId;
        ApprovedAtUtc = nowUtc;
        VarianceApproved = MatchStatus == InvoiceMatchStatuses.ApprovalRequired || approvePriceVariance;
        if (applyPaymentBlock)
        {
            PaymentBlockStatus = PaymentBlockStatuses.PaymentBlocked;
            PaymentBlockReason = GuardValidation.NormalizeOptional(paymentBlockReason, 300, nameof(paymentBlockReason)) ?? cleanReason;
        }

        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkPosted(Guid journalId, string idempotencyKey, Guid postedByUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(journalId, nameof(journalId));
        GuardValidation.RequireNonEmptyGuid(postedByUserId, nameof(postedByUserId));
        if (Status != SupplierBillStatuses.Approved)
        {
            throw new InvalidOperationException($"bill_must_be_approved_before_post: Bill in status '{Status}' cannot post.");
        }

        Status = SupplierBillStatuses.Posted;
        PostedJournalId = journalId;
        PostIdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80);
        PostedByUserId = postedByUserId;
        PostedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void UpdatePaymentBlock(string targetBlockStatus, string reason, DateTimeOffset nowUtc)
    {
        if (Status != SupplierBillStatuses.Posted)
        {
            throw new InvalidOperationException("bill_must_be_posted_for_payment_block: Payment block can only be updated on posted bills.");
        }

        var normalized = targetBlockStatus?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized is not (PaymentBlockStatuses.PaymentBlocked or PaymentBlockStatuses.Released))
        {
            throw new ArgumentException("PaymentBlockStatus must be PAYMENT_BLOCKED or RELEASED.", nameof(targetBlockStatus));
        }

        PaymentBlockStatus = normalized;
        PaymentBlockReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class SupplierBillLine
{
    public Guid SupplierBillLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierBillId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ReceiptAccrualLineId { get; private set; }
    public Guid ValuedAcceptanceLineId { get; private set; }
    public Guid GoodsReceiptLineId { get; private set; }
    public Guid PoLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal OrderedBaseQtySnapshot { get; private set; }
    public decimal AcceptedUnbilledQtySnapshot { get; private set; }
    public decimal BilledBaseQuantity { get; private set; }
    public decimal PoUnitPriceFunctional { get; private set; }
    public decimal InvoiceUnitPriceBase { get; private set; }
    public decimal LineNetAmount { get; private set; }
    public decimal GrniClearAmount { get; private set; }
    public decimal PriceVarianceAmount { get; private set; }
    public Guid PpnTaxCodeId { get; private set; }
    public string PpnTaxCodeSnapshot { get; private set; } = string.Empty;
    public decimal PpnTaxAmount { get; private set; }
    public Guid? WithholdingTaxCodeId { get; private set; }
    public string? WithholdingTaxCodeSnapshot { get; private set; }
    public decimal WithholdingTaxAmount { get; private set; }
    public string DqOutcome { get; private set; } = MatchDimensionOutcomes.Matched;
    public string PpOutcome { get; private set; } = MatchDimensionOutcomes.Matched;
    public string? LineNotes { get; private set; }

    private SupplierBillLine()
    {
    }

    public static SupplierBillLine Create(
        Guid supplierBillLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierBillId,
        int lineNumber,
        Guid receiptAccrualLineId,
        Guid valuedAcceptanceLineId,
        Guid goodsReceiptLineId,
        Guid poLineId,
        Guid itemId,
        string itemCodeSnapshot,
        string itemNameSnapshot,
        Guid baseUomId,
        string baseUomCodeSnapshot,
        decimal orderedBaseQtySnapshot,
        decimal acceptedUnbilledQtySnapshot,
        decimal billedBaseQuantity,
        decimal poUnitPriceFunctional,
        decimal invoiceUnitPriceBase,
        decimal lineNetAmount,
        decimal grniClearAmount,
        decimal priceVarianceAmount,
        Guid ppnTaxCodeId,
        string ppnTaxCodeSnapshot,
        decimal ppnTaxAmount,
        Guid? withholdingTaxCodeId,
        string? withholdingTaxCodeSnapshot,
        decimal withholdingTaxAmount,
        string dqOutcome,
        string ppOutcome,
        string? lineNotes)
    {
        GuardValidation.RequireNonEmptyGuid(supplierBillLineId, nameof(supplierBillLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierBillId, nameof(supplierBillId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualLineId, nameof(receiptAccrualLineId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceLineId, nameof(valuedAcceptanceLineId));
        GuardValidation.RequireNonEmptyGuid(goodsReceiptLineId, nameof(goodsReceiptLineId));
        GuardValidation.RequireNonEmptyGuid(poLineId, nameof(poLineId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));
        GuardValidation.RequireNonEmptyGuid(ppnTaxCodeId, nameof(ppnTaxCodeId));

        if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");

        var qty = decimal.Round(billedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var invUnit = decimal.Round(invoiceUnitPriceBase, 6, MidpointRounding.AwayFromZero);
        var net = decimal.Round(lineNetAmount, 4, MidpointRounding.AwayFromZero);
        if (qty <= 0m || invUnit <= 0m || net <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(billedBaseQuantity), "Bill line quantity, price and net amount must be positive.");
        }

        return new SupplierBillLine
        {
            SupplierBillLineId = supplierBillLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierBillId = supplierBillId,
            LineNumber = lineNumber,
            ReceiptAccrualLineId = receiptAccrualLineId,
            ValuedAcceptanceLineId = valuedAcceptanceLineId,
            GoodsReceiptLineId = goodsReceiptLineId,
            PoLineId = poLineId,
            ItemId = itemId,
            ItemCodeSnapshot = GuardValidation.RequireMasterCode(itemCodeSnapshot, nameof(itemCodeSnapshot), 2, 48),
            ItemNameSnapshot = GuardValidation.RequireName(itemNameSnapshot, nameof(itemNameSnapshot), 200),
            BaseUomId = baseUomId,
            BaseUomCodeSnapshot = GuardValidation.RequireMasterCode(baseUomCodeSnapshot, nameof(baseUomCodeSnapshot), 1, 20),
            OrderedBaseQtySnapshot = decimal.Round(orderedBaseQtySnapshot, 6, MidpointRounding.AwayFromZero),
            AcceptedUnbilledQtySnapshot = decimal.Round(acceptedUnbilledQtySnapshot, 6, MidpointRounding.AwayFromZero),
            BilledBaseQuantity = qty,
            PoUnitPriceFunctional = decimal.Round(poUnitPriceFunctional, 6, MidpointRounding.AwayFromZero),
            InvoiceUnitPriceBase = invUnit,
            LineNetAmount = net,
            GrniClearAmount = decimal.Round(grniClearAmount, 4, MidpointRounding.AwayFromZero),
            PriceVarianceAmount = decimal.Round(priceVarianceAmount, 4, MidpointRounding.AwayFromZero),
            PpnTaxCodeId = ppnTaxCodeId,
            PpnTaxCodeSnapshot = GuardValidation.RequireMasterCode(ppnTaxCodeSnapshot, nameof(ppnTaxCodeSnapshot), 2, 32),
            PpnTaxAmount = decimal.Round(ppnTaxAmount, 4, MidpointRounding.AwayFromZero),
            WithholdingTaxCodeId = withholdingTaxCodeId,
            WithholdingTaxCodeSnapshot = withholdingTaxCodeSnapshot,
            WithholdingTaxAmount = decimal.Round(withholdingTaxAmount, 4, MidpointRounding.AwayFromZero),
            DqOutcome = dqOutcome,
            PpOutcome = ppOutcome,
            LineNotes = GuardValidation.NormalizeOptional(lineNotes, 240, nameof(lineNotes))
        };
    }
}

/// <summary>
/// Strictly append-only match evaluation snapshot recorded at preview/submit/post boundaries (`OD-03`).
/// </summary>
public sealed class InvoiceMatchEvaluation
{
    public Guid EvaluationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierBillId { get; private set; }
    public Guid PolicyId { get; private set; }
    public string EvaluationStage { get; private set; } = MatchEvaluationStages.DraftPreview;
    public string DqOutcome { get; private set; } = MatchDimensionOutcomes.Matched;
    public string PpOutcome { get; private set; } = MatchDimensionOutcomes.Matched;
    public string BdOutcome { get; private set; } = MatchDimensionOutcomes.Matched;
    public string OverallMatchStatus { get; private set; } = InvoiceMatchStatuses.Matched;
    public bool IsPostingBlocked { get; private set; }
    public bool RequiresApproval { get; private set; }
    public bool RequiresPaymentBlock { get; private set; }
    public string BlockingReasonsJson { get; private set; } = "[]";
    public string ComparisonJson { get; private set; } = "[]";
    public Guid EvaluatedByUserId { get; private set; }
    public DateTimeOffset EvaluatedAtUtc { get; private set; }

    private InvoiceMatchEvaluation()
    {
    }

    public static InvoiceMatchEvaluation Record(
        Guid evaluationId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierBillId,
        Guid policyId,
        string evaluationStage,
        InvoiceMatchComputation computation,
        bool requiresPaymentBlock,
        Guid evaluatedByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(evaluationId, nameof(evaluationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierBillId, nameof(supplierBillId));
        GuardValidation.RequireNonEmptyGuid(policyId, nameof(policyId));
        GuardValidation.RequireNonEmptyGuid(evaluatedByUserId, nameof(evaluatedByUserId));
        ArgumentNullException.ThrowIfNull(computation);

        return new InvoiceMatchEvaluation
        {
            EvaluationId = evaluationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierBillId = supplierBillId,
            PolicyId = policyId,
            EvaluationStage = evaluationStage,
            DqOutcome = computation.DqOutcome,
            PpOutcome = computation.PpOutcome,
            BdOutcome = computation.BdOutcome,
            OverallMatchStatus = computation.OverallMatchStatus,
            IsPostingBlocked = computation.IsPostingBlocked,
            RequiresApproval = computation.RequiresApproval,
            RequiresPaymentBlock = requiresPaymentBlock,
            BlockingReasonsJson = JsonSerializer.Serialize(computation.BlockingReasons),
            ComparisonJson = JsonSerializer.Serialize(computation.Lines),
            EvaluatedByUserId = evaluatedByUserId,
            EvaluatedAtUtc = nowUtc
        };
    }
}

/// <summary>
/// Strictly append-only three-way link connecting posted bill lines to accepted GRNI accrual capacities and DEC-P2P-013 effects.
/// </summary>
public sealed class SupplierBillReceiptMatch
{
    public Guid MatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierBillId { get; private set; }
    public Guid SupplierBillLineId { get; private set; }
    public Guid ReceiptAccrualId { get; private set; }
    public Guid ReceiptAccrualLineId { get; private set; }
    public Guid ValuedAcceptanceLineId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid PoLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal MatchedBaseQuantity { get; private set; }
    public decimal ReceiptUnitCostBase { get; private set; }
    public decimal InvoiceUnitPriceBase { get; private set; }
    public decimal GrniClearedAmount { get; private set; }
    public decimal PriceVarianceAmount { get; private set; }
    public decimal CoveredBaseQuantity { get; private set; }
    public decimal InventoryAdjustmentAmount { get; private set; }
    public decimal PnlVarianceAmount { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SupplierBillReceiptMatch()
    {
    }

    public static SupplierBillReceiptMatch Record(
        Guid matchId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierBillId,
        Guid supplierBillLineId,
        Guid receiptAccrualId,
        Guid receiptAccrualLineId,
        Guid valuedAcceptanceLineId,
        Guid purchaseOrderId,
        Guid poLineId,
        Guid itemId,
        decimal matchedBaseQuantity,
        decimal receiptUnitCostBase,
        decimal invoiceUnitPriceBase,
        decimal grniClearedAmount,
        decimal priceVarianceAmount,
        decimal coveredBaseQuantity,
        decimal inventoryAdjustmentAmount,
        decimal pnlVarianceAmount,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(matchId, nameof(matchId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierBillId, nameof(supplierBillId));
        GuardValidation.RequireNonEmptyGuid(supplierBillLineId, nameof(supplierBillLineId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualId, nameof(receiptAccrualId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualLineId, nameof(receiptAccrualLineId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceLineId, nameof(valuedAcceptanceLineId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(poLineId, nameof(poLineId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        return new SupplierBillReceiptMatch
        {
            MatchId = matchId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierBillId = supplierBillId,
            SupplierBillLineId = supplierBillLineId,
            ReceiptAccrualId = receiptAccrualId,
            ReceiptAccrualLineId = receiptAccrualLineId,
            ValuedAcceptanceLineId = valuedAcceptanceLineId,
            PurchaseOrderId = purchaseOrderId,
            PoLineId = poLineId,
            ItemId = itemId,
            MatchedBaseQuantity = decimal.Round(matchedBaseQuantity, 6, MidpointRounding.AwayFromZero),
            ReceiptUnitCostBase = decimal.Round(receiptUnitCostBase, 6, MidpointRounding.AwayFromZero),
            InvoiceUnitPriceBase = decimal.Round(invoiceUnitPriceBase, 6, MidpointRounding.AwayFromZero),
            GrniClearedAmount = decimal.Round(grniClearedAmount, 4, MidpointRounding.AwayFromZero),
            PriceVarianceAmount = decimal.Round(priceVarianceAmount, 4, MidpointRounding.AwayFromZero),
            CoveredBaseQuantity = decimal.Round(coveredBaseQuantity, 6, MidpointRounding.AwayFromZero),
            InventoryAdjustmentAmount = decimal.Round(inventoryAdjustmentAmount, 4, MidpointRounding.AwayFromZero),
            PnlVarianceAmount = decimal.Round(pnlVarianceAmount, 4, MidpointRounding.AwayFromZero),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}

/// <summary>
/// Open Accounts Payable liability created atomically upon `TX-BILL` ready for S11 payment batching.
/// </summary>
public sealed class PayableItem
{
    public Guid PayableItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string PayableNumber { get; private set; } = string.Empty;
    public Guid SupplierBillId { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string SupplierInvoiceNumber { get; private set; } = string.Empty;
    public Guid GlJournalId { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public DateOnly PostingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal GrossBillAmount { get; private set; }
    public decimal WithholdingTaxAmount { get; private set; }
    public decimal NetPayableAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal CreditedAmount { get; private set; }
    public decimal RemainingOpenAmount { get; private set; }
    public string Status { get; private set; } = PayableStatuses.Open;
    public string PaymentBlockStatus { get; private set; } = PaymentBlockStatuses.None;
    public string? PaymentBlockReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private PayableItem()
    {
    }

    public static PayableItem Create(
        Guid payableItemId,
        Guid tenantId,
        Guid legalEntityId,
        string payableNumber,
        SupplierBill bill,
        Guid glJournalId,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(payableItemId, nameof(payableItemId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));
        ArgumentNullException.ThrowIfNull(bill);

        var net = decimal.Round(bill.NetPayableAmount, 4, MidpointRounding.AwayFromZero);
        if (net <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bill), "NetPayableAmount must be positive.");
        }

        return new PayableItem
        {
            PayableItemId = payableItemId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PayableNumber = GuardValidation.RequireCode(payableNumber, nameof(payableNumber), 40),
            SupplierBillId = bill.SupplierBillId,
            SupplierPartnerId = bill.SupplierPartnerId,
            SupplierCodeSnapshot = bill.SupplierCodeSnapshot,
            SupplierLegalNameSnapshot = bill.SupplierLegalNameSnapshot,
            SupplierInvoiceNumber = bill.SupplierInvoiceNumber,
            GlJournalId = glJournalId,
            CurrencyCode = bill.CurrencyCode,
            PostingDate = postingDate,
            DueDate = bill.DueDate,
            GrossBillAmount = bill.ComputedGrossTotal,
            WithholdingTaxAmount = bill.WithholdingTaxTotal,
            NetPayableAmount = net,
            PaidAmount = 0m,
            CreditedAmount = 0m,
            RemainingOpenAmount = net,
            Status = PayableStatuses.Open,
            PaymentBlockStatus = bill.PaymentBlockStatus,
            PaymentBlockReason = bill.PaymentBlockReason,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdatePaymentBlock(string targetBlockStatus, string reason, DateTimeOffset nowUtc)
    {
        var normalized = targetBlockStatus?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized is not (PaymentBlockStatuses.PaymentBlocked or PaymentBlockStatuses.Released))
        {
            throw new ArgumentException("PaymentBlockStatus must be PAYMENT_BLOCKED or RELEASED.", nameof(targetBlockStatus));
        }

        PaymentBlockStatus = normalized;
        PaymentBlockReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplySupplierCredit(decimal creditAmount, DateTimeOffset nowUtc)
    {
        if (PaidAmount > 0m)
        {
            throw new InvalidOperationException(
                "od05_paid_bill_return_policy_blocked: Paid or partially settled payables cannot be credited automatically until OD-05 refund policy is approved (ATH-CR-007).");
        }

        var amt = decimal.Round(creditAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(creditAmount), "Credit amount must be strictly positive.");
        }

        if (amt > RemainingOpenAmount)
        {
            throw new InvalidOperationException(
                $"credit_exceeds_open_payable: Credit amount ({amt:F4}) exceeds open payable balance ({RemainingOpenAmount:F4}).");
        }

        CreditedAmount = decimal.Round(CreditedAmount + amt, 4, MidpointRounding.AwayFromZero);
        RemainingOpenAmount = decimal.Round(NetPayableAmount - PaidAmount - CreditedAmount, 4, MidpointRounding.AwayFromZero);
        if (RemainingOpenAmount < 0m || NetPayableAmount != PaidAmount + CreditedAmount + RemainingOpenAmount)
        {
            throw new InvalidOperationException("payable_credit_conservation_failed: Payable amounts failed conservation.");
        }

        Status = RemainingOpenAmount == 0m ? PayableStatuses.Credited : PayableStatuses.Open;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class CreditSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private CreditSequence()
    {
    }
}

/// <summary>
/// Strictly append-only Supplier Credit Note created atomically when M3 valued billed returns execute.
/// </summary>
public sealed class SupplierCredit
{
    public Guid SupplierCreditId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string CreditNumber { get; private set; } = string.Empty;
    public Guid SupplierReturnId { get; private set; }
    public string ReturnNumberSnapshot { get; private set; } = string.Empty;
    public Guid SupplierBillId { get; private set; }
    public string BillNumberSnapshot { get; private set; } = string.Empty;
    public Guid PayableItemId { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string? SupplierNpwpSnapshot { get; private set; }
    public string? TaxReturnNoteNumber { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal SubtotalCreditAmount { get; private set; }
    public decimal PpnReversalAmount { get; private set; }
    public decimal TotalCreditAmount { get; private set; }
    public decimal InventoryCreditAmount { get; private set; }
    public decimal PpvDifferenceAmount { get; private set; }
    public Guid GlJournalId { get; private set; }
    public Guid StockMovementId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SupplierCredit()
    {
    }

    public static SupplierCredit Record(
        Guid supplierCreditId,
        Guid tenantId,
        Guid legalEntityId,
        string creditNumber,
        Guid supplierReturnId,
        string returnNumberSnapshot,
        SupplierBill bill,
        Guid payableItemId,
        string? taxReturnNoteNumber,
        DateOnly postingDate,
        decimal subtotalCreditAmount,
        decimal ppnReversalAmount,
        decimal totalCreditAmount,
        decimal inventoryCreditAmount,
        decimal ppvDifferenceAmount,
        Guid glJournalId,
        Guid stockMovementId,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(supplierCreditId, nameof(supplierCreditId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierReturnId, nameof(supplierReturnId));
        GuardValidation.RequireNonEmptyGuid(payableItemId, nameof(payableItemId));
        GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId));
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));
        ArgumentNullException.ThrowIfNull(bill);

        var sub = decimal.Round(subtotalCreditAmount, 4, MidpointRounding.AwayFromZero);
        var ppn = decimal.Round(ppnReversalAmount, 4, MidpointRounding.AwayFromZero);
        var total = decimal.Round(totalCreditAmount, 4, MidpointRounding.AwayFromZero);
        var inv = decimal.Round(inventoryCreditAmount, 4, MidpointRounding.AwayFromZero);
        var ppv = decimal.Round(ppvDifferenceAmount, 4, MidpointRounding.AwayFromZero);

        if (sub <= 0m || ppn < 0m || total != sub + ppn || inv <= 0m || sub - inv != ppv)
        {
            throw new InvalidOperationException(
                $"supplier_credit_balance_invalid: Credit amounts failed invariant (Sub={sub:F4}, PPN={ppn:F4}, Total={total:F4}, Inv={inv:F4}, PPV={ppv:F4}).");
        }

        return new SupplierCredit
        {
            SupplierCreditId = supplierCreditId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            CreditNumber = GuardValidation.RequireCode(creditNumber, nameof(creditNumber), 40),
            SupplierReturnId = supplierReturnId,
            ReturnNumberSnapshot = GuardValidation.RequireCode(returnNumberSnapshot, nameof(returnNumberSnapshot), 40),
            SupplierBillId = bill.SupplierBillId,
            BillNumberSnapshot = GuardValidation.RequireCode(bill.BillNumber ?? bill.DraftCode, nameof(bill.BillNumber), 40),
            PayableItemId = payableItemId,
            SupplierPartnerId = bill.SupplierPartnerId,
            SupplierCodeSnapshot = bill.SupplierCodeSnapshot,
            SupplierLegalNameSnapshot = bill.SupplierLegalNameSnapshot,
            SupplierNpwpSnapshot = bill.SupplierNpwpSnapshot,
            TaxReturnNoteNumber = GuardValidation.NormalizeOptional(taxReturnNoteNumber, 64, nameof(taxReturnNoteNumber)),
            PostingDate = postingDate,
            CurrencyCode = bill.CurrencyCode,
            SubtotalCreditAmount = sub,
            PpnReversalAmount = ppn,
            TotalCreditAmount = total,
            InventoryCreditAmount = inv,
            PpvDifferenceAmount = ppv,
            GlJournalId = glJournalId,
            StockMovementId = stockMovementId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}

/// <summary>
/// Strictly append-only Supplier Credit line linking AP credit allocation back to SupplierReturnLine & SupplierBillLine.
/// </summary>
public sealed class SupplierCreditLine
{
    public Guid SupplierCreditLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierCreditId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid SupplierReturnLineId { get; private set; }
    public Guid SupplierBillLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal CreditedBaseQuantity { get; private set; }
    public decimal UnitPriceBase { get; private set; }
    public decimal SubtotalCreditAmount { get; private set; }
    public decimal PpnReversalAmount { get; private set; }
    public decimal TotalCreditAmount { get; private set; }
    public decimal InventoryCreditAmount { get; private set; }
    public decimal PpvDifferenceAmount { get; private set; }

    private SupplierCreditLine()
    {
    }

    public static SupplierCreditLine Record(
        Guid supplierCreditLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierCreditId,
        int lineNumber,
        Guid supplierReturnLineId,
        Guid supplierBillLineId,
        Guid itemId,
        decimal creditedBaseQuantity,
        decimal unitPriceBase,
        decimal subtotalCreditAmount,
        decimal ppnReversalAmount,
        decimal totalCreditAmount,
        decimal inventoryCreditAmount,
        decimal ppvDifferenceAmount)
    {
        GuardValidation.RequireNonEmptyGuid(supplierCreditLineId, nameof(supplierCreditLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierCreditId, nameof(supplierCreditId));
        GuardValidation.RequireNonEmptyGuid(supplierReturnLineId, nameof(supplierReturnLineId));
        GuardValidation.RequireNonEmptyGuid(supplierBillLineId, nameof(supplierBillLineId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        var qty = decimal.Round(creditedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var price = decimal.Round(unitPriceBase, 6, MidpointRounding.AwayFromZero);
        var sub = decimal.Round(subtotalCreditAmount, 4, MidpointRounding.AwayFromZero);
        var ppn = decimal.Round(ppnReversalAmount, 4, MidpointRounding.AwayFromZero);
        var total = decimal.Round(totalCreditAmount, 4, MidpointRounding.AwayFromZero);
        var inv = decimal.Round(inventoryCreditAmount, 4, MidpointRounding.AwayFromZero);
        var ppv = decimal.Round(ppvDifferenceAmount, 4, MidpointRounding.AwayFromZero);

        if (qty <= 0m || price <= 0m || sub <= 0m || ppn < 0m || total != sub + ppn || inv <= 0m || sub - inv != ppv)
        {
            throw new InvalidOperationException("supplier_credit_line_invalid: Credit line amounts or quantities failed conservation.");
        }

        return new SupplierCreditLine
        {
            SupplierCreditLineId = supplierCreditLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierCreditId = supplierCreditId,
            LineNumber = lineNumber,
            SupplierReturnLineId = supplierReturnLineId,
            SupplierBillLineId = supplierBillLineId,
            ItemId = itemId,
            CreditedBaseQuantity = qty,
            UnitPriceBase = price,
            SubtotalCreditAmount = sub,
            PpnReversalAmount = ppn,
            TotalCreditAmount = total,
            InventoryCreditAmount = inv,
            PpvDifferenceAmount = ppv
        };
    }
}

public sealed record MatchedLineEvaluation(
    int LineNumber,
    Guid ReceiptAccrualLineId,
    Guid ValuedAcceptanceLineId,
    Guid GoodsReceiptLineId,
    Guid PoLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    decimal OrderedBaseQty,
    decimal RemainingUnbilledBaseQty,
    decimal BilledBaseQuantity,
    decimal PoUnitPriceFunctional,
    decimal ReceiptUnitCostBase,
    decimal InvoiceUnitPriceBase,
    decimal LineNetAmount,
    decimal GrniClearAmount,
    decimal PriceVarianceAmount,
    decimal CurrentPoolOnHandQty,
    decimal CurrentPoolCarryingValue,
    decimal CurrentPoolMovingAvgCost,
    decimal CoveredBaseQuantity,
    decimal UncoveredBaseQuantity,
    decimal InventoryAdjustmentAmount,
    decimal PnlVarianceAmount,
    decimal ProjectedMovingAvgCost,
    Guid PpnTaxCodeId,
    string PpnTaxCodeSnapshot,
    decimal PpnTaxAmount,
    Guid? WithholdingTaxCodeId,
    string? WithholdingTaxCodeSnapshot,
    decimal WithholdingTaxAmount,
    string DqOutcome,
    string PpOutcome,
    string? BlockingReason);

public sealed record InvoiceMatchComputation(
    string DqOutcome,
    string PpOutcome,
    string BdOutcome,
    string OverallMatchStatus,
    bool IsPostingBlocked,
    bool RequiresApproval,
    decimal LinesNetSubtotal,
    decimal TotalGrniClearAmount,
    decimal TotalPriceVarianceAmount,
    decimal TotalInventoryAdjAmount,
    decimal TotalPnlVarianceAmount,
    decimal DeclaredGrossAmount,
    decimal ComputedGrossTotal,
    decimal BdWriteOffAmount,
    decimal PpnTaxTotal,
    decimal WithholdingTaxTotal,
    decimal NetPayableAmount,
    IReadOnlyList<MatchedLineEvaluation> Lines,
    IReadOnlyList<string> BlockingReasons);

public sealed record MatchLineSourceInput(
    int LineNumber,
    Guid ReceiptAccrualLineId,
    Guid ValuedAcceptanceLineId,
    Guid GoodsReceiptLineId,
    Guid PoLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    decimal OrderedBaseQty,
    decimal RemainingUnbilledBaseQty,
    decimal RemainingOpenLineAmount,
    decimal ReceiptUnitCostBase,
    decimal PoUnitPriceFunctional,
    decimal BilledBaseQuantity,
    decimal InvoiceUnitPriceBase,
    decimal PoolOnHandQty,
    decimal PoolCarryingValue,
    decimal PoolMovingAvgCost,
    Guid PpnTaxCodeId,
    string PpnTaxCodeSnapshot,
    decimal PpnTaxAmount,
    bool PpnBlocked,
    string? PpnBlockReason,
    Guid? WithholdingTaxCodeId,
    string? WithholdingTaxCodeSnapshot,
    decimal WithholdingTaxAmount,
    bool WithholdingBlocked,
    string? WithholdingBlockReason);

/// <summary>
/// Pure decimal-safe 3-way match (PP/DQ/BD) & DEC-P2P-013 stock-coverage calculator.
/// Shared symmetrically between read-only previews and atomic TX-BILL execution.
/// </summary>
public static class InvoiceThreeWayMatchCalculator
{
    public static InvoiceMatchComputation Compute(
        InvoiceMatchPolicy policy,
        decimal declaredGrossAmount,
        IReadOnlyList<MatchLineSourceInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(inputs);

        var blockingReasons = new List<string>();
        var evaluatedLines = new List<MatchedLineEvaluation>(inputs.Count);

        if (inputs.Count == 0)
        {
            blockingReasons.Add("bill_lines_required: At least one matched receipt bill line is required.");
        }

        // Group running pool projections per item so multiple lines for the same SKU accumulate cleanly.
        var runningPool = new Dictionary<Guid, (decimal Qty, decimal Carrying, decimal Cost)>();
        var seenAccrualLines = new HashSet<Guid>();

        decimal linesNetSum = 0m;
        decimal grniClearSum = 0m;
        decimal priceVarSum = 0m;
        decimal invAdjSum = 0m;
        decimal pnlVarSum = 0m;
        decimal ppnSum = 0m;
        decimal whSum = 0m;

        var anyDqBlocked = false;
        var anyPpApprovalRequired = false;
        var anyPpWithinTolerance = false;

        foreach (var src in inputs)
        {
            if (!seenAccrualLines.Add(src.ReceiptAccrualLineId))
            {
                anyDqBlocked = true;
                blockingReasons.Add($"duplicate_accrual_line_in_bill: Receipt accrual line on '{src.ItemCode}' cannot appear more than once on the same bill.");
            }

            if (!runningPool.TryGetValue(src.ItemId, out var poolState))
            {
                poolState = (
                    decimal.Round(src.PoolOnHandQty, 6, MidpointRounding.AwayFromZero),
                    decimal.Round(src.PoolCarryingValue, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(src.PoolMovingAvgCost, 6, MidpointRounding.AwayFromZero));
            }

            var qty = decimal.Round(src.BilledBaseQuantity, 6, MidpointRounding.AwayFromZero);
            var invUnit = decimal.Round(src.InvoiceUnitPriceBase, 6, MidpointRounding.AwayFromZero);
            var lineNet = decimal.Round(qty * invUnit, 4, MidpointRounding.AwayFromZero);
            if (qty <= 0m || invUnit <= 0m || lineNet <= 0m)
            {
                anyDqBlocked = true;
                blockingReasons.Add($"invalid_bill_line_values: Line #{src.LineNumber} ({src.ItemCode}) requires positive quantity, unit price, and net amount.");
            }

            string? lineBlock = null;
            var dqOutcome = MatchDimensionOutcomes.Matched;
            decimal grniClear;
            if (qty > src.RemainingUnbilledBaseQty || qty <= 0m)
            {
                dqOutcome = MatchDimensionOutcomes.Blocked;
                anyDqBlocked = true;
                lineBlock = $"dq_exceeds_unbilled_accepted_capacity: Line #{src.LineNumber} ({src.ItemCode}) bills {qty:0.######} vs remaining unbilled accepted capacity {src.RemainingUnbilledBaseQty:0.######}.";
                blockingReasons.Add(lineBlock);
                grniClear = 0m;
            }
            else if (qty == src.RemainingUnbilledBaseQty)
            {
                grniClear = decimal.Round(src.RemainingOpenLineAmount, 4, MidpointRounding.AwayFromZero);
            }
            else
            {
                grniClear = decimal.Round(qty * src.ReceiptUnitCostBase, 4, MidpointRounding.AwayFromZero);
                if (grniClear <= 0m) grniClear = 0.0001m;
                if (grniClear >= src.RemainingOpenLineAmount)
                {
                    grniClear = decimal.Round(src.RemainingOpenLineAmount - 0.0001m, 4, MidpointRounding.AwayFromZero);
                }
            }

            if (src.PpnBlocked)
            {
                blockingReasons.Add(src.PpnBlockReason ?? $"ppn_tax_blocked: Line #{src.LineNumber} PPN tax code failed OD-07 validation.");
            }

            if (src.WithholdingBlocked)
            {
                blockingReasons.Add(src.WithholdingBlockReason ?? $"pph_withholding_blocked: Line #{src.LineNumber} withholding tax code failed OD-07 validation.");
            }

            var priceVariance = dqOutcome == MatchDimensionOutcomes.Blocked
                ? 0m
                : decimal.Round(lineNet - grniClear, 4, MidpointRounding.AwayFromZero);

            var ppOutcome = MatchDimensionOutcomes.Matched;
            if (priceVariance != 0m && dqOutcome != MatchDimensionOutcomes.Blocked)
            {
                var unitDiff = Math.Abs(invUnit - src.ReceiptUnitCostBase);
                var withinPct = policy.ReviewStatus == InvoiceMatchPolicyReviewStatuses.ReviewedInternal &&
                    policy.PpTolerancePercent > 0m &&
                    unitDiff <= decimal.Round(src.ReceiptUnitCostBase * (policy.PpTolerancePercent / 100m), 6, MidpointRounding.AwayFromZero);
                var withinAmt = policy.ReviewStatus == InvoiceMatchPolicyReviewStatuses.ReviewedInternal &&
                    policy.PpToleranceAmount > 0m &&
                    Math.Abs(priceVariance) <= policy.PpToleranceAmount;

                var autoTolAllowed =
                    policy.ReviewStatus == InvoiceMatchPolicyReviewStatuses.ReviewedInternal &&
                    (policy.PpTolerancePercent > 0m || policy.PpToleranceAmount > 0m) &&
                    (policy.PpTolerancePercent == 0m || withinPct) &&
                    (policy.PpToleranceAmount == 0m || withinAmt);

                if (autoTolAllowed)
                {
                    ppOutcome = MatchDimensionOutcomes.WithinTolerance;
                    anyPpWithinTolerance = true;
                }
                else
                {
                    ppOutcome = MatchDimensionOutcomes.VarianceApprovalRequired;
                    anyPpApprovalRequired = true;
                }
            }

            decimal coveredQty = 0m;
            decimal uncoveredQty = 0m;
            decimal invAdj = 0m;
            decimal pnlVar = 0m;
            var projectedCost = poolState.Cost;

            if (priceVariance != 0m && dqOutcome != MatchDimensionOutcomes.Blocked)
            {
                try
                {
                    var step = Inventory.ValuationPool.SimulateInvoicePriceAdjustment(
                        Inventory.ValuationPoolStatuses.Active,
                        poolState.Qty,
                        poolState.Carrying,
                        poolState.Cost,
                        0L,
                        qty,
                        priceVariance);
                    coveredQty = step.CoveredBaseQuantity;
                    uncoveredQty = step.UncoveredBaseQuantity;
                    invAdj = step.InventoryAdjustmentAmount;
                    pnlVar = step.PnlVarianceAmount;
                    projectedCost = step.ValuationStep.NewMovingAverageCost;
                    poolState = (poolState.Qty, step.ValuationStep.NewCarryingValue, projectedCost);
                }
                catch (InvalidOperationException ex)
                {
                    lineBlock ??= ex.Message;
                    blockingReasons.Add(ex.Message);
                }
            }

            runningPool[src.ItemId] = poolState;

            linesNetSum += lineNet;
            grniClearSum += grniClear;
            priceVarSum += priceVariance;
            invAdjSum += invAdj;
            pnlVarSum += pnlVar;
            ppnSum += decimal.Round(src.PpnTaxAmount, 4, MidpointRounding.AwayFromZero);
            whSum += decimal.Round(src.WithholdingTaxAmount, 4, MidpointRounding.AwayFromZero);

            evaluatedLines.Add(new MatchedLineEvaluation(
                src.LineNumber,
                src.ReceiptAccrualLineId,
                src.ValuedAcceptanceLineId,
                src.GoodsReceiptLineId,
                src.PoLineId,
                src.ItemId,
                src.ItemCode,
                src.ItemName,
                src.BaseUomId,
                src.BaseUomCode,
                src.OrderedBaseQty,
                src.RemainingUnbilledBaseQty,
                qty,
                src.PoUnitPriceFunctional,
                src.ReceiptUnitCostBase,
                invUnit,
                lineNet,
                grniClear,
                priceVariance,
                src.PoolOnHandQty,
                src.PoolCarryingValue,
                src.PoolMovingAvgCost,
                coveredQty,
                uncoveredQty,
                invAdj,
                pnlVar,
                projectedCost,
                src.PpnTaxCodeId,
                src.PpnTaxCodeSnapshot,
                decimal.Round(src.PpnTaxAmount, 4, MidpointRounding.AwayFromZero),
                src.WithholdingTaxCodeId,
                src.WithholdingTaxCodeSnapshot,
                decimal.Round(src.WithholdingTaxAmount, 4, MidpointRounding.AwayFromZero),
                dqOutcome,
                ppOutcome,
                lineBlock));
        }

        var declared = decimal.Round(declaredGrossAmount, 4, MidpointRounding.AwayFromZero);
        var computedGross = decimal.Round(linesNetSum + ppnSum, 4, MidpointRounding.AwayFromZero);
        var bdDiff = decimal.Round(declared - computedGross, 4, MidpointRounding.AwayFromZero);
        var bdOutcome = MatchDimensionOutcomes.Matched;
        var bdWriteOff = 0m;

        if (bdDiff != 0m)
        {
            var allowBd = policy.ReviewStatus == InvoiceMatchPolicyReviewStatuses.ReviewedInternal &&
                policy.BdWriteOffAccountId.HasValue &&
                policy.BdWriteOffAccountId.Value != Guid.Empty &&
                Math.Abs(bdDiff) <= policy.BdWriteOffLimitAmount;

            if (allowBd)
            {
                bdOutcome = MatchDimensionOutcomes.WithinTolerance;
                bdWriteOff = bdDiff;
            }
            else
            {
                bdOutcome = MatchDimensionOutcomes.Blocked;
                blockingReasons.Add(
                    $"bd_header_difference_blocked: Declared gross ({declared:N0} IDR) differs from computed subtotal+PPN ({computedGross:N0} IDR) by {bdDiff:N0} IDR under OD-03.");
            }
        }

        var netPayable = decimal.Round(declared - whSum, 4, MidpointRounding.AwayFromZero);
        if (netPayable <= 0m)
        {
            blockingReasons.Add("withholding_exceeds_gross_bill: Net AP payable after withholding tax must be strictly positive.");
        }

        var dqOverall = anyDqBlocked ? MatchDimensionOutcomes.Blocked : MatchDimensionOutcomes.Matched;
        var ppOverall = anyPpApprovalRequired
            ? MatchDimensionOutcomes.VarianceApprovalRequired
            : anyPpWithinTolerance
                ? MatchDimensionOutcomes.WithinTolerance
                : MatchDimensionOutcomes.Matched;

        var blocked = blockingReasons.Count > 0 || dqOverall == MatchDimensionOutcomes.Blocked || bdOutcome == MatchDimensionOutcomes.Blocked;
        var overall = blocked
            ? InvoiceMatchStatuses.PostingBlocked
            : ppOverall == MatchDimensionOutcomes.VarianceApprovalRequired
                ? InvoiceMatchStatuses.ApprovalRequired
                : (ppOverall == MatchDimensionOutcomes.WithinTolerance || bdOutcome == MatchDimensionOutcomes.WithinTolerance)
                    ? InvoiceMatchStatuses.WithinTolerance
                    : InvoiceMatchStatuses.Matched;

        return new InvoiceMatchComputation(
            dqOverall,
            ppOverall,
            bdOutcome,
            overall,
            blocked,
            ppOverall == MatchDimensionOutcomes.VarianceApprovalRequired,
            decimal.Round(linesNetSum, 4, MidpointRounding.AwayFromZero),
            decimal.Round(grniClearSum, 4, MidpointRounding.AwayFromZero),
            decimal.Round(priceVarSum, 4, MidpointRounding.AwayFromZero),
            decimal.Round(invAdjSum, 4, MidpointRounding.AwayFromZero),
            decimal.Round(pnlVarSum, 4, MidpointRounding.AwayFromZero),
            declared,
            computedGross,
            bdWriteOff,
            decimal.Round(ppnSum, 4, MidpointRounding.AwayFromZero),
            decimal.Round(whSum, 4, MidpointRounding.AwayFromZero),
            netPayable,
            evaluatedLines,
            blockingReasons);
    }
}
