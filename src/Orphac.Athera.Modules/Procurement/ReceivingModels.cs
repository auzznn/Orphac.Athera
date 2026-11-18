using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Procurement;

public static class GoodsReceiptStatuses
{
    public const string Arrived = "ARRIVED";
    public const string InDockInspection = "IN_DOCK_INSPECTION";
    public const string ReleasedForAcceptance = "RELEASED_FOR_ACCEPTANCE";
    public const string PartiallyAccepted = "PARTIALLY_ACCEPTED";
    public const string ValuedAccepted = "VALUED_ACCEPTED";
    public const string FullyRejectedUnvalued = "FULLY_REJECTED_UNVALUED";

    public static readonly IReadOnlyList<string> All =
    [
        Arrived,
        InDockInspection,
        ReleasedForAcceptance,
        PartiallyAccepted,
        ValuedAccepted,
        FullyRejectedUnvalued
    ];
}

public static class DockCustodyStatuses
{
    public const string Arrived = "ARRIVED";
    public const string DockCheckPending = "DOCK_CHECK_PENDING";
    public const string DockHeld = "DOCK_HELD";
    public const string DockRejected = "DOCK_REJECTED";
    public const string DockReleased = "DOCK_RELEASED";

    public static readonly IReadOnlyList<string> All =
    [
        Arrived,
        DockCheckPending,
        DockHeld,
        DockRejected,
        DockReleased
    ];
}

public static class DiscrepancyReasonCodes
{
    public const string None = "NONE";
    public const string DamagedPackaging = "DAMAGED_PACKAGING";
    public const string WetOrDefectiveGoods = "WET_OR_DEFECTIVE_GOODS";
    public const string OverdeliveryOd01Hold = "OVERDELIVERY_OD01_HOLD";
    public const string WrongItemOrSpec = "WRONG_ITEM_OR_SPEC";
    public const string MissingSupplierDocs = "MISSING_SUPPLIER_DOCS";

    public static readonly IReadOnlyList<string> All =
    [
        None,
        DamagedPackaging,
        WetOrDefectiveGoods,
        OverdeliveryOd01Hold,
        WrongItemOrSpec,
        MissingSupplierDocs
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.Ordinal);

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return None;
        }

        var normalized = raw.Trim().ToUpperInvariant();
        if (!KnownSet.Contains(normalized))
        {
            throw new ArgumentException($"Unknown discrepancy reason code '{normalized}'.", nameof(raw));
        }

        return normalized;
    }
}

public sealed class ReceiptSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private ReceiptSequence()
    {
    }
}

/// <summary>
/// Authoritative Gate 1 physical custody header in `procurement.goods_receipts`.
/// Unvalued by design in S06: `HasFinancialPosting` stays `false` and `TotalValuedAcceptedBaseQuantity` stays `0`.
/// </summary>
public sealed class GoodsReceipt
{
    public Guid GoodsReceiptId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;

