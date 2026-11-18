using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Quality;

public sealed record RecordInspectionDispositionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ReceiptInspectionId,
    DateOnly DispositionDate,
    decimal ReleasedBaseQuantity,
    Guid? TargetReleaseLocationId,
    decimal BlockedBaseQuantity,
    Guid? TargetBlockLocationId,
    string ReasonCode,
    string? EvidenceReference,
    string? Notes,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record InspectionDispositionOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    ReceiptInspection? Inspection,
    InspectionDisposition? Disposition,
    StockMovement? StockMovement)
{
    public static InspectionDispositionOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, null, null);
}

/// <summary>
/// Quality module service managing post-acceptance quarantine queues and append-only release/block dispositions.
/// Delegates physical availability moves to InventoryAvailabilityService so stock & status history remain conserved.
/// </summary>
public static class QualityInspectionService
{
    public static async Task<IReadOnlyList<ReceiptInspection>> CreateInspectionsForAcceptedReceiptAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid valuedAcceptanceId,
        string acceptanceNumber,
        Guid goodsReceiptId,
        DateOnly acceptanceDate,
        IReadOnlyList<ResolvedAcceptedStockTarget> targets,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var created = new List<ReceiptInspection>(targets.Count);

        foreach (var target in targets)
        {
            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db,
                "quality.inspection_sequences",
                tenantId,
                entityId,
                acceptanceDate.Year,
                ct);
            var number = $"QCI-{acceptanceDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";

