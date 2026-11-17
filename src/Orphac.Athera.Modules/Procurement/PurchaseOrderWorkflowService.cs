using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Modules.Procurement;

public sealed record PoLineInput(
    string? LineageLineKey,
    Guid ItemId,
    Guid OrderedUomId,
    decimal OrderedQuantity,
    decimal UnitPrice,
    Guid? TaxCodeId,
    decimal OverdeliveryTolerancePercent = 0m,
    decimal OverdeliveryToleranceBaseQty = 0m,
    bool AllowUnlimitedOverdelivery = false,
    string? LineDescription = null);

public sealed record CreatePoDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    string? CurrencyCode,
    Guid SupplierPartnerId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid? TolerancePolicyId,
    string? Notes,
    IReadOnlyList<PoLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdatePoDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    Guid RevisionId,
    Guid SupplierPartnerId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string DeliveryDestination,
    int PaymentTermsDays,
    Guid? TolerancePolicyId,
    string? ChangeReason,
    string? Notes,
    IReadOnlyList<PoLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CreatePoAmendmentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string ChangeReason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SubmitPoRevisionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    Guid RevisionId,
    string? PoNumberOverride,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record DecidePoRevisionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    Guid RevisionId,
    string Decision,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CloseOrCancelPoCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record PoWorkflowOutcome(
    bool Succeeded,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    PurchaseOrder? PurchaseOrder,
    PurchaseOrderRevision? Revision)
{
    public static PoWorkflowOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, null, null);

    public static PoWorkflowOutcome Success(PurchaseOrder po, PurchaseOrderRevision revision, int statusCode = 200) =>
        new(true, statusCode, null, null, po, revision);
}

/// <summary>
/// Authoritative transactional workflow handler for S05 Purchase Orders, frozen revisions,
/// four-eyes approvals, and OD-01 delivery tolerance enforcement.
/// Caller MUST execute inside a scoped PostgreSQL transaction (`ExecuteInEntityAsync`).
/// </summary>
public static class PurchaseOrderWorkflowService
{
    public const string PoApprovalPolicyCode = "PURCHASE_ORDER_COMMITMENT";

