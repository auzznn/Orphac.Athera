using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Procurement;

public sealed record ArrivalLineInput(
    Guid PoLineId,
    Guid ReceivedUomId,
    decimal PhysicallyDeliveredQuantity,
    bool FlagForInspection = false,
    string? DiscrepancyReasonCode = null,
    string? DiscrepancyNotes = null,
    string? ConditionEvidenceRef = null);

public sealed record RecordGoodsReceiptCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string SupplierDeliveryNoteNumber,
    string IdempotencyKey,
    DateOnly ReceivedDate,
    string ReceivingStation,
    string? VehicleOrSealRef,
    string? DocumentEvidenceRef,
    string? Notes,
    IReadOnlyList<ArrivalLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record RecordDockCheckCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    string IdempotencyKey,
    decimal PendingCheckBaseQuantity,
    decimal DockHeldBaseQuantity,
    decimal DockReleasedBaseQuantity,
    decimal DockRejectedBaseQuantity,
    string? DiscrepancyReasonCode,
    string? InspectionNotes,
    string? EvidenceReference,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReceivingWorkflowOutcome(
    bool Succeeded,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    GoodsReceipt? Receipt,
    bool WasIdempotentReplay)
{
    public static ReceivingWorkflowOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, null, false);

    public static ReceivingWorkflowOutcome Success(GoodsReceipt receipt, int statusCode = 200, bool replay = false) =>
        new(true, statusCode, null, null, receipt, replay);
}

/// <summary>
/// Authoritative transactional coordinator for S06 Gate 1 dock custody arrival & inspection.
/// Never mutates inventory stock/valuation pools or GL/GRNI ledgers.
/// </summary>
public static class GoodsReceiptWorkflowService
{
    public static async Task<ReceivingWorkflowOutcome> RecordArrivalAsync(
        AtheraDbContext db,
        RecordGoodsReceiptCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return ReceivingWorkflowOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        if (cmd.Lines.Count == 0)
        {
            return ReceivingWorkflowOutcome.Fail(400, "gr_lines_required", "Provide at least one delivered PO line.");
        }

        string normalizedDeliveryNote;
        string cleanStation;
        try
        {
            normalizedDeliveryNote = GuardValidation.RequireCode(cmd.SupplierDeliveryNoteNumber, nameof(cmd.SupplierDeliveryNoteNumber), 80);
            cleanStation = GuardValidation.RequireName(cmd.ReceivingStation, nameof(cmd.ReceivingStation), 160);
        }
        catch (Exception ex) when (ex is ArgumentException)
        {
            return ReceivingWorkflowOutcome.Fail(400, "receiving_validation_failed", ex.Message);
        }

        var normalizedIdemKey = cmd.IdempotencyKey.Trim();
        var duplicateLineIds = cmd.Lines.GroupBy(l => l.PoLineId).Any(g => g.Count() > 1);
        if (duplicateLineIds)
        {
            return ReceivingWorkflowOutcome.Fail(400, "duplicate_po_line_in_arrival", "Each PO line may appear only once per dock receipt arrival.");
        }

        string arrivalPayloadHash;
        try
        {
            arrivalPayloadHash = ReceivingHash.ComputeArrivalPayloadHash(
                cmd.PurchaseOrderId,
                normalizedDeliveryNote,
                cmd.ReceivedDate,
                cleanStation,
                cmd.VehicleOrSealRef,
                cmd.DocumentEvidenceRef,
                cmd.Lines.Select(l => (
                    l.PoLineId,
                    l.ReceivedUomId,
                    l.PhysicallyDeliveredQuantity,
                    l.FlagForInspection,
                    DiscrepancyReasonCodes.Normalize(l.DiscrepancyReasonCode))));
        }
        catch (ArgumentException ex)
        {
            return ReceivingWorkflowOutcome.Fail(400, "invalid_discrepancy_reason", ex.Message);
        }

        var existingIdem = await db.GoodsReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.IdempotencyKey == normalizedIdemKey,
                ct);

        if (existingIdem is not null)
        {
            return existingIdem.ArrivalPayloadHash == arrivalPayloadHash
                ? ReceivingWorkflowOutcome.Success(existingIdem, 200, replay: true)
                : ReceivingWorkflowOutcome.Fail(
                    409,
                    "duplicate_receipt_idempotency_conflict",
                    "Idempotency key already belongs to a different dock arrival payload.");
        }