            var inspection = ReceiptInspection.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                number,
                valuedAcceptanceId,
                target.ValuedAcceptanceLineId,
                goodsReceiptId,
                target.ItemId,
                target.BaseUomId,
                target.WarehouseId,
                target.LocationId,
                target.StockLotId,
                target.AcceptedBaseQuantity,
                actorUserId,
                nowUtc);

            created.Add(inspection);
            db.ReceiptInspections.Add(inspection);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                tenantId,
                entityId,
                actorUserId,
                "QUALITY_INSPECTION_QUEUED",
                "RECEIPT_INSPECTION",
                inspection.ReceiptInspectionId.ToString("D"),
                $"Queued {inspection.InspectionNumber} from {acceptanceNumber} ({target.AcceptedBaseQuantity:0.######} units in QUALITY_INSPECTION)",
                JsonSerializer.Serialize(new
                {
                    inspection.ReceiptInspectionId,
                    inspection.InspectionNumber,
                    valuedAcceptanceId,
                    acceptanceNumber,
                    target.ItemId,
                    target.LocationId,
                    target.StockLotId,
                    target.AcceptedBaseQuantity
                }),
                nowUtc));
        }

        return created;
    }

    public static async Task<InspectionDispositionOutcome> RecordDispositionAsync(
        AtheraDbContext db,
        RecordInspectionDispositionCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return InspectionDispositionOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        var releaseQty = decimal.Round(cmd.ReleasedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var blockQty = decimal.Round(cmd.BlockedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (releaseQty < 0m || blockQty < 0m || releaseQty + blockQty <= 0m)
        {
            return InspectionDispositionOutcome.Fail(400, "disposition_qty_must_be_positive", "Provide positive release and/or block quantities.");
        }

        string normalizedReason;
        try
        {
            normalizedReason = QualityReasonCodes.Normalize(cmd.ReasonCode);
        }
        catch (ArgumentException ex)
        {
            return InspectionDispositionOutcome.Fail(400, "invalid_quality_reason", ex.Message);
        }

        var idempotencyKey = cmd.IdempotencyKey.Trim();
        var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);

        WarehouseLocation? releaseLoc = null;
        WarehouseLocation? blockLoc = null;
        try
        {
            if (releaseQty > 0m)
            {
                releaseLoc = layout.ResolveForStatus(StockStatuses.Unrestricted, cmd.TargetReleaseLocationId);
            }

            if (blockQty > 0m)
            {
                blockLoc = layout.ResolveForStatus(StockStatuses.Blocked, cmd.TargetBlockLocationId);
            }
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            return InspectionDispositionOutcome.Fail(409, idx > 0 ? ex.Message[..idx].Trim() : "location_invalid", idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message);
        }

        var payloadHash = QualityDispositionHash.ComputePayloadHash(
            cmd.ReceiptInspectionId,
            cmd.DispositionDate,
            releaseQty,
            releaseLoc?.LocationId,
            blockQty,
            blockLoc?.LocationId,
            normalizedReason);

        var existingFast = await db.InspectionDispositions.AsNoTracking()
            .SingleOrDefaultAsync(d =>
                d.TenantId == cmd.TenantId &&
                d.LegalEntityId == cmd.LegalEntityId &&
                d.ReceiptInspectionId == cmd.ReceiptInspectionId &&
                d.IdempotencyKey == idempotencyKey,
                ct);
        if (existingFast is not null)
        {
            return await BuildReplayAsync(db, cmd.TenantId, cmd.LegalEntityId, existingFast, payloadHash, ct);
        }

        var inspection = (await db.ReceiptInspections
            .FromSqlInterpolated(
                $"""
                SELECT * FROM quality.receipt_inspections
                WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND receipt_inspection_id = {cmd.ReceiptInspectionId}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .SingleOrDefault();

        if (inspection is null)
        {
            return InspectionDispositionOutcome.Fail(404, "receipt_inspection_not_found", "Receipt inspection record was not found in this legal entity.");
        }

        var existingLocked = await db.InspectionDispositions.AsNoTracking()
            .SingleOrDefaultAsync(d =>
                d.TenantId == cmd.TenantId &&
                d.LegalEntityId == cmd.LegalEntityId &&
                d.ReceiptInspectionId == cmd.ReceiptInspectionId &&
                d.IdempotencyKey == idempotencyKey,
                ct);
        if (existingLocked is not null)
        {
            return await BuildReplayAsync(db, cmd.TenantId, cmd.LegalEntityId, existingLocked, payloadHash, ct);
        }

        if (releaseLoc is not null && releaseLoc.WarehouseId != inspection.WarehouseId)
        {
            return InspectionDispositionOutcome.Fail(409, "cross_warehouse_transfer_not_in_scope", "Release destination location must belong to the same warehouse.");
        }

        if (blockLoc is not null && blockLoc.WarehouseId != inspection.WarehouseId)
        {
            return InspectionDispositionOutcome.Fail(409, "cross_warehouse_transfer_not_in_scope", "Blocked destination location must belong to the same warehouse.");
        }

        if (releaseQty + blockQty > inspection.PendingBaseQuantity)
        {
            return InspectionDispositionOutcome.Fail(
                409,
                "disposition_exceeds_pending_quantity",
                $"Decided quantity ({releaseQty + blockQty:0.######}) exceeds pending quarantine balance ({inspection.PendingBaseQuantity:0.######}).");
        }

        var item = await db.Items.AsNoTracking().SingleAsync(i => i.TenantId == cmd.TenantId && i.ItemId == inspection.ItemId, ct);
        var fromLoc = layout.LocationsById[inspection.QuarantineLocationId];

        var targets = new List<(WarehouseLocation TargetLocation, string TargetStatus, decimal Quantity)>();
        if (releaseLoc is not null && releaseQty > 0m)
        {
            targets.Add((releaseLoc, StockStatuses.Unrestricted, releaseQty));
        }

        if (blockLoc is not null && blockQty > 0m)
        {
            targets.Add((blockLoc, StockStatuses.Blocked, blockQty));
        }

        var dispositionId = Guid.NewGuid();
        var disposition = null as InspectionDisposition;
        try
        {
            // Validate block reason/evidence upfront before moving inventory or allocating sequence.
            _ = InspectionDisposition.Record(
                dispositionId,
                cmd.TenantId,
                cmd.LegalEntityId,
                inspection.ReceiptInspectionId,
                Guid.NewGuid(),
                cmd.DispositionDate,
                releaseQty,
                releaseLoc?.LocationId,
                blockQty,
                blockLoc?.LocationId,
                normalizedReason,
                cmd.EvidenceReference,
                cmd.Notes,
                idempotencyKey,
                payloadHash,
                cmd.ActorUserId,
                cmd.NowUtc);
        }
        catch (ArgumentException ex)
        {
            return InspectionDispositionOutcome.Fail(400, "invalid_quality_disposition", ex.Message);
        }

        var movOutcome = await InventoryAvailabilityService.PostQualitySplitMovementAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            item,
            fromLoc,
            StockStatuses.QualityInspection,
            inspection.StockLotId,
            targets,
            cmd.DispositionDate,
            "QUALITY_DISPOSITION",
            inspection.ReceiptInspectionId,
            inspection.InspectionNumber,
            dispositionId,
            idempotencyKey,
            cmd.Notes ?? $"{normalizedReason} {(cmd.EvidenceReference ?? string.Empty)}".Trim(),
            cmd.ActorUserId,
            cmd.NowUtc,
            ct);

        if (!movOutcome.Succeeded || movOutcome.Movement is null)
        {
            return InspectionDispositionOutcome.Fail(
                movOutcome.FailureHttpStatus,
                movOutcome.ErrorCode ?? "quality_movement_failed",
                movOutcome.ErrorDetail ?? "Failed applying stock status transition for quality disposition.");
        }

        try
        {
            inspection.ApplyDisposition(releaseQty, blockQty, cmd.NowUtc);
            disposition = InspectionDisposition.Record(
                dispositionId,
                cmd.TenantId,
                cmd.LegalEntityId,
                inspection.ReceiptInspectionId,
                movOutcome.Movement.StockMovementId,
                cmd.DispositionDate,
                releaseQty,
                releaseLoc?.LocationId,
                blockQty,
                blockLoc?.LocationId,
                normalizedReason,
                cmd.EvidenceReference,
                cmd.Notes,
                idempotencyKey,
                payloadHash,
                cmd.ActorUserId,
                cmd.NowUtc);
        }
        catch (Exception ex)
        {
            return InspectionDispositionOutcome.Fail(409, "invalid_quality_disposition", ex.Message);
        }

        db.InspectionDispositions.Add(disposition);

        var payloadJson = JsonSerializer.Serialize(new
        {
            inspection.ReceiptInspectionId,
            inspection.InspectionNumber,
            disposition.InspectionDispositionId,
            movOutcome.Movement.StockMovementId,
            movOutcome.Movement.MovementNumber,
            ReleasedBaseQuantity = releaseQty,
            BlockedBaseQuantity = blockQty,
            PendingBaseQuantity = inspection.PendingBaseQuantity,
            inspection.Status,
            ReasonCode = normalizedReason,
            disposition.EvidenceReference
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "QUALITY_INSPECTION_DISPOSITION_RECORDED",
            "RECEIPT_INSPECTION",
            inspection.ReceiptInspectionId.ToString("D"),
            $"Disposed {inspection.InspectionNumber}: Released {releaseQty:0.######}, Blocked {blockQty:0.######}, Pending {inspection.PendingBaseQuantity:0.######}",
            payloadJson,
            cmd.NowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "quality.inspection.decided.v1",
            $"quality:disposition:{disposition.InspectionDispositionId:D}",
            payloadJson,
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return new InspectionDispositionOutcome(true, false, 201, null, null, inspection, disposition, movOutcome.Movement);
    }

    private static async Task<InspectionDispositionOutcome> BuildReplayAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        InspectionDisposition existing,
        string expectedHash,
        CancellationToken ct)
    {
        if (!string.Equals(existing.PayloadHash, expectedHash, StringComparison.Ordinal))
        {
            return InspectionDispositionOutcome.Fail(
                409,
                "duplicate_disposition_idempotency_conflict",
                "Idempotency key already belongs to a different quality inspection disposition.");
        }

        var inspection = await db.ReceiptInspections.AsNoTracking()
            .SingleAsync(i => i.TenantId == tenantId && i.LegalEntityId == entityId && i.ReceiptInspectionId == existing.ReceiptInspectionId, ct);
        var mov = await db.StockMovements.AsNoTracking()
            .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.StockMovementId == existing.StockMovementId, ct);

        return new InspectionDispositionOutcome(true, true, 200, null, null, inspection, existing, mov);
    }
}
