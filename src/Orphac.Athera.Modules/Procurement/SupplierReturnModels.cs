using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Procurement;

public static class SupplierReturnCaseTypes
{
    public const string UnvaluedDockRejection = "UNVALUED_DOCK_REJECTION";
    public const string ValuedUnbilledReturn = "VALUED_UNBILLED_RETURN";
    public const string ValuedBilledReturn = "VALUED_BILLED_RETURN";

    public static readonly IReadOnlyList<string> All =
        [UnvaluedDockRejection, ValuedUnbilledReturn, ValuedBilledReturn];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported supplier return case type '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReturnResolutionModes
{
    public const string CloseWithoutReplacement = "CLOSE_WITHOUT_REPLACEMENT";
    public const string ReplacementOnly = "REPLACEMENT_ONLY";
    public const string ApCreditNote = "AP_CREDIT_NOTE";

    public static readonly IReadOnlyList<string> All =
        [CloseWithoutReplacement, ReplacementOnly, ApCreditNote];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported return resolution mode '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReturnMatrixDispositions
{
    public const string Supported = "SUPPORTED";
    public const string BlockedPolicyOd05 = "BLOCKED_POLICY_OD05";
    public const string BlockedPolicyOd07 = "BLOCKED_POLICY_OD07";
    public const string BlockedClosedPeriod = "BLOCKED_CLOSED_PERIOD";

    public static readonly IReadOnlyList<string> All =
        [Supported, BlockedPolicyOd05, BlockedPolicyOd07, BlockedClosedPeriod];
}

public static class SupplierReturnStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Executed = "EXECUTED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All =
        [Draft, PendingApproval, Approved, Executed, Cancelled];
}

public static class ReplacementAuthorizationStatuses
{
    public const string Active = "ACTIVE";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Fulfilled = "FULFILLED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All =
        [Active, PartiallyReceived, Fulfilled, Cancelled];
}

public static class SupplierReturnReasonCodes
{
    public const string DamagedOrDefective = "DAMAGED_OR_DEFECTIVE";
    public const string WrongItemOrSpec = "WRONG_ITEM_OR_SPEC";
    public const string OverdeliveryReturn = "OVERDELIVERY_RETURN";
    public const string ExpiredOrShortShelfLife = "EXPIRED_OR_SHORT_SHELF_LIFE";
    public const string CommercialAgreementReturn = "COMMERCIAL_AGREEMENT_RETURN";

    public static readonly IReadOnlyList<string> All =
        [DamagedOrDefective, WrongItemOrSpec, OverdeliveryReturn, ExpiredOrShortShelfLife, CommercialAgreementReturn];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? DamagedOrDefective : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported supplier return reason code '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReturnPolicyDefaults
{
    public const string SupplierReturnApprovalPolicyCode = "SUPPLIER_RETURN_AUTHORIZATION";
}

public sealed class ReturnSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ReturnSequence()
    {
    }
}

public sealed class ReplacementSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ReplacementSequence()
    {
    }
}

/// <summary>
/// Commercial and financial return/correction header in `procurement.supplier_returns` (`ATH-S10`).
/// Keeps four-eyes approval (`OD-02`), explicit support matrix disposition (`OD-05`/`OD-07`), and immutable executed state.
/// </summary>
public sealed class SupplierReturn
{
    public Guid SupplierReturnId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DraftCode { get; private set; } = string.Empty;
    public string? ReturnNumber { get; private set; }
    public string ReturnCaseType { get; private set; } = SupplierReturnCaseTypes.UnvaluedDockRejection;
    public string ResolutionMode { get; private set; } = ReturnResolutionModes.ReplacementOnly;
    public string MatrixDisposition { get; private set; } = ReturnMatrixDispositions.Supported;
    public string? MatrixBlockingCode { get; private set; }
    public string? MatrixBlockingReason { get; private set; }