    public static async Task<PoWorkflowOutcome> CreateDraftAsync(
        AtheraDbContext db,
        CreatePoDraftCommand cmd,
        CancellationToken ct)
    {
        var poId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var draftCode = $"DRF-{cmd.NowUtc:yyyyMMdd}-{poId.ToString("N")[..6].ToUpperInvariant()}";

        PurchaseOrder po;
        try
        {
            po = PurchaseOrder.CreateDraft(
                poId,
                cmd.TenantId,
                cmd.LegalEntityId,
                draftCode,
                cmd.SupplierPartnerId,
                cmd.CurrencyCode ?? cmd.ExpectedFunctionalCurrencyCode,
                cmd.ExpectedFunctionalCurrencyCode,
                cmd.OrderDate,
                cmd.ExpectedDeliveryDate,
                cmd.DeliveryDestination,
                cmd.PaymentTermsDays,
                cmd.ActorUserId,
                cmd.NowUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }

        var validated = await ValidateAndBuildLinesAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.PurchaseOrderId,
            revId,
            cmd.SupplierPartnerId,
            cmd.OrderDate,
            cmd.TolerancePolicyId,
            cmd.Lines,
            ct);

        if (validated.Failure is not null)
        {
            return validated.Failure;
        }

        var rev = PurchaseOrderRevision.CreateDraft(
            revId,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.PurchaseOrderId,
            revisionNumber: 1,
            supersedesRevisionId: null,
            validated.Supplier!.PartnerId,
            validated.Supplier.PartnerCode,
            validated.Supplier.LegalName,
            validated.Supplier.TaxRegistrationNpwp,
            validated.Supplier.TaxPkpStatus,
            po.CurrencyCode,
            cmd.OrderDate,
            cmd.ExpectedDeliveryDate,
            po.DeliveryDestination,
            po.PaymentTermsDays,
            validated.TolerancePolicy!.PolicyId,
            validated.TolerancePolicy.Mode,
            changeReason: null,
            notes: cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        rev.ApplyTotalsAndHash(
            validated.SubtotalAmount,
            validated.PpnTaxTotalAmount,
            validated.WithholdingTaxTotalAmount,
            validated.GrandTotalAmount,
            validated.TotalOrderedBaseQuantity,
            validated.BuiltLines,
            cmd.NowUtc);

        db.PurchaseOrders.Add(po);
        db.PurchaseOrderRevisions.Add(rev);
        db.PurchaseOrderLines.AddRange(validated.BuiltLines);

        RecordPoAuditAndOutbox(
            db,
            po,
            rev,
            cmd.ActorUserId,
            "PO_DRAFT_CREATED",
            $"Created PO draft {po.DraftCode} rev {rev.RevisionNumber} ({rev.GrandTotalAmount:F2} {po.CurrencyCode})",
            "procurement.po.draft_created.v1",
            $"po:draft:{rev.RevisionId:N}:v{rev.Version}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return PoWorkflowOutcome.Success(po, rev, StatusCodes_Created);
    }

    public static async Task<PoWorkflowOutcome> UpdateDraftRevisionAsync(
        AtheraDbContext db,
        UpdatePoDraftCommand cmd,
        CancellationToken ct)
    {
        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        if (po.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
        {
            return PoWorkflowOutcome.Fail(409, "po_invalid_status_transition", $"Cannot edit revision when PO is '{po.Status}'.");
        }

        var rev = (await db.PurchaseOrderRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} AND revision_id = {cmd.RevisionId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (rev is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_revision_not_found", "Purchase order revision was not found.");
        }

        if (rev.IsFrozen || rev.Status != PurchaseOrderRevisionStatuses.Draft)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "po_revision_frozen_immutable",
                $"Revision {rev.RevisionNumber} is {rev.Status} (frozen={rev.IsFrozen}) and cannot be edited; create a new revision.");
        }

        var validated = await ValidateAndBuildLinesAsync(
            db,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.PurchaseOrderId,
            rev.RevisionId,
            cmd.SupplierPartnerId,
            cmd.OrderDate,
            cmd.TolerancePolicyId,
            cmd.Lines,
            ct);

        if (validated.Failure is not null)
        {
            return validated.Failure;
        }

        try
        {
            if (po.CurrentApprovedRevisionId is null)
            {
                po.UpdatePreApprovalHeader(
                    cmd.SupplierPartnerId,
                    cmd.OrderDate,
                    cmd.ExpectedDeliveryDate,
                    cmd.DeliveryDestination,
                    cmd.PaymentTermsDays,
                    cmd.NowUtc);
            }

            var existingLines = await db.PurchaseOrderLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
                .ToListAsync(ct);

            db.PurchaseOrderLines.RemoveRange(existingLines);
            await db.SaveChangesAsync(ct);

            rev.UpdateDraftDetailsAndTotals(
                validated.Supplier!.PartnerId,
                validated.Supplier.PartnerCode,
                validated.Supplier.LegalName,
                validated.Supplier.TaxRegistrationNpwp,
                validated.Supplier.TaxPkpStatus,
                cmd.OrderDate,
                cmd.ExpectedDeliveryDate,
                cmd.DeliveryDestination,
                cmd.PaymentTermsDays,
                validated.TolerancePolicy!.PolicyId,
                validated.TolerancePolicy.Mode,
                cmd.ChangeReason,
                cmd.Notes,
                validated.SubtotalAmount,
                validated.PpnTaxTotalAmount,
                validated.WithholdingTaxTotalAmount,
                validated.GrandTotalAmount,
                validated.TotalOrderedBaseQuantity,
                validated.BuiltLines,
                cmd.NowUtc);

            db.PurchaseOrderLines.AddRange(validated.BuiltLines);

            RecordPoAuditAndOutbox(
                db,
                po,
                rev,
                cmd.ActorUserId,
                "PO_DRAFT_UPDATED",
                $"Updated PO {(po.PoNumber ?? po.DraftCode)} rev {rev.RevisionNumber} ({rev.GrandTotalAmount:F2} {po.CurrencyCode})",
                "procurement.po.draft_updated.v1",
                $"po:draft:{rev.RevisionId:N}:v{rev.Version}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return PoWorkflowOutcome.Success(po, rev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<PoWorkflowOutcome> CreateAmendmentRevisionAsync(
        AtheraDbContext db,
        CreatePoAmendmentCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.ChangeReason) || cmd.ChangeReason.Trim().Length > 300)
        {
            return PoWorkflowOutcome.Fail(400, "change_reason_required", "Provide a concise revision reason (1–300 characters).");
        }

        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        var canRevise = (po.Status == PurchaseOrderStatuses.Approved && po.CurrentApprovedRevisionId is not null)
            || (po.Status == PurchaseOrderStatuses.Draft && po.CurrentApprovedRevisionId is null);

        if (!canRevise)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "po_invalid_status_transition",
                $"Revisions can only be created from an APPROVED PO or a DRAFT PO recovering from rejection (current: '{po.Status}').");
        }

        var hasOpenRevision = await db.PurchaseOrderRevisions
            .AsNoTracking()
            .AnyAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.PurchaseOrderId == po.PurchaseOrderId &&
                (r.Status == PurchaseOrderRevisionStatuses.Draft || r.Status == PurchaseOrderRevisionStatuses.PendingApproval),
                ct);