    public Guid PurchaseOrderId { get; private set; }
    public Guid PoRevisionId { get; private set; }
    public string PoNumberSnapshot { get; private set; } = string.Empty;
    public int PoRevisionNumberSnapshot { get; private set; }
    public string PoSnapshotHash { get; private set; } = string.Empty;
    public Guid? ReplacementAuthorizationId { get; private set; }

    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string SupplierDeliveryNoteNumber { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;
    public string ArrivalPayloadHash { get; private set; } = string.Empty;

    public DateOnly ReceivedDate { get; private set; }
    public string ReceivingStation { get; private set; } = string.Empty;
    public string? VehicleOrSealRef { get; private set; }
    public string? DocumentEvidenceRef { get; private set; }
    public string Status { get; private set; } = GoodsReceiptStatuses.Arrived;

    public decimal TotalDeliveredBaseQuantity { get; private set; }
    public decimal TotalRejectedBaseQuantity { get; private set; }
    public decimal TotalValuedAcceptedBaseQuantity { get; private set; }
    public decimal TotalUnresolvedCustodyBaseQuantity { get; private set; }
    public decimal TotalReleasedBaseQuantity { get; private set; }
    public decimal TotalHeldBaseQuantity { get; private set; }
    public bool HasFinancialPosting { get; private set; }

    public string? Notes { get; private set; }
    public Guid ReceivedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private GoodsReceipt()
    {
    }

    public static GoodsReceipt Create(
        Guid goodsReceiptId,
        Guid tenantId,
        Guid legalEntityId,
        string receiptNumber,
        PurchaseOrder po,
        PurchaseOrderRevision approvedRevision,
        string supplierDeliveryNoteNumber,
        string idempotencyKey,
        string arrivalPayloadHash,
        DateOnly receivedDate,
        string receivingStation,
        string? vehicleOrSealRef,
        string? documentEvidenceRef,
        string? notes,
        IReadOnlyCollection<GoodsReceiptLine> lines,
        Guid receivedByUserId,
        DateTimeOffset nowUtc,
        Guid? replacementAuthorizationId = null)
    {
        GuardValidation.RequireNonEmptyGuid(goodsReceiptId, nameof(goodsReceiptId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(receivedByUserId, nameof(receivedByUserId));
        ArgumentNullException.ThrowIfNull(po);
        ArgumentNullException.ThrowIfNull(approvedRevision);

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("gr_lines_required: Goods receipt requires at least one positive delivered line.");
        }

        var receipt = new GoodsReceipt
        {
            GoodsReceiptId = goodsReceiptId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ReceiptNumber = GuardValidation.RequireCode(receiptNumber, nameof(receiptNumber), 40),
            PurchaseOrderId = po.PurchaseOrderId,
            PoRevisionId = approvedRevision.RevisionId,
            PoNumberSnapshot = GuardValidation.RequireCode(po.PoNumber ?? po.DraftCode, nameof(po.PoNumber), 40),
            PoRevisionNumberSnapshot = approvedRevision.RevisionNumber,
            PoSnapshotHash = GuardValidation.RequireName(approvedRevision.SnapshotHash, nameof(approvedRevision.SnapshotHash), 64),
            ReplacementAuthorizationId = replacementAuthorizationId,
            SupplierPartnerId = approvedRevision.SupplierPartnerId,
            SupplierCodeSnapshot = GuardValidation.RequireName(approvedRevision.SupplierCodeSnapshot, nameof(approvedRevision.SupplierCodeSnapshot), 32),
            SupplierLegalNameSnapshot = GuardValidation.RequireName(approvedRevision.SupplierLegalNameSnapshot, nameof(approvedRevision.SupplierLegalNameSnapshot), 200),
            SupplierDeliveryNoteNumber = GuardValidation.RequireCode(supplierDeliveryNoteNumber, nameof(supplierDeliveryNoteNumber), 80),
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            ArrivalPayloadHash = GuardValidation.RequireName(arrivalPayloadHash, nameof(arrivalPayloadHash), 64),
            ReceivedDate = receivedDate,
            ReceivingStation = GuardValidation.RequireName(receivingStation, nameof(receivingStation), 160),
            VehicleOrSealRef = GuardValidation.NormalizeOptional(vehicleOrSealRef, 80, nameof(vehicleOrSealRef)),
            DocumentEvidenceRef = GuardValidation.NormalizeOptional(documentEvidenceRef, 200, nameof(documentEvidenceRef)),
            Notes = GuardValidation.NormalizeOptional(notes, 500, nameof(notes)),
            HasFinancialPosting = false,
            TotalValuedAcceptedBaseQuantity = 0m,
            ReceivedByUserId = receivedByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };

        receipt.RecalculateHeaderFromLines(lines, nowUtc, incrementVersion: false);
        return receipt;
    }

    public void RecalculateHeaderFromLines(
        IReadOnlyCollection<GoodsReceiptLine> lines,
        DateTimeOffset nowUtc,
        bool incrementVersion = true)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("gr_lines_required: Goods receipt must have at least one line.");
        }

        TotalDeliveredBaseQuantity = lines.Sum(l => l.PhysicallyDeliveredBaseQuantity);
        TotalRejectedBaseQuantity = lines.Sum(l => l.DockRejectedBaseQuantity);
        TotalValuedAcceptedBaseQuantity = lines.Sum(l => l.ValuedAcceptedBaseQuantity);
        TotalUnresolvedCustodyBaseQuantity = lines.Sum(l => l.UnresolvedCustodyBaseQuantity);
        TotalReleasedBaseQuantity = lines.Sum(l => l.DockReleasedBaseQuantity);
        TotalHeldBaseQuantity = lines.Sum(l => l.DockHeldBaseQuantity);

        if (TotalDeliveredBaseQuantity != TotalRejectedBaseQuantity + TotalValuedAcceptedBaseQuantity + TotalUnresolvedCustodyBaseQuantity)
        {
            throw new InvalidOperationException("dock_quantity_conservation_mismatch: Receipt totals do not conserve delivered base quantity.");
        }

        HasFinancialPosting = TotalValuedAcceptedBaseQuantity > 0m;

        if (TotalRejectedBaseQuantity == TotalDeliveredBaseQuantity)
        {
            Status = GoodsReceiptStatuses.FullyRejectedUnvalued;
        }
        else if (TotalValuedAcceptedBaseQuantity > 0m)
        {
            Status = TotalUnresolvedCustodyBaseQuantity == 0m
                ? GoodsReceiptStatuses.ValuedAccepted
                : GoodsReceiptStatuses.PartiallyAccepted;
        }
        else if (lines.All(l => l.CustodyStatus == DockCustodyStatuses.Arrived))
        {
            Status = GoodsReceiptStatuses.Arrived;
        }
        else if (lines.Any(l => l.PendingCheckBaseQuantity > 0m || l.DockHeldBaseQuantity > 0m))
        {
            Status = GoodsReceiptStatuses.InDockInspection;
        }
        else
        {
            Status = GoodsReceiptStatuses.ReleasedForAcceptance;
        }

        UpdatedAtUtc = nowUtc;
        if (incrementVersion)
        {
            Version++;
        }
    }
}

