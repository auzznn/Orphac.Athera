using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Modules.Procurement;

public sealed record ReturnLineDraftInput(
    Guid GoodsReceiptLineId,
    Guid? ValuedAcceptanceLineId,
    Guid? ReceiptAccrualLineId,
    Guid? SupplierBillLineId,
    decimal ReturnedBaseQuantity,
    Guid? SourceLocationId = null,
    Guid? SourceStockLotId = null,
    string? SourceStockStatus = null,
    string? LineNotes = null);

public sealed record PreviewSupplierReturnCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ReturnCaseType,
    string ResolutionMode,
    Guid PurchaseOrderId,
    Guid? GoodsReceiptId,
    Guid? ReceiptAccrualId,
    Guid? SupplierBillId,
    DateOnly ReturnDate,
    string? TaxReturnNoteNumber,
    IReadOnlyList<ReturnLineDraftInput> Lines,
    Guid? ExcludeSupplierReturnId = null);

public sealed record CreateSupplierReturnDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ReturnCaseType,
    string ResolutionMode,
    Guid PurchaseOrderId,
    Guid? GoodsReceiptId,
    Guid? ReceiptAccrualId,
    Guid? SupplierBillId,
    DateOnly ReturnDate,
    string ReasonCode,
    string? CarrierOrDispatchRef,
    string? TaxReturnNoteNumber,
    string? Notes,
    IReadOnlyList<ReturnLineDraftInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdateSupplierReturnDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierReturnId,
    string ResolutionMode,
    DateOnly ReturnDate,
    string ReasonCode,
    string? CarrierOrDispatchRef,
    string? TaxReturnNoteNumber,
    string? Notes,
    IReadOnlyList<ReturnLineDraftInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SubmitSupplierReturnCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierReturnId,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record DecideSupplierReturnCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierReturnId,
    string Decision,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ExecuteSupplierReturnCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierReturnId,
    string ExpectedFunctionalCurrencyCode,
    DateOnly PostingDate,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc,
    Action? FaultAfterSubledgerBeforeCommit = null);

public sealed record ReceiveReplacementCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ReplacementAuthorizationId,
    Guid ReceivedUomId,
    decimal PhysicallyDeliveredQuantity,
    DateOnly ReceivedDate,
    string SupplierDeliveryNoteNumber,
    string IdempotencyKey,
    string ReceivingStation,
    string? VehicleOrSealRef,
    string? DocumentEvidenceRef,
    string? Notes,
    bool FlagForInspection,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReturnCommandOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    SupplierReturn? ReturnHeader,
    IReadOnlyList<SupplierReturnLine> Lines,
    SupplierReturnMatrixResult? Matrix,
    StockMovement? StockMovement,
    JournalEntry? Journal,
    SupplierCredit? SupplierCredit,
    IReadOnlyList<ReplacementAuthorization> ReplacementAuthorizations,
    IReadOnlyList<TaxDetermination> TaxDeterminations)
{
    public static ReturnCommandOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(
            false,
            false,
            statusCode,
            errorCode,
            detail,
            null,
            Array.Empty<SupplierReturnLine>(),
            null,
            null,
            null,
            null,
            Array.Empty<ReplacementAuthorization>(),
            Array.Empty<TaxDetermination>());
}

/// <summary>
/// Authoritative transactional coordinator for S10 supplier returns, linked replacements, and AP credit notes.
/// Implements ATH-CR-007 (OD-05/OD-07), OD-02 four-eyes approval, OD-04 MAC protection, and savepoint atomicity.
/// </summary>
public static class SupplierReturnCoordinator
{
    private sealed record ResolvedHeaderContext(
        PurchaseOrder PurchaseOrder,
        Partner Partner,
        GoodsReceipt? GoodsReceipt,
        ReceiptAccrual? ReceiptAccrual,
        SupplierBill? SupplierBill,
        PayableItem? PayableItem);

    private sealed record ResolvedLineBinding(
        ReturnLineDraftInput Input,
        GoodsReceiptLine GoodsReceiptLine,
        ValuedAcceptanceLine? ValuedAcceptanceLine,
        ReceiptAccrualLine? ReceiptAccrualLine,
        SupplierBillLine? SupplierBillLine,
        PurchaseOrderLine PoLine,
        Item Item,
        UnitOfMeasure BaseUom,
        WarehouseLocation? SourceLocation,
        Guid? SourceStockLotId,
        string? SourceStockStatus,
        TaxCode? PpnTaxCode);

    private sealed record EvaluatedContext(
        ResolvedHeaderContext Header,
        List<ResolvedLineBinding> Bindings,
        SupplierReturnMatrixResult Matrix);

    public static async Task<ReturnCommandOutcome> PreviewReturnAsync(
        AtheraDbContext db,
        PreviewSupplierReturnCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var eval = await EvaluateContextAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ReturnCaseType,
                cmd.ResolutionMode,
                cmd.PurchaseOrderId,
                cmd.GoodsReceiptId,
                cmd.ReceiptAccrualId,
                cmd.SupplierBillId,
                cmd.ReturnDate,
                cmd.TaxReturnNoteNumber,
                cmd.Lines,
                cmd.ExcludeSupplierReturnId,
                DateTimeOffset.UtcNow,
                allowLocationCreation: false,
                lockedPo: null,
                lockedReceipt: null,
                lockedAccrual: null,
                lockedAccrualLines: null,
                lockedBill: null,
                lockedPayable: null,
                lockedPools: null,
                lockedLocationBalances: null,
                includePendingClaimsInCapacity: true,
                ct);

            return new ReturnCommandOutcome(
                true,
                false,
                200,
                null,
                null,
                null,
                Array.Empty<SupplierReturnLine>(),
                eval.Matrix,
                null,
                null,
                null,
                Array.Empty<ReplacementAuthorization>(),
                Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<ReturnCommandOutcome> CreateDraftAsync(
        AtheraDbContext db,
        CreateSupplierReturnDraftCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var eval = await EvaluateContextAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ReturnCaseType,
                cmd.ResolutionMode,
                cmd.PurchaseOrderId,
                cmd.GoodsReceiptId,
                cmd.ReceiptAccrualId,
                cmd.SupplierBillId,
                cmd.ReturnDate,
                cmd.TaxReturnNoteNumber,
                cmd.Lines,
                excludeSupplierReturnId: null,
                cmd.NowUtc,
                allowLocationCreation: true,
                lockedPo: null,
                lockedReceipt: null,
                lockedAccrual: null,
                lockedAccrualLines: null,
                lockedBill: null,
                lockedPayable: null,
                lockedPools: null,
                lockedLocationBalances: null,
                includePendingClaimsInCapacity: true,
                ct);

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "procurement.return_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.ReturnDate.Year, ct);
            var draftCode = $"DRT-{cmd.ReturnDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";

            var returnId = Guid.NewGuid();
            var header = SupplierReturn.CreateDraft(
                returnId,
                cmd.TenantId,
                cmd.LegalEntityId,
                draftCode,
                cmd.ReturnCaseType,
                cmd.ResolutionMode,
                eval.Matrix,
                eval.Header.PurchaseOrder,
                eval.Header.Partner,
                eval.Header.GoodsReceipt?.GoodsReceiptId,
                eval.Header.GoodsReceipt?.ReceiptNumber,
                eval.Header.ReceiptAccrual?.ReceiptAccrualId,
                eval.Header.SupplierBill?.SupplierBillId,
                eval.Header.SupplierBill?.BillNumber ?? eval.Header.SupplierBill?.DraftCode,
                eval.Header.PayableItem?.PayableItemId,
                cmd.ReturnDate,
                cmd.ReasonCode,
                cmd.CarrierOrDispatchRef,
                cmd.TaxReturnNoteNumber,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            var builtLines = BuildEntityLines(cmd.TenantId, cmd.LegalEntityId, returnId, cmd.Lines, eval.Matrix.Lines);

            db.SupplierReturns.Add(header);
            db.SupplierReturnLines.AddRange(builtLines);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SUPPLIER_RETURN_DRAFT_CREATED",
                "SUPPLIER_RETURN",
                header.SupplierReturnId.ToString("D"),
                $"Created supplier return draft {draftCode} ({header.ReturnCaseType} / {header.ResolutionMode})",
                JsonSerializer.Serialize(new
                {
                    header.SupplierReturnId,
                    header.DraftCode,
                    header.ReturnCaseType,
                    header.ResolutionMode,
                    eval.Matrix.MatrixCode,
                    eval.Matrix.Disposition
                }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new ReturnCommandOutcome(
                true,
                false,
                201,
                null,
                null,
                header,
                builtLines,
                eval.Matrix,
                null,
                null,
                null,
                Array.Empty<ReplacementAuthorization>(),
                Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<ReturnCommandOutcome> UpdateDraftAsync(
        AtheraDbContext db,
        UpdateSupplierReturnDraftCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var header = await LockReturnForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierReturnId, ct);
            if (header is null)
            {
                return ReturnCommandOutcome.Fail(404, "return_not_found", "Supplier return was not found in this legal entity.");
            }

            if (header.Status != SupplierReturnStatuses.Draft)
            {
                return ReturnCommandOutcome.Fail(
                    409,
                    "return_not_editable_in_status",
                    $"Only DRAFT supplier returns can be edited (current: '{header.Status}').");
            }

            var eval = await EvaluateContextAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                header.ReturnCaseType,
                cmd.ResolutionMode,
                header.PurchaseOrderId,
                header.GoodsReceiptId,
                header.ReceiptAccrualId,
                header.SupplierBillId,
                cmd.ReturnDate,
                cmd.TaxReturnNoteNumber,
                cmd.Lines,
                header.SupplierReturnId,
                cmd.NowUtc,
                allowLocationCreation: true,
                lockedPo: null,
                lockedReceipt: null,
                lockedAccrual: null,
                lockedAccrualLines: null,
                lockedBill: null,
                lockedPayable: null,
                lockedPools: null,
                lockedLocationBalances: null,
                includePendingClaimsInCapacity: true,
                ct);

            var existingLines = await db.SupplierReturnLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierReturnId == header.SupplierReturnId)
                .ToListAsync(ct);
            db.SupplierReturnLines.RemoveRange(existingLines);

            var rebuilt = BuildEntityLines(cmd.TenantId, cmd.LegalEntityId, header.SupplierReturnId, cmd.Lines, eval.Matrix.Lines);
            db.SupplierReturnLines.AddRange(rebuilt);

            header.UpdateDraft(
                cmd.ResolutionMode,
                eval.Matrix,
                cmd.ReturnDate,
                cmd.ReasonCode,
                cmd.CarrierOrDispatchRef,
                cmd.TaxReturnNoteNumber,
                cmd.Notes,
                cmd.NowUtc);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SUPPLIER_RETURN_DRAFT_UPDATED",
                "SUPPLIER_RETURN",
                header.SupplierReturnId.ToString("D"),
                $"Updated supplier return draft {header.DraftCode} ({header.ResolutionMode})",
                JsonSerializer.Serialize(new
                {
                    header.SupplierReturnId,
                    header.DraftCode,
                    header.ResolutionMode,
                    eval.Matrix.MatrixCode,
                    eval.Matrix.Disposition
                }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new ReturnCommandOutcome(
                true,
                false,
                200,
                null,
                null,
                header,
                rebuilt,
                eval.Matrix,
                null,
                null,
                null,
                Array.Empty<ReplacementAuthorization>(),
                Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<ReturnCommandOutcome> SubmitAsync(
        AtheraDbContext db,
        SubmitSupplierReturnCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var header = await LockReturnForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierReturnId, ct);
            if (header is null)
            {
                return ReturnCommandOutcome.Fail(404, "return_not_found", "Supplier return was not found in this legal entity.");
            }

            if (header.Status != SupplierReturnStatuses.Draft)
            {
                return ReturnCommandOutcome.Fail(
                    409,
                    "return_invalid_status_for_submit",
                    $"Only DRAFT returns may be submitted (current: '{header.Status}').");
            }

            var returnLines = await db.SupplierReturnLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierReturnId == header.SupplierReturnId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            if (returnLines.Count == 0)
            {
                return ReturnCommandOutcome.Fail(400, "return_lines_required", "At least one return line is required.");
            }

            var draftInputs = returnLines.Select(l => new ReturnLineDraftInput(
                l.GoodsReceiptLineId,
                l.ValuedAcceptanceLineId,
                l.ReceiptAccrualLineId,
                l.SupplierBillLineId,
                l.ReturnedBaseQuantity,
                l.SourceLocationId,
                l.SourceStockLotId,
                l.SourceStockStatus,
                l.LineNotes)).ToList();

            var eval = await EvaluateContextAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                header.ReturnCaseType,
                header.ResolutionMode,
                header.PurchaseOrderId,
                header.GoodsReceiptId,
                header.ReceiptAccrualId,
                header.SupplierBillId,
                header.ReturnDate,
                header.TaxReturnNoteNumber,
                draftInputs,
                header.SupplierReturnId,
                cmd.NowUtc,
                allowLocationCreation: false,
                lockedPo: null,
                lockedReceipt: null,
                lockedAccrual: null,
                lockedAccrualLines: null,
                lockedBill: null,
                lockedPayable: null,
                lockedPools: null,
                lockedLocationBalances: null,
                includePendingClaimsInCapacity: true,
                ct);

            header.Submit(eval.Matrix, cmd.ActorUserId, cmd.NowUtc);

            var approvalPolicy = await db.ApprovalPolicies.AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.TenantId == cmd.TenantId &&
                         p.LegalEntityId == cmd.LegalEntityId &&
                         p.PolicyCode == ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode &&
                         p.IsActive,
                    ct);
            if (approvalPolicy is null)
            {
                approvalPolicy = ApprovalPolicy.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode,
                    "Supplier Return & Financial Correction Four-Eyes Authorization",
                    requiresFourEyes: true,
                    requiresApproverMfa: false,
                    cmd.NowUtc);
                db.ApprovalPolicies.Add(approvalPolicy);
            }

            var existingPending = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == header.SupplierReturnId && a.Status == DocumentApprovalStatuses.Pending)
                .ToListAsync(ct);
            foreach (var oldApproval in existingPending)
            {
                oldApproval.Cancel(cmd.NowUtc);
            }

