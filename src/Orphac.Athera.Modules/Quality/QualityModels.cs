using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Quality;

public static class QualityReasonCodes
{
    public const string PassedSpecification = "PASSED_SPECIFICATION";
    public const string DamagedOrContaminated = "DAMAGED_OR_CONTAMINATED";
    public const string SpecDeviationHold = "SPEC_DEVIATION_HOLD";
    public const string MissingCertificate = "MISSING_CERTIFICATE";
    public const string SupervisorReEvaluation = "SUPERVISOR_REEVALUATION";

    public static readonly IReadOnlyList<string> All =
    [
        PassedSpecification,
        DamagedOrContaminated,
        SpecDeviationHold,
        MissingCertificate,
        SupervisorReEvaluation
    ];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? PassedSpecification : raw.Trim().ToUpperInvariant();
        if (value is not (PassedSpecification or DamagedOrContaminated or SpecDeviationHold or MissingCertificate or SupervisorReEvaluation))
        {
            throw new ArgumentException($"Unsupported quality reason code '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class ReceiptInspectionStatuses
{
    public const string Pending = "PENDING";
    public const string PartiallyDecided = "PARTIALLY_DECIDED";
    public const string Completed = "COMPLETED";

    public static readonly IReadOnlyList<string> All = [Pending, PartiallyDecided, Completed];
}

public sealed class InspectionSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private InspectionSequence()
    {
    }
}

/// <summary>
/// Quality inspection work-queue header created atomically when Gate 2 receives stock into QUALITY_INSPECTION.
/// Conserves Pending + Released + Blocked == InspectedTotalBaseQuantity without mutating valuation carrying value.
/// </summary>
public sealed class ReceiptInspection
{
    public Guid ReceiptInspectionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string InspectionNumber { get; private set; } = string.Empty;
    public Guid ValuedAcceptanceId { get; private set; }
    public Guid ValuedAcceptanceLineId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BaseUomId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid QuarantineLocationId { get; private set; }
    public Guid? StockLotId { get; private set; }

    public decimal InspectedTotalBaseQuantity { get; private set; }
    public decimal PendingBaseQuantity { get; private set; }
    public decimal ReleasedBaseQuantity { get; private set; }
    public decimal BlockedBaseQuantity { get; private set; }
    public string Status { get; private set; } = ReceiptInspectionStatuses.Pending;

    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ReceiptInspection()
    {
    }

    public static ReceiptInspection Create(
        Guid receiptInspectionId,
        Guid tenantId,
        Guid legalEntityId,
        string inspectionNumber,
        Guid valuedAcceptanceId,
        Guid valuedAcceptanceLineId,
        Guid goodsReceiptId,
        Guid itemId,
        Guid baseUomId,
        Guid warehouseId,
        Guid quarantineLocationId,
        Guid? stockLotId,
        decimal inspectedQuantity,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(receiptInspectionId, nameof(receiptInspectionId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceId, nameof(valuedAcceptanceId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceLineId, nameof(valuedAcceptanceLineId));
        GuardValidation.RequireNonEmptyGuid(goodsReceiptId, nameof(goodsReceiptId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));
        GuardValidation.RequireNonEmptyGuid(warehouseId, nameof(warehouseId));
        GuardValidation.RequireNonEmptyGuid(quarantineLocationId, nameof(quarantineLocationId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var qty = decimal.Round(inspectedQuantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentException("Inspection quantity must be positive.", nameof(inspectedQuantity));
        }

        return new ReceiptInspection
        {
            ReceiptInspectionId = receiptInspectionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            InspectionNumber = GuardValidation.RequireCode(inspectionNumber, nameof(inspectionNumber), 40),
            ValuedAcceptanceId = valuedAcceptanceId,
            ValuedAcceptanceLineId = valuedAcceptanceLineId,
            GoodsReceiptId = goodsReceiptId,
            ItemId = itemId,
            BaseUomId = baseUomId,
            WarehouseId = warehouseId,
            QuarantineLocationId = quarantineLocationId,
            StockLotId = stockLotId,
            InspectedTotalBaseQuantity = qty,
            PendingBaseQuantity = qty,
            ReleasedBaseQuantity = 0m,
            BlockedBaseQuantity = 0m,
            Status = ReceiptInspectionStatuses.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyDisposition(decimal releaseQuantity, decimal blockQuantity, DateTimeOffset nowUtc)
    {
        var release = decimal.Round(releaseQuantity, 6, MidpointRounding.AwayFromZero);
        var block = decimal.Round(blockQuantity, 6, MidpointRounding.AwayFromZero);
        if (release < 0m || block < 0m || release + block <= 0m)
        {
            throw new ArgumentException("disposition_qty_must_be_positive: Provide positive release and/or block quantities.");
        }

        var decidedTotal = decimal.Round(release + block, 6, MidpointRounding.AwayFromZero);
        if (decidedTotal > PendingBaseQuantity)
        {
            throw new InvalidOperationException(
                $"disposition_exceeds_pending_quantity: Decided quantity ({decidedTotal:0.######}) exceeds pending inspection quantity ({PendingBaseQuantity:0.######}).");
        }

        PendingBaseQuantity = decimal.Round(PendingBaseQuantity - decidedTotal, 6, MidpointRounding.AwayFromZero);
        ReleasedBaseQuantity = decimal.Round(ReleasedBaseQuantity + release, 6, MidpointRounding.AwayFromZero);
        BlockedBaseQuantity = decimal.Round(BlockedBaseQuantity + block, 6, MidpointRounding.AwayFromZero);

        if (PendingBaseQuantity + ReleasedBaseQuantity + BlockedBaseQuantity != InspectedTotalBaseQuantity)
        {
            throw new InvalidOperationException("inspection_conservation_mismatch: Inspection quantities failed conservation check.");
        }

        Status = PendingBaseQuantity == 0m ? ReceiptInspectionStatuses.Completed : ReceiptInspectionStatuses.PartiallyDecided;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

/// <summary>
/// Strictly append-only quality disposition record in `quality.inspection_dispositions`.
/// </summary>
public sealed class InspectionDisposition
{
    public Guid InspectionDispositionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ReceiptInspectionId { get; private set; }
    public Guid StockMovementId { get; private set; }
    public DateOnly DispositionDate { get; private set; }
    public decimal ReleasedBaseQuantity { get; private set; }
    public Guid? TargetReleaseLocationId { get; private set; }
    public decimal BlockedBaseQuantity { get; private set; }
    public Guid? TargetBlockLocationId { get; private set; }
    public string ReasonCode { get; private set; } = QualityReasonCodes.PassedSpecification;
    public string? EvidenceReference { get; private set; }
    public string? Notes { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public Guid DecidedByUserId { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }

    private InspectionDisposition()
    {
    }

    public static InspectionDisposition Record(
        Guid dispositionId,
        Guid tenantId,
        Guid legalEntityId,
        Guid receiptInspectionId,
        Guid stockMovementId,
        DateOnly dispositionDate,
        decimal releasedBaseQuantity,
        Guid? targetReleaseLocationId,
        decimal blockedBaseQuantity,
        Guid? targetBlockLocationId,
        string reasonCode,
        string? evidenceReference,
        string? notes,
        string idempotencyKey,
        string payloadHash,
        Guid decidedByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(dispositionId, nameof(dispositionId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(receiptInspectionId, nameof(receiptInspectionId));
        GuardValidation.RequireNonEmptyGuid(stockMovementId, nameof(stockMovementId));
        GuardValidation.RequireNonEmptyGuid(decidedByUserId, nameof(decidedByUserId));

        var release = decimal.Round(releasedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var block = decimal.Round(blockedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var normalizedReason = QualityReasonCodes.Normalize(reasonCode);
        var normEvidence = GuardValidation.NormalizeOptional(evidenceReference, 120, nameof(evidenceReference));
        var normNotes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes));

        if (release < 0m || block < 0m || release + block <= 0m)
        {
            throw new ArgumentException("disposition_qty_must_be_positive: Provide positive release and/or block quantities.");
        }

        if (block > 0m && normalizedReason == QualityReasonCodes.PassedSpecification && normNotes is null && normEvidence is null)
        {
            throw new ArgumentException("disposition_block_reason_required: Blocking stock requires an inspection failure reason, evidence reference, or note.");
        }

        if (release > 0m && !targetReleaseLocationId.HasValue)
        {
            throw new ArgumentException("Target release location is required when releasing stock.", nameof(targetReleaseLocationId));
        }

        if (block > 0m && !targetBlockLocationId.HasValue)
        {
            throw new ArgumentException("Target blocked location is required when blocking stock.", nameof(targetBlockLocationId));
        }

        return new InspectionDisposition
        {
            InspectionDispositionId = dispositionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ReceiptInspectionId = receiptInspectionId,
            StockMovementId = stockMovementId,
            DispositionDate = dispositionDate,
            ReleasedBaseQuantity = release,
            TargetReleaseLocationId = release > 0m ? targetReleaseLocationId : null,
            BlockedBaseQuantity = block,
            TargetBlockLocationId = block > 0m ? targetBlockLocationId : null,
            ReasonCode = normalizedReason,
            EvidenceReference = normEvidence,
            Notes = normNotes,
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            PayloadHash = GuardValidation.RequireName(payloadHash, nameof(payloadHash), 64),
            DecidedByUserId = decidedByUserId,
            DecidedAtUtc = nowUtc
        };
    }
}

public static class QualityDispositionHash
{
    public static string ComputePayloadHash(
        Guid receiptInspectionId,
        DateOnly dispositionDate,
        decimal releasedBaseQty,
        Guid? targetReleaseLocationId,
        decimal blockedBaseQty,
        Guid? targetBlockLocationId,
        string reasonCode)
    {
        var raw = string.Join(
            '|',
            receiptInspectionId.ToString("D"),
            dispositionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal.Round(releasedBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            targetReleaseLocationId?.ToString("D") ?? "NONE",
            decimal.Round(blockedBaseQty, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture),
            targetBlockLocationId?.ToString("D") ?? "NONE",
            reasonCode.Trim().ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