    public Guid PurchaseOrderId { get; private set; }
    public string PoNumberSnapshot { get; private set; } = string.Empty;
    public Guid? GoodsReceiptId { get; private set; }
    public string? ReceiptNumberSnapshot { get; private set; }
    public Guid? ReceiptAccrualId { get; private set; }
    public Guid? SupplierBillId { get; private set; }
    public string? BillNumberSnapshot { get; private set; }
    public Guid? PayableItemId { get; private set; }

    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string? SupplierNpwpSnapshot { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";

    public DateOnly ReturnDate { get; private set; }
    public string ReasonCode { get; private set; } = SupplierReturnReasonCodes.DamagedOrDefective;
    public string? CarrierOrDispatchRef { get; private set; }
    public string? TaxReturnNoteNumber { get; private set; }

    public string Status { get; private set; } = SupplierReturnStatuses.Draft;
    public string ApprovalPolicyCode { get; private set; } = ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode;
    public string? LastApprovalDecision { get; private set; }
    public string? LastRejectionReason { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ExecutedByUserId { get; private set; }
    public DateTimeOffset? ExecutedAtUtc { get; private set; }
    public string? ExecuteIdempotencyKey { get; private set; }

    public Guid? StockMovementId { get; private set; }
    public Guid? GlJournalId { get; private set; }
    public Guid? SupplierCreditId { get; private set; }

    public decimal TotalReturnedBaseQuantity { get; private set; }
    public decimal TotalInventoryCreditAmount { get; private set; }
    public decimal TotalGrniReversalAmount { get; private set; }
    public decimal TotalCreditSubtotalAmount { get; private set; }
    public decimal TotalPpnReversalAmount { get; private set; }
    public decimal TotalCreditNoteAmount { get; private set; }
    public decimal TotalPpvDifferenceAmount { get; private set; }

    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SupplierReturn()
    {
    }

    public static SupplierReturn CreateDraft(
        Guid supplierReturnId,
        Guid tenantId,
        Guid legalEntityId,
        string draftCode,
        string returnCaseType,
        string resolutionMode,
        SupplierReturnMatrixResult matrix,
        PurchaseOrder po,
        Partner partner,
        Guid? goodsReceiptId,
        string? receiptNumberSnapshot,
        Guid? receiptAccrualId,
        Guid? supplierBillId,
        string? billNumberSnapshot,
        Guid? payableItemId,
        DateOnly returnDate,
        string reasonCode,
        string? carrierOrDispatchRef,
        string? taxReturnNoteNumber,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(supplierReturnId, nameof(supplierReturnId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(po);
        ArgumentNullException.ThrowIfNull(partner);

        var header = new SupplierReturn
        {
            SupplierReturnId = supplierReturnId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            DraftCode = GuardValidation.RequireCode(draftCode, nameof(draftCode), 40),
            ReturnCaseType = SupplierReturnCaseTypes.Normalize(returnCaseType),
            ResolutionMode = ReturnResolutionModes.Normalize(resolutionMode),
            PurchaseOrderId = po.PurchaseOrderId,
            PoNumberSnapshot = GuardValidation.RequireCode(po.PoNumber ?? po.DraftCode, nameof(po.PoNumber), 40),
            GoodsReceiptId = goodsReceiptId,
            ReceiptNumberSnapshot = GuardValidation.NormalizeOptional(receiptNumberSnapshot, 40, nameof(receiptNumberSnapshot)),
            ReceiptAccrualId = receiptAccrualId,
            SupplierBillId = supplierBillId,
            BillNumberSnapshot = GuardValidation.NormalizeOptional(billNumberSnapshot, 40, nameof(billNumberSnapshot)),
            PayableItemId = payableItemId,
            SupplierPartnerId = po.SupplierPartnerId,
            SupplierCodeSnapshot = partner.PartnerCode,
            SupplierLegalNameSnapshot = partner.LegalName,
            SupplierNpwpSnapshot = GuardValidation.NormalizeOptional(partner.TaxRegistrationNpwp, 64, nameof(partner.TaxRegistrationNpwp)),
            CurrencyCode = po.CurrencyCode,
            ReturnDate = returnDate,
            ReasonCode = SupplierReturnReasonCodes.Normalize(reasonCode),
            CarrierOrDispatchRef = GuardValidation.NormalizeOptional(carrierOrDispatchRef, 120, nameof(carrierOrDispatchRef)),
            TaxReturnNoteNumber = GuardValidation.NormalizeOptional(taxReturnNoteNumber, 64, nameof(taxReturnNoteNumber)),
            Status = SupplierReturnStatuses.Draft,
            ApprovalPolicyCode = ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode,
            Notes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes)),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };

        header.ApplyMatrixEvaluation(matrix, nowUtc, incrementVersion: false);
        return header;
    }

    public void UpdateDraft(
        string resolutionMode,
        SupplierReturnMatrixResult matrix,
        DateOnly returnDate,
        string reasonCode,
        string? carrierOrDispatchRef,
        string? taxReturnNoteNumber,
        string? notes,
        DateTimeOffset nowUtc)
    {
        if (Status != SupplierReturnStatuses.Draft)
        {
            throw new InvalidOperationException(
                $"return_not_editable_in_status: Only DRAFT supplier returns can be edited (current: '{Status}').");
        }

        ResolutionMode = ReturnResolutionModes.Normalize(resolutionMode);
        ReturnDate = returnDate;
        ReasonCode = SupplierReturnReasonCodes.Normalize(reasonCode);
        CarrierOrDispatchRef = GuardValidation.NormalizeOptional(carrierOrDispatchRef, 120, nameof(carrierOrDispatchRef));
        TaxReturnNoteNumber = GuardValidation.NormalizeOptional(taxReturnNoteNumber, 64, nameof(taxReturnNoteNumber));
        Notes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes));
        ApplyMatrixEvaluation(matrix, nowUtc, incrementVersion: true);
    }

    public void Submit(SupplierReturnMatrixResult matrix, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != SupplierReturnStatuses.Draft)
        {
            throw new InvalidOperationException(
                $"return_invalid_status_for_submit: Only DRAFT returns may be submitted (current: '{Status}').");
        }

        ApplyMatrixEvaluation(matrix, nowUtc, incrementVersion: false);
        EnsureMatrixPermitsProgression(requireOpenPeriodCheck: false);

        if (ReturnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn &&
            TotalPpnReversalAmount > 0m &&
            string.IsNullOrWhiteSpace(TaxReturnNoteNumber))
        {
            throw new InvalidOperationException(
                "od07_nota_retur_required: Posting or submitting a PPN input tax reversal requires a valid supplier Tax Return Note (Nota Retur) number under OD-07.");
        }

