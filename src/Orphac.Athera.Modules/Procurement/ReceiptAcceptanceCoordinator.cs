using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Procurement;

public sealed record AcceptReceiptLineInput(
    Guid GoodsReceiptLineId,
    decimal AcceptedBaseQuantity);

public sealed record PreviewAcceptanceCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid GoodsReceiptId,
    DateOnly AcceptanceDate,
    string StockStatus,
    IReadOnlyList<AcceptReceiptLineInput> Lines);

public sealed record AcceptReceiptCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid GoodsReceiptId,
    string IdempotencyKey,
    DateOnly AcceptanceDate,
    string StockStatus,
    string? Notes,
    IReadOnlyList<AcceptReceiptLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PreviewJournalLineSuggestion(
    string Side,
    string AccountCode,
    string AccountName,
    decimal Amount,
    string Description);

public sealed record AcceptancePreviewResult(
    bool Succeeded,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    Guid GoodsReceiptId,
    string ReceiptNumber,
    string CurrencyCode,
    DateOnly AcceptanceDate,
    string StockStatus,
    bool CanPost,
    decimal TotalAcceptedBaseQuantity,
    decimal TotalAcceptedAmount,
    IReadOnlyList<InventoryLineValuationPreview> Lines,
    IReadOnlyList<PreviewJournalLineSuggestion> JournalPreview,
    IReadOnlyList<string> BlockingReasons)
{
    public static AcceptancePreviewResult Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, Guid.Empty, string.Empty, "IDR", default, StockStatuses.Unrestricted, false, 0m, 0m, Array.Empty<InventoryLineValuationPreview>(), Array.Empty<PreviewJournalLineSuggestion>(), [detail]);
}

public sealed record ReceiptAcceptanceOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    ValuedAcceptance? Acceptance,
    IReadOnlyList<ValuedAcceptanceLine> AcceptanceLines,
    GoodsReceipt? Receipt,
    StockMovement? StockMovement,
    ReceiptAccrual? ReceiptAccrual,
    JournalEntry? GlJournal,
    IReadOnlyList<JournalLine> GlJournalLines)
{
    public static ReceiptAcceptanceOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, Array.Empty<ValuedAcceptanceLine>(), null, null, null, null, Array.Empty<JournalLine>());
}

/// <summary>
/// S07 Gate 2 transaction coordinator (TX-RECEIPT) atomically committing valued acceptance source records,
/// inventory positions/movements/moving-average pools, balanced Dr Inventory / Cr GRNI journals, audit, and outbox.
/// </summary>
public static class ReceiptAcceptanceCoordinator
{
    private sealed record ResolvedLinePlan(
        GoodsReceiptLine ReceiptLine,
        InventoryLineValuationInput ValuationInput,
        decimal UnitPriceBaseSnapshot);

    public static async Task<AcceptancePreviewResult> PreviewAcceptanceAsync(
        AtheraDbContext db,
        PreviewAcceptanceCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return AcceptancePreviewResult.Fail(400, "acceptance_lines_required", "Provide at least one receipt line with positive accepted quantity.");
        }

        string normalizedStatus;
        try
        {
            normalizedStatus = StockStatuses.Normalize(cmd.StockStatus);
        }
        catch (ArgumentException ex)
        {
            return AcceptancePreviewResult.Fail(400, "invalid_stock_status", ex.Message);
        }