        // Serialize per-PO arrivals & releases so concurrent capacity checks never overshoot.
        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return ReceivingWorkflowOutcome.Fail(404, "foreign_or_missing_purchase_order", "Purchase order does not exist in this legal entity.");
        }

        // Re-check idempotency inside PO lock in case a concurrent thread committed while waiting.
        var idemAfterLock = await db.GoodsReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.IdempotencyKey == normalizedIdemKey,
                ct);

        if (idemAfterLock is not null)
        {
            return idemAfterLock.ArrivalPayloadHash == arrivalPayloadHash
                ? ReceivingWorkflowOutcome.Success(idemAfterLock, 200, replay: true)
                : ReceivingWorkflowOutcome.Fail(
                    409,
                    "duplicate_receipt_idempotency_conflict",
                    "Idempotency key already belongs to a different dock arrival payload.");
        }

        if (po.Status != PurchaseOrderStatuses.Approved || po.CurrentApprovedRevisionId is not { } approvedRevId)
        {
            return ReceivingWorkflowOutcome.Fail(
                409,
                "po_not_eligible_for_receiving",
                $"Only APPROVED purchase orders may record physical dock arrivals (current: '{po.Status}').");
        }

        var duplicateDeliveryNote = await db.GoodsReceipts
            .AsNoTracking()
            .AnyAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.SupplierPartnerId == po.SupplierPartnerId &&
                r.SupplierDeliveryNoteNumber == normalizedDeliveryNote,
                ct);

        if (duplicateDeliveryNote)
        {
            return ReceivingWorkflowOutcome.Fail(
                409,
                "duplicate_supplier_delivery_note",
                $"Supplier delivery note '{normalizedDeliveryNote}' is already recorded in this legal entity.");
        }

        var approvedRev = await db.PurchaseOrderRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.RevisionId == approvedRevId,
                ct);

        if (approvedRev is null || approvedRev.Status != PurchaseOrderRevisionStatuses.Approved)
        {
            return ReceivingWorkflowOutcome.Fail(409, "po_not_eligible_for_receiving", "Current approved PO revision snapshot is unavailable.");
        }

        var approvedLines = await db.PurchaseOrderLines
            .AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == approvedRevId)
            .ToDictionaryAsync(l => l.PoLineId, ct);

        foreach (var lineInput in cmd.Lines)
        {
            if (!approvedLines.ContainsKey(lineInput.PoLineId))
            {
                return ReceivingWorkflowOutcome.Fail(
                    409,
                    "po_line_not_in_current_approved_revision",
                    $"PO line '{lineInput.PoLineId}' is not active in approved revision Rev {approvedRev.RevisionNumber}.");
            }
        }

        var itemIds = approvedLines.Values.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == cmd.TenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        var uoms = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == cmd.TenantId && u.Status == CatalogStatuses.Active)
            .ToDictionaryAsync(u => u.UomId, ct);

        var itemUoms = await db.ItemUomConversions.AsNoTracking()
            .Where(c => c.TenantId == cmd.TenantId && c.Status == CatalogStatuses.Active && itemIds.Contains(c.ItemId))
            .ToListAsync(ct);

        var priorClaimedByKey = await db.GoodsReceiptLines
            .AsNoTracking()
            .Where(gl => gl.TenantId == cmd.TenantId && gl.LegalEntityId == cmd.LegalEntityId && gl.PurchaseOrderId == po.PurchaseOrderId)
            .GroupBy(gl => gl.LineageLineKey)
            .Select(g => new
            {
                LineageLineKey = g.Key,
                ClaimedBase = g.Sum(x => x.PendingCheckBaseQuantity + x.DockReleasedBaseQuantity)
            })
            .ToDictionaryAsync(x => x.LineageLineKey, x => x.ClaimedBase, ct);

        var receiptId = Guid.NewGuid();
        var builtLines = new List<GoodsReceiptLine>(cmd.Lines.Count);

        for (var i = 0; i < cmd.Lines.Count; i++)
        {
            var input = cmd.Lines[i];
            var poLine = approvedLines[input.PoLineId];

            if (!items.TryGetValue(poLine.ItemId, out var item))
            {
                return ReceivingWorkflowOutcome.Fail(404, "item_not_found", $"Catalogue item '{poLine.ItemCodeSnapshot}' was not found.");
            }

            if (!uoms.TryGetValue(poLine.BaseUomId, out var baseUom) || !uoms.TryGetValue(input.ReceivedUomId, out var receivedUom))
            {
                return ReceivingWorkflowOutcome.Fail(404, "uom_not_found", $"Line {i + 1}: Selected unit of measure is inactive or missing.");
            }

            var conversion = itemUoms.FirstOrDefault(iu => iu.ItemId == item.ItemId && iu.UomId == receivedUom.UomId);
            ItemTransactionSnapshot uomSnap;
            try
            {
                uomSnap = UomConverter.CaptureSnapshot(item, baseUom, receivedUom, conversion, input.PhysicallyDeliveredQuantity, cmd.NowUtc);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return MapDomainFailure(ex);
            }

            var alreadyClaimed = priorClaimedByKey.GetValueOrDefault(poLine.LineageLineKey, 0m);
            var remainingEligible = Math.Max(0m, poLine.OrderedBaseQuantity - alreadyClaimed);

            try
            {
                var line = GoodsReceiptLine.CreateArrival(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    receiptId,
                    i + 1,
                    poLine,
                    receivedUom.UomId,
                    receivedUom.UomCode,
                    uomSnap.FactorToBase,
                    input.PhysicallyDeliveredQuantity,
                    uomSnap.ConvertedBaseQuantity,
                    remainingEligible,
                    input.FlagForInspection,
                    input.DiscrepancyReasonCode ?? DiscrepancyReasonCodes.None,
                    input.DiscrepancyNotes,
                    input.ConditionEvidenceRef,
                    cmd.NowUtc);
                builtLines.Add(line);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return MapDomainFailure(ex);
            }
        }

        var seq = await IncrementReceiptSequenceAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.ReceivedDate.Year, ct);
        var receiptNumber = $"GRN-{cmd.ReceivedDate.Year:D4}-{seq:D5}";

        GoodsReceipt receipt;
        try
        {
            receipt = GoodsReceipt.Create(
                receiptId,
                cmd.TenantId,
                cmd.LegalEntityId,
                receiptNumber,
                po,
                approvedRev,
                normalizedDeliveryNote,
                normalizedIdemKey,
                arrivalPayloadHash,
                cmd.ReceivedDate,
                cleanStation,
                cmd.VehicleOrSealRef,
                cmd.DocumentEvidenceRef,
                cmd.Notes,
                builtLines,
                cmd.ActorUserId,
                cmd.NowUtc);

            // Lock PO from cancellation while explicitly keeping AcceptedBaseQuantityTotal = 0m in Gate 1.
            po.RegisterDownstreamReference(0m, cmd.NowUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }

        db.GoodsReceipts.Add(receipt);
        db.GoodsReceiptLines.AddRange(builtLines);

        var dockChecks = builtLines
            .Select(l => DockCheck.Record(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                l,
                DockCustodyStatuses.Arrived,
                $"arrival:{l.GoodsReceiptLineId:N}",
                ReceivingHash.ComputeDockCheckPayloadHash(
                    l.GoodsReceiptLineId,
                    l.PendingCheckBaseQuantity,
                    l.DockHeldBaseQuantity,
                    l.DockReleasedBaseQuantity,
                    l.DockRejectedBaseQuantity,
                    l.DiscrepancyReasonCode,
                    l.DiscrepancyNotes,
                    l.ConditionEvidenceRef),
                cmd.ActorUserId,
                cmd.NowUtc))
            .ToList();

        db.DockChecks.AddRange(dockChecks);

        RecordReceivingAuditAndOutbox(
            db,
            receipt,
            cmd.ActorUserId,
            "GOODS_RECEIPT_RECORDED",
            $"Gate 1 dock arrival {receipt.ReceiptNumber} recorded (Surat Jalan {receipt.SupplierDeliveryNoteNumber}; unvalued custody).",
            "procurement.dockreceipt.recorded.v1",
            $"gr:arrival:{receipt.GoodsReceiptId:N}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return ReceivingWorkflowOutcome.Success(receipt, 201, replay: false);
    }

    public static async Task<ReceivingWorkflowOutcome> RecordDockCheckAsync(
        AtheraDbContext db,
        RecordDockCheckCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return ReceivingWorkflowOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        var normalizedIdemKey = cmd.IdempotencyKey.Trim();
        string normalizedReason;
        try
        {
            normalizedReason = DiscrepancyReasonCodes.Normalize(cmd.DiscrepancyReasonCode);
        }
        catch (ArgumentException ex)
        {
            return ReceivingWorkflowOutcome.Fail(400, "invalid_discrepancy_reason", ex.Message);
        }

        var payloadHash = ReceivingHash.ComputeDockCheckPayloadHash(
            cmd.GoodsReceiptLineId,
            cmd.PendingCheckBaseQuantity,
            cmd.DockHeldBaseQuantity,
            cmd.DockReleasedBaseQuantity,
            cmd.DockRejectedBaseQuantity,
            normalizedReason,
            cmd.InspectionNotes,
            cmd.EvidenceReference);

        var existingIdem = await db.DockChecks
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                d.TenantId == cmd.TenantId &&
                d.LegalEntityId == cmd.LegalEntityId &&
                d.GoodsReceiptLineId == cmd.GoodsReceiptLineId &&
                d.IdempotencyKey == normalizedIdemKey,
                ct);

        if (existingIdem is not null)
        {
            if (existingIdem.PayloadHash != payloadHash)
            {
                return ReceivingWorkflowOutcome.Fail(
                    409,
                    "duplicate_dock_check_idempotency_conflict",
                    "Dock check idempotency key already belongs to a different disposition split.");
            }

            var existingReceipt = await db.GoodsReceipts
                .AsNoTracking()
                .SingleAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.GoodsReceiptId == cmd.GoodsReceiptId, ct);
            return ReceivingWorkflowOutcome.Success(existingReceipt, 200, replay: true);
        }

        var receipt = await db.GoodsReceipts
            .SingleOrDefaultAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.GoodsReceiptId == cmd.GoodsReceiptId,
                ct);

        if (receipt is null)
        {
            return ReceivingWorkflowOutcome.Fail(404, "goods_receipt_not_found", "Goods receipt was not found in this legal entity.");
        }

        // Lock parent PO to serialize concurrent Gate 1 releases against approved capacity.
        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {receipt.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null || po.CurrentApprovedRevisionId is not { } currentApprovedRevId)
        {
            return ReceivingWorkflowOutcome.Fail(409, "po_not_eligible_for_receiving", "Parent PO has no active approved revision.");
        }

        var idemAfterLock = await db.DockChecks
            .AsNoTracking()
            .SingleOrDefaultAsync(d =>
                d.TenantId == cmd.TenantId &&
                d.LegalEntityId == cmd.LegalEntityId &&
                d.GoodsReceiptLineId == cmd.GoodsReceiptLineId &&
                d.IdempotencyKey == normalizedIdemKey,
                ct);

        if (idemAfterLock is not null)
        {
            return idemAfterLock.PayloadHash == payloadHash
                ? ReceivingWorkflowOutcome.Success(receipt, 200, replay: true)
                : ReceivingWorkflowOutcome.Fail(
                    409,
                    "duplicate_dock_check_idempotency_conflict",
                    "Dock check idempotency key already belongs to a different disposition split.");
        }

        var allReceiptLines = await db.GoodsReceiptLines
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.GoodsReceiptId == receipt.GoodsReceiptId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var targetLine = allReceiptLines.SingleOrDefault(l => l.GoodsReceiptLineId == cmd.GoodsReceiptLineId);
        if (targetLine is null)
        {
            return ReceivingWorkflowOutcome.Fail(404, "goods_receipt_line_not_found", "Goods receipt line was not found.");
        }

        var approvedLineQty = await db.PurchaseOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == cmd.TenantId &&
                l.LegalEntityId == cmd.LegalEntityId &&
                l.RevisionId == currentApprovedRevId &&
                l.LineageLineKey == targetLine.LineageLineKey)
            .Select(l => (decimal?)l.OrderedBaseQuantity)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var otherReleasedBase = await db.GoodsReceiptLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == cmd.TenantId &&
                l.LegalEntityId == cmd.LegalEntityId &&
                l.PurchaseOrderId == po.PurchaseOrderId &&
                l.LineageLineKey == targetLine.LineageLineKey &&
                l.GoodsReceiptLineId != targetLine.GoodsReceiptLineId)
            .SumAsync(l => (decimal?)l.DockReleasedBaseQuantity, ct) ?? 0m;

        var maxApprovedAllowedForLine = po.Status == PurchaseOrderStatuses.Cancelled
            ? 0m
            : Math.Max(0m, approvedLineQty - otherReleasedBase);

        var baseUomScale = await db.UnitsOfMeasure
            .AsNoTracking()
            .Where(u => u.TenantId == cmd.TenantId && u.UomId == targetLine.BaseUomId)
            .Select(u => u.QuantityPrecisionScale)
            .SingleAsync(ct);

        var previousCustodyStatus = targetLine.CustodyStatus;
        try
        {
            targetLine.ApplyDockDisposition(
                cmd.PendingCheckBaseQuantity,
                cmd.DockHeldBaseQuantity,
                cmd.DockReleasedBaseQuantity,
                cmd.DockRejectedBaseQuantity,
                maxApprovedAllowedForLine,
                baseUomScale,
                normalizedReason,
                cmd.InspectionNotes,
                cmd.EvidenceReference,
                cmd.NowUtc);

            receipt.RecalculateHeaderFromLines(allReceiptLines, cmd.NowUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }

        var checkRecord = DockCheck.Record(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            targetLine,
            previousCustodyStatus,
            normalizedIdemKey,
            payloadHash,
            cmd.ActorUserId,
            cmd.NowUtc);

        db.DockChecks.Add(checkRecord);

        RecordReceivingAuditAndOutbox(
            db,
            receipt,
            cmd.ActorUserId,
            "GOODS_RECEIPT_DOCK_CHECK_RECORDED",
            $"Dock check on {receipt.ReceiptNumber} L{targetLine.LineNumber} ({previousCustodyStatus} -> {targetLine.CustodyStatus}; Released={targetLine.DockReleasedBaseQuantity}, Held={targetLine.DockHeldBaseQuantity}, Rejected={targetLine.DockRejectedBaseQuantity}).",
            "procurement.dockcheck.recorded.v1",
            $"gr:check:{checkRecord.DockCheckId:N}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return ReceivingWorkflowOutcome.Success(receipt, 200, replay: false);
    }

    private static async Task<long> IncrementReceiptSequenceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        int fiscalYear,
        CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = """
            INSERT INTO procurement.receipt_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
            VALUES (@tid, @eid, @fy, 1)
            ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
            DO UPDATE SET last_number = procurement.receipt_sequences.last_number + 1
            RETURNING last_number;
            """;

        var pTid = cmd.CreateParameter();
        pTid.ParameterName = "tid";
        pTid.Value = tenantId;
        cmd.Parameters.Add(pTid);

        var pEid = cmd.CreateParameter();
        pEid.ParameterName = "eid";
        pEid.Value = entityId;
        cmd.Parameters.Add(pEid);

        var pFy = cmd.CreateParameter();
        pFy.ParameterName = "fy";
        pFy.Value = fiscalYear;
        cmd.Parameters.Add(pFy);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static void RecordReceivingAuditAndOutbox(
        AtheraDbContext db,
        GoodsReceipt receipt,
        Guid actorUserId,
        string eventType,
        string summary,
        string outboxTopic,
        string sourceKey,
        DateTimeOffset nowUtc)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            receipt.GoodsReceiptId,
            receipt.ReceiptNumber,
            receipt.PurchaseOrderId,
            receipt.PoNumberSnapshot,
            receipt.PoRevisionNumberSnapshot,
            receipt.SupplierDeliveryNoteNumber,
            receipt.Status,
            receipt.HasFinancialPosting,
            receipt.TotalDeliveredBaseQuantity,
            receipt.TotalUnresolvedCustodyBaseQuantity,
            receipt.TotalReleasedBaseQuantity,
            receipt.TotalHeldBaseQuantity,
            receipt.TotalRejectedBaseQuantity,
            receipt.TotalValuedAcceptedBaseQuantity
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            receipt.TenantId,
            receipt.LegalEntityId,
            actorUserId,
            eventType,
            "GOODS_RECEIPT",
            receipt.GoodsReceiptId.ToString("D"),
            summary,
            payloadJson,
            nowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            receipt.TenantId,
            receipt.LegalEntityId,
            outboxTopic,
            sourceKey,
            payloadJson,
            nowUtc));
    }

    private static ReceivingWorkflowOutcome MapDomainFailure(Exception ex)
    {
        var msg = ex.Message ?? "Receiving validation failed.";
        var colon = msg.IndexOf(':');
        if (colon > 0 && colon < 64)
        {
            var code = msg[..colon].Trim();
            if (code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                var detail = msg[(colon + 1)..].Trim();
                var status = code switch
                {
                    "od01_overdelivery_release_blocked" or
                    "po_not_eligible_for_receiving" or
                    "duplicate_supplier_delivery_note" or
                    "unsupported_uom_conversion" or
                    "fractional_discrete_base_quantity" or
                    "dock_quantity_conservation_mismatch" or
                    "dock_release_below_valued_accepted" => 409,
                    _ => 400
                };
                return ReceivingWorkflowOutcome.Fail(status, code, detail);
            }
        }

        return ReceivingWorkflowOutcome.Fail(400, "receiving_validation_failed", msg);
    }
}