        Status = SupplierReturnStatuses.PendingApproval;
        SubmittedByUserId = actorUserId;
        SubmittedAtUtc = nowUtc;
        LastApprovalDecision = null;
        LastRejectionReason = null;
        ApprovedByUserId = null;
        ApprovedAtUtc = null;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Decide(string decision, string reason, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != SupplierReturnStatuses.PendingApproval)
        {
            throw new InvalidOperationException(
                $"return_not_pending_approval: Return must be PENDING_APPROVAL to decide (current: '{Status}').");
        }

        if (SubmittedByUserId == actorUserId)
        {
            throw new InvalidOperationException(
                "self_approval_forbidden: Four-eyes Segregation of Duties (OD-02) prohibits the submitter from approving or rejecting their own return.");
        }

        var normalized = decision?.Trim().ToUpperInvariant() ?? string.Empty;
        var cleanReason = GuardValidation.RequireName(reason, nameof(reason), 300);

        if (normalized == "REJECTED")
        {
            Status = SupplierReturnStatuses.Draft;
            LastApprovalDecision = "REJECTED";
            LastRejectionReason = cleanReason;
            ApprovedByUserId = null;
            ApprovedAtUtc = null;
            UpdatedAtUtc = nowUtc;
            Version++;
            return;
        }

        if (normalized != "APPROVED")
        {
            throw new ArgumentException("Decision must be APPROVED or REJECTED.", nameof(decision));
        }

        EnsureMatrixPermitsProgression(requireOpenPeriodCheck: false);
        Status = SupplierReturnStatuses.Approved;
        LastApprovalDecision = "APPROVED";
        LastRejectionReason = null;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        if (Status is SupplierReturnStatuses.Executed or SupplierReturnStatuses.Cancelled)
        {
            throw new InvalidOperationException($"return_cannot_cancel: Return is already '{Status}'.");
        }

        if (Status != SupplierReturnStatuses.Draft)
        {
            throw new InvalidOperationException("return_cancel_draft_only: Return to DRAFT before cancelling.");
        }

        Status = SupplierReturnStatuses.Cancelled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ValidateExecutePreconditions(SupplierReturnMatrixResult matrix, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 80)
        {
            throw new ArgumentException("Provide an idempotency key between 1 and 80 characters.", nameof(idempotencyKey));
        }

        if (Status != SupplierReturnStatuses.Approved)
        {
            throw new InvalidOperationException(
                $"return_must_be_approved_before_execute: Supplier return must pass four-eyes approval before execution (current: '{Status}').");
        }

        if (SubmittedByUserId.HasValue && ApprovedByUserId.HasValue && SubmittedByUserId == ApprovedByUserId)
        {
            throw new InvalidOperationException(
                "self_approval_forbidden: Four-eyes Segregation of Duties (OD-02) prohibits execution when submitter equals approver.");
        }

        ApplyMatrixEvaluation(matrix, UpdatedAtUtc, incrementVersion: false);
        EnsureMatrixPermitsProgression(requireOpenPeriodCheck: true);