public sealed class GoodsReceiptLine
{
    public Guid GoodsReceiptLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public int LineNumber { get; private set; }

    public Guid PurchaseOrderId { get; private set; }
    public Guid PoRevisionId { get; private set; }
    public Guid PoLineId { get; private set; }
    public string LineageLineKey { get; private set; } = string.Empty;

    public Guid ItemId { get; private set; }
    public string ItemCodeSnapshot { get; private set; } = string.Empty;
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public string ItemKindSnapshot { get; private set; } = string.Empty;

    public Guid ReceivedUomId { get; private set; }
    public string ReceivedUomCodeSnapshot { get; private set; } = string.Empty;
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal UomFactorToBaseSnapshot { get; private set; }
    public decimal PoOrderedBaseQuantitySnapshot { get; private set; }

    public decimal PhysicallyDeliveredQuantity { get; private set; }
    public decimal PhysicallyDeliveredBaseQuantity { get; private set; }
    public decimal PendingCheckBaseQuantity { get; private set; }
    public decimal DockHeldBaseQuantity { get; private set; }
    public decimal DockReleasedBaseQuantity { get; private set; }
    public decimal DockRejectedBaseQuantity { get; private set; }
    public decimal ValuedAcceptedBaseQuantity { get; private set; }
    public decimal UnresolvedCustodyBaseQuantity { get; private set; }
    public decimal OverdeliveryExcessBaseQuantity { get; private set; }