            var totalPrior = await db.DocumentApprovals.CountAsync(
                a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == header.SupplierReturnId,
                ct);
            var nextRevNo = Math.Max(1, totalPrior + 1);

            db.DocumentApprovals.Add(DocumentApproval.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                approvalPolicy.PolicyId,
                "PROCUREMENT",
                "SUPPLIER_RETURN",
                header.SupplierReturnId,
                header.SupplierReturnId,
                nextRevNo,
                $"Authorize supplier return {header.DraftCode} ({header.ReturnCaseType} · {header.ResolutionMode})",
                cmd.ActorUserId,
                cmd.NowUtc));

            var payloadJson = JsonSerializer.Serialize(new
            {
                header.SupplierReturnId,
                header.DraftCode,
                header.ReturnCaseType,
                header.ResolutionMode,
                eval.Matrix.MatrixCode,
                header.TotalReturnedBaseQuantity
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SUPPLIER_RETURN_SUBMITTED",
                "SUPPLIER_RETURN",
                header.SupplierReturnId.ToString("D"),
                $"Submitted supplier return {header.DraftCode} ({header.ReturnCaseType}) for four-eyes approval",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "procurement.supplierreturn.submitted.v1",
                $"procurement:return-submit:{header.SupplierReturnId:D}:{nextRevNo}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new ReturnCommandOutcome(
                true,
                false,
                200,
                null,
                null,
                header,
                returnLines,
                eval.Matrix,
                null,
                null,
                null,
                Array.Empty<ReplacementAuthorization>(),
                Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<ReturnCommandOutcome> DecideAsync(
        AtheraDbContext db,
        DecideSupplierReturnCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var header = await LockReturnForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierReturnId, ct);
            if (header is null)
            {
                return ReturnCommandOutcome.Fail(404, "return_not_found", "Supplier return was not found in this legal entity.");
            }

            if (header.Status != SupplierReturnStatuses.PendingApproval)
            {
                return ReturnCommandOutcome.Fail(
                    409,
                    "return_not_pending_approval",
                    $"Supplier return in status '{header.Status}' is not awaiting an approval decision.");
            }

            header.Decide(cmd.Decision, cmd.Reason, cmd.ActorUserId, cmd.NowUtc);

            var pendingApproval = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == header.SupplierReturnId && a.Status == DocumentApprovalStatuses.Pending)
                .OrderByDescending(a => a.RevisionNumber)
                .FirstOrDefaultAsync(ct);
            pendingApproval?.RecordDecision(cmd.ActorUserId, cmd.Decision, cmd.Reason, mfaVerified: false, cmd.NowUtc);

            var lines = await db.SupplierReturnLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierReturnId == header.SupplierReturnId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            var payloadJson = JsonSerializer.Serialize(new
            {
                header.SupplierReturnId,
                header.DraftCode,
                header.Status,
                Decision = cmd.Decision,
                cmd.Reason
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                header.Status == SupplierReturnStatuses.Approved ? "SUPPLIER_RETURN_APPROVED" : "SUPPLIER_RETURN_REJECTED",
                "SUPPLIER_RETURN",
                header.SupplierReturnId.ToString("D"),
                $"Decided supplier return {header.DraftCode}: {cmd.Decision} ({cmd.Reason})",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "procurement.supplierreturn.decided.v1",
                $"procurement:return-decide:{header.SupplierReturnId:D}:{header.Version}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            return new ReturnCommandOutcome(
                true,
                false,
                200,
                null,
                null,
                header,
                lines,
                null,
                null,
                null,
                null,
                Array.Empty<ReplacementAuthorization>(),
                Array.Empty<TaxDetermination>());
        }
        catch (ArgumentException ex)
        {
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<ReturnCommandOutcome> ExecuteAsync(
        AtheraDbContext db,
        ExecuteSupplierReturnCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction;
        if (tx is null)
        {
            throw new InvalidOperationException("ExecuteAsync requires an active entity PostgreSQL transaction.");
        }

        const string savepointName = "sp_execute_supplier_return";
        await tx.CreateSavepointAsync(savepointName, ct);

        try
        {
            var idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);

            // 1. Lock SupplierReturn header
            var header = await LockReturnForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierReturnId, ct);
            if (header is null)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return ReturnCommandOutcome.Fail(404, "return_not_found", "Supplier return was not found in this legal entity.");
            }

            if (header.Status == SupplierReturnStatuses.Executed)
            {
                if (!string.Equals(header.ExecuteIdempotencyKey, idempKey, StringComparison.Ordinal))
                {
                    await tx.RollbackToSavepointAsync(savepointName, ct);
                    return ReturnCommandOutcome.Fail(
                        409,
                        "return_already_executed",
                        $"Supplier return {header.ReturnNumber ?? header.DraftCode} is already executed under a different idempotency key.");
                }

                await tx.ReleaseSavepointAsync(savepointName, ct);
                return await LoadExecutedOutcomeAsync(db, header, ct);
            }

            if (header.Status != SupplierReturnStatuses.Approved)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return ReturnCommandOutcome.Fail(
                    409,
                    "return_must_be_approved_before_execute",
                    $"Supplier return '{header.DraftCode}' is in status '{header.Status}' and must pass four-eyes approval before execution.");
            }

            var returnLines = await db.SupplierReturnLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.SupplierReturnId == header.SupplierReturnId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            if (returnLines.Count == 0)
            {
                await tx.RollbackToSavepointAsync(savepointName, ct);
                return ReturnCommandOutcome.Fail(400, "return_lines_required", "Supplier return requires at least one line.");
            }

            // 2. Deterministic lock hierarchy across procurement -> GL/AP -> inventory
            var po = (await db.PurchaseOrders
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM procurement.purchase_orders
                    WHERE tenant_id = {cmd.TenantId}
                      AND legal_entity_id = {cmd.LegalEntityId}
                      AND purchase_order_id = {header.PurchaseOrderId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();

            GoodsReceipt? lockedReceipt = null;
            if (header.GoodsReceiptId is { } grId)
            {
                lockedReceipt = (await db.GoodsReceipts
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM procurement.goods_receipts
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND goods_receipt_id = {grId}
                        FOR UPDATE
                        """)
                    .ToListAsync(ct))
                    .Single();
            }

            ReceiptAccrual? lockedAccrual = null;
            List<ReceiptAccrualLine>? lockedAccrualLines = null;
            if (header.ReturnCaseType == SupplierReturnCaseTypes.ValuedUnbilledReturn && header.ReceiptAccrualId is { } raId)
            {
                lockedAccrual = (await db.ReceiptAccruals
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM gl.receipt_accruals
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND receipt_accrual_id = {raId}
                        FOR UPDATE
                        """)
                    .ToListAsync(ct))
                    .Single();

                lockedAccrualLines = await db.ReceiptAccrualLines
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM gl.receipt_accrual_lines
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND receipt_accrual_id = {raId}
                        ORDER BY line_number ASC
                        FOR UPDATE
                        """)
                    .ToListAsync(ct);
            }

            SupplierBill? lockedBill = null;
            PayableItem? lockedPayable = null;
            if (header.ReturnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn && header.SupplierBillId is { } billId)
            {
                lockedBill = (await db.SupplierBills
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM ap.supplier_bills
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND supplier_bill_id = {billId}
                        FOR UPDATE
                        """)
                    .ToListAsync(ct))
                    .Single();

                lockedPayable = (await db.PayableItems
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM ap.payable_items
                        WHERE tenant_id = {cmd.TenantId}
                          AND legal_entity_id = {cmd.LegalEntityId}
                          AND supplier_bill_id = {billId}
                        FOR UPDATE
                        """)
                    .ToListAsync(ct))
                    .SingleOrDefault();
            }

            var expectedCurrency = GuardValidation.RequireExactIsoCode(
                cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));

            IReadOnlyDictionary<Guid, StockPosition>? lockedPositions = null;
            IReadOnlyDictionary<Guid, ValuationPool>? lockedPools = null;
            Dictionary<(Guid ItemId, Guid LocationId, Guid? StockLotId, string StockStatus), StockLocationBalance>? lockedLocBalances = null;

            if (header.ReturnCaseType != SupplierReturnCaseTypes.UnvaluedDockRejection)
            {
                var itemUomPairs = returnLines.Select(l => (l.ItemId, l.BaseUomId)).Distinct().ToList();
                var invTargets = await InventoryValuationService.EnsureAndLockItemTargetsAsync(
                    db, cmd.TenantId, cmd.LegalEntityId, expectedCurrency, itemUomPairs, cmd.NowUtc, ct);
                lockedPositions = invTargets.PositionsByItem;
                lockedPools = invTargets.PoolsByItem;

                var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(
                    db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
                var locMap = await db.WarehouseLocations.AsNoTracking()
                    .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId)
                    .ToDictionaryAsync(l => l.LocationId, ct);

                lockedLocBalances = new Dictionary<(Guid, Guid, Guid?, string), StockLocationBalance>();
                var distinctBalanceKeys = returnLines
                    .Select(l =>
                    {
                        var normStatus = StockStatuses.Normalize(
                            string.IsNullOrWhiteSpace(l.SourceStockStatus) ? StockStatuses.Unrestricted : l.SourceStockStatus);
                        var explicitLoc = l.SourceLocationId.HasValue ? locMap.GetValueOrDefault(l.SourceLocationId.Value) : null;
                        var chosenLoc = explicitLoc ?? layout.ResolveForStatus(normStatus, l.SourceLocationId);
                        return (l.ItemId, l.BaseUomId, chosenLoc.WarehouseId, chosenLoc.LocationId, l.SourceStockLotId, normStatus);
                    })
                    .Distinct()
                    .OrderBy(k => k.ItemId)
                    .ThenBy(k => k.LocationId)
                    .ThenBy(k => k.SourceStockLotId ?? Guid.Empty)
                    .ThenBy(k => k.normStatus, StringComparer.Ordinal)
                    .ToList();

                foreach (var k in distinctBalanceKeys)
                {
                    var pos = lockedPositions[k.ItemId];
                    var bal = await InventoryAvailabilityService.EnsureAndLockLocationBalanceAsync(
                        db, cmd.TenantId, cmd.LegalEntityId, pos.StockPositionId, k.ItemId, k.BaseUomId,
                        k.WarehouseId, k.LocationId, k.SourceStockLotId, k.normStatus, cmd.NowUtc, ct);
                    lockedLocBalances[(k.ItemId, k.LocationId, k.SourceStockLotId, k.normStatus)] = bal;
                }
            }

            // 3. Re-evaluate live matrix against locked rows and execution PostingDate
            var draftInputs = returnLines.Select(l => new ReturnLineDraftInput(
                l.GoodsReceiptLineId,
                l.ValuedAcceptanceLineId,
                l.ReceiptAccrualLineId,
                l.SupplierBillLineId,
                l.ReturnedBaseQuantity,
                l.SourceLocationId,
                l.SourceStockLotId,
                l.SourceStockStatus,
                l.LineNotes)).ToList();

            var liveEval = await EvaluateContextAsync(
                db,
                cmd.TenantId,
                cmd.LegalEntityId,
                header.ReturnCaseType,
                header.ResolutionMode,
                header.PurchaseOrderId,
                header.GoodsReceiptId,
                header.ReceiptAccrualId,
                header.SupplierBillId,
                cmd.PostingDate,
                header.TaxReturnNoteNumber,
                draftInputs,
                header.SupplierReturnId,
                cmd.NowUtc,
                allowLocationCreation: false,
                lockedPo: po,
                lockedReceipt: lockedReceipt,
                lockedAccrual: lockedAccrual,
                lockedAccrualLines: lockedAccrualLines,
                lockedBill: lockedBill,
                lockedPayable: lockedPayable,
                lockedPools: lockedPools,
                lockedLocationBalances: lockedLocBalances,
                includePendingClaimsInCapacity: false,
                ct);

            header.ValidateExecutePreconditions(liveEval.Matrix, idempKey);

            // 4. Assign sequential return number SRT-YYYY-NNNNN
            var retSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "procurement.return_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
            var returnNumber = $"SRT-{cmd.PostingDate.Year}-{retSeq.ToString("D5", CultureInfo.InvariantCulture)}";

            StockMovement? createdMovement = null;
            JournalEntry? createdJournal = null;
            SupplierCredit? createdCredit = null;
            var createdReplacements = new List<ReplacementAuthorization>();
            var createdTaxDets = new List<TaxDetermination>();

            if (header.ReturnCaseType == SupplierReturnCaseTypes.UnvaluedDockRejection)
            {
                if (header.ResolutionMode == ReturnResolutionModes.ReplacementOnly)
                {
                    for (var i = 0; i < returnLines.Count; i++)
                    {
                        var rpaSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                            db, "procurement.replacement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
                        var rpaNumber = $"RPA-{cmd.PostingDate.Year}-{rpaSeq.ToString("D5", CultureInfo.InvariantCulture)}";
                        var auth = ReplacementAuthorization.CreateFromReturnLine(
                            Guid.NewGuid(),
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            rpaNumber,
                            header,
                            returnLines[i],
                            liveEval.Matrix.Lines[i].AuthorizedUnitCostBase,
                            header.ApprovedByUserId ?? cmd.ActorUserId,
                            cmd.ActorUserId,
                            cmd.NowUtc);
                        createdReplacements.Add(auth);
                    }
                    db.ReplacementAuthorizations.AddRange(createdReplacements);
                }

                header.MarkExecuted(
                    returnNumber,
                    idempKey,
                    liveEval.Matrix,
                    stockMovementId: null,
                    glJournalId: null,
                    supplierCreditId: null,
                    cmd.ActorUserId,
                    cmd.NowUtc);
            }
            else if (header.ReturnCaseType == SupplierReturnCaseTypes.ValuedUnbilledReturn)
            {
                var accountIds = await ResolveRequiredAccountsAsync(
                    db,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    [GlMappingPurposes.GrniClearing, GlMappingPurposes.InventoryAsset, GlMappingPurposes.PurchasePriceVariance],
                    ct);

                var grniAccId = accountIds[GlMappingPurposes.GrniClearing];
                var invAccId = accountIds[GlMappingPurposes.InventoryAsset];
                var ppvAccId = accountIds[GlMappingPurposes.PurchasePriceVariance];

                var movementId = Guid.NewGuid();
                var movSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                    db, "inventory.movement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
                var movementNumber = $"MOV-{cmd.PostingDate.Year}-{movSeq.ToString("D5", CultureInfo.InvariantCulture)}";
                var movIdempKey = BuildScopedKey($"ret:{header.SupplierReturnId:N}", idempKey);

                createdMovement = StockMovement.Create(
                    movementId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    movementNumber,
                    StockMovementTypes.PurchaseReturnOutbound,
                    cmd.PostingDate,
                    "PROCUREMENT",
                    "SUPPLIER_RETURN",
                    header.SupplierReturnId,
                    returnNumber,
                    movIdempKey,
                    header.Notes ?? $"Valued unbilled supplier return {returnNumber}",
                    cmd.ActorUserId,
                    cmd.NowUtc);

                var movLines = new List<MovementLine>(returnLines.Count);
                var valEntries = new List<ValuationEntry>(returnLines.Count);
                var glLines = new List<GlJournalLineInput>();

                for (var i = 0; i < returnLines.Count; i++)
                {
                    var retLine = returnLines[i];
                    var evalLine = liveEval.Matrix.Lines[i];
                    var binding = liveEval.Bindings[i];
                    var status = StockStatuses.Normalize(
                        string.IsNullOrWhiteSpace(binding.SourceStockStatus) ? StockStatuses.Unrestricted : binding.SourceStockStatus);
                    var location = binding.SourceLocation
                        ?? throw new InvalidOperationException("source_location_required: Source warehouse location is required for valued return.");

                    var pool = lockedPools![retLine.ItemId];
                    var pos = lockedPositions![retLine.ItemId];
                    var locBalance = lockedLocBalances![(retLine.ItemId, location.LocationId, binding.SourceStockLotId, status)];

                    var step = pool.ApplyPurchaseReturn(evalLine.ReturnedBaseQuantity, cmd.NowUtc);
                    pos.ApplyPurchaseReturnOutbound(status, evalLine.ReturnedBaseQuantity, cmd.NowUtc);
                    locBalance.AdjustOnHand(-evalLine.ReturnedBaseQuantity, cmd.NowUtc);

                    var accLine = binding.ReceiptAccrualLine
                        ?? throw new InvalidOperationException("receipt_accrual_line_missing: Receipt accrual line was not resolved.");
                    var appliedGrniRev = accLine.ApplyUnbilledReturn(evalLine.ReturnedBaseQuantity);
                    var ppvDelta = decimal.Round(appliedGrniRev - step.IssuedCarryingAmount, 4, MidpointRounding.AwayFromZero);

                    var movLineId = Guid.NewGuid();
                    movLines.Add(MovementLine.CreatePurchaseReturnOutbound(
                        movLineId,
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        movementId,
                        i + 1,
                        pos.StockPositionId,
                        retLine.ItemId,
                        retLine.BaseUomId,
                        retLine.SupplierReturnLineId,
                        status,
                        evalLine.ReturnedBaseQuantity,
                        step.AppliedUnitCost,
                        step.IssuedCarryingAmount,
                        location.WarehouseId,
                        location.LocationId,
                        binding.SourceStockLotId));

                    var valEntry = ValuationEntry.Record(
                        Guid.NewGuid(),
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        pool.ValuationPoolId,
                        retLine.ItemId,
                        ValuationEntryTypes.PurchaseReturnOutbound,
                        cmd.PostingDate,
                        expectedCurrency,
                        movementId,
                        movLineId,
                        "SUPPLIER_RETURN",
                        header.SupplierReturnId,
                        returnNumber,
                        -evalLine.ReturnedBaseQuantity,
                        -step.IssuedCarryingAmount,
                        step.AppliedUnitCost,
                        step.ValuationStep,
                        glJournalId: null,
                        cmd.ActorUserId,
                        cmd.NowUtc);
                    valEntries.Add(valEntry);

                    glLines.Add(new GlJournalLineInput(
                        grniAccId,
                        $"Dr GRNI Clearing ({evalLine.ItemCode}) · {returnNumber}",
                        DebitAmount: appliedGrniRev,
                        CreditAmount: 0m,
                        ItemId: retLine.ItemId));
                    glLines.Add(new GlJournalLineInput(
                        invAccId,
                        $"Cr Inventory Asset ({evalLine.ItemCode}) · {returnNumber}",
                        DebitAmount: 0m,
                        CreditAmount: step.IssuedCarryingAmount,
                        ItemId: retLine.ItemId));

                    if (ppvDelta > 0m)
                    {
                        glLines.Add(new GlJournalLineInput(
                            ppvAccId,
                            $"Cr Purchase Price Variance ({evalLine.ItemCode}) · {returnNumber}",
                            DebitAmount: 0m,
                            CreditAmount: ppvDelta,
                            ItemId: retLine.ItemId));
                    }
                    else if (ppvDelta < 0m)
                    {
                        glLines.Add(new GlJournalLineInput(
                            ppvAccId,
                            $"Dr Purchase Price Variance ({evalLine.ItemCode}) · {returnNumber}",
                            DebitAmount: -ppvDelta,
                            CreditAmount: 0m,
                            ItemId: retLine.ItemId));
                    }
                }

                lockedAccrual!.ApplyLineClearings(lockedAccrualLines!, cmd.NowUtc);
                po.ReverseAcceptedQuantityForReturn(liveEval.Matrix.TotalReturnedBaseQuantity, cmd.NowUtc);

                var jrnOutcome = await GlPostingKernel.PostJournalAsync(
                    db,
                    new GlPostJournalCommand(
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        expectedCurrency,
                        cmd.PostingDate,
                        expectedCurrency,
                        $"Unbilled Supplier Return {returnNumber} · {header.PoNumberSnapshot}",
                        "PROCUREMENT",
                        "SUPPLIER_RETURN",
                        returnNumber,
                        BuildScopedKey($"retjrn:{header.SupplierReturnId:N}", idempKey),
                        glLines,
                        cmd.ActorUserId,
                        cmd.NowUtc),
                    ct);

                if (!jrnOutcome.Succeeded || jrnOutcome.Journal is null)
                {
                    await tx.RollbackToSavepointAsync(savepointName, ct);
                    db.ChangeTracker.Clear();
                    return ReturnCommandOutcome.Fail(
                        jrnOutcome.FailureHttpStatus,
                        jrnOutcome.ErrorCode ?? "gl_posting_failed",
                        jrnOutcome.ErrorDetail ?? "Failed posting supplier return GL reversal journal.");
                }

                createdJournal = jrnOutcome.Journal;
                foreach (var ve in valEntries)
                {
                    ve.AttachJournalId(createdJournal.JournalId);
                }

                db.StockMovements.Add(createdMovement);
                db.MovementLines.AddRange(movLines);
                db.ValuationEntries.AddRange(valEntries);

                if (header.ResolutionMode == ReturnResolutionModes.ReplacementOnly)
                {
                    for (var i = 0; i < returnLines.Count; i++)
                    {
                        var rpaSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                            db, "procurement.replacement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
                        var rpaNumber = $"RPA-{cmd.PostingDate.Year}-{rpaSeq.ToString("D5", CultureInfo.InvariantCulture)}";
                        createdReplacements.Add(ReplacementAuthorization.CreateFromReturnLine(
                            Guid.NewGuid(),
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            rpaNumber,
                            header,
                            returnLines[i],
                            liveEval.Matrix.Lines[i].AuthorizedUnitCostBase,
                            header.ApprovedByUserId ?? cmd.ActorUserId,
                            cmd.ActorUserId,
                            cmd.NowUtc));
                    }
                    db.ReplacementAuthorizations.AddRange(createdReplacements);
                }

                header.MarkExecuted(
                    returnNumber,
                    idempKey,
                    liveEval.Matrix,
                    createdMovement.StockMovementId,
                    createdJournal.JournalId,
                    supplierCreditId: null,
                    cmd.ActorUserId,
                    cmd.NowUtc);
            }
            else
            {
                // M3: VALUED_BILLED_RETURN + AP_CREDIT_NOTE
                if (lockedBill is null || lockedPayable is null)
                {
                    await tx.RollbackToSavepointAsync(savepointName, ct);
                    db.ChangeTracker.Clear();
                    return ReturnCommandOutcome.Fail(404, "supplier_bill_not_found", "Posted supplier bill and payable item are required.");
                }

                var purposesNeeded = new List<string>
                {
                    GlMappingPurposes.ApTradeControl,
                    GlMappingPurposes.InventoryAsset,
                    GlMappingPurposes.PurchasePriceVariance
                };
                if (liveEval.Matrix.TotalPpnReversalAmount > 0m)
                {
                    purposesNeeded.Add(GlMappingPurposes.PpnInputTax);
                }

                var accountIds = await ResolveRequiredAccountsAsync(
                    db, cmd.TenantId, cmd.LegalEntityId, purposesNeeded, ct);
                var apAccId = accountIds[GlMappingPurposes.ApTradeControl];
                var invAccId = accountIds[GlMappingPurposes.InventoryAsset];
                var ppvAccId = accountIds[GlMappingPurposes.PurchasePriceVariance];
                var defaultPpnAccId = liveEval.Matrix.TotalPpnReversalAmount > 0m ? accountIds[GlMappingPurposes.PpnInputTax] : Guid.Empty;

                var movementId = Guid.NewGuid();
                var movSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                    db, "inventory.movement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
                var movementNumber = $"MOV-{cmd.PostingDate.Year}-{movSeq.ToString("D5", CultureInfo.InvariantCulture)}";

                var creditSeq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                    db, "ap.credit_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
                var creditNumber = $"SCN-{cmd.PostingDate.Year}-{creditSeq.ToString("D5", CultureInfo.InvariantCulture)}";
                var creditId = Guid.NewGuid();

                createdMovement = StockMovement.Create(
                    movementId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    movementNumber,
                    StockMovementTypes.PurchaseReturnOutbound,
                    cmd.PostingDate,
                    "PROCUREMENT",
                    "SUPPLIER_RETURN",
                    header.SupplierReturnId,
                    returnNumber,
                    BuildScopedKey($"ret:{header.SupplierReturnId:N}", idempKey),
                    header.Notes ?? $"Valued billed return {returnNumber} -> Credit {creditNumber}",
                    cmd.ActorUserId,
                    cmd.NowUtc);

                var movLines = new List<MovementLine>(returnLines.Count);
                var valEntries = new List<ValuationEntry>(returnLines.Count);
                var creditLines = new List<SupplierCreditLine>(returnLines.Count);
                var glLines = new List<GlJournalLineInput>();

                glLines.Add(new GlJournalLineInput(
                    apAccId,
                    $"Dr AP Trade Control ({creditNumber}) · {returnNumber}",
                    DebitAmount: liveEval.Matrix.TotalCreditNoteAmount,
                    CreditAmount: 0m));

                decimal totalSub = 0m;
                decimal totalPpn = 0m;
                decimal totalCredit = 0m;
                decimal totalInv = 0m;
                decimal totalPpv = 0m;

                for (var i = 0; i < returnLines.Count; i++)
                {
                    var retLine = returnLines[i];
                    var evalLine = liveEval.Matrix.Lines[i];
                    var binding = liveEval.Bindings[i];
                    var billLine = binding.SupplierBillLine
                        ?? throw new InvalidOperationException("supplier_bill_line_missing: Supplier bill line was not resolved.");
                    var status = StockStatuses.Normalize(
                        string.IsNullOrWhiteSpace(binding.SourceStockStatus) ? StockStatuses.Unrestricted : binding.SourceStockStatus);
                    var location = binding.SourceLocation
                        ?? throw new InvalidOperationException("source_location_required: Source warehouse location is required for valued return.");

                    var pool = lockedPools![retLine.ItemId];
                    var pos = lockedPositions![retLine.ItemId];
                    var locBalance = lockedLocBalances![(retLine.ItemId, location.LocationId, binding.SourceStockLotId, status)];

                    var step = pool.ApplyPurchaseReturn(evalLine.ReturnedBaseQuantity, cmd.NowUtc);
                    pos.ApplyPurchaseReturnOutbound(status, evalLine.ReturnedBaseQuantity, cmd.NowUtc);
                    locBalance.AdjustOnHand(-evalLine.ReturnedBaseQuantity, cmd.NowUtc);

                    var ppvDelta = decimal.Round(evalLine.CreditSubtotalAmount - step.IssuedCarryingAmount, 4, MidpointRounding.AwayFromZero);

                    var movLineId = Guid.NewGuid();
                    movLines.Add(MovementLine.CreatePurchaseReturnOutbound(
                        movLineId,
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        movementId,
                        i + 1,
                        pos.StockPositionId,
                        retLine.ItemId,
                        retLine.BaseUomId,
                        retLine.SupplierReturnLineId,
                        status,
                        evalLine.ReturnedBaseQuantity,
                        step.AppliedUnitCost,
                        step.IssuedCarryingAmount,
                        location.WarehouseId,
                        location.LocationId,
                        binding.SourceStockLotId));

                    valEntries.Add(ValuationEntry.Record(
                        Guid.NewGuid(),
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        pool.ValuationPoolId,
                        retLine.ItemId,
                        ValuationEntryTypes.PurchaseReturnOutbound,
                        cmd.PostingDate,
                        expectedCurrency,
                        movementId,
                        movLineId,
                        "SUPPLIER_RETURN",
                        header.SupplierReturnId,
                        returnNumber,
                        -evalLine.ReturnedBaseQuantity,
                        -step.IssuedCarryingAmount,
                        step.AppliedUnitCost,
                        step.ValuationStep,
                        glJournalId: null,
                        cmd.ActorUserId,
                        cmd.NowUtc));

                    glLines.Add(new GlJournalLineInput(
                        invAccId,
                        $"Cr Inventory Asset ({evalLine.ItemCode}) · {creditNumber}",
                        DebitAmount: 0m,
                        CreditAmount: step.IssuedCarryingAmount,
                        ItemId: retLine.ItemId));

                    if (evalLine.PpnReversalAmount > 0m)
                    {
                        var ppnCode = binding.PpnTaxCode
                            ?? throw new InvalidOperationException("ppn_tax_code_missing: Bill line PPN tax code was not found for Nota Retur reversal.");
                        var ppnAccId = ppnCode.TaxAccountId ?? defaultPpnAccId;
                        glLines.Add(new GlJournalLineInput(
                            ppnAccId,
                            $"Cr PPN Input Reversal ({header.TaxReturnNoteNumber}) · {evalLine.ItemCode}",
                            DebitAmount: 0m,
                            CreditAmount: evalLine.PpnReversalAmount,
                            ItemId: retLine.ItemId,
                            TaxCodeId: ppnCode.TaxCodeId));

                        var dppAmount = ppnCode.TaxableBaseRatioDenominator > 0
                            ? decimal.Round(evalLine.CreditSubtotalAmount * ppnCode.TaxableBaseRatioNumerator / ppnCode.TaxableBaseRatioDenominator, 4, MidpointRounding.AwayFromZero)
                            : evalLine.CreditSubtotalAmount;
                        var effRate = evalLine.CreditSubtotalAmount > 0m
                            ? decimal.Round(evalLine.PpnReversalAmount * 100m / evalLine.CreditSubtotalAmount, 4, MidpointRounding.AwayFromZero)
                            : ppnCode.NominalRatePercent;

                        var revEnv = new TaxDeterminationEnvelope(
                            ppnCode.TaxCodeId,
                            ppnCode.Code,
                            ppnCode.TaxFamily,
                            ppnCode.Direction,
                            ppnCode.TreatmentClass,
                            evalLine.CreditSubtotalAmount,
                            dppAmount,
                            ppnCode.NominalRatePercent,
                            effRate,
                            evalLine.PpnReversalAmount,
                            ppnCode.ReviewStatus,
                            TaxEvidenceStatuses.EvidenceProvided,
                            IsBlocked: false,
                            BlockedReason: null,
                            TaxDeterminationEvaluator.Od07Disclaimer);

                        var creditLineId = Guid.NewGuid();
                        creditLines.Add(SupplierCreditLine.Record(
                            creditLineId,
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            creditId,
                            i + 1,
                            retLine.SupplierReturnLineId,
                            billLine.SupplierBillLineId,
                            retLine.ItemId,
                            evalLine.ReturnedBaseQuantity,
                            billLine.InvoiceUnitPriceBase,
                            evalLine.CreditSubtotalAmount,
                            evalLine.PpnReversalAmount,
                            evalLine.TotalCreditAmount,
                            step.IssuedCarryingAmount,
                            ppvDelta));

                        createdTaxDets.Add(TaxDetermination.FromEnvelope(
                            Guid.NewGuid(),
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            "ACCOUNTS_PAYABLE",
                            "SUPPLIER_CREDIT",
                            creditId,
                            creditLineId,
                            TaxDeterminationRoles.PpnInputReversal,
                            revEnv,
                            header.TaxReturnNoteNumber,
                            header.SupplierNpwpSnapshot,
                            cmd.PostingDate,
                            cmd.ActorUserId,
                            cmd.NowUtc));
                    }
                    else
                    {
                        creditLines.Add(SupplierCreditLine.Record(
                            Guid.NewGuid(),
                            cmd.TenantId,
                            cmd.LegalEntityId,
                            creditId,
                            i + 1,
                            retLine.SupplierReturnLineId,
                            billLine.SupplierBillLineId,
                            retLine.ItemId,
                            evalLine.ReturnedBaseQuantity,
                            billLine.InvoiceUnitPriceBase,
                            evalLine.CreditSubtotalAmount,
                            evalLine.PpnReversalAmount,
                            evalLine.TotalCreditAmount,
                            step.IssuedCarryingAmount,
                            ppvDelta));
                    }

                    if (ppvDelta > 0m)
                    {
                        glLines.Add(new GlJournalLineInput(
                            ppvAccId,
                            $"Cr Purchase Price Variance ({evalLine.ItemCode}) · {creditNumber}",
                            DebitAmount: 0m,
                            CreditAmount: ppvDelta,
                            ItemId: retLine.ItemId));
                    }
                    else if (ppvDelta < 0m)
                    {
                        glLines.Add(new GlJournalLineInput(
                            ppvAccId,
                            $"Dr Purchase Price Variance ({evalLine.ItemCode}) · {creditNumber}",
                            DebitAmount: -ppvDelta,
                            CreditAmount: 0m,
                            ItemId: retLine.ItemId));
                    }

                    totalSub += evalLine.CreditSubtotalAmount;
                    totalPpn += evalLine.PpnReversalAmount;
                    totalCredit += evalLine.TotalCreditAmount;
                    totalInv += step.IssuedCarryingAmount;
                    totalPpv += ppvDelta;
                }

                var jrnOutcome = await GlPostingKernel.PostJournalAsync(
                    db,
                    new GlPostJournalCommand(
                        cmd.TenantId,
                        cmd.LegalEntityId,
                        expectedCurrency,
                        cmd.PostingDate,
                        expectedCurrency,
                        $"Supplier Credit Note {creditNumber} ({returnNumber}) · {lockedBill.BillNumber ?? lockedBill.DraftCode}",
                        "ACCOUNTS_PAYABLE",
                        "SUPPLIER_CREDIT",
                        creditNumber,
                        BuildScopedKey($"retjrn:{header.SupplierReturnId:N}", idempKey),
                        glLines,
                        cmd.ActorUserId,
                        cmd.NowUtc),
                    ct);

                if (!jrnOutcome.Succeeded || jrnOutcome.Journal is null)
                {
                    await tx.RollbackToSavepointAsync(savepointName, ct);
                    db.ChangeTracker.Clear();
                    return ReturnCommandOutcome.Fail(
                        jrnOutcome.FailureHttpStatus,
                        jrnOutcome.ErrorCode ?? "gl_posting_failed",
                        jrnOutcome.ErrorDetail ?? "Failed posting AP credit note journal.");
                }

                createdJournal = jrnOutcome.Journal;
                foreach (var ve in valEntries)
                {
                    ve.AttachJournalId(createdJournal.JournalId);
                }

                createdCredit = SupplierCredit.Record(
                    creditId,
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    creditNumber,
                    header.SupplierReturnId,
                    returnNumber,
                    lockedBill,
                    lockedPayable.PayableItemId,
                    header.TaxReturnNoteNumber,
                    cmd.PostingDate,
                    totalSub,
                    totalPpn,
                    totalCredit,
                    totalInv,
                    totalPpv,
                    createdJournal.JournalId,
                    createdMovement.StockMovementId,
                    cmd.ActorUserId,
                    cmd.NowUtc);

                lockedPayable.ApplySupplierCredit(totalCredit, cmd.NowUtc);

                db.StockMovements.Add(createdMovement);
                db.MovementLines.AddRange(movLines);
                db.ValuationEntries.AddRange(valEntries);
                db.SupplierCredits.Add(createdCredit);
                db.SupplierCreditLines.AddRange(creditLines);
                if (createdTaxDets.Count > 0)
                {
                    db.TaxDeterminations.AddRange(createdTaxDets);
                }

                header.MarkExecuted(
                    returnNumber,
                    idempKey,
                    liveEval.Matrix,
                    createdMovement.StockMovementId,
                    createdJournal.JournalId,
                    createdCredit.SupplierCreditId,
                    cmd.ActorUserId,
                    cmd.NowUtc);
            }

            var payloadJson = JsonSerializer.Serialize(new
            {
                header.SupplierReturnId,
                ReturnNumber = header.ReturnNumber,
                header.ReturnCaseType,
                header.ResolutionMode,
                liveEval.Matrix.MatrixCode,
                StockMovementId = createdMovement?.StockMovementId,
                GlJournalId = createdJournal?.JournalId,
                SupplierCreditId = createdCredit?.SupplierCreditId,
                CreditNumber = createdCredit?.CreditNumber,
                ReplacementCount = createdReplacements.Count,
                ReplacementNumbers = createdReplacements.Select(r => r.AuthorizationNumber).ToArray()
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "SUPPLIER_RETURN_EXECUTED",
                "SUPPLIER_RETURN",
                header.SupplierReturnId.ToString("D"),
                $"Executed supplier return {header.ReturnNumber} ({header.ReturnCaseType} / {header.ResolutionMode})",
                payloadJson,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "procurement.supplierreturn.executed.v1",
                $"procurement:return-exec:{header.SupplierReturnId:D}",
                payloadJson,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            cmd.FaultAfterSubledgerBeforeCommit?.Invoke();

            await tx.ReleaseSavepointAsync(savepointName, ct);

            return new ReturnCommandOutcome(
                true,
                false,
                200,
                null,
                null,
                header,
                returnLines,
                liveEval.Matrix,
                createdMovement,
                createdJournal,
                createdCredit,
                createdReplacements,
                createdTaxDets);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(savepointName, ct);
            db.ChangeTracker.Clear();
            return MapDomainFailure(ex);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(savepointName, ct);
            db.ChangeTracker.Clear();
            return ReturnCommandOutcome.Fail(400, "return_validation_failed", ex.Message);
        }
        catch
        {
            await tx.RollbackToSavepointAsync(savepointName, ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public static async Task<ReceivingWorkflowOutcome> ReceiveReplacementAsync(
        AtheraDbContext db,
        ReceiveReplacementCommand cmd,
        CancellationToken ct)
    {
        var auth = await db.ReplacementAuthorizations.AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.TenantId == cmd.TenantId &&
                     a.LegalEntityId == cmd.LegalEntityId &&
                     a.ReplacementAuthorizationId == cmd.ReplacementAuthorizationId,
                ct);

        if (auth is null)
        {
            return ReceivingWorkflowOutcome.Fail(
                404,
                "replacement_authorization_not_found",
                "Replacement authorization was not found in this legal entity.");
        }

        if (auth.Status is not (ReplacementAuthorizationStatuses.Active or ReplacementAuthorizationStatuses.PartiallyReceived))
        {
            return ReceivingWorkflowOutcome.Fail(
                409,
                "replacement_not_receivable_in_status",
                $"Replacement authorization '{auth.AuthorizationNumber}' is '{auth.Status}'.");
        }

        var po = await db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.TenantId == cmd.TenantId &&
                     p.LegalEntityId == cmd.LegalEntityId &&
                     p.PurchaseOrderId == auth.PurchaseOrderId,
                ct);

        if (po is null || po.CurrentApprovedRevisionId is null)
        {
            return ReceivingWorkflowOutcome.Fail(
                404,
                "po_not_found",
                "Originating purchase order or approved revision was not found.");
        }

        var poLineId = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l =>
                l.TenantId == cmd.TenantId &&
                l.LegalEntityId == cmd.LegalEntityId &&
                l.RevisionId == po.CurrentApprovedRevisionId.Value &&
                l.LineageLineKey == auth.LineageLineKey)
            .Select(l => l.PoLineId)
            .FirstOrDefaultAsync(ct);

        if (poLineId == Guid.Empty)
        {
            poLineId = auth.PoLineId;
        }

        var uomId = cmd.ReceivedUomId == Guid.Empty ? auth.BaseUomId : cmd.ReceivedUomId;

        return await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                auth.PurchaseOrderId,
                cmd.SupplierDeliveryNoteNumber,
                cmd.IdempotencyKey,
                cmd.ReceivedDate,
                cmd.ReceivingStation,
                cmd.VehicleOrSealRef,
                cmd.DocumentEvidenceRef,
                cmd.Notes,
                [
                    new ArrivalLineInput(
                        poLineId,
                        uomId,
                        cmd.PhysicallyDeliveredQuantity,
                        cmd.FlagForInspection,
                        cmd.FlagForInspection ? DiscrepancyReasonCodes.DamagedPackaging : null,
                        cmd.FlagForInspection ? $"Linked replacement {auth.AuthorizationNumber}" : null,
                        null)
                ],
                cmd.ActorUserId,
                cmd.NowUtc,
                auth.ReplacementAuthorizationId),
            ct);
    }

    private static async Task<EvaluatedContext> EvaluateContextAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string returnCaseType,
        string resolutionMode,
        Guid purchaseOrderId,
        Guid? goodsReceiptId,
        Guid? receiptAccrualId,
        Guid? supplierBillId,
        DateOnly evaluationDate,
        string? taxReturnNoteNumber,
        IReadOnlyList<ReturnLineDraftInput> lines,
        Guid? excludeSupplierReturnId,
        DateTimeOffset nowUtc,
        bool allowLocationCreation,
        PurchaseOrder? lockedPo,
        GoodsReceipt? lockedReceipt,
        ReceiptAccrual? lockedAccrual,
        List<ReceiptAccrualLine>? lockedAccrualLines,
        SupplierBill? lockedBill,
        PayableItem? lockedPayable,
        IReadOnlyDictionary<Guid, ValuationPool>? lockedPools,
        IReadOnlyDictionary<(Guid ItemId, Guid LocationId, Guid? StockLotId, string StockStatus), StockLocationBalance>? lockedLocationBalances,
        bool includePendingClaimsInCapacity,
        CancellationToken ct)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new ArgumentException("At least one return line is required.", nameof(lines));
        }

        var seenGrLines = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (line.GoodsReceiptLineId == Guid.Empty)
            {
                throw new ArgumentException("GoodsReceiptLineId must be provided on every return line.", nameof(lines));
            }

            if (!seenGrLines.Add(line.GoodsReceiptLineId))
            {
                throw new InvalidOperationException("duplicate_goods_receipt_line_in_return: Each goods receipt line may appear only once in a return.");
            }
        }

        var normCase = SupplierReturnCaseTypes.Normalize(returnCaseType);
        var normMode = ReturnResolutionModes.Normalize(resolutionMode);

        var po = lockedPo ?? await db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new InvalidOperationException("po_not_found: Purchase order was not found in this legal entity.");

        var partner = await db.Partners.AsNoTracking()
            .SingleAsync(p => p.TenantId == tenantId && p.PartnerId == po.SupplierPartnerId, ct);

        var grLineIds = lines.Select(l => l.GoodsReceiptLineId).Distinct().ToList();
        var grLinesMap = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && grLineIds.Contains(l.GoodsReceiptLineId))
            .ToDictionaryAsync(l => l.GoodsReceiptLineId, ct);

        if (grLinesMap.Count != grLineIds.Count)
        {
            throw new InvalidOperationException("goods_receipt_line_not_found: One or more goods receipt lines were not found in this legal entity.");
        }

        var firstGrId = goodsReceiptId ?? grLinesMap.Values.Select(l => l.GoodsReceiptId).Distinct().First();
        var receipt = lockedReceipt ?? await db.GoodsReceipts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.GoodsReceiptId == firstGrId, ct);

        if (receipt is not null && receipt.PurchaseOrderId != po.PurchaseOrderId)
        {
            throw new InvalidOperationException("return_po_mismatch: Goods receipt does not belong to the selected purchase order.");
        }

        foreach (var gl in grLinesMap.Values)
        {
            if (gl.PurchaseOrderId != po.PurchaseOrderId || (goodsReceiptId.HasValue && gl.GoodsReceiptId != goodsReceiptId.Value))
            {
                throw new InvalidOperationException("return_po_mismatch: All return lines must belong to the selected PO and receipt.");
            }
        }

        var poLineIds = grLinesMap.Values.Select(l => l.PoLineId).Distinct().ToList();
        var poLinesMap = await db.PurchaseOrderLines.AsNoTracking()
            .Where(pl => pl.TenantId == tenantId && pl.LegalEntityId == entityId && poLineIds.Contains(pl.PoLineId))
            .ToDictionaryAsync(pl => pl.PoLineId, ct);

        var itemIds = grLinesMap.Values.Select(l => l.ItemId).Distinct().ToList();
        var itemsMap = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);
        var uomIds = itemsMap.Values.Select(i => i.BaseUomId).Distinct().ToList();
        var uomsMap = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
            .ToDictionaryAsync(u => u.UomId, ct);

        // Load case-specific source documents
        ReceiptAccrual? accrual = lockedAccrual;
        var accrualLineById = new Dictionary<Guid, ReceiptAccrualLine>();
        var accrualLineByGrLine = new Dictionary<Guid, ReceiptAccrualLine>();
        var valLineById = new Dictionary<Guid, ValuedAcceptanceLine>();
        var valLineByGrLine = new Dictionary<Guid, ValuedAcceptanceLine>();

        if (normCase == SupplierReturnCaseTypes.ValuedUnbilledReturn)
        {
            if (lockedAccrualLines is not null)
            {
                foreach (var al in lockedAccrualLines)
                {
                    accrualLineById[al.ReceiptAccrualLineId] = al;
                    accrualLineByGrLine.TryAdd(al.GoodsReceiptLineId, al);
                }
            }
            else
            {
                var explicitAccLineIds = lines.Where(l => l.ReceiptAccrualLineId.HasValue).Select(l => l.ReceiptAccrualLineId!.Value).Distinct().ToList();
                var candidateAccLines = await db.ReceiptAccrualLines.AsNoTracking()
                    .Where(al =>
                        al.TenantId == tenantId &&
                        al.LegalEntityId == entityId &&
                        (explicitAccLineIds.Contains(al.ReceiptAccrualLineId) || grLineIds.Contains(al.GoodsReceiptLineId)))
                    .ToListAsync(ct);

                foreach (var al in candidateAccLines)
                {
                    if (receiptAccrualId.HasValue && al.ReceiptAccrualId != receiptAccrualId.Value)
                    {
                        continue;
                    }
                    accrualLineById[al.ReceiptAccrualLineId] = al;
                    accrualLineByGrLine.TryAdd(al.GoodsReceiptLineId, al);
                }
            }

            var resolvedAccrualId = receiptAccrualId ?? accrualLineById.Values.Select(a => a.ReceiptAccrualId).Distinct().FirstOrDefault();
            if (resolvedAccrualId == Guid.Empty)
            {
                throw new InvalidOperationException("receipt_accrual_not_found: Valued unbilled return requires an open receipt accrual.");
            }

            accrual ??= await db.ReceiptAccruals.AsNoTracking()
                .SingleOrDefaultAsync(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.ReceiptAccrualId == resolvedAccrualId, ct)
                ?? throw new InvalidOperationException("receipt_accrual_not_found: Receipt accrual was not found in this legal entity.");

            if (accrual.PurchaseOrderId != po.PurchaseOrderId)
            {
                throw new InvalidOperationException("return_po_mismatch: Receipt accrual does not belong to the selected purchase order.");
            }

            receipt ??= await db.GoodsReceipts.AsNoTracking()
                .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.GoodsReceiptId == accrual.GoodsReceiptId, ct);

            var valLineIds = accrualLineById.Values.Select(a => a.ValuedAcceptanceLineId).Distinct().ToList();
            if (valLineIds.Count > 0)
            {
                var valLines = await db.ValuedAcceptanceLines.AsNoTracking()
                    .Where(vl => vl.TenantId == tenantId && vl.LegalEntityId == entityId && valLineIds.Contains(vl.ValuedAcceptanceLineId))
                    .ToListAsync(ct);
                foreach (var vl in valLines)
                {
                    valLineById[vl.ValuedAcceptanceLineId] = vl;
                    valLineByGrLine.TryAdd(vl.GoodsReceiptLineId, vl);
                }
            }
        }

        SupplierBill? bill = lockedBill;
        PayableItem? payable = lockedPayable;
        var billLineById = new Dictionary<Guid, SupplierBillLine>();
        var billLineByGrLine = new Dictionary<Guid, SupplierBillLine>();
        var priorCreditedByBillLine = new Dictionary<Guid, (decimal Qty, decimal Sub, decimal Ppn)>();
        var taxCodesMap = new Dictionary<Guid, TaxCode>();

        if (normCase == SupplierReturnCaseTypes.ValuedBilledReturn)
        {
            if (!supplierBillId.HasValue || supplierBillId.Value == Guid.Empty)
            {
                throw new ArgumentException("SupplierBillId is required for VALUED_BILLED_RETURN.", nameof(supplierBillId));
            }

            bill ??= await db.SupplierBills.AsNoTracking()
                .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.SupplierBillId == supplierBillId.Value, ct)
                ?? throw new InvalidOperationException("supplier_bill_not_found: Supplier bill was not found in this legal entity.");

            if (bill.Status != SupplierBillStatuses.Posted)
            {
                throw new InvalidOperationException(
                    $"bill_not_posted: Valued billed return requires a POSTED supplier bill (current: '{bill.Status}').");
            }

            if (bill.PurchaseOrderId != po.PurchaseOrderId)
            {
                throw new InvalidOperationException("return_po_mismatch: Supplier bill does not belong to the selected purchase order.");
            }

            payable ??= await db.PayableItems.AsNoTracking()
                .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.SupplierBillId == bill.SupplierBillId, ct)
                ?? throw new InvalidOperationException("payable_item_not_found: AP payable item for supplier bill was not found.");

            var billLines = await db.SupplierBillLines.AsNoTracking()
                .Where(bl => bl.TenantId == tenantId && bl.LegalEntityId == entityId && bl.SupplierBillId == bill.SupplierBillId)
                .ToListAsync(ct);
            foreach (var bl in billLines)
            {
                billLineById[bl.SupplierBillLineId] = bl;
                billLineByGrLine.TryAdd(bl.GoodsReceiptLineId, bl);
            }

            var billLineIds = billLines.Select(l => l.SupplierBillLineId).ToList();
            if (billLineIds.Count > 0)
            {
                var priorCredits = await db.SupplierCreditLines.AsNoTracking()
                    .Where(cl => cl.TenantId == tenantId && cl.LegalEntityId == entityId && billLineIds.Contains(cl.SupplierBillLineId))
                    .GroupBy(cl => cl.SupplierBillLineId)
                    .Select(g => new
                    {
                        BillLineId = g.Key,
                        Qty = g.Sum(x => x.CreditedBaseQuantity),
                        Sub = g.Sum(x => x.SubtotalCreditAmount),
                        Ppn = g.Sum(x => x.PpnReversalAmount)
                    })
                    .ToListAsync(ct);

                foreach (var pc in priorCredits)
                {
                    priorCreditedByBillLine[pc.BillLineId] = (pc.Qty, pc.Sub, pc.Ppn);
                }
            }

            var taxIds = billLines.Select(bl => bl.PpnTaxCodeId).Distinct().ToList();
            if (taxIds.Count > 0)
            {
                taxCodesMap = await db.TaxCodes.AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && taxIds.Contains(t.TaxCodeId))
                    .ToDictionaryAsync(t => t.TaxCodeId, ct);
            }

            // Fill accrual/val lines referenced by billed lines for complete lineage
            var refAccIds = billLines.Select(bl => bl.ReceiptAccrualLineId).Distinct().ToList();
            if (refAccIds.Count > 0)
            {
                var accLines = await db.ReceiptAccrualLines.AsNoTracking()
                    .Where(al => al.TenantId == tenantId && al.LegalEntityId == entityId && refAccIds.Contains(al.ReceiptAccrualLineId))
                    .ToListAsync(ct);
                foreach (var al in accLines)
                {
                    accrualLineById[al.ReceiptAccrualLineId] = al;
                }
            }

            var refValIds = billLines.Select(bl => bl.ValuedAcceptanceLineId).Distinct().ToList();
            if (refValIds.Count > 0)
            {
                var valLines = await db.ValuedAcceptanceLines.AsNoTracking()
                    .Where(vl => vl.TenantId == tenantId && vl.LegalEntityId == entityId && refValIds.Contains(vl.ValuedAcceptanceLineId))
                    .ToListAsync(ct);
                foreach (var vl in valLines)
                {
                    valLineById[vl.ValuedAcceptanceLineId] = vl;
                }
            }
        }

        // Prior unvalued returns claimed on the same GR lines (M1 protection)
        var eligibleStatuses = includePendingClaimsInCapacity
            ? new[] { SupplierReturnStatuses.Executed, SupplierReturnStatuses.Approved, SupplierReturnStatuses.PendingApproval }
            : new[] { SupplierReturnStatuses.Executed };

        var priorM1Claimed = normCase == SupplierReturnCaseTypes.UnvaluedDockRejection
            ? await (from rl in db.SupplierReturnLines.AsNoTracking()
                     join r in db.SupplierReturns.AsNoTracking()
                         on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                         equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
                     where rl.TenantId == tenantId
                           && rl.LegalEntityId == entityId
                           && r.ReturnCaseType == SupplierReturnCaseTypes.UnvaluedDockRejection
                           && eligibleStatuses.Contains(r.Status)
                           && (!excludeSupplierReturnId.HasValue || r.SupplierReturnId != excludeSupplierReturnId.Value)
                           && grLineIds.Contains(rl.GoodsReceiptLineId)
                     group rl by rl.GoodsReceiptLineId into g
                     select new { GrLineId = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
                .ToDictionaryAsync(x => x.GrLineId, x => x.Qty, ct)
            : new Dictionary<Guid, decimal>();

        var inFlightUnbilledByAccLine = normCase == SupplierReturnCaseTypes.ValuedUnbilledReturn && includePendingClaimsInCapacity
            ? await (from rl in db.SupplierReturnLines.AsNoTracking()
                     join r in db.SupplierReturns.AsNoTracking()
                         on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                         equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
                     where rl.TenantId == tenantId
                           && rl.LegalEntityId == entityId
                           && r.ReturnCaseType == SupplierReturnCaseTypes.ValuedUnbilledReturn
                           && (r.Status == SupplierReturnStatuses.Approved || r.Status == SupplierReturnStatuses.PendingApproval)
                           && (!excludeSupplierReturnId.HasValue || r.SupplierReturnId != excludeSupplierReturnId.Value)
                           && rl.ReceiptAccrualLineId != null
                     group rl by rl.ReceiptAccrualLineId!.Value into g
                     select new { AccLineId = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
                .ToDictionaryAsync(x => x.AccLineId, x => x.Qty, ct)
            : new Dictionary<Guid, decimal>();

        var inFlightCreditedByBillLine = normCase == SupplierReturnCaseTypes.ValuedBilledReturn && includePendingClaimsInCapacity
            ? await (from rl in db.SupplierReturnLines.AsNoTracking()
                     join r in db.SupplierReturns.AsNoTracking()
                         on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                         equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
                     where rl.TenantId == tenantId
                           && rl.LegalEntityId == entityId
                           && r.ReturnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn
                           && (r.Status == SupplierReturnStatuses.Approved || r.Status == SupplierReturnStatuses.PendingApproval)
                           && (!excludeSupplierReturnId.HasValue || r.SupplierReturnId != excludeSupplierReturnId.Value)
                           && rl.SupplierBillLineId != null
                     group rl by rl.SupplierBillLineId!.Value into g
                     select new { BillLineId = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
                .ToDictionaryAsync(x => x.BillLineId, x => x.Qty, ct)
            : new Dictionary<Guid, decimal>();

        // Inventory locations & pools for valued returns
        var locationsById = new Dictionary<Guid, WarehouseLocation>();
        var poolSimState = new Dictionary<Guid, (decimal Qty, decimal Value, decimal Mac, long Seq)>();
        var locSimRemaining = new Dictionary<(Guid ItemId, Guid LocationId, Guid? LotId, string Status), decimal>();

        if (normCase != SupplierReturnCaseTypes.UnvaluedDockRejection)
        {
            if (allowLocationCreation)
            {
                await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantId, entityId, nowUtc, ct);
            }

            locationsById = await db.WarehouseLocations.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.Status == MasterStatuses.Active)
                .ToDictionaryAsync(l => l.LocationId, ct);

            var poolsMap = lockedPools is not null
                ? lockedPools
                : await db.ValuationPools.AsNoTracking()
                    .Where(p =>
                        p.TenantId == tenantId &&
                        p.LegalEntityId == entityId &&
                        p.CurrencyCode == po.CurrencyCode &&
                        p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode &&
                        itemIds.Contains(p.ItemId))
                    .ToDictionaryAsync(p => p.ItemId, ct);

            foreach (var itemId in itemIds)
            {
                poolSimState[itemId] = poolsMap.TryGetValue(itemId, out var p)
                    ? (p.ValuedBaseQuantity, p.CarryingValueAmount, p.MovingAverageUnitCost, p.LastEntrySequence)
                    : (0m, 0m, 0m, 0L);
            }

            if (lockedLocationBalances is not null)
            {
                foreach (var kv in lockedLocationBalances)
                {
                    var elig = kv.Key.StockStatus == StockStatuses.Unrestricted
                        ? kv.Value.AvailableBaseQuantity
                        : kv.Value.OnHandBaseQuantity;
                    locSimRemaining[kv.Key] = elig;
                }
            }
            else
            {
                var existingBalances = await db.StockLocationBalances.AsNoTracking()
                    .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && itemIds.Contains(b.ItemId))
                    .ToListAsync(ct);

                foreach (var b in existingBalances)
                {
                    var key = (b.ItemId, b.LocationId, b.StockLotId, b.StockStatus);
                    var elig = b.StockStatus == StockStatuses.Unrestricted
                        ? b.AvailableBaseQuantity
                        : b.OnHandBaseQuantity;
                    locSimRemaining[key] = elig;
                }
            }
        }

        var period = await db.FinancialPeriods.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.StartDate <= evaluationDate && p.EndDate >= evaluationDate)
            .OrderBy(p => p.PeriodNumber)
            .FirstOrDefaultAsync(ct);

        var bindings = new List<ResolvedLineBinding>(lines.Count);
        var calcInputs = new List<SupplierReturnMatrixLineInput>(lines.Count);

        foreach (var input in lines)
        {
            var grLine = grLinesMap[input.GoodsReceiptLineId];
            var poLine = poLinesMap[grLine.PoLineId];
            var item = itemsMap[grLine.ItemId];
            var baseUom = uomsMap[item.BaseUomId];

            ValuedAcceptanceLine? valLine = null;
            ReceiptAccrualLine? accLine = null;
            SupplierBillLine? billLine = null;
            TaxCode? ppnTaxCode = null;
            WarehouseLocation? srcLoc = null;
            Guid? srcLotId = input.SourceStockLotId;
            string? srcStatus = null;

            decimal authCost;
            decimal eligibleCapacity;
            decimal poolQty = 0m, poolVal = 0m, poolMac = 0m;
            long poolSeq = 0;
            decimal unbilledRemQty = 0m, unbilledRemAmt = 0m;
            decimal billNet = 0m, billQty = 0m, priorCredQty = 0m, priorCredSub = 0m, priorCredPpn = 0m, billPpn = 0m, billWh = 0m;

            if (normCase == SupplierReturnCaseTypes.UnvaluedDockRejection)
            {
                if (grLine.DockRejectedBaseQuantity <= 0m)
                {
                    throw new InvalidOperationException(
                        $"no_dock_rejected_quantity: Receipt line '{grLine.ItemCodeSnapshot}' has zero dock-rejected quantity for M1 unvalued return.");
                }

                var claimed = priorM1Claimed.GetValueOrDefault(grLine.GoodsReceiptLineId, 0m);
                eligibleCapacity = Math.Max(0m, decimal.Round(grLine.DockRejectedBaseQuantity - claimed, 6, MidpointRounding.AwayFromZero));
                authCost = poLine.OrderedBaseQuantity > 0m
                    ? decimal.Round(poLine.LineSubtotalAmount / poLine.OrderedBaseQuantity, 6, MidpointRounding.AwayFromZero)
                    : poLine.UnitPrice;
            }
            else
            {
                srcStatus = StockStatuses.Normalize(
                    string.IsNullOrWhiteSpace(input.SourceStockStatus) ? StockStatuses.Unrestricted : input.SourceStockStatus);
                if (srcStatus is not (StockStatuses.Unrestricted or StockStatuses.Blocked))
                {
                    throw new InvalidOperationException(
                        "return_stock_status_invalid: Dispose QUALITY_INSPECTION stock to UNRESTRICTED or BLOCKED before returning to supplier.");
                }

                if (input.SourceLocationId is { } explicitLocId)
                {
                    if (!locationsById.TryGetValue(explicitLocId, out srcLoc))
                    {
                        throw new InvalidOperationException("warehouse_location_not_found: Selected source warehouse location is inactive or missing.");
                    }
                }
                else
                {
                    // Pick matching balance location if available, else default zone location for status
                    var matchingKey = locSimRemaining
                        .Where(kv =>
                            kv.Key.ItemId == item.ItemId &&
                            kv.Key.Status == srcStatus &&
                            (!srcLotId.HasValue || kv.Key.LotId == srcLotId) &&
                            kv.Value > 0m &&
                            locationsById.ContainsKey(kv.Key.LocationId))
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();

                    if (matchingKey.LocationId != Guid.Empty)
                    {
                        srcLoc = locationsById[matchingKey.LocationId];
                        srcLotId ??= matchingKey.LotId;
                    }
                    else
                    {
                        var desiredZone = srcStatus == StockStatuses.Blocked ? LocationZoneTypes.Quarantine : LocationZoneTypes.Storage;
                        srcLoc = locationsById.Values
                            .Where(l => l.ZoneType == desiredZone)
                            .OrderByDescending(l => l.IsDefaultReceiving)
                            .FirstOrDefault()
                            ?? locationsById.Values.FirstOrDefault();
                    }
                }

                if (srcLoc is null)
                {
                    throw new InvalidOperationException("warehouse_location_not_found: No active warehouse location found for stock return.");
                }

                var balKey = (item.ItemId, srcLoc.LocationId, srcLotId, srcStatus);
                var locAvail = locSimRemaining.GetValueOrDefault(balKey, 0m);
                var qtyRounded = decimal.Round(input.ReturnedBaseQuantity, 6, MidpointRounding.AwayFromZero);

                var simPool = poolSimState[item.ItemId];
                poolQty = simPool.Qty;
                poolVal = simPool.Value;
                poolMac = simPool.Mac;
                poolSeq = simPool.Seq;

                if (normCase == SupplierReturnCaseTypes.ValuedUnbilledReturn)
                {
                    if (input.ReceiptAccrualLineId is { } accLineId)
                    {
                        if (!accrualLineById.TryGetValue(accLineId, out accLine))
                        {
                            throw new InvalidOperationException("receipt_accrual_line_not_found: Selected accrual line was not found in this receipt accrual.");
                        }
                    }
                    else if (!accrualLineByGrLine.TryGetValue(grLine.GoodsReceiptLineId, out accLine))
                    {
                        throw new InvalidOperationException("receipt_accrual_line_not_found: No receipt accrual line found for this goods receipt line.");
                    }

                    valLineById.TryGetValue(accLine.ValuedAcceptanceLineId, out valLine);
                    var inFlight = inFlightUnbilledByAccLine.GetValueOrDefault(accLine.ReceiptAccrualLineId, 0m);
                    unbilledRemQty = Math.Max(0m, decimal.Round(accLine.RemainingUnbilledBaseQuantity - inFlight, 6, MidpointRounding.AwayFromZero));
                    unbilledRemAmt = accLine.RemainingOpenLineAmount;
                    authCost = accLine.UnitCostBase;
                    eligibleCapacity = Math.Min(unbilledRemQty, locAvail);
                }
                else
                {
                    if (input.SupplierBillLineId is { } blId)
                    {
                        if (!billLineById.TryGetValue(blId, out billLine))
                        {
                            throw new InvalidOperationException("supplier_bill_line_not_found: Selected supplier bill line was not found on this bill.");
                        }
                    }
                    else if (!billLineByGrLine.TryGetValue(grLine.GoodsReceiptLineId, out billLine))
                    {
                        throw new InvalidOperationException("supplier_bill_line_not_found: No billed line found for this goods receipt line on the selected invoice.");
                    }

                    accrualLineById.TryGetValue(billLine.ReceiptAccrualLineId, out accLine);
                    valLineById.TryGetValue(billLine.ValuedAcceptanceLineId, out valLine);
                    taxCodesMap.TryGetValue(billLine.PpnTaxCodeId, out ppnTaxCode);

                    (decimal Qty, decimal Sub, decimal Ppn) prior = priorCreditedByBillLine.TryGetValue(billLine.SupplierBillLineId, out var priorTuple)
                        ? priorTuple
                        : (0m, 0m, 0m);
                    var inFlightCred = inFlightCreditedByBillLine.GetValueOrDefault(billLine.SupplierBillLineId, 0m);
                    priorCredQty = decimal.Round(prior.Qty + inFlightCred, 6, MidpointRounding.AwayFromZero);
                    priorCredSub = prior.Sub;
                    priorCredPpn = prior.Ppn;
                    billNet = billLine.LineNetAmount;
                    billQty = billLine.BilledBaseQuantity;
                    billPpn = billLine.PpnTaxAmount;
                    billWh = billLine.WithholdingTaxAmount;
                    authCost = billLine.InvoiceUnitPriceBase;
                    var remBillQty = Math.Max(0m, decimal.Round(billLine.BilledBaseQuantity - priorCredQty, 6, MidpointRounding.AwayFromZero));
                    eligibleCapacity = Math.Min(remBillQty, locAvail);
                }

                // Advance cumulative pool & location simulation so multiple lines of same item/location cascade properly
                if (qtyRounded > 0m && qtyRounded <= locAvail)
                {
                    locSimRemaining[balKey] = decimal.Round(locAvail - qtyRounded, 6, MidpointRounding.AwayFromZero);
                }
                if (qtyRounded > 0m && qtyRounded <= poolQty)
                {
                    try
                    {
                        var nextStep = ValuationPool.SimulatePurchaseReturn(
                            ValuationPoolStatuses.Active, poolQty, poolVal, poolMac, poolSeq, qtyRounded);
                        poolSimState[item.ItemId] = (
                            nextStep.ValuationStep.NewBaseQuantity,
                            nextStep.ValuationStep.NewCarryingValue,
                            nextStep.ValuationStep.NewMovingAverageCost,
                            nextStep.ValuationStep.EntrySequence);
                    }
                    catch
                    {
                        // Let MatrixCalculator capture OD-04 / negative pool blocks per line cleanly.
                    }
                }
            }

            bindings.Add(new ResolvedLineBinding(
                input,
                grLine,
                valLine,
                accLine,
                billLine,
                poLine,
                item,
                baseUom,
                srcLoc,
                srcLotId,
                srcStatus,
                ppnTaxCode));

            calcInputs.Add(new SupplierReturnMatrixLineInput(
                grLine.GoodsReceiptLineId,
                valLine?.ValuedAcceptanceLineId,
                accLine?.ReceiptAccrualLineId,
                billLine?.SupplierBillLineId,
                poLine.PoLineId,
                poLine.LineageLineKey,
                item.ItemId,
                item.ItemCode,
                item.Name,
                item.BaseUomId,
                baseUom.UomCode,
                srcLoc?.LocationId,
                srcLotId,
                srcStatus,
                input.ReturnedBaseQuantity,
                authCost,
                poolQty,
                poolVal,
                poolMac,
                poolSeq,
                unbilledRemQty,
                unbilledRemAmt,
                billNet,
                billQty,
                priorCredQty,
                priorCredSub,
                priorCredPpn,
                billPpn,
                billWh,
                eligibleCapacity));
        }

        var matrix = SupplierReturnMatrixCalculator.Evaluate(
            normCase,
            normMode,
            payable?.PaidAmount ?? 0m,
            period?.Status,
            taxReturnNoteNumber,
            calcInputs);

        return new EvaluatedContext(
            new ResolvedHeaderContext(po, partner, receipt, accrual, bill, payable),
            bindings,
            matrix);
    }

    private static List<SupplierReturnLine> BuildEntityLines(
        Guid tenantId,
        Guid entityId,
        Guid supplierReturnId,
        IReadOnlyList<ReturnLineDraftInput> draftInputs,
        IReadOnlyList<SupplierReturnEvaluatedLine> evaluatedLines)
    {
        var result = new List<SupplierReturnLine>(evaluatedLines.Count);
        for (var i = 0; i < evaluatedLines.Count; i++)
        {
            result.Add(SupplierReturnLine.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                supplierReturnId,
                i + 1,
                evaluatedLines[i],
                draftInputs[i].LineNotes));
        }

        return result;
    }

    private static async Task<SupplierReturn?> LockReturnForUpdateAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierReturnId,
        CancellationToken ct)
    {
        return (await db.SupplierReturns
            .FromSqlInterpolated(
                $"""
                SELECT * FROM procurement.supplier_returns
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {entityId}
                  AND supplier_return_id = {supplierReturnId}
                FOR UPDATE
                """)
            .ToListAsync(ct))
            .SingleOrDefault();
    }

    private static async Task<Dictionary<string, Guid>> ResolveRequiredAccountsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        IReadOnlyCollection<string> purposes,
        CancellationToken ct)
    {
        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && purposes.Contains(m.PurposeCode))
            .ToDictionaryAsync(m => m.PurposeCode, ct);

        var accountIds = mappings.Values.Select(m => m.AccountId).Distinct().ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, GlAccount>()
            : await db.GlAccounts.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accountIds.Contains(a.AccountId))
                .ToDictionaryAsync(a => a.AccountId, ct);

        var result = new Dictionary<string, Guid>(purposes.Count, StringComparer.Ordinal);
        foreach (var p in purposes)
        {
            if (!mappings.TryGetValue(p, out var map) || !accounts.TryGetValue(map.AccountId, out var acc))
            {
                throw new InvalidOperationException($"missing_account_mapping: Configure active GL account mapping for '{p}'.");
            }

            if (acc.Status != MasterStatuses.Active || !acc.AllowPosting)
            {
                throw new InvalidOperationException($"account_not_postable: Account '{acc.AccountCode}' ({p}) must be active and postable.");
            }

            result[p] = acc.AccountId;
        }

        return result;
    }

    private static async Task<ReturnCommandOutcome> LoadExecutedOutcomeAsync(
        AtheraDbContext db,
        SupplierReturn header,
        CancellationToken ct)
    {
        var lines = await db.SupplierReturnLines.AsNoTracking()
            .Where(l => l.TenantId == header.TenantId && l.LegalEntityId == header.LegalEntityId && l.SupplierReturnId == header.SupplierReturnId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var movement = header.StockMovementId.HasValue
            ? await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(
                m => m.TenantId == header.TenantId && m.LegalEntityId == header.LegalEntityId && m.StockMovementId == header.StockMovementId.Value, ct)
            : null;

        var journal = header.GlJournalId.HasValue
            ? await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(
                j => j.TenantId == header.TenantId && j.LegalEntityId == header.LegalEntityId && j.JournalId == header.GlJournalId.Value, ct)
            : null;

        var credit = header.SupplierCreditId.HasValue
            ? await db.SupplierCredits.AsNoTracking().SingleOrDefaultAsync(
                c => c.TenantId == header.TenantId && c.LegalEntityId == header.LegalEntityId && c.SupplierCreditId == header.SupplierCreditId.Value, ct)
            : null;

        var replacements = await db.ReplacementAuthorizations.AsNoTracking()
            .Where(a => a.TenantId == header.TenantId && a.LegalEntityId == header.LegalEntityId && a.SupplierReturnId == header.SupplierReturnId)
            .OrderBy(a => a.AuthorizationNumber)
            .ToListAsync(ct);

        var taxDets = credit is not null
            ? await db.TaxDeterminations.AsNoTracking()
                .Where(t => t.TenantId == header.TenantId && t.LegalEntityId == header.LegalEntityId && t.SourceDocumentId == credit.SupplierCreditId)
                .ToListAsync(ct)
            : [];

        return new ReturnCommandOutcome(
            true,
            true,
            200,
            null,
            null,
            header,
            lines,
            null,
            movement,
            journal,
            credit,
            replacements,
            taxDets);
    }

    private static string BuildScopedKey(string prefix, string idempKey)
    {
        var raw = $"{prefix}:{idempKey}";
        if (raw.Length <= 80)
        {
            return raw;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..32].ToLowerInvariant();
        return $"{prefix[..Math.Min(prefix.Length, 44)]}:{hash}";
    }

    private static ReturnCommandOutcome MapDomainFailure(InvalidOperationException ex)
    {
        var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
        var code = idx > 0 ? ex.Message[..idx].Trim() : "return_domain_error";
        var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;

        var statusCode = code switch
        {
            "po_not_found" or
            "goods_receipt_line_not_found" or
            "receipt_accrual_not_found" or
            "receipt_accrual_line_not_found" or
            "supplier_bill_not_found" or
            "supplier_bill_line_not_found" or
            "payable_item_not_found" or
            "warehouse_location_not_found" => 404,
            "self_approval_forbidden" => 403,
            _ => 409
        };

        return ReturnCommandOutcome.Fail(statusCode, code, detail);
    }
}