        var receipt = await db.GoodsReceipts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.GoodsReceiptId == cmd.GoodsReceiptId, ct);
        if (receipt is null)
        {
            return AcceptancePreviewResult.Fail(404, "goods_receipt_not_found", "Goods receipt was not found in this legal entity.");
        }

        var po = await db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PurchaseOrderId == receipt.PurchaseOrderId, ct);

        var receiptLines = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.GoodsReceiptId == receipt.GoodsReceiptId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var validation = await BuildValidatedLinePlanAsync(db, cmd.TenantId, cmd.LegalEntityId, receipt, po, receiptLines, cmd.Lines, ct);
        if (!validation.ValidHeader)
        {
            return AcceptancePreviewResult.Fail(validation.HttpStatus, validation.ErrorCode!, validation.ErrorDetail!);
        }

        var previews = validation.ValuationInputs.Count == 0
            ? Array.Empty<InventoryLineValuationPreview>()
            : await InventoryValuationService.PreviewReceiptValuationAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                po!.CurrencyCode,
                validation.ValuationInputs.Select(x => x.ValuationInput).ToList(),
                ct);

        var accrualResolution = await GlPostingKernel.PreviewPeriodAndAccrualMappingsAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.AcceptanceDate,
            ct);

        var blockingReasons = new List<string>(validation.BlockingReasons);
        foreach (var linePrev in previews)
        {
            if (!string.IsNullOrWhiteSpace(linePrev.BlockingReason))
            {
                blockingReasons.Add(linePrev.BlockingReason);
            }
        }
        blockingReasons.AddRange(accrualResolution.BlockingReasons);

        var totalQty = decimal.Round(previews.Sum(p => p.AcceptedBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var totalAmount = decimal.Round(previews.Sum(p => p.AcceptedLineAmount), 4, MidpointRounding.AwayFromZero);

        var journalPreview = new List<PreviewJournalLineSuggestion>();
        if (totalAmount > 0m)
        {
            var invCode = string.IsNullOrWhiteSpace(accrualResolution.InventoryAssetAccountCode) ? "1300" : accrualResolution.InventoryAssetAccountCode;
            var invName = string.IsNullOrWhiteSpace(accrualResolution.InventoryAssetAccountName) ? "Inventory Asset" : accrualResolution.InventoryAssetAccountName;
            var grniCode = string.IsNullOrWhiteSpace(accrualResolution.GrniAccountCode) ? "2110" : accrualResolution.GrniAccountCode;
            var grniName = string.IsNullOrWhiteSpace(accrualResolution.GrniAccountName) ? "GRNI Clearing" : accrualResolution.GrniAccountName;

            journalPreview.Add(new PreviewJournalLineSuggestion("DEBIT", invCode, invName, totalAmount, $"Inventory acceptance ({normalizedStatus})"));
            journalPreview.Add(new PreviewJournalLineSuggestion("CREDIT", grniCode, grniName, totalAmount, "GRNI accrual"));
        }

        return new AcceptancePreviewResult(
            true,
            200,
            null,
            null,
            receipt.GoodsReceiptId,
            receipt.ReceiptNumber,
            po!.CurrencyCode,
            cmd.AcceptanceDate,
            normalizedStatus,
            blockingReasons.Count == 0 && previews.Count > 0,
            totalQty,
            totalAmount,
            previews,
            journalPreview,
            blockingReasons.Distinct(StringComparer.Ordinal).ToList());
    }

    public static async Task<ReceiptAcceptanceOutcome> AcceptReceiptAsync(
        AtheraDbContext db,
        AcceptReceiptCommand cmd,
        CancellationToken ct,
        Func<CancellationToken, Task>? beforeCommitFaultHook = null)
    {
        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Trim().Length > 80)
        {
            return ReceiptAcceptanceOutcome.Fail(400, "idempotency_key_required", "Provide an idempotency key between 1 and 80 characters.");
        }

        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return ReceiptAcceptanceOutcome.Fail(400, "acceptance_lines_required", "Provide at least one receipt line with positive accepted quantity.");
        }

        string normalizedStatus;
        try
        {
            normalizedStatus = StockStatuses.Normalize(cmd.StockStatus);
        }
        catch (ArgumentException ex)
        {
            return ReceiptAcceptanceOutcome.Fail(400, "invalid_stock_status", ex.Message);
        }

        var idempotencyKey = cmd.IdempotencyKey.Trim();
        var payloadHash = ValuedAcceptanceHash.ComputePayloadHash(
            cmd.GoodsReceiptId,
            cmd.AcceptanceDate,
            normalizedStatus,
            cmd.Lines.Select(l => (l.GoodsReceiptLineId, l.AcceptedBaseQuantity)));

        var existingFast = await db.ValuedAcceptances.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.GoodsReceiptId == cmd.GoodsReceiptId && x.IdempotencyKey == idempotencyKey, ct);
        if (existingFast is not null)
        {
            return await BuildReplayOutcomeAsync(db, cmd.TenantId, cmd.LegalEntityId, existingFast, payloadHash, ct);
        }

        var receiptRef = await db.GoodsReceipts.AsNoTracking()
            .Where(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.GoodsReceiptId == cmd.GoodsReceiptId)
            .Select(r => new { r.PurchaseOrderId })
            .SingleOrDefaultAsync(ct);

        if (receiptRef is null)
        {
            return ReceiptAcceptanceOutcome.Fail(404, "goods_receipt_not_found", "Goods receipt was not found in this legal entity.");
        }

        // Follow strict global lock hierarchy (PurchaseOrder -> GoodsReceipt/Lines -> Inventory Pools/Positions -> Period).
        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {receiptRef.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (po is null)
        {
            return ReceiptAcceptanceOutcome.Fail(409, "po_not_eligible_for_receiving", "Parent purchase order was not found.");
        }

        var receipt = (await db.GoodsReceipts
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.goods_receipts WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND goods_receipt_id = {cmd.GoodsReceiptId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

        if (receipt is null)
        {
            return ReceiptAcceptanceOutcome.Fail(404, "goods_receipt_not_found", "Goods receipt was not found in this legal entity.");
        }

        var existingLocked = await db.ValuedAcceptances.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == cmd.TenantId && x.LegalEntityId == cmd.LegalEntityId && x.GoodsReceiptId == cmd.GoodsReceiptId && x.IdempotencyKey == idempotencyKey, ct);
        if (existingLocked is not null)
        {
            return await BuildReplayOutcomeAsync(db, cmd.TenantId, cmd.LegalEntityId, existingLocked, payloadHash, ct);
        }

        var receiptLines = await db.GoodsReceiptLines
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.goods_receipt_lines WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND goods_receipt_id = {receipt.GoodsReceiptId} ORDER BY line_number FOR UPDATE")
            .ToListAsync(ct);

        var validation = await BuildValidatedLinePlanAsync(db, cmd.TenantId, cmd.LegalEntityId, receipt, po, receiptLines, cmd.Lines, ct);
        if (!validation.ValidHeader)
        {
            return ReceiptAcceptanceOutcome.Fail(validation.HttpStatus, validation.ErrorCode!, validation.ErrorDetail!);
        }

        if (validation.FirstBlockingErrorCode is not null)
        {
            return ReceiptAcceptanceOutcome.Fail(
                validation.FirstBlockingStatus,
                validation.FirstBlockingErrorCode,
                validation.FirstBlockingDetail!);
        }

        const string savepointName = "sp_accept_receipt";
        var activeTx = db.Database.CurrentTransaction;
        if (activeTx is not null)
        {
            await activeTx.CreateSavepointAsync(savepointName, ct);
        }

        async Task<ReceiptAcceptanceOutcome> RollbackAndFailAsync(int status, string code, string detail)
        {
            if (activeTx is not null)
            {
                await activeTx.RollbackToSavepointAsync(savepointName, ct);
            }

            db.ChangeTracker.Clear();
            return ReceiptAcceptanceOutcome.Fail(status, code, detail);
        }

        // Lock inventory pools & positions cleanly before invoking GL posting kernel.
        var lockedInventory = await InventoryValuationService.EnsureAndLockItemTargetsAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.CurrencyCode,
            validation.ValuationInputs.Select(x => (x.ValuationInput.ItemId, x.ValuationInput.BaseUomId)),
            cmd.NowUtc,
            ct);

        // Pre-validate locked ValuationPool health under OD-04 before allocating sequences or posting GL.
        var poolPreviews = await InventoryValuationService.PreviewReceiptValuationAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.CurrencyCode,
            validation.ValuationInputs.Select(x => x.ValuationInput).ToList(),
            ct);
        var blockedPoolLine = poolPreviews.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.BlockingReason));
        if (blockedPoolLine?.BlockingReason is { } blockMsg)
        {
            var idx = blockMsg.IndexOf(':', StringComparison.Ordinal);
            var blockCode = idx > 0 ? blockMsg[..idx].Trim() : "od04_anomalous_valuation_pool_blocked";
            var blockDetail = idx > 0 ? blockMsg[(idx + 1)..].Trim() : blockMsg;
            return await RollbackAndFailAsync(409, blockCode, blockDetail);
        }

        var acceptanceId = Guid.NewGuid();
        var accSeq = await IncrementAcceptanceSequenceAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.AcceptanceDate.Year, ct);
        var acceptanceNumber = $"ACC-{cmd.AcceptanceDate.Year}-{accSeq.ToString("D5", CultureInfo.InvariantCulture)}";

        var preparedLines = validation.ValuationInputs
            .Select(x => (Plan: x, ValuedAcceptanceLineId: Guid.NewGuid()))
            .ToList();

        // 1. Post balanced Dr Inventory Asset / Cr GRNI journal & receipt accrual subledger inside transaction.
        var accrualOutcome = await GlPostingKernel.PostReceiptAccrualAsync(
            db,
            new GlPostingKernel.PostReceiptAccrualCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ExpectedFunctionalCurrencyCode,
                cmd.AcceptanceDate,
                po.CurrencyCode,
                acceptanceId,
                acceptanceNumber,
                receipt.GoodsReceiptId,
                po.PurchaseOrderId,
                receipt.SupplierPartnerId,
                idempotencyKey,
                preparedLines
                    .Select((p, idx) => new GlPostingKernel.ReceiptAccrualLineInput(
                        p.ValuedAcceptanceLineId,
                        p.Plan.ReceiptLine.GoodsReceiptLineId,
                        p.Plan.ReceiptLine.PoLineId,
                        idx + 1,
                        p.Plan.ValuationInput.ItemId,
                        p.Plan.ValuationInput.ItemCode,
                        p.Plan.ValuationInput.AcceptedBaseQuantity,
                        p.Plan.ValuationInput.UnitCostBase,
                        p.Plan.ValuationInput.AcceptedLineAmount))
                    .ToList(),
                cmd.ActorUserId,
                cmd.NowUtc),
            ct);

        if (!accrualOutcome.Succeeded || accrualOutcome.Journal is null || accrualOutcome.Accrual is null)
        {
            return await RollbackAndFailAsync(
                accrualOutcome.FailureHttpStatus,
                accrualOutcome.ErrorCode ?? "gl_posting_failed",
                accrualOutcome.ErrorDetail ?? "Failed posting Inventory / GRNI journal.");
        }

        // 2. Post Inventory StockMovement, MovementLines, StockPosition & OD-04 moving-average ValuationPool/Entries.
        var stockOutcome = await InventoryValuationService.PostAcceptedStockAndValuationAsync(
            db,
            lockedInventory,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.CurrencyCode,
            cmd.AcceptanceDate,
            normalizedStatus,
            acceptanceId,
            acceptanceNumber,
            idempotencyKey,
            cmd.Notes,
            preparedLines
                .Select(p => (p.Plan.ValuationInput, p.ValuedAcceptanceLineId))
                .ToList(),
            accrualOutcome.Journal.JournalId,
            cmd.ActorUserId,
            cmd.NowUtc,
            ct);

        if (!stockOutcome.Succeeded || stockOutcome.Movement is null)
        {
            return await RollbackAndFailAsync(
                stockOutcome.FailureHttpStatus,
                stockOutcome.ErrorCode ?? "inventory_valuation_failed",
                stockOutcome.ErrorDetail ?? "Failed updating inventory stock and moving-average valuation.");
        }

        // 3. Persist immutable ValuedAcceptance + Lines & update receipt/PO custody projections.
        var totalAcceptedQty = decimal.Round(preparedLines.Sum(x => x.Plan.ValuationInput.AcceptedBaseQuantity), 6, MidpointRounding.AwayFromZero);
        var totalAcceptedAmt = decimal.Round(preparedLines.Sum(x => x.Plan.ValuationInput.AcceptedLineAmount), 4, MidpointRounding.AwayFromZero);

        ValuedAcceptance acceptance;
        List<ValuedAcceptanceLine> acceptanceLines;
        try
        {
            var baseUomScales = await db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.TenantId == cmd.TenantId && preparedLines.Select(p => p.Plan.ReceiptLine.BaseUomId).Distinct().Contains(u.UomId))
                .ToDictionaryAsync(u => u.UomId, u => u.QuantityPrecisionScale, ct);

            acceptance = ValuedAcceptance.Create(
                acceptanceId,
                cmd.TenantId,
                cmd.LegalEntityId,
                acceptanceNumber,
                receipt,
                cmd.AcceptanceDate,
                normalizedStatus,
                po.CurrencyCode,
                idempotencyKey,
                payloadHash,
                totalAcceptedQty,
                totalAcceptedAmt,
                stockOutcome.Movement.StockMovementId,
                accrualOutcome.Accrual.ReceiptAccrualId,
                accrualOutcome.Journal.JournalId,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            acceptanceLines = new List<ValuedAcceptanceLine>(preparedLines.Count);
            for (var i = 0; i < preparedLines.Count; i++)
            {
                var p = preparedLines[i];
                var scale = baseUomScales.GetValueOrDefault(p.Plan.ReceiptLine.BaseUomId, 6);
                p.Plan.ReceiptLine.RecordValuedAcceptance(p.Plan.ValuationInput.AcceptedBaseQuantity, scale, cmd.NowUtc);

                acceptanceLines.Add(ValuedAcceptanceLine.Create(
                    p.ValuedAcceptanceLineId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    acceptanceId,
                    i + 1,
                    p.Plan.ReceiptLine,
                    p.Plan.ValuationInput.AcceptedBaseQuantity,
                    p.Plan.UnitPriceBaseSnapshot,
                    p.Plan.ValuationInput.AcceptedLineAmount));
            }

            receipt.RecalculateHeaderFromLines(receiptLines, cmd.NowUtc);
            po.RecordAcceptedQuantity(totalAcceptedQty, cmd.NowUtc);
        }
        catch (ArgumentException ex)
        {
            return await RollbackAndFailAsync(400, "acceptance_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
            var code = idx > 0 ? ex.Message[..idx].Trim() : "acceptance_domain_error";
            var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
            return await RollbackAndFailAsync(409, code, detail);
        }

        db.ValuedAcceptances.Add(acceptance);
        db.ValuedAcceptanceLines.AddRange(acceptanceLines);

        var eventPayload = JsonSerializer.Serialize(new
        {
            acceptance.ValuedAcceptanceId,
            acceptance.AcceptanceNumber,
            receipt.GoodsReceiptId,
            receipt.ReceiptNumber,
            po.PurchaseOrderId,
            PurchaseOrderNumber = po.PoNumber ?? po.DraftCode,
            acceptance.StockStatus,
            acceptance.TotalAcceptedBaseQuantity,
            acceptance.TotalAcceptedAmount,
            stockOutcome.Movement.StockMovementId,
            stockOutcome.Movement.MovementNumber,
            accrualOutcome.Accrual.ReceiptAccrualId,
            accrualOutcome.Accrual.AccrualNumber,
            accrualOutcome.Journal.JournalId,
            accrualOutcome.Journal.JournalNumber
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "GOODS_RECEIPT_VALUED_ACCEPTED",
            "VALUED_ACCEPTANCE",
            acceptance.ValuedAcceptanceId.ToString("D"),
            $"Accepted {acceptance.AcceptanceNumber} on {receipt.ReceiptNumber} ({totalAcceptedQty:0.######} units, {totalAcceptedAmt:F2} {po.CurrencyCode})",
            eventPayload,
            cmd.NowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "procurement.receipt.accepted.v1",
            $"procurement:acceptance:{acceptance.ValuedAcceptanceId:D}",
            eventPayload,
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);

        if (beforeCommitFaultHook is not null)
        {
            await beforeCommitFaultHook(ct);
        }

        return new ReceiptAcceptanceOutcome(
            true,
            false,
            201,
            null,
            null,
            acceptance,
            acceptanceLines,
            receipt,
            stockOutcome.Movement,
            accrualOutcome.Accrual,
            accrualOutcome.Journal,
            accrualOutcome.JournalLines);
    }

    private sealed record PlanValidationResult(
        bool ValidHeader,
        int HttpStatus,
        string? ErrorCode,
        string? ErrorDetail,
        List<ResolvedLinePlan> ValuationInputs,
        List<string> BlockingReasons,
        int FirstBlockingStatus,
        string? FirstBlockingErrorCode,
        string? FirstBlockingDetail);

    private static async Task<PlanValidationResult> BuildValidatedLinePlanAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        GoodsReceipt receipt,
        PurchaseOrder? po,
        IReadOnlyList<GoodsReceiptLine> receiptLines,
        IReadOnlyList<AcceptReceiptLineInput> inputs,
        CancellationToken ct)
    {
        if (receipt.Status == GoodsReceiptStatuses.FullyRejectedUnvalued)
        {
            return new PlanValidationResult(false, 409, "receipt_fully_rejected_unvalued", "Fully rejected Gate 1 receipts have no eligible quantity for valued acceptance.", new(), new(), 409, null, null);
        }

        if (po is null || po.Status != PurchaseOrderStatuses.Approved)
        {
            return new PlanValidationResult(false, 409, "po_not_eligible_for_receiving", "Parent purchase order must remain APPROVED for Gate 2 valued acceptance.", new(), new(), 409, null, null);
        }

        var lineById = receiptLines.ToDictionary(l => l.GoodsReceiptLineId);
        var poLineIds = receiptLines.Select(l => l.PoLineId).Distinct().ToList();
        var poLines = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && poLineIds.Contains(l.PoLineId))
            .ToDictionaryAsync(l => l.PoLineId, ct);

        var baseUomIds = receiptLines.Select(l => l.BaseUomId).Distinct().ToList();
        var uomScales = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && baseUomIds.Contains(u.UomId))
            .ToDictionaryAsync(u => u.UomId, u => u.QuantityPrecisionScale, ct);

        var plans = new List<ResolvedLinePlan>(inputs.Count);
        var blocks = new List<string>();
        var firstStatus = 200;
        string? firstCode = null;
        string? firstDetail = null;
        var seenLineIds = new HashSet<Guid>();

        void CaptureBlock(int status, string code, string detail)
        {
            blocks.Add($"{code}: {detail}");
            if (firstCode is null)
            {
                firstStatus = status;
                firstCode = code;
                firstDetail = detail;
            }
        }

        foreach (var input in inputs)
        {
            if (!seenLineIds.Add(input.GoodsReceiptLineId))
            {
                return new PlanValidationResult(false, 400, "duplicate_acceptance_line", "Each goods receipt line can appear only once per acceptance submission.", new(), new(), 400, null, null);
            }

            if (!lineById.TryGetValue(input.GoodsReceiptLineId, out var grLine))
            {
                return new PlanValidationResult(false, 404, "goods_receipt_line_not_found", $"Goods receipt line '{input.GoodsReceiptLineId}' was not found on receipt '{receipt.ReceiptNumber}'.", new(), new(), 404, null, null);
            }

            if (grLine.ItemKindSnapshot != Catalog.ItemKinds.StockedGood)
            {
                CaptureBlock(409, "non_stocked_item_not_inventory_eligible", $"Line {grLine.LineNumber} ('{grLine.ItemCodeSnapshot}') is not a stocked item.");
                continue;
            }

            var qty = decimal.Round(input.AcceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
            if (qty <= 0m)
            {
                return new PlanValidationResult(false, 400, "acceptance_qty_must_be_positive", $"Line {grLine.LineNumber}: Accepted quantity must be positive.", new(), new(), 400, null, null);
            }

            var scale = uomScales.GetValueOrDefault(grLine.BaseUomId, 6);
            if (scale == 0 && qty != decimal.Truncate(qty))
            {
                CaptureBlock(409, "fractional_discrete_base_quantity", $"Line {grLine.LineNumber} ('{grLine.BaseUomCodeSnapshot}') requires a whole integer quantity.");
            }

            var eligible = decimal.Round(grLine.DockReleasedBaseQuantity - grLine.ValuedAcceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
            if (qty > eligible)
            {
                CaptureBlock(
                    409,
                    "acceptance_exceeds_released_quantity",
                    $"Line {grLine.LineNumber} ('{grLine.ItemCodeSnapshot}'): Accepted quantity ({qty:0.######} {grLine.BaseUomCodeSnapshot}) exceeds released Gate 1 balance ({Math.Max(0m, eligible):0.######} {grLine.BaseUomCodeSnapshot}). Hold/pending/rejected quantities cannot enter valued inventory.");
            }

            if (!poLines.TryGetValue(grLine.PoLineId, out var poLine) || poLine.UomFactorToBaseSnapshot <= 0m)
            {
                return new PlanValidationResult(false, 409, "po_line_not_found", $"Source PO line snapshot missing for receipt line {grLine.LineNumber}.", new(), new(), 409, null, null);
            }

            var unitPriceBase = decimal.Round(poLine.UnitPrice / poLine.UomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero);
            var lineAmount = decimal.Round((qty * poLine.UnitPrice) / poLine.UomFactorToBaseSnapshot, 4, MidpointRounding.AwayFromZero);
            if (unitPriceBase <= 0m || lineAmount <= 0m)
            {
                CaptureBlock(409, "od04_zero_or_negative_acceptance_value_blocked", $"Line {grLine.LineNumber}: Valuation amount ({lineAmount:F4}) must be positive under OD-04.");
            }

            plans.Add(new ResolvedLinePlan(
                grLine,
                new InventoryLineValuationInput(
                    grLine.PoLineId,
                    grLine.GoodsReceiptLineId,
                    grLine.LineNumber,
                    grLine.ItemId,
                    grLine.ItemCodeSnapshot,
                    grLine.ItemNameSnapshot,
                    grLine.BaseUomId,
                    grLine.BaseUomCodeSnapshot,
                    qty,
                    unitPriceBase,
                    lineAmount),
                unitPriceBase));
        }

        return new PlanValidationResult(true, 200, null, null, plans, blocks, firstStatus, firstCode, firstDetail);
    }

    private static async Task<ReceiptAcceptanceOutcome> BuildReplayOutcomeAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        ValuedAcceptance existing,
        string payloadHash,
        CancellationToken ct)
    {
        if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            return ReceiptAcceptanceOutcome.Fail(
                409,
                "duplicate_acceptance_idempotency_conflict",
                "Idempotency key already belongs to a different Gate 2 valued acceptance payload.");
        }

        var lines = await db.ValuedAcceptanceLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.ValuedAcceptanceId == existing.ValuedAcceptanceId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var receipt = await db.GoodsReceipts.AsNoTracking()
            .SingleAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.GoodsReceiptId == existing.GoodsReceiptId, ct);

        var movement = existing.StockMovementId is { } mid
            ? await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.StockMovementId == mid, ct)
            : null;

        var accrual = existing.ReceiptAccrualId is { } aid
            ? await db.ReceiptAccruals.AsNoTracking().SingleOrDefaultAsync(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.ReceiptAccrualId == aid, ct)
            : null;

        var journal = existing.GlJournalId is { } jid
            ? await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(j => j.TenantId == tenantId && j.LegalEntityId == entityId && j.JournalId == jid, ct)
            : null;

        var jLines = journal is null
            ? new List<JournalLine>()
            : await db.JournalLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.JournalId == journal.JournalId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

        return new ReceiptAcceptanceOutcome(
            true,
            true,
            200,
            null,
            null,
            existing,
            lines,
            receipt,
            movement,
            accrual,
            journal,
            jLines);
    }

    private static async Task<long> IncrementAcceptanceSequenceAsync(
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
            INSERT INTO procurement.acceptance_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
            VALUES (@tid, @eid, @fy, 1)
            ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
            DO UPDATE SET last_number = procurement.acceptance_sequences.last_number + 1
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
}