    public string CustodyStatus { get; private set; } = DockCustodyStatuses.Arrived;
    public string DiscrepancyReasonCode { get; private set; } = DiscrepancyReasonCodes.None;
    public string? DiscrepancyNotes { get; private set; }
    public string? ConditionEvidenceRef { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private GoodsReceiptLine()
    {
    }

    public static GoodsReceiptLine CreateArrival(
        Guid goodsReceiptLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid goodsReceiptId,
        int lineNumber,
        PurchaseOrderLine poLine,
        Guid receivedUomId,
        string receivedUomCodeSnapshot,
        decimal uomFactorToBaseSnapshot,
        decimal physicallyDeliveredQuantity,
        decimal physicallyDeliveredBaseQuantity,
        decimal remainingApprovedEligibleBaseQty,
        bool flagForInspection,
        string discrepancyReasonCode,
        string? discrepancyNotes,
        string? conditionEvidenceRef,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(goodsReceiptLineId, nameof(goodsReceiptLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(goodsReceiptId, nameof(goodsReceiptId));
        GuardValidation.RequireNonEmptyGuid(receivedUomId, nameof(receivedUomId));
        ArgumentNullException.ThrowIfNull(poLine);

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        var roundedQty = decimal.Round(physicallyDeliveredQuantity, 6, MidpointRounding.AwayFromZero);
        var roundedBaseQty = decimal.Round(physicallyDeliveredBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var roundedFactor = decimal.Round(uomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero);
        if (roundedQty <= 0m || roundedBaseQty <= 0m || roundedFactor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(physicallyDeliveredQuantity), "Delivered quantity and conversion factor must be positive.");
        }

        var eligibleCap = Math.Max(0m, decimal.Round(remainingApprovedEligibleBaseQty, 6, MidpointRounding.AwayFromZero));
        var normalizedReason = DiscrepancyReasonCodes.Normalize(discrepancyReasonCode);
        var cleanNotes = GuardValidation.NormalizeOptional(discrepancyNotes, 300, nameof(discrepancyNotes));
        var cleanEvidence = GuardValidation.NormalizeOptional(conditionEvidenceRef, 200, nameof(conditionEvidenceRef));

        var excessHold = Math.Max(0m, roundedBaseQty - eligibleCap);
        var pendingEligible = roundedBaseQty - excessHold;

        string initialStatus;
        if (excessHold > 0m)
        {
            normalizedReason = normalizedReason != DiscrepancyReasonCodes.None
                ? normalizedReason
                : DiscrepancyReasonCodes.OverdeliveryOd01Hold;
            initialStatus = pendingEligible > 0m
                ? DockCustodyStatuses.DockCheckPending
                : DockCustodyStatuses.DockHeld;
        }
        else if (flagForInspection || normalizedReason != DiscrepancyReasonCodes.None)
        {
            initialStatus = DockCustodyStatuses.DockCheckPending;
        }
        else
        {
            initialStatus = DockCustodyStatuses.Arrived;
        }

        var line = new GoodsReceiptLine
        {
            GoodsReceiptLineId = goodsReceiptLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            GoodsReceiptId = goodsReceiptId,
            LineNumber = lineNumber,
            PurchaseOrderId = poLine.PurchaseOrderId,
            PoRevisionId = poLine.RevisionId,
            PoLineId = poLine.PoLineId,
            LineageLineKey = poLine.LineageLineKey,
            ItemId = poLine.ItemId,
            ItemCodeSnapshot = poLine.ItemCodeSnapshot,
            ItemNameSnapshot = poLine.ItemNameSnapshot,
            ItemKindSnapshot = poLine.ItemKindSnapshot,
            ReceivedUomId = receivedUomId,
            ReceivedUomCodeSnapshot = GuardValidation.RequireCode(receivedUomCodeSnapshot, nameof(receivedUomCodeSnapshot), 16),
            BaseUomId = poLine.BaseUomId,
            BaseUomCodeSnapshot = poLine.BaseUomCodeSnapshot,
            UomFactorToBaseSnapshot = roundedFactor,
            PoOrderedBaseQuantitySnapshot = poLine.OrderedBaseQuantity,
            PhysicallyDeliveredQuantity = roundedQty,
            PhysicallyDeliveredBaseQuantity = roundedBaseQty,
            PendingCheckBaseQuantity = pendingEligible,
            DockHeldBaseQuantity = excessHold,
            DockReleasedBaseQuantity = 0m,
            DockRejectedBaseQuantity = 0m,
            ValuedAcceptedBaseQuantity = 0m,
            UnresolvedCustodyBaseQuantity = pendingEligible + excessHold,
            OverdeliveryExcessBaseQuantity = excessHold,
            CustodyStatus = initialStatus,
            DiscrepancyReasonCode = normalizedReason,
            DiscrepancyNotes = cleanNotes,
            ConditionEvidenceRef = cleanEvidence,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };

        line.AssertConservation();
        return line;
    }

    public void ApplyDockDisposition(
        decimal pendingCheckBaseQty,
        decimal dockHeldBaseQty,
        decimal dockReleasedBaseQty,
        decimal dockRejectedBaseQty,
        decimal maxApprovedAllowedForRelease,
        int baseUomPrecisionScale,
        string discrepancyReasonCode,
        string? discrepancyNotes,
        string? conditionEvidenceRef,
        DateTimeOffset nowUtc)
    {
        var scale = Math.Clamp(baseUomPrecisionScale, 0, 6);
        var p = decimal.Round(pendingCheckBaseQty, 6, MidpointRounding.AwayFromZero);
        var h = decimal.Round(dockHeldBaseQty, 6, MidpointRounding.AwayFromZero);
        var r = decimal.Round(dockReleasedBaseQty, 6, MidpointRounding.AwayFromZero);
        var x = decimal.Round(dockRejectedBaseQty, 6, MidpointRounding.AwayFromZero);

        if (p < 0m || h < 0m || r < 0m || x < 0m)
        {
            throw new InvalidOperationException("dock_negative_quantity_forbidden: Custody bucket quantities cannot be negative.");
        }

        if (scale == 0 && (p != decimal.Truncate(p) || h != decimal.Truncate(h) || r != decimal.Truncate(r) || x != decimal.Truncate(x)))
        {
            throw new InvalidOperationException(
                $"fractional_discrete_base_quantity: Disposition quantities for '{BaseUomCodeSnapshot}' must be exact integers.");
        }

        if (p + h + r + x != PhysicallyDeliveredBaseQuantity)
        {
            throw new InvalidOperationException(
                $"dock_quantity_conservation_mismatch: Split total ({p + h + r + x}) must equal physically delivered base quantity ({PhysicallyDeliveredBaseQuantity}).");
        }

        if (r < ValuedAcceptedBaseQuantity)
        {
            throw new InvalidOperationException(
                "dock_release_below_valued_accepted: Cannot reduce released quantity below already valued accepted quantity.");
        }

        if (r > decimal.Round(Math.Max(0m, maxApprovedAllowedForRelease), 6, MidpointRounding.AwayFromZero))
        {
            throw new InvalidOperationException(
                $"od01_overdelivery_release_blocked: Released quantity ({r} {BaseUomCodeSnapshot}) exceeds remaining approved PO capacity ({Math.Max(0m, maxApprovedAllowedForRelease)} {BaseUomCodeSnapshot}) under OD-01 exact-quantity policy (ATH-CR-003).");
        }

        var normalizedReason = DiscrepancyReasonCodes.Normalize(discrepancyReasonCode);
        if ((h > 0m || x > 0m) && normalizedReason == DiscrepancyReasonCodes.None)
        {
            throw new InvalidOperationException(
                "discrepancy_reason_required: Held or rejected dock custody quantities require an explicit discrepancy reason code.");
        }

        PendingCheckBaseQuantity = p;
        DockHeldBaseQuantity = h;
        DockReleasedBaseQuantity = r;
        DockRejectedBaseQuantity = x;
        UnresolvedCustodyBaseQuantity = p + h + (r - ValuedAcceptedBaseQuantity);
        OverdeliveryExcessBaseQuantity = Math.Max(
            0m,
            PhysicallyDeliveredBaseQuantity - x - decimal.Round(Math.Max(0m, maxApprovedAllowedForRelease), 6, MidpointRounding.AwayFromZero));
        DiscrepancyReasonCode = normalizedReason;
        DiscrepancyNotes = GuardValidation.NormalizeOptional(discrepancyNotes, 300, nameof(discrepancyNotes));
        ConditionEvidenceRef = GuardValidation.NormalizeOptional(conditionEvidenceRef, 200, nameof(conditionEvidenceRef));

        CustodyStatus = x == PhysicallyDeliveredBaseQuantity
            ? DockCustodyStatuses.DockRejected
            : p > 0m
                ? DockCustodyStatuses.DockCheckPending
                : h > 0m
                    ? DockCustodyStatuses.DockHeld
                    : DockCustodyStatuses.DockReleased;

        AssertConservation();
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RecordValuedAcceptance(decimal acceptedDeltaBaseQty, int baseUomPrecisionScale, DateTimeOffset nowUtc)
    {
        var scale = Math.Clamp(baseUomPrecisionScale, 0, 6);
        var delta = decimal.Round(acceptedDeltaBaseQty, 6, MidpointRounding.AwayFromZero);
        if (delta <= 0m)
        {
            throw new InvalidOperationException("acceptance_qty_must_be_positive: Valued acceptance quantity must be positive.");
        }

        if (scale == 0 && delta != decimal.Truncate(delta))
        {
            throw new InvalidOperationException(
                $"fractional_discrete_base_quantity: Accepted quantity for '{BaseUomCodeSnapshot}' must be a whole integer.");
        }

        var availableToAccept = decimal.Round(DockReleasedBaseQuantity - ValuedAcceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (delta > availableToAccept)
        {
            throw new InvalidOperationException(
                $"acceptance_exceeds_released_quantity: Accepted quantity ({delta} {BaseUomCodeSnapshot}) exceeds eligible Gate 1 released balance ({Math.Max(0m, availableToAccept)} {BaseUomCodeSnapshot}).");
        }

        ValuedAcceptedBaseQuantity = decimal.Round(ValuedAcceptedBaseQuantity + delta, 6, MidpointRounding.AwayFromZero);
        UnresolvedCustodyBaseQuantity = decimal.Round(
            PendingCheckBaseQuantity + DockHeldBaseQuantity + (DockReleasedBaseQuantity - ValuedAcceptedBaseQuantity),
            6,
            MidpointRounding.AwayFromZero);

        AssertConservation();
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void AssertConservation()
    {
        var expectedUnresolved = PendingCheckBaseQuantity + DockHeldBaseQuantity + (DockReleasedBaseQuantity - ValuedAcceptedBaseQuantity);
        if (UnresolvedCustodyBaseQuantity != expectedUnresolved
            || PhysicallyDeliveredBaseQuantity != DockRejectedBaseQuantity + ValuedAcceptedBaseQuantity + UnresolvedCustodyBaseQuantity)
        {
            throw new InvalidOperationException(
                "dock_quantity_conservation_mismatch: Delivered = DockRejected + ValuedAccepted + UnresolvedCustody invariant failed.");
        }
    }
}

/// <summary>
/// Strictly append-only Gate 1 inspection & disposition history in `procurement.dock_checks`.
/// </summary>
public sealed class DockCheck
{
    public Guid DockCheckId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid GoodsReceiptLineId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string PreviousCustodyStatus { get; private set; } = string.Empty;
    public string ResultingCustodyStatus { get; private set; } = string.Empty;
    public decimal PendingCheckBaseQuantity { get; private set; }
    public decimal DockHeldBaseQuantity { get; private set; }
    public decimal DockReleasedBaseQuantity { get; private set; }
    public decimal DockRejectedBaseQuantity { get; private set; }
    public string DiscrepancyReasonCode { get; private set; } = DiscrepancyReasonCodes.None;
    public string? InspectionNotes { get; private set; }
    public string? EvidenceReference { get; private set; }
    public Guid CheckedByUserId { get; private set; }
    public DateTimeOffset CheckedAtUtc { get; private set; }

    private DockCheck()
    {
    }

    public static DockCheck Record(
        Guid dockCheckId,
        Guid tenantId,
        Guid legalEntityId,
        GoodsReceiptLine line,
        string previousCustodyStatus,
        string idempotencyKey,
        string payloadHash,
        Guid checkedByUserId,
        DateTimeOffset checkedAtUtc)
    {
        GuardValidation.RequireNonEmptyGuid(dockCheckId, nameof(dockCheckId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(checkedByUserId, nameof(checkedByUserId));
        ArgumentNullException.ThrowIfNull(line);

        return new DockCheck
        {
            DockCheckId = dockCheckId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            GoodsReceiptId = line.GoodsReceiptId,
            GoodsReceiptLineId = line.GoodsReceiptLineId,
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            PayloadHash = GuardValidation.RequireName(payloadHash, nameof(payloadHash), 64),
            PreviousCustodyStatus = GuardValidation.RequireCode(previousCustodyStatus, nameof(previousCustodyStatus), 24),
            ResultingCustodyStatus = line.CustodyStatus,
            PendingCheckBaseQuantity = line.PendingCheckBaseQuantity,
            DockHeldBaseQuantity = line.DockHeldBaseQuantity,
            DockReleasedBaseQuantity = line.DockReleasedBaseQuantity,
            DockRejectedBaseQuantity = line.DockRejectedBaseQuantity,
            DiscrepancyReasonCode = line.DiscrepancyReasonCode,
            InspectionNotes = line.DiscrepancyNotes,
            EvidenceReference = line.ConditionEvidenceRef,
            CheckedByUserId = checkedByUserId,
            CheckedAtUtc = checkedAtUtc
        };
    }
}

public static class ReceivingHash
{
    public static string ComputeArrivalPayloadHash(
        Guid purchaseOrderId,
        string supplierDeliveryNoteNumber,
        DateOnly receivedDate,
        string receivingStation,
        string? vehicleOrSealRef,
        string? documentEvidenceRef,
        IEnumerable<(Guid PoLineId, Guid OrderedUomId, decimal Qty, bool FlagInspection, string Reason)> lines)
    {
        var sb = new StringBuilder();
        sb.Append(purchaseOrderId.ToString("D")).Append('|')
          .Append(supplierDeliveryNoteNumber.Trim().ToUpperInvariant()).Append('|')
          .Append(receivedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
          .Append(receivingStation.Trim()).Append('|')
          .Append(vehicleOrSealRef?.Trim() ?? string.Empty).Append('|')
          .Append(documentEvidenceRef?.Trim() ?? string.Empty).AppendLine();

        foreach (var l in lines.OrderBy(x => x.PoLineId))
        {
            sb.Append(l.PoLineId.ToString("D")).Append('|')
              .Append(l.OrderedUomId.ToString("D")).Append('|')
              .Append(decimal.Round(l.Qty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture)).Append('|')
              .Append(l.FlagInspection ? '1' : '0').Append('|')
              .Append(l.Reason.Trim().ToUpperInvariant()).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public static string ComputeDockCheckPayloadHash(
        Guid receiptLineId,
        decimal pendingBaseQty,
        decimal heldBaseQty,
        decimal releasedBaseQty,
        decimal rejectedBaseQty,
        string discrepancyReasonCode,
        string? inspectionNotes,
        string? evidenceReference)
    {
        var raw = string.Join('|',
            receiptLineId.ToString("D"),
            decimal.Round(pendingBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            decimal.Round(heldBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            decimal.Round(releasedBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            decimal.Round(rejectedBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            discrepancyReasonCode.Trim().ToUpperInvariant(),
            inspectionNotes?.Trim() ?? string.Empty,
            evidenceReference?.Trim() ?? string.Empty);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}

public sealed class AcceptanceSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private AcceptanceSequence()
    {
    }
}

/// <summary>
/// Strictly append-only Gate 2 valued acceptance header in `procurement.valued_acceptances`.
/// Links immutable Gate 1 receipt lines to Inventory StockMovement, GRNI ReceiptAccrual, and GL JournalEntry.
/// </summary>
public sealed class ValuedAcceptance
{
    public Guid ValuedAcceptanceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string AcceptanceNumber { get; private set; } = string.Empty;
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid PoRevisionId { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public DateOnly AcceptanceDate { get; private set; }
    public string StockStatus { get; private set; } = "UNRESTRICTED";
    public string CurrencyCode { get; private set; } = "IDR";
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public decimal TotalAcceptedBaseQuantity { get; private set; }
    public decimal TotalAcceptedAmount { get; private set; }
    public Guid? StockMovementId { get; private set; }
    public Guid? ReceiptAccrualId { get; private set; }
    public Guid? GlJournalId { get; private set; }
    public string? Notes { get; private set; }
    public Guid AcceptedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ValuedAcceptance()
    {
    }

    public static ValuedAcceptance Create(
        Guid valuedAcceptanceId,
        Guid tenantId,
        Guid legalEntityId,
        string acceptanceNumber,
        GoodsReceipt receipt,
        DateOnly acceptanceDate,
        string stockStatus,
        string currencyCode,
        string idempotencyKey,
        string payloadHash,
        decimal totalAcceptedBaseQuantity,
        decimal totalAcceptedAmount,
        Guid stockMovementId,
        Guid receiptAccrualId,
        Guid glJournalId,
        string? notes,
        Guid acceptedByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceId, nameof(valuedAcceptanceId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualId, nameof(receiptAccrualId));
        GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId));
        GuardValidation.RequireNonEmptyGuid(acceptedByUserId, nameof(acceptedByUserId));
        ArgumentNullException.ThrowIfNull(receipt);

        var totalQty = decimal.Round(totalAcceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var totalAmt = decimal.Round(totalAcceptedAmount, 4, MidpointRounding.AwayFromZero);
        if (totalQty <= 0m || totalAmt <= 0m)
        {
            throw new InvalidOperationException("od04_zero_or_negative_acceptance_value_blocked: Valued acceptance requires positive quantity and positive monetary amount under OD-04.");
        }

        return new ValuedAcceptance
        {
            ValuedAcceptanceId = valuedAcceptanceId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            AcceptanceNumber = GuardValidation.RequireCode(acceptanceNumber, nameof(acceptanceNumber), 40),
            GoodsReceiptId = receipt.GoodsReceiptId,
            PurchaseOrderId = receipt.PurchaseOrderId,
            PoRevisionId = receipt.PoRevisionId,
            SupplierPartnerId = receipt.SupplierPartnerId,
            AcceptanceDate = acceptanceDate,
            StockStatus = GuardValidation.RequireCode(stockStatus, nameof(stockStatus), 24),
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            PayloadHash = GuardValidation.RequireName(payloadHash, nameof(payloadHash), 64),
            TotalAcceptedBaseQuantity = totalQty,
            TotalAcceptedAmount = totalAmt,
            StockMovementId = stockMovementId,
            ReceiptAccrualId = receiptAccrualId,
            GlJournalId = glJournalId,
            Notes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes)),
            AcceptedByUserId = acceptedByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}

/// <summary>
/// Strictly append-only Gate 2 valued acceptance line in `procurement.valued_acceptance_lines`.
/// </summary>
public sealed class ValuedAcceptanceLine
{
    public Guid ValuedAcceptanceLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ValuedAcceptanceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid GoodsReceiptLineId { get; private set; }
    public Guid PoLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public string BaseUomCodeSnapshot { get; private set; } = string.Empty;
    public decimal AcceptedBaseQuantity { get; private set; }
    public decimal UnitPriceBaseSnapshot { get; private set; }
    public decimal AcceptedLineAmount { get; private set; }

    private ValuedAcceptanceLine()
    {
    }

    public static ValuedAcceptanceLine Create(
        Guid valuedAcceptanceLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid valuedAcceptanceId,
        int lineNumber,
        GoodsReceiptLine receiptLine,
        decimal acceptedBaseQuantity,
        decimal unitPriceBaseSnapshot,
        decimal acceptedLineAmount)
    {
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceLineId, nameof(valuedAcceptanceLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceId, nameof(valuedAcceptanceId));
        ArgumentNullException.ThrowIfNull(receiptLine);

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        var qty = decimal.Round(acceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var unitPriceBase = decimal.Round(unitPriceBaseSnapshot, 6, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(acceptedLineAmount, 4, MidpointRounding.AwayFromZero);
        if (qty <= 0m || unitPriceBase <= 0m || amount <= 0m)
        {
            throw new InvalidOperationException("od04_zero_or_negative_acceptance_value_blocked: Acceptance line requires positive quantity and valuation amount.");
        }

        return new ValuedAcceptanceLine
        {
            ValuedAcceptanceLineId = valuedAcceptanceLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ValuedAcceptanceId = valuedAcceptanceId,
            LineNumber = lineNumber,
            GoodsReceiptLineId = receiptLine.GoodsReceiptLineId,
            PoLineId = receiptLine.PoLineId,
            ItemId = receiptLine.ItemId,
            BaseUomId = receiptLine.BaseUomId,
            BaseUomCodeSnapshot = receiptLine.BaseUomCodeSnapshot,
            AcceptedBaseQuantity = qty,
            UnitPriceBaseSnapshot = unitPriceBase,
            AcceptedLineAmount = amount
        };
    }
}

public static class ValuedAcceptanceHash
{
    public static string ComputePayloadHash(
        Guid goodsReceiptId,
        DateOnly acceptanceDate,
        string stockStatus,
        IEnumerable<(Guid GoodsReceiptLineId, decimal AcceptedBaseQty)> lines) =>
        ComputePayloadHash(
            goodsReceiptId,
            acceptanceDate,
            stockStatus,
            lines.Select(x => (x.GoodsReceiptLineId, x.AcceptedBaseQty, (Guid?)null, (string?)null)));

    public static string ComputePayloadHash(
        Guid goodsReceiptId,
        DateOnly acceptanceDate,
        string stockStatus,
        IEnumerable<(Guid GoodsReceiptLineId, decimal AcceptedBaseQty, Guid? DestinationLocationId, string? LotNumber)> lines)
    {
        var sb = new StringBuilder();
        sb.Append(goodsReceiptId.ToString("D")).Append('|')
          .Append(acceptanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
          .Append(stockStatus.Trim().ToUpperInvariant()).AppendLine();

        foreach (var l in lines.OrderBy(x => x.GoodsReceiptLineId))
        {
            sb.Append(l.GoodsReceiptLineId.ToString("D")).Append('|')
              .Append(decimal.Round(l.AcceptedBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture));
            if (l.DestinationLocationId.HasValue || !string.IsNullOrWhiteSpace(l.LotNumber))
            {
                sb.Append('|').Append(l.DestinationLocationId?.ToString("D") ?? "DEFAULT")
                  .Append('|').Append(string.IsNullOrWhiteSpace(l.LotNumber) ? "NOLOT" : l.LotNumber.Trim().ToUpperInvariant());
            }

            sb.AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