        if (hasOpenRevision)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "po_open_revision_exists",
                "An open DRAFT or PENDING_APPROVAL revision already exists for this purchase order.");
        }

        var baseline = po.CurrentApprovedRevisionId is { } appRevId
            ? await db.PurchaseOrderRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(r =>
                    r.TenantId == cmd.TenantId &&
                    r.LegalEntityId == cmd.LegalEntityId &&
                    r.RevisionId == appRevId,
                    ct)
            : await db.PurchaseOrderRevisions
                .AsNoTracking()
                .Where(r =>
                    r.TenantId == cmd.TenantId &&
                    r.LegalEntityId == cmd.LegalEntityId &&
                    r.PurchaseOrderId == po.PurchaseOrderId)
                .OrderByDescending(r => r.RevisionNumber)
                .FirstOrDefaultAsync(ct);

        if (baseline is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_revision_not_found", "Baseline purchase order revision was not found.");
        }

        var baselineLines = await db.PurchaseOrderLines
            .AsNoTracking()
            .Where(l =>
                l.TenantId == cmd.TenantId &&
                l.LegalEntityId == cmd.LegalEntityId &&
                l.RevisionId == baseline.RevisionId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var nextRevNum = po.AllocateNextRevisionNumber(cmd.NowUtc);
        var newRevId = Guid.NewGuid();
        var newRev = PurchaseOrderRevision.CreateDraft(
            newRevId,
            cmd.TenantId,
            cmd.LegalEntityId,
            po.PurchaseOrderId,
            nextRevNum,
            supersedesRevisionId: baseline.RevisionId,
            baseline.SupplierPartnerId,
            baseline.SupplierCodeSnapshot,
            baseline.SupplierLegalNameSnapshot,
            baseline.SupplierNpwpSnapshot,
            baseline.SupplierPkpStatusSnapshot,
            baseline.CurrencyCode,
            baseline.OrderDate,
            baseline.ExpectedDeliveryDate,
            baseline.DeliveryDestination,
            baseline.PaymentTermsDays,
            baseline.TolerancePolicyId,
            baseline.ToleranceModeSnapshot,
            cmd.ChangeReason.Trim(),
            baseline.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        var clonedLines = baselineLines.Select(l => PurchaseOrderLine.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            po.PurchaseOrderId,
            newRevId,
            l.LineNumber,
            l.LineageLineKey,
            l.ItemId,
            l.ItemCodeSnapshot,
            l.ItemNameSnapshot,
            l.ItemKindSnapshot,
            l.OrderedUomId,
            l.OrderedUomCodeSnapshot,
            l.BaseUomId,
            l.BaseUomCodeSnapshot,
            l.UomFactorToBaseSnapshot,
            l.OrderedQuantity,
            l.OrderedBaseQuantity,
            l.UnitPrice,
            l.TaxCodeId,
            l.TaxCodeSnapshot,
            l.TaxFamilySnapshot,
            l.TaxRatePercentSnapshot,
            l.TaxableBaseAmount,
            l.TaxAmount,
            l.OverdeliveryTolerancePercent,
            l.OverdeliveryToleranceBaseQty,
            l.AllowUnlimitedOverdelivery,
            l.LineDescription)).ToList();

        newRev.ApplyTotalsAndHash(
            baseline.SubtotalAmount,
            baseline.PpnTaxTotalAmount,
            baseline.WithholdingTaxTotalAmount,
            baseline.GrandTotalAmount,
            baseline.TotalOrderedBaseQuantity,
            clonedLines,
            cmd.NowUtc);

        db.PurchaseOrderRevisions.Add(newRev);
        db.PurchaseOrderLines.AddRange(clonedLines);

        RecordPoAuditAndOutbox(
            db,
            po,
            newRev,
            cmd.ActorUserId,
            "PO_REVISION_CREATED",
            $"Created draft revision {newRev.RevisionNumber} for {po.PoNumber}: {newRev.ChangeReason}",
            "procurement.po.revised.v1",
            $"po:rev-created:{newRev.RevisionId:N}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return PoWorkflowOutcome.Success(po, newRev, StatusCodes_Created);
    }

    public static async Task<PoWorkflowOutcome> SubmitRevisionAsync(
        AtheraDbContext db,
        SubmitPoRevisionCommand cmd,
        CancellationToken ct)
    {
        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        if (po.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
        {
            return PoWorkflowOutcome.Fail(409, "po_invalid_status_transition", $"Cannot submit revision when PO is '{po.Status}'.");
        }

        var rev = (await db.PurchaseOrderRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} AND revision_id = {cmd.RevisionId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (rev is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_revision_not_found", "Purchase order revision was not found.");
        }

        if (rev.IsFrozen || rev.Status != PurchaseOrderRevisionStatuses.Draft)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "po_revision_frozen_immutable",
                $"Revision {rev.RevisionNumber} is already {rev.Status} and cannot be submitted again.");
        }

        var otherPending = await db.PurchaseOrderRevisions
            .AsNoTracking()
            .AnyAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.PurchaseOrderId == po.PurchaseOrderId &&
                r.RevisionId != rev.RevisionId &&
                r.Status == PurchaseOrderRevisionStatuses.PendingApproval,
                ct);

        if (otherPending)
        {
            return PoWorkflowOutcome.Fail(409, "po_pending_approval_exists", "Another revision is already pending approval on this purchase order.");
        }

        var revLines = await db.PurchaseOrderLines
            .AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (revLines.Count == 0)
        {
            return PoWorkflowOutcome.Fail(400, "po_lines_required", "At least one positive line item is required before submitting.");
        }

        var eligibilityCheck = await RevalidateMasterEligibilityAndTaxAsync(
            db, cmd.TenantId, cmd.LegalEntityId, rev.SupplierPartnerId, rev.OrderDate, revLines, ct);
        if (eligibilityCheck is not null)
        {
            return eligibilityCheck;
        }

        var policy = await db.ApprovalPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(p =>
                p.TenantId == cmd.TenantId &&
                p.LegalEntityId == cmd.LegalEntityId &&
                p.PolicyCode == PoApprovalPolicyCode &&
                p.IsActive,
                ct);

        if (policy is null)
        {
            return PoWorkflowOutcome.Fail(409, "approval_policy_not_configured", $"Active approval policy '{PoApprovalPolicyCode}' is missing for this legal entity.");
        }

        string? assignedPoNumber = null;
        if (string.IsNullOrWhiteSpace(po.PoNumber))
        {
            if (!string.IsNullOrWhiteSpace(cmd.PoNumberOverride))
            {
                assignedPoNumber = cmd.PoNumberOverride.Trim().ToUpperInvariant();
                if (assignedPoNumber.Length > 40)
                {
                    return PoWorkflowOutcome.Fail(400, "invalid_po_number", "PO number must not exceed 40 characters.");
                }
            }
            else
            {
                var seq = await IncrementPoSequenceAsync(db, cmd.TenantId, cmd.LegalEntityId, rev.OrderDate.Year, ct);
                assignedPoNumber = $"PO-{rev.OrderDate.Year:D4}-{seq:D5}";
            }
        }

        try
        {
            po.MarkSubmitted(assignedPoNumber, cmd.NowUtc);

            var snapshotHash = PurchaseOrderHash.ComputeRevisionHash(rev, revLines);
            var approvalId = Guid.NewGuid();
            var approval = DocumentApproval.Create(
                approvalId,
                cmd.TenantId,
                cmd.LegalEntityId,
                policy.PolicyId,
                "PROCUREMENT",
                "PURCHASE_ORDER_REVISION",
                po.PurchaseOrderId,
                rev.RevisionId,
                rev.RevisionNumber,
                $"{po.PoNumber} rev {rev.RevisionNumber} ({rev.SupplierCodeSnapshot} · {rev.GrandTotalAmount:F0} {po.CurrencyCode})",
                cmd.ActorUserId,
                cmd.NowUtc);

            rev.FreezeAndMarkSubmitted(cmd.ActorUserId, approval.ApprovalId, snapshotHash, cmd.NowUtc);
            db.DocumentApprovals.Add(approval);

            RecordPoAuditAndOutbox(
                db,
                po,
                rev,
                cmd.ActorUserId,
                "PO_REVISION_SUBMITTED",
                $"Submitted {po.PoNumber} rev {rev.RevisionNumber} for independent four-eyes approval",
                "procurement.po.submitted.v1",
                $"po:submitted:{rev.RevisionId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return PoWorkflowOutcome.Success(po, rev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<PoWorkflowOutcome> DecideRevisionAsync(
        AtheraDbContext db,
        DecidePoRevisionCommand cmd,
        CancellationToken ct)
    {
        var normalizedDecision = cmd.Decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedDecision != ApprovalDecisions.Approved && normalizedDecision != ApprovalDecisions.Rejected)
        {
            return PoWorkflowOutcome.Fail(400, "invalid_decision", "Decision must be APPROVED or REJECTED.");
        }

        if (string.IsNullOrWhiteSpace(cmd.Reason) || cmd.Reason.Trim().Length > 500)
        {
            return PoWorkflowOutcome.Fail(400, "decision_reason_required", "Provide a written decision reason (1–500 characters).");
        }

        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        var rev = (await db.PurchaseOrderRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} AND revision_id = {cmd.RevisionId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (rev is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_revision_not_found", "Purchase order revision was not found.");
        }

        if (rev.Status != PurchaseOrderRevisionStatuses.PendingApproval || !rev.IsFrozen || rev.ApprovalRecordId is null)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "approval_already_decided",
                $"PO revision {rev.RevisionNumber} is not pending approval (current: '{rev.Status}').");
        }

        var approval = (await db.DocumentApprovals
            .FromSqlInterpolated(
                $"SELECT * FROM approval.document_approvals WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND approval_id = {rev.ApprovalRecordId.Value} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (approval is null || approval.Status != DocumentApprovalStatuses.Pending)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "approval_already_decided",
                "This purchase order approval request has already been decided.");
        }

        if (approval.RequestedByUserId == cmd.ActorUserId || rev.SubmittedByUserId == cmd.ActorUserId)
        {
            return PoWorkflowOutcome.Fail(
                403,
                "four_eyes_self_approval_forbidden",
                "Four-eyes policy (OD-02) forbids approving or deciding your own submitted purchase order revision.");
        }

        var policy = await db.ApprovalPolicies
            .AsNoTracking()
            .SingleAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PolicyId == approval.PolicyId, ct);

        var actorUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == cmd.ActorUserId, ct);
        if (policy.RequiresApproverMfa && !actorUser.TwoFactorEnabled)
        {
            return PoWorkflowOutcome.Fail(403, "mfa_required", "This approval policy requires TOTP MFA enrollment before deciding.");
        }

        var isApproved = normalizedDecision == ApprovalDecisions.Approved;
        if (isApproved)
        {
            if (rev.TotalOrderedBaseQuantity < po.AcceptedBaseQuantityTotal)
            {
                return PoWorkflowOutcome.Fail(
                    409,
                    "po_revision_below_accepted_quantity",
                    $"Amended total base quantity ({rev.TotalOrderedBaseQuantity:F4}) cannot be less than already accepted base quantity ({po.AcceptedBaseQuantityTotal:F4}).");
            }

            var revLines = await db.PurchaseOrderLines
                .AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
                .ToListAsync(ct);

            var eligibilityCheck = await RevalidateMasterEligibilityAndTaxAsync(
                db, cmd.TenantId, cmd.LegalEntityId, rev.SupplierPartnerId, rev.OrderDate, revLines, ct);
            if (eligibilityCheck is not null)
            {
                return eligibilityCheck;
            }
        }

        try
        {
            if (isApproved && po.CurrentApprovedRevisionId is { } prevRevId && prevRevId != rev.RevisionId)
            {
                var prevRev = (await db.PurchaseOrderRevisions
                    .FromSqlInterpolated(
                        $"SELECT * FROM procurement.purchase_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND revision_id = {prevRevId} FOR UPDATE")
                    .ToListAsync(ct))
                    .Single();

                if (prevRev.Status == PurchaseOrderRevisionStatuses.Approved)
                {
                    prevRev.MarkSuperseded(cmd.NowUtc);
                }
            }

            approval.RecordDecision(cmd.ActorUserId, normalizedDecision, cmd.Reason, actorUser.TwoFactorEnabled, cmd.NowUtc);
            rev.ApplyDecision(cmd.ActorUserId, isApproved, cmd.Reason, cmd.NowUtc);
            po.ApplyRevisionDecision(
                rev.RevisionId,
                rev.RevisionNumber,
                isApproved,
                rev.SupplierPartnerId,
                rev.OrderDate,
                rev.ExpectedDeliveryDate,
                rev.DeliveryDestination,
                rev.PaymentTermsDays,
                cmd.NowUtc);

            RecordPoAuditAndOutbox(
                db,
                po,
                rev,
                cmd.ActorUserId,
                isApproved ? "PO_REVISION_APPROVED" : "PO_REVISION_REJECTED",
                $"{normalizedDecision} {po.PoNumber} rev {rev.RevisionNumber}: {cmd.Reason.Trim()}",
                isApproved ? "procurement.po.approved.v1" : "procurement.po.rejected.v1",
                $"po:decision:{rev.RevisionId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return PoWorkflowOutcome.Success(po, rev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<PoWorkflowOutcome> CancelPurchaseOrderAsync(
        AtheraDbContext db,
        CloseOrCancelPoCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason) || cmd.Reason.Trim().Length > 300)
        {
            return PoWorkflowOutcome.Fail(400, "cancellation_reason_required", "Provide a written cancellation reason (1–300 characters).");
        }

        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        try
        {
            po.Cancel(cmd.ActorUserId, cmd.Reason, cmd.NowUtc);

            var openRevisions = await db.PurchaseOrderRevisions
                .Where(r =>
                    r.TenantId == cmd.TenantId &&
                    r.LegalEntityId == cmd.LegalEntityId &&
                    r.PurchaseOrderId == po.PurchaseOrderId &&
                    (r.Status == PurchaseOrderRevisionStatuses.Draft || r.Status == PurchaseOrderRevisionStatuses.PendingApproval))
                .ToListAsync(ct);

            foreach (var r in openRevisions)
            {
                r.MarkCancelled(cmd.NowUtc);
            }

            var pendingApprovals = await db.DocumentApprovals
                .Where(a =>
                    a.TenantId == cmd.TenantId &&
                    a.LegalEntityId == cmd.LegalEntityId &&
                    a.SourceModule == "PROCUREMENT" &&
                    a.SourceDocumentId == po.PurchaseOrderId &&
                    a.Status == DocumentApprovalStatuses.Pending)
                .ToListAsync(ct);

            foreach (var a in pendingApprovals)
            {
                a.Cancel(cmd.NowUtc);
            }

            var latestRev = await db.PurchaseOrderRevisions
                .AsNoTracking()
                .Where(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.PurchaseOrderId == po.PurchaseOrderId)
                .OrderByDescending(r => r.RevisionNumber)
                .FirstAsync(ct);

            RecordPoAuditAndOutbox(
                db,
                po,
                latestRev,
                cmd.ActorUserId,
                "PO_CANCELLED",
                $"Cancelled {(po.PoNumber ?? po.DraftCode)}: {po.CancellationReason}",
                "procurement.po.cancelled.v1",
                $"po:cancelled:{po.PurchaseOrderId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return PoWorkflowOutcome.Success(po, latestRev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<PoWorkflowOutcome> ShortClosePurchaseOrderAsync(
        AtheraDbContext db,
        CloseOrCancelPoCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason) || cmd.Reason.Trim().Length > 300)
        {
            return PoWorkflowOutcome.Fail(400, "closure_reason_required", "Provide a written closure reason (1–300 characters).");
        }

        var po = (await db.PurchaseOrders
            .FromSqlInterpolated(
                $"SELECT * FROM procurement.purchase_orders WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND purchase_order_id = {cmd.PurchaseOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .FirstOrDefault();

        if (po is null)
        {
            return PoWorkflowOutcome.Fail(404, "po_not_found", "Purchase order was not found in this legal entity.");
        }

        var hasOpenRev = await db.PurchaseOrderRevisions
            .AsNoTracking()
            .AnyAsync(r =>
                r.TenantId == cmd.TenantId &&
                r.LegalEntityId == cmd.LegalEntityId &&
                r.PurchaseOrderId == po.PurchaseOrderId &&
                (r.Status == PurchaseOrderRevisionStatuses.Draft || r.Status == PurchaseOrderRevisionStatuses.PendingApproval),
                ct);

        try
        {
            po.ShortClose(cmd.ActorUserId, cmd.Reason, hasOpenRev, cmd.NowUtc);

            var currentRev = await db.PurchaseOrderRevisions
                .AsNoTracking()
                .SingleAsync(r =>
                    r.TenantId == cmd.TenantId &&
                    r.LegalEntityId == cmd.LegalEntityId &&
                    r.RevisionId == po.CurrentApprovedRevisionId!.Value,
                    ct);

            RecordPoAuditAndOutbox(
                db,
                po,
                currentRev,
                cmd.ActorUserId,
                "PO_SHORT_CLOSED",
                $"Short-closed {po.PoNumber} rev {currentRev.RevisionNumber}: {po.ClosureReason}",
                "procurement.po.short_closed.v1",
                $"po:short-closed:{po.PurchaseOrderId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return PoWorkflowOutcome.Success(po, currentRev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    private const int StatusCodes_Created = 201;

    private sealed record ValidatedLinesResult(
        PoWorkflowOutcome? Failure,
        Partner? Supplier,
        DeliveryTolerancePolicy? TolerancePolicy,
        IReadOnlyList<PurchaseOrderLine> BuiltLines,
        decimal SubtotalAmount,
        decimal PpnTaxTotalAmount,
        decimal WithholdingTaxTotalAmount,
        decimal GrandTotalAmount,
        decimal TotalOrderedBaseQuantity);

    private static async Task<ValidatedLinesResult> ValidateAndBuildLinesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid purchaseOrderId,
        Guid revisionId,
        Guid supplierPartnerId,
        DateOnly orderDate,
        Guid? tolerancePolicyId,
        IReadOnlyList<PoLineInput> lineInputs,
        CancellationToken ct)
    {
        if (lineInputs is null || lineInputs.Count == 0)
        {
            return new ValidatedLinesResult(
                PoWorkflowOutcome.Fail(400, "po_lines_required", "At least one PO line item is required."),
                null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
        }

        // OD-01 fail-closed check before looking up items
        foreach (var li in lineInputs)
        {
            if (li.OverdeliveryTolerancePercent < 0m || li.OverdeliveryToleranceBaseQty < 0m)
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(400, "invalid_tolerance", "Delivery tolerance values cannot be negative."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            if (li.OverdeliveryTolerancePercent != 0m || li.OverdeliveryToleranceBaseQty != 0m || li.AllowUnlimitedOverdelivery)
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(
                        409,
                        "od01_overdelivery_tolerance_blocked",
                        "OD-01 requires Product Owner approval before configuring non-zero overdelivery tolerance or unlimited delivery flags (ATH-CR-003)."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }
        }

        var tolerancePolicy = tolerancePolicyId is { } tpid && tpid != Guid.Empty
            ? await db.DeliveryTolerancePolicies.AsNoTracking().SingleOrDefaultAsync(
                p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyId == tpid && p.Status == MasterStatuses.Active, ct)
            : await db.DeliveryTolerancePolicies.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.Status == MasterStatuses.Active)
                .OrderByDescending(p => p.IsDefault)
                .FirstOrDefaultAsync(ct);

        if (tolerancePolicy is null)
        {
            return new ValidatedLinesResult(
                PoWorkflowOutcome.Fail(404, "tolerance_policy_not_found", "Active delivery tolerance policy was not found for this legal entity."),
                null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
        }

        var supplier = await db.Partners.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == supplierPartnerId, ct);
        if (supplier is null)
        {
            return new ValidatedLinesResult(
                PoWorkflowOutcome.Fail(404, "supplier_not_found", "Selected supplier partner was not found in this tenant."),
                null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
        }

        var partnerProfile = await db.PartnerEntityProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PartnerId == supplierPartnerId, ct);

        var (supplierEligible, _) = PartnerEligibilityEvaluator.Evaluate(supplier, partnerProfile);
        if (!supplierEligible)
        {
            return new ValidatedLinesResult(
                PoWorkflowOutcome.Fail(
                    409,
                    "supplier_not_eligible_for_entity",
                    $"Partner '{supplier.PartnerCode}' is inactive or not eligible as a supplier in this legal entity."),
                null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
        }

        var itemIds = lineInputs.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        var itemPolicies = await db.ItemEntityPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && itemIds.Contains(p.ItemId))
            .ToDictionaryAsync(p => p.ItemId, ct);

        var uomIds = lineInputs.Select(l => l.OrderedUomId)
            .Concat(items.Values.Select(i => i.BaseUomId))
            .Distinct()
            .ToList();

        var uoms = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
            .ToDictionaryAsync(u => u.UomId, ct);

        var itemUoms = await db.ItemUomConversions.AsNoTracking()
            .Where(iu => iu.TenantId == tenantId && itemIds.Contains(iu.ItemId) && iu.Status == CatalogStatuses.Active)
            .ToListAsync(ct);

        var taxIds = lineInputs.Where(l => l.TaxCodeId.HasValue).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        var taxCodes = taxIds.Count == 0
            ? new Dictionary<Guid, TaxCode>()
            : await db.TaxCodes.AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && taxIds.Contains(t.TaxCodeId))
                .ToDictionaryAsync(t => t.TaxCodeId, ct);

        var hasSupplierNpwp = !string.IsNullOrWhiteSpace(supplier.TaxRegistrationNpwp);
        var usedLineageKeys = new HashSet<string>(StringComparer.Ordinal);
        var builtLines = new List<PurchaseOrderLine>(lineInputs.Count);
        decimal subtotal = 0m;
        decimal ppnTotal = 0m;
        decimal whtTotal = 0m;
        decimal totalBaseQty = 0m;

        for (var idx = 0; idx < lineInputs.Count; idx++)
        {
            var input = lineInputs[idx];
            if (input.UnitPrice <= 0m)
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(400, "invalid_unit_price", $"Line {idx + 1}: UnitPrice must be greater than zero."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            if (input.OrderedQuantity <= 0m)
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(400, "invalid_quantity", $"Line {idx + 1}: OrderedQuantity must be greater than zero."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            if (!items.TryGetValue(input.ItemId, out var item))
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(404, "item_not_found", $"Line {idx + 1}: Catalogue item was not found in this tenant."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            itemPolicies.TryGetValue(item.ItemId, out var itemPolicy);
            var (purchaseAllowed, _) = ItemEligibilityEvaluator.Evaluate(item, itemPolicy);
            if (!purchaseAllowed)
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(
                        409,
                        "item_not_eligible_for_entity_purchase",
                        $"Item '{item.ItemCode}' is inactive or restricted from purchasing in this legal entity."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            if (!uoms.TryGetValue(item.BaseUomId, out var baseUom) || !uoms.TryGetValue(input.OrderedUomId, out var orderedUom))
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(404, "uom_not_found", $"Line {idx + 1}: Unit of measure was not found in this tenant."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            var conversion = itemUoms.FirstOrDefault(iu => iu.ItemId == item.ItemId && iu.UomId == orderedUom.UomId);
            ItemTransactionSnapshot uomSnapshot;
            try
            {
                uomSnapshot = UomConverter.CaptureSnapshot(item, baseUom, orderedUom, conversion, input.OrderedQuantity, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new ValidatedLinesResult(MapDomainFailure(ex), null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            var lineSubtotal = decimal.Round(decimal.Round(input.OrderedQuantity, 6, MidpointRounding.AwayFromZero) * decimal.Round(input.UnitPrice, 4, MidpointRounding.AwayFromZero), 4, MidpointRounding.AwayFromZero);

            string? taxCodeSnap = null;
            string? taxFamilySnap = null;
            decimal? taxRateSnap = null;
            decimal dppAmount = lineSubtotal;
            decimal taxAmount = 0m;

            if (input.TaxCodeId is { } tcId)
            {
                if (!taxCodes.TryGetValue(tcId, out var taxCode))
                {
                    return new ValidatedLinesResult(
                        PoWorkflowOutcome.Fail(404, "tax_code_not_found", $"Line {idx + 1}: Selected tax code was not found in this legal entity."),
                        null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
                }

                TaxDeterminationEnvelope taxEnv;
                try
                {
                    taxEnv = TaxDeterminationEvaluator.Evaluate(
                        taxCode,
                        lineSubtotal,
                        orderDate,
                        hasSupplierNpwp,
                        evidenceProvided: true,
                        currencyScale: 0);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    return new ValidatedLinesResult(MapDomainFailure(ex), null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
                }

                if (taxEnv.IsBlocked)
                {
                    return new ValidatedLinesResult(
                        PoWorkflowOutcome.Fail(
                            409,
                            taxEnv.BlockedReason ?? "tax_rule_blocked",
                            $"Line {idx + 1}: Tax code '{taxCode.Code}' is blocked ({taxEnv.BlockedReason})."),
                        null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
                }

                taxCodeSnap = taxCode.Code;
                taxFamilySnap = taxCode.TaxFamily;
                taxRateSnap = taxEnv.EffectiveRatePercent;
                dppAmount = taxEnv.TaxableBaseAmount;
                taxAmount = taxEnv.TaxAmount;

                if (taxCode.TaxFamily == TaxFamilies.Ppn)
                {
                    ppnTotal += taxAmount;
                }
                else if (taxCode.TaxFamily == TaxFamilies.Pph)
                {
                    whtTotal += taxAmount;
                }
            }

            var lineageKey = string.IsNullOrWhiteSpace(input.LineageLineKey)
                ? $"L{idx + 1:D3}"
                : GuardValidation.RequireMasterCode(input.LineageLineKey, nameof(input.LineageLineKey), 2, 48);

            if (!usedLineageKeys.Add(lineageKey))
            {
                return new ValidatedLinesResult(
                    PoWorkflowOutcome.Fail(400, "duplicate_lineage_line_key", $"Duplicate lineage line key '{lineageKey}' within revision."),
                    null, null, Array.Empty<PurchaseOrderLine>(), 0m, 0m, 0m, 0m, 0m);
            }

            var poLine = PurchaseOrderLine.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                purchaseOrderId,
                revisionId,
                idx + 1,
                lineageKey,
                item.ItemId,
                item.ItemCode,
                item.Name,
                item.ItemKind,
                orderedUom.UomId,
                orderedUom.UomCode,
                baseUom.UomId,
                baseUom.UomCode,
                uomSnapshot.FactorToBase,
                input.OrderedQuantity,
                uomSnapshot.ConvertedBaseQuantity,
                input.UnitPrice,
                input.TaxCodeId,
                taxCodeSnap,
                taxFamilySnap,
                taxRateSnap,
                dppAmount,
                taxAmount,
                0m,
                0m,
                false,
                input.LineDescription);

            builtLines.Add(poLine);
            subtotal += poLine.LineSubtotalAmount;
            totalBaseQty += poLine.OrderedBaseQuantity;
        }

        return new ValidatedLinesResult(
            null,
            supplier,
            tolerancePolicy,
            builtLines,
            subtotal,
            ppnTotal,
            whtTotal,
            subtotal + ppnTotal,
            totalBaseQty);
    }

    private static async Task<PoWorkflowOutcome?> RevalidateMasterEligibilityAndTaxAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierPartnerId,
        DateOnly orderDate,
        IReadOnlyList<PurchaseOrderLine> lines,
        CancellationToken ct)
    {
        var supplier = await db.Partners.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == supplierPartnerId, ct);
        if (supplier is null)
        {
            return PoWorkflowOutcome.Fail(404, "supplier_not_found", "Supplier no longer exists in this tenant.");
        }

        var profile = await db.PartnerEntityProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PartnerId == supplierPartnerId, ct);

        if (!PartnerEligibilityEvaluator.Evaluate(supplier, profile).SupplierEligible)
        {
            return PoWorkflowOutcome.Fail(
                409,
                "supplier_not_eligible_for_entity",
                $"Partner '{supplier.PartnerCode}' is no longer eligible as a supplier in this legal entity.");
        }

        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        var policies = await db.ItemEntityPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && itemIds.Contains(p.ItemId))
            .ToDictionaryAsync(p => p.ItemId, ct);

        foreach (var line in lines)
        {
            if (!items.TryGetValue(line.ItemId, out var item))
            {
                return PoWorkflowOutcome.Fail(404, "item_not_found", $"Item '{line.ItemCodeSnapshot}' was not found.");
            }

            policies.TryGetValue(item.ItemId, out var policy);
            if (!ItemEligibilityEvaluator.Evaluate(item, policy).PurchaseAllowed)
            {
                return PoWorkflowOutcome.Fail(
                    409,
                    "item_not_eligible_for_entity_purchase",
                    $"Item '{item.ItemCode}' is no longer eligible for purchasing in this legal entity.");
            }

            if (line.TaxCodeId is { } tcId)
            {
                var tc = await db.TaxCodes.AsNoTracking()
                    .SingleOrDefaultAsync(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.TaxCodeId == tcId, ct);
                if (tc is null || !tc.IsEffectiveOn(orderDate))
                {
                    return PoWorkflowOutcome.Fail(
                        400,
                        "tax_code_not_effective_on_date",
                        $"Tax code '{line.TaxCodeSnapshot}' is not active on {orderDate:yyyy-MM-dd}.");
                }
            }
        }

        return null;
    }

    private static async Task<long> IncrementPoSequenceAsync(
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
            INSERT INTO procurement.po_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
            VALUES (@tid, @eid, @fy, 1)
            ON CONFLICT (tenant_id, legal_entity_id, fiscal_year)
            DO UPDATE SET last_number = procurement.po_sequences.last_number + 1
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

    private static void RecordPoAuditAndOutbox(
        AtheraDbContext db,
        PurchaseOrder po,
        PurchaseOrderRevision rev,
        Guid actorUserId,
        string eventType,
        string summary,
        string outboxTopic,
        string sourceKey,
        DateTimeOffset nowUtc)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            po.PurchaseOrderId,
            po.DraftCode,
            po.PoNumber,
            po.Status,
            rev.RevisionId,
            rev.RevisionNumber,
            RevisionStatus = rev.Status,
            rev.SubtotalAmount,
            rev.PpnTaxTotalAmount,
            rev.GrandTotalAmount,
            rev.TotalOrderedBaseQuantity,
            rev.SnapshotHash
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            po.TenantId,
            po.LegalEntityId,
            actorUserId,
            eventType,
            "PURCHASE_ORDER",
            po.PurchaseOrderId.ToString("D"),
            summary,
            payloadJson,
            nowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            po.TenantId,
            po.LegalEntityId,
            outboxTopic,
            sourceKey,
            payloadJson,
            nowUtc));
    }

    private static PoWorkflowOutcome MapDomainFailure(Exception ex)
    {
        var msg = ex.Message ?? "Validation failed.";
        var colon = msg.IndexOf(':');
        if (colon > 0 && colon < 64)
        {
            var code = msg[..colon].Trim();
            if (code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                var detail = msg[(colon + 1)..].Trim();
                var status = code switch
                {
                    "four_eyes_self_approval_forbidden" => 403,
                    "od01_overdelivery_tolerance_blocked" or
                    "po_has_downstream_references" or
                    "po_revision_frozen_immutable" or
                    "po_invalid_status_transition" or
                    "po_header_locked_by_approval" or
                    "approval_already_decided" => 409,
                    _ => 400
                };
                return PoWorkflowOutcome.Fail(status, code, detail);
            }
        }

        return PoWorkflowOutcome.Fail(400, "po_validation_failed", msg);
    }
}