        if (ReturnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn &&
            TotalPpnReversalAmount > 0m &&
            string.IsNullOrWhiteSpace(TaxReturnNoteNumber))
        {
            throw new InvalidOperationException(
                "od07_nota_retur_required: PPN input tax reversal requires a valid Nota Retur document reference under OD-07.");
        }
    }

    public void MarkExecuted(
        string returnNumber,
        string idempotencyKey,
        SupplierReturnMatrixResult matrix,
        Guid? stockMovementId,
        Guid? glJournalId,
        Guid? supplierCreditId,
        Guid executedByUserId,
        DateTimeOffset nowUtc)
    {
        ValidateExecutePreconditions(matrix, idempotencyKey);
        GuardValidation.RequireNonEmptyGuid(executedByUserId, nameof(executedByUserId));

        ReturnNumber = GuardValidation.RequireCode(returnNumber, nameof(returnNumber), 40);
        ExecuteIdempotencyKey = idempotencyKey.Trim();
        StockMovementId = stockMovementId;
        GlJournalId = glJournalId;
        SupplierCreditId = supplierCreditId;
        ExecutedByUserId = executedByUserId;
        ExecutedAtUtc = nowUtc;
        Status = SupplierReturnStatuses.Executed;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    private void ApplyMatrixEvaluation(SupplierReturnMatrixResult matrix, DateTimeOffset nowUtc, bool incrementVersion)
    {
        MatrixDisposition = matrix.Disposition;
        MatrixBlockingCode = matrix.BlockingCode;
        MatrixBlockingReason = GuardValidation.NormalizeOptional(matrix.BlockingReason, 400, nameof(matrix.BlockingReason));
        TotalReturnedBaseQuantity = decimal.Round(matrix.TotalReturnedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        TotalInventoryCreditAmount = decimal.Round(matrix.TotalInventoryCreditAmount, 4, MidpointRounding.AwayFromZero);
        TotalGrniReversalAmount = decimal.Round(matrix.TotalGrniReversalAmount, 4, MidpointRounding.AwayFromZero);
        TotalCreditSubtotalAmount = decimal.Round(matrix.TotalCreditSubtotalAmount, 4, MidpointRounding.AwayFromZero);
        TotalPpnReversalAmount = decimal.Round(matrix.TotalPpnReversalAmount, 4, MidpointRounding.AwayFromZero);
        TotalCreditNoteAmount = decimal.Round(matrix.TotalCreditNoteAmount, 4, MidpointRounding.AwayFromZero);
        TotalPpvDifferenceAmount = decimal.Round(matrix.TotalPpvDifferenceAmount, 4, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc;
        if (incrementVersion)
        {
            Version++;
        }
    }

    private void EnsureMatrixPermitsProgression(bool requireOpenPeriodCheck)
    {
        if (MatrixDisposition == ReturnMatrixDispositions.BlockedPolicyOd05 ||
            MatrixDisposition == ReturnMatrixDispositions.BlockedPolicyOd07)
        {
            throw new InvalidOperationException($"{MatrixBlockingCode}: {MatrixBlockingReason}");
        }

        if (requireOpenPeriodCheck && MatrixDisposition == ReturnMatrixDispositions.BlockedClosedPeriod)
        {
            throw new InvalidOperationException($"{MatrixBlockingCode}: {MatrixBlockingReason}");
        }

        if (MatrixDisposition != ReturnMatrixDispositions.Supported &&
            !(MatrixDisposition == ReturnMatrixDispositions.BlockedClosedPeriod && !requireOpenPeriodCheck))
        {
            throw new InvalidOperationException(
                $"{MatrixBlockingCode ?? "return_matrix_blocked"}: {MatrixBlockingReason ?? "Return is blocked by policy matrix."}");
        }
    }
}

/// <summary>
/// Line item of a SupplierReturn (`procurement.supplier_return_lines`) retaining immutable link to originating PO/GRN/Accrual/Bill lines.
/// </summary>
public sealed class SupplierReturnLine
{
    public Guid SupplierReturnLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierReturnId { get; private set; }
    public int LineNumber { get; private set; }

    public Guid GoodsReceiptLineId { get; private set; }
    public Guid? ValuedAcceptanceLineId { get; private set; }
    public Guid? ReceiptAccrualLineId { get; private set; }
    public Guid? SupplierBillLineId { get; private set; }
    public Guid PoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;

    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;

    public Guid? SourceLocationId { get; private set; }
    public Guid? SourceStockLotId { get; private set; }
    public string? SourceStockStatus { get; private set; }

    public decimal ReturnedBaseQuantity { get; private set; }
    public decimal AuthorizedUnitCostBase { get; private set; }
    public decimal InventoryCreditAmount { get; private set; }
    public decimal GrniReversalAmount { get; private set; }
    public decimal CreditSubtotalAmount { get; private set; }
    public decimal PpnReversalAmount { get; private set; }
    public decimal TotalCreditAmount { get; private set; }
    public decimal PpvDifferenceAmount { get; private set; }
    public string? LineNotes { get; private set; }

    private SupplierReturnLine()
    {
    }

    public static SupplierReturnLine Create(
        Guid supplierReturnLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierReturnId,
        int lineNumber,
        SupplierReturnEvaluatedLine eval,
        string? lineNotes)
    {
        GuardValidation.RequireNonEmptyGuid(supplierReturnLineId, nameof(supplierReturnLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierReturnId, nameof(supplierReturnId));
        ArgumentNullException.ThrowIfNull(eval);

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        return new SupplierReturnLine
        {
            SupplierReturnLineId = supplierReturnLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierReturnId = supplierReturnId,
            LineNumber = lineNumber,
            GoodsReceiptLineId = eval.GoodsReceiptLineId,
            ValuedAcceptanceLineId = eval.ValuedAcceptanceLineId,
            ReceiptAccrualLineId = eval.ReceiptAccrualLineId,
            SupplierBillLineId = eval.SupplierBillLineId,
            PoLineId = eval.PoLineId,
            LineageLineKey = eval.LineageLineKey,
            ItemId = eval.ItemId,
            ItemCodeSnapshot = eval.ItemCode,
            ItemNameSnapshot = eval.ItemName,
            BaseUomId = eval.BaseUomId,
            BaseUomCodeSnapshot = eval.BaseUomCode,
            SourceLocationId = eval.SourceLocationId,
            SourceStockLotId = eval.SourceStockLotId,
            SourceStockStatus = eval.SourceStockStatus,
            ReturnedBaseQuantity = eval.ReturnedBaseQuantity,
            AuthorizedUnitCostBase = eval.AuthorizedUnitCostBase,
            InventoryCreditAmount = eval.InventoryCreditAmount,
            GrniReversalAmount = eval.GrniReversalAmount,
            CreditSubtotalAmount = eval.CreditSubtotalAmount,
            PpnReversalAmount = eval.PpnReversalAmount,
            TotalCreditAmount = eval.TotalCreditAmount,
            PpvDifferenceAmount = eval.PpvDifferenceAmount,
            LineNotes = GuardValidation.NormalizeOptional(lineNotes, 240, nameof(lineNotes))
        };
    }
}

/// <summary>
/// Linked replacement entitlement (`procurement.replacement_authorizations`, `RPA-YYYY-NNNNN`).
/// Commercial incremental price stays `0`, while `AuthorizedInventoryUnitCostBase` remains strictly positive (`BRS §13`).
/// </summary>
public sealed class ReplacementAuthorization
{
    public Guid ReplacementAuthorizationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string AuthorizationNumber { get; private set; } = string.Empty;
    public Guid SupplierReturnId { get; private set; }
    public Guid SupplierReturnLineId { get; private set; }
    public string SourceCaseType { get; private set; } = SupplierReturnCaseTypes.UnvaluedDockRejection;
    public Guid PurchaseOrderId { get; private set; }
    public Guid PoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;
    public Guid? SourceGoodsReceiptId { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal AuthorizedBaseQuantity { get; private set; }
    public decimal DeliveredBaseQuantity { get; private set; }
    public decimal AcceptedBaseQuantity { get; private set; }
    public decimal RemainingBaseQuantity { get; private set; }
    public decimal CommercialIncrementalUnitPrice { get; private set; }
    public decimal AuthorizedInventoryUnitCostBase { get; private set; }
    public string Status { get; private set; } = ReplacementAuthorizationStatuses.Active;
    public Guid ApprovedByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ReplacementAuthorization()
    {
    }

    public static ReplacementAuthorization CreateFromReturnLine(
        Guid replacementAuthorizationId,
        Guid tenantId,
        Guid legalEntityId,
        string authorizationNumber,
        SupplierReturn parentReturn,
        SupplierReturnLine returnLine,
        decimal authorizedInventoryUnitCostBase,
        Guid approvedByUserId,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(replacementAuthorizationId, nameof(replacementAuthorizationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(approvedByUserId, nameof(approvedByUserId));
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(parentReturn);
        ArgumentNullException.ThrowIfNull(returnLine);

        var authQty = decimal.Round(returnLine.ReturnedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var invCost = decimal.Round(authorizedInventoryUnitCostBase, 6, MidpointRounding.AwayFromZero);
        if (authQty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(returnLine), "Authorized replacement quantity must be positive.");
        }

        if (invCost <= 0m)
        {
            throw new InvalidOperationException(
                "od04_zero_or_negative_replacement_cost_blocked: BRS §13 forbids zero inventory valuation on linked replacements; positive authorized unit cost is required.");
        }

        return new ReplacementAuthorization
        {
            ReplacementAuthorizationId = replacementAuthorizationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            AuthorizationNumber = GuardValidation.RequireCode(authorizationNumber, nameof(authorizationNumber), 40),
            SupplierReturnId = parentReturn.SupplierReturnId,
            SupplierReturnLineId = returnLine.SupplierReturnLineId,
            SourceCaseType = parentReturn.ReturnCaseType,
            PurchaseOrderId = parentReturn.PurchaseOrderId,
            PoLineId = returnLine.PoLineId,
            LineageLineKey = returnLine.LineageLineKey,
            SourceGoodsReceiptId = parentReturn.GoodsReceiptId,
            SupplierPartnerId = parentReturn.SupplierPartnerId,
            ItemId = returnLine.ItemId,
            BaseUomId = returnLine.BaseUomId,
            BaseUomCodeSnapshot = returnLine.BaseUomCodeSnapshot,
            AuthorizedBaseQuantity = authQty,
            DeliveredBaseQuantity = 0m,
            AcceptedBaseQuantity = 0m,
            RemainingBaseQuantity = authQty,
            CommercialIncrementalUnitPrice = 0m,
            AuthorizedInventoryUnitCostBase = invCost,
            Status = ReplacementAuthorizationStatuses.Active,
            ApprovedByUserId = approvedByUserId,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void RecordDelivered(decimal deliveredBaseQty, DateTimeOffset nowUtc)
    {
        if (Status is not (ReplacementAuthorizationStatuses.Active or ReplacementAuthorizationStatuses.PartiallyReceived))
        {
            throw new InvalidOperationException(
                $"replacement_not_receivable_in_status: Replacement authorization '{AuthorizationNumber}' is '{Status}'.");
        }

        var qty = decimal.Round(deliveredBaseQty, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveredBaseQty), "Replacement delivered base quantity must be positive.");
        }

        if (qty > RemainingBaseQuantity)
        {
            throw new InvalidOperationException(
                $"replacement_entitlement_exceeded: Replacement receipt quantity ({qty:0.######} {BaseUomCodeSnapshot}) exceeds remaining authorized entitlement ({RemainingBaseQuantity:0.######} {BaseUomCodeSnapshot}) on '{AuthorizationNumber}'.");
        }

        DeliveredBaseQuantity = decimal.Round(DeliveredBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
        RemainingBaseQuantity = decimal.Round(AuthorizedBaseQuantity - DeliveredBaseQuantity, 6, MidpointRounding.AwayFromZero);
        Status = ReplacementAuthorizationStatuses.PartiallyReceived;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RecordAccepted(decimal acceptedBaseQty, DateTimeOffset nowUtc)
    {
        var qty = decimal.Round(acceptedBaseQty, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedBaseQty), "Replacement accepted base quantity must be positive.");
        }

        if (AcceptedBaseQuantity + qty > DeliveredBaseQuantity)
        {
            throw new InvalidOperationException(
                $"replacement_acceptance_exceeds_delivered: Accepted total ({AcceptedBaseQuantity + qty:0.######}) exceeds delivered replacement quantity ({DeliveredBaseQuantity:0.######}).");
        }

        AcceptedBaseQuantity = decimal.Round(AcceptedBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
        Status = RemainingBaseQuantity == 0m && AcceptedBaseQuantity == DeliveredBaseQuantity
            ? ReplacementAuthorizationStatuses.Fulfilled
            : ReplacementAuthorizationStatuses.PartiallyReceived;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed record SupplierReturnMatrixLineInput(
    Guid GoodsReceiptLineId,
    Guid? ValuedAcceptanceLineId,
    Guid? ReceiptAccrualLineId,
    Guid? SupplierBillLineId,
    Guid PoLineId,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    Guid? SourceLocationId,
    Guid? SourceStockLotId,
    string? SourceStockStatus,
    decimal ReturnedBaseQuantity,
    decimal AuthorizedUnitCostBase,
    decimal PoolValuedQty,
    decimal PoolCarryingValue,
    decimal PoolMovingAvgCost,
    long PoolSequence,
    decimal UnbilledGrniRemainingQty,
    decimal UnbilledGrniRemainingAmt,
    decimal BillLineNetAmount,
    decimal BillLineBilledQty,
    decimal PriorCreditedQty,
    decimal PriorCreditedSubtotal,
    decimal PriorCreditedPpn,
    decimal BillLinePpnAmount,
    decimal BillLineWithholdingAmount,
    decimal EligibleCapacityQty);

public sealed record SupplierReturnEvaluatedLine(
    Guid GoodsReceiptLineId,
    Guid? ValuedAcceptanceLineId,
    Guid? ReceiptAccrualLineId,
    Guid? SupplierBillLineId,
    Guid PoLineId,
    string LineageLineKey,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    Guid? SourceLocationId,
    Guid? SourceStockLotId,
    string? SourceStockStatus,
    decimal ReturnedBaseQuantity,
    decimal AuthorizedUnitCostBase,
    decimal InventoryCreditAmount,
    decimal GrniReversalAmount,
    decimal CreditSubtotalAmount,
    decimal PpnReversalAmount,
    decimal TotalCreditAmount,
    decimal PpvDifferenceAmount,
    bool CreatesReplacement,
    string? BlockingReason);

public sealed record SupplierReturnJournalSuggestion(
    string Side,
    string AccountCode,
    string AccountName,
    decimal Amount,
    string Description);

public sealed record SupplierReturnMatrixResult(
    string MatrixCode,
    string Disposition,
    string? BlockingCode,
    string? BlockingReason,
    bool RequiresPeriodOpen,
    bool IsPeriodOpen,
    bool RequiresNotaRetur,
    decimal TotalReturnedBaseQuantity,
    decimal TotalInventoryCreditAmount,
    decimal TotalGrniReversalAmount,
    decimal TotalCreditSubtotalAmount,
    decimal TotalPpnReversalAmount,
    decimal TotalCreditNoteAmount,
    decimal TotalPpvDifferenceAmount,
    IReadOnlyList<SupplierReturnEvaluatedLine> Lines,
    IReadOnlyList<SupplierReturnJournalSuggestion> JournalSuggestions);

/// <summary>
/// Pure matrix evaluator implementing S10 OD-05 / OD-07 / OD-04 / BRS §13 rules cleanly.
/// </summary>
public static class SupplierReturnMatrixCalculator
{
    public static SupplierReturnMatrixResult Evaluate(
        string returnCaseType,
        string resolutionMode,
        decimal payablePaidAmount,
        string? fiscalPeriodStatus,
        string? taxReturnNoteNumber,
        IReadOnlyList<SupplierReturnMatrixLineInput> lines)
    {
        var normalizedCase = SupplierReturnCaseTypes.Normalize(returnCaseType);
        var normalizedMode = ReturnResolutionModes.Normalize(resolutionMode);
        var requiresPeriod = normalizedCase != SupplierReturnCaseTypes.UnvaluedDockRejection;
        var periodOpen = !requiresPeriod || fiscalPeriodStatus == FinancialPeriodStatuses.Open;

        string matrixCode;
        string disposition = ReturnMatrixDispositions.Supported;
        string? blockingCode = null;
        string? blockingReason = null;

        if (normalizedCase == SupplierReturnCaseTypes.UnvaluedDockRejection)
        {
            if (normalizedMode == ReturnResolutionModes.ApCreditNote)
            {
                throw new InvalidOperationException(
                    "invalid_resolution_for_unvalued_return: Unvalued dock rejections never accrued GRNI/AP liability; choose REPLACEMENT_ONLY or CLOSE_WITHOUT_REPLACEMENT.");
            }

            matrixCode = "M1";
        }
        else if (normalizedCase == SupplierReturnCaseTypes.ValuedUnbilledReturn)
        {
            if (normalizedMode == ReturnResolutionModes.ApCreditNote)
            {
                throw new InvalidOperationException(
                    "invalid_resolution_for_unbilled_return: Valued unbilled receipts have not generated an AP payable; choose REPLACEMENT_ONLY or CLOSE_WITHOUT_REPLACEMENT.");
            }

            matrixCode = periodOpen ? "M2" : "M7";
            if (!periodOpen)
            {
                disposition = ReturnMatrixDispositions.BlockedClosedPeriod;
                blockingCode = "period_closed";
                blockingReason = $"Fiscal period for return date is '{fiscalPeriodStatus ?? "MISSING"}'; valued return posting requires OPEN period (M7).";
            }
        }
        else
        {
            // VALUED_BILLED_RETURN
            if (normalizedMode == ReturnResolutionModes.ReplacementOnly)
            {
                matrixCode = "M4";
                disposition = ReturnMatrixDispositions.BlockedPolicyOd05;
                blockingCode = "od05_billed_replacement_policy_blocked";
                blockingReason = "OD-05 blocks replacement-only execution on an already billed supplier invoice without approved credit/replacement policy (ATH-CR-007 M4).";
            }
            else if (normalizedMode != ReturnResolutionModes.ApCreditNote)
            {
                matrixCode = "M4";
                disposition = ReturnMatrixDispositions.BlockedPolicyOd05;
                blockingCode = "od05_billed_replacement_policy_blocked";
                blockingReason = "Valued billed return requires AP_CREDIT_NOTE under OD-05 safe default (ATH-CR-007).";
            }
            else if (payablePaidAmount > 0m)
            {
                matrixCode = "M5";
                disposition = ReturnMatrixDispositions.BlockedPolicyOd05;
                blockingCode = "od05_paid_bill_return_policy_blocked";
                blockingReason = $"OD-05 blocks auto-crediting a billed supplier invoice with PaidAmount={payablePaidAmount:N0} IDR pending refund policy (ATH-CR-007 M5).";
            }
            else if (lines.Any(l => l.BillLineWithholdingAmount > 0m))
            {
                matrixCode = "M6";
                disposition = ReturnMatrixDispositions.BlockedPolicyOd07;
                blockingCode = "od07_withholding_return_reversal_blocked";
                blockingReason = "OD-07 blocks returning billed lines carrying PPh 23 withholding pending Bukti Potong amendment rules (ATH-CR-007 M6).";
            }
            else if (!periodOpen)
            {
                matrixCode = "M7";
                disposition = ReturnMatrixDispositions.BlockedClosedPeriod;
                blockingCode = "period_closed";
                blockingReason = $"Fiscal period for return date is '{fiscalPeriodStatus ?? "MISSING"}'; valued credit posting requires OPEN period (M7).";
            }
            else
            {
                matrixCode = "M3";
            }
        }

        var evaluated = new List<SupplierReturnEvaluatedLine>(lines.Count);
        decimal totalQty = 0m;
        decimal totalInvCredit = 0m;
        decimal totalGrniRev = 0m;
        decimal totalSubCredit = 0m;
        decimal totalPpnRev = 0m;
        decimal totalCreditNote = 0m;
        decimal totalPpv = 0m;

        foreach (var line in lines)
        {
            var qty = decimal.Round(line.ReturnedBaseQuantity, 6, MidpointRounding.AwayFromZero);
            if (qty <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(lines), $"Line '{line.ItemCode}': return quantity must be positive.");
            }

            string? lineBlock = null;
            if (qty > line.EligibleCapacityQty)
            {
                lineBlock = $"return_exceeds_eligible_capacity: Line '{line.ItemCode}' return qty ({qty:0.######}) exceeds eligible capacity ({line.EligibleCapacityQty:0.######}).";
            }

            decimal invCredit = 0m;
            decimal grniRev = 0m;
            decimal subCredit = 0m;
            decimal ppnRev = 0m;
            decimal creditNote = 0m;
            decimal ppvDelta = 0m;

            if (normalizedCase != SupplierReturnCaseTypes.UnvaluedDockRejection)
            {
                try
                {
                    var invStep = ValuationPool.SimulatePurchaseReturn(
                        ValuationPoolStatuses.Active,
                        line.PoolValuedQty,
                        line.PoolCarryingValue,
                        line.PoolMovingAvgCost,
                        line.PoolSequence,
                        qty);
                    invCredit = invStep.IssuedCarryingAmount;
                }
                catch (InvalidOperationException ex)
                {
                    lineBlock ??= ex.Message;
                }

                if (normalizedCase == SupplierReturnCaseTypes.ValuedUnbilledReturn)
                {
                    if (qty > line.UnbilledGrniRemainingQty)
                    {
                        lineBlock ??= $"return_exceeds_unbilled_grni_capacity: Return qty ({qty:0.######}) exceeds open GRNI qty ({line.UnbilledGrniRemainingQty:0.######}).";
                    }
                    else
                    {
                        grniRev = qty == line.UnbilledGrniRemainingQty
                            ? line.UnbilledGrniRemainingAmt
                            : Math.Min(line.UnbilledGrniRemainingAmt, decimal.Round(qty * line.AuthorizedUnitCostBase, 4, MidpointRounding.AwayFromZero));
                        if (qty < line.UnbilledGrniRemainingQty && grniRev >= line.UnbilledGrniRemainingAmt)
                        {
                            grniRev = decimal.Round(line.UnbilledGrniRemainingAmt - 0.0001m, 4, MidpointRounding.AwayFromZero);
                        }
                        if (invCredit > 0m)
                        {
                            ppvDelta = decimal.Round(grniRev - invCredit, 4, MidpointRounding.AwayFromZero);
                        }
                    }
                }
                else
                {
                    var remBillQty = decimal.Round(line.BillLineBilledQty - line.PriorCreditedQty, 6, MidpointRounding.AwayFromZero);
                    var remBillSub = decimal.Round(line.BillLineNetAmount - line.PriorCreditedSubtotal, 4, MidpointRounding.AwayFromZero);
                    var remBillPpn = decimal.Round(line.BillLinePpnAmount - line.PriorCreditedPpn, 4, MidpointRounding.AwayFromZero);

                    if (qty > remBillQty || remBillSub <= 0m)
                    {
                        lineBlock ??= $"return_exceeds_billed_capacity: Return qty ({qty:0.######}) exceeds remaining uncredited billed qty ({remBillQty:0.######}).";
                    }
                    else
                    {
                        subCredit = qty == remBillQty
                            ? remBillSub
                            : decimal.Round(line.BillLineNetAmount * qty / line.BillLineBilledQty, 4, MidpointRounding.AwayFromZero);
                        ppnRev = qty == remBillQty
                            ? remBillPpn
                            : decimal.Round(line.BillLinePpnAmount * qty / line.BillLineBilledQty, 4, MidpointRounding.AwayFromZero);
                        creditNote = decimal.Round(subCredit + ppnRev, 4, MidpointRounding.AwayFromZero);
                        if (invCredit > 0m)
                        {
                            ppvDelta = decimal.Round(subCredit - invCredit, 4, MidpointRounding.AwayFromZero);
                        }
                    }
                }
            }

            if (lineBlock is not null && blockingCode is null)
            {
                var colon = lineBlock.IndexOf(':', StringComparison.Ordinal);
                blockingCode = colon > 0 ? lineBlock[..colon].Trim() : "return_line_blocked";
                blockingReason = colon > 0 ? lineBlock[(colon + 1)..].Trim() : lineBlock;
                disposition = ReturnMatrixDispositions.BlockedPolicyOd05;
            }

            totalQty += qty;
            totalInvCredit += invCredit;
            totalGrniRev += grniRev;
            totalSubCredit += subCredit;
            totalPpnRev += ppnRev;
            totalCreditNote += creditNote;
            totalPpv += ppvDelta;

            evaluated.Add(new SupplierReturnEvaluatedLine(
                line.GoodsReceiptLineId,
                line.ValuedAcceptanceLineId,
                line.ReceiptAccrualLineId,
                line.SupplierBillLineId,
                line.PoLineId,
                line.LineageLineKey,
                line.ItemId,
                line.ItemCode,
                line.ItemName,
                line.BaseUomId,
                line.BaseUomCode,
                line.SourceLocationId,
                line.SourceStockLotId,
                line.SourceStockStatus,
                qty,
                decimal.Round(line.AuthorizedUnitCostBase, 6, MidpointRounding.AwayFromZero),
                invCredit,
                grniRev,
                subCredit,
                ppnRev,
                creditNote,
                ppvDelta,
                normalizedMode == ReturnResolutionModes.ReplacementOnly,
                lineBlock));
        }

        var requiresNotaRetur = normalizedCase == SupplierReturnCaseTypes.ValuedBilledReturn && totalPpnRev > 0m;
        var suggestions = BuildJournalSuggestions(normalizedCase, totalInvCredit, totalGrniRev, totalCreditNote, totalPpnRev, totalPpv);

        return new SupplierReturnMatrixResult(
            matrixCode,
            disposition,
            blockingCode,
            blockingReason,
            requiresPeriod,
            periodOpen,
            requiresNotaRetur,
            decimal.Round(totalQty, 6, MidpointRounding.AwayFromZero),
            decimal.Round(totalInvCredit, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalGrniRev, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalSubCredit, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalPpnRev, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalCreditNote, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalPpv, 4, MidpointRounding.AwayFromZero),
            evaluated,
            suggestions);
    }

    private static List<SupplierReturnJournalSuggestion> BuildJournalSuggestions(
        string caseType,
        decimal invCredit,
        decimal grniRev,
        decimal creditNote,
        decimal ppnRev,
        decimal ppvDelta)
    {
        var list = new List<SupplierReturnJournalSuggestion>();
        if (caseType == SupplierReturnCaseTypes.UnvaluedDockRejection)
        {
            return list;
        }

        if (caseType == SupplierReturnCaseTypes.ValuedUnbilledReturn && grniRev > 0m && invCredit > 0m)
        {
            list.Add(new("DEBIT", "2110", "GRNI Clearing", grniRev, "Dr GRNI Clearing (Unbilled Return Reversal)"));
            list.Add(new("CREDIT", "1300", "Inventory Asset", invCredit, "Cr Inventory Asset (Outbound Return at MAC)"));
        }
        else if (caseType == SupplierReturnCaseTypes.ValuedBilledReturn && creditNote > 0m && invCredit > 0m)
        {
            list.Add(new("DEBIT", "2100", "Accounts Payable Control", creditNote, "Dr AP Trade Control (Supplier Credit Note)"));
            list.Add(new("CREDIT", "1300", "Inventory Asset", invCredit, "Cr Inventory Asset (Outbound Return at MAC)"));
            if (ppnRev > 0m)
            {
                list.Add(new("CREDIT", "1410", "PPN Masukan", ppnRev, "Cr PPN Input Reversal (Nota Retur)"));
            }
        }

        if (ppvDelta > 0m)
        {
            list.Add(new("CREDIT", "5200", "Purchase Price Variance", ppvDelta, "Cr PPV (Return MAC variance)"));
        }
        else if (ppvDelta < 0m)
        {
            list.Add(new("DEBIT", "5200", "Purchase Price Variance", -ppvDelta, "Dr PPV (Return MAC variance)"));
        }

        return list;
    }
}
