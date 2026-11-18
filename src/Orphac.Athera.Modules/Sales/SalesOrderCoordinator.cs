using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Modules.Sales;

public sealed record SoLineInput(
    string? LineageLineKey,
    Guid ItemId,
    Guid OrderedUomId,
    decimal OrderedQuantity,
    decimal UnitPrice,
    decimal DiscountAmount = 0m,
    Guid? TaxCodeId = null,
    DateOnly? RequestedDeliveryDate = null,
    string? LineDescription = null);

public sealed record CreateSoDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    string? CurrencyCode,
    Guid CustomerPartnerId,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    bool CreditLimitOverrideRequested,
    string? Notes,
    IReadOnlyList<SoLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdateSoDraftCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    Guid RevisionId,
    Guid CustomerPartnerId,
    DateOnly OrderDate,
    DateOnly? RequestedDeliveryDate,
    string DeliveryAddressSnapshot,
    int PaymentTermsDays,
    string? CustomerReference,
    bool CreditLimitOverrideRequested,
    string? ChangeReason,
    string? Notes,
    IReadOnlyList<SoLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CreateSoAmendmentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    string ChangeReason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SubmitSoRevisionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    Guid RevisionId,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record DecideSoRevisionCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    Guid RevisionId,
    string Decision,
    string Reason,
    string? SoNumberOverride,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record CloseOrCancelSoCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SalesOrderId,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SalesWorkflowOutcome(
    bool Succeeded,
    int HttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    SalesOrder? SalesOrder,
    SalesOrderRevision? Revision)
{
    public static SalesWorkflowOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, statusCode, errorCode, detail, null, null);

    public static SalesWorkflowOutcome Success(SalesOrder so, SalesOrderRevision revision, int statusCode = 200) =>
        new(true, statusCode, null, null, so, revision);
}

public static class SalesOrderCoordinator
{
    public static async Task<SalesWorkflowOutcome> CreateDraftAsync(
        AtheraDbContext db,
        CreateSoDraftCommand cmd,
        CancellationToken ct)
    {
        if (cmd.CreditLimitOverrideRequested)
        {
            return SalesWorkflowOutcome.Fail(
                422,
                "od09_credit_limit_policy_unresolved",
                "OD-09 safe default forbids unapproved credit-limit overrides until Product Owner sign-off (ATH-CR-009).");
        }

        var soId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var draftCode = $"SO-DRF-{cmd.NowUtc:yyyyMMdd}-{soId.ToString("N")[..6].ToUpperInvariant()}";

        SalesOrder so;
        try
        {
            so = SalesOrder.CreateDraft(
                soId,
                cmd.TenantId,
                cmd.LegalEntityId,
                draftCode,
                cmd.CustomerPartnerId,
                cmd.CurrencyCode ?? cmd.ExpectedFunctionalCurrencyCode,
                cmd.ExpectedFunctionalCurrencyCode,
                cmd.OrderDate,
                cmd.RequestedDeliveryDate,
                cmd.DeliveryAddressSnapshot,
                cmd.PaymentTermsDays,
                cmd.CustomerReference,
                cmd.ActorUserId,
                cmd.NowUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }

        var validated = await ValidateAndBuildLinesAsync(
            db, cmd.TenantId, cmd.LegalEntityId, so.SalesOrderId, revId,
            cmd.CustomerPartnerId, cmd.OrderDate, cmd.Lines, ct);

        if (validated.Failure is not null)
        {
            return validated.Failure;
        }

        var rev = SalesOrderRevision.CreateDraft(
            revId,
            cmd.TenantId,
            cmd.LegalEntityId,
            so.SalesOrderId,
            revisionNumber: 1,
            supersedesRevisionId: null,
            validated.Customer!.PartnerId,
            validated.Customer.PartnerCode,
            validated.Customer.LegalName,
            validated.Customer.TaxRegistrationNpwp,
            validated.Customer.TaxPkpStatus,
            so.CurrencyCode,
            cmd.OrderDate,
            cmd.RequestedDeliveryDate,
            so.DeliveryAddressSnapshot,
            so.PaymentTermsDays,
            so.CustomerReference,
            changeReason: null,
            notes: cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        rev.ApplyTotalsAndHash(
            validated.Subtotal,
            validated.OutputTaxTotal,
            validated.GrandTotal,
            validated.TotalBaseQty,
            validated.Lines,
            cmd.NowUtc);

        db.SalesOrders.Add(so);
        db.SalesOrderRevisions.Add(rev);
        db.SalesOrderLines.AddRange(validated.Lines);
        RecordAuditAndOutbox(
            db, so, rev, cmd.ActorUserId,
            "SO_DRAFT_CREATED",
            $"Created SO draft {so.DraftCode} rev 1 ({rev.GrandTotalAmount:0.00} {so.CurrencyCode})",
            eventType: null,
            idempotencyKey: null,
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return SalesWorkflowOutcome.Success(so, rev, 201);
    }

    public static async Task<SalesWorkflowOutcome> UpdateDraftAsync(
        AtheraDbContext db,
        UpdateSoDraftCommand cmd,
        CancellationToken ct)
    {
        if (cmd.CreditLimitOverrideRequested)
        {
            return SalesWorkflowOutcome.Fail(
                422,
                "od09_credit_limit_policy_unresolved",
                "OD-09 safe default forbids unapproved credit-limit overrides until Product Owner sign-off (ATH-CR-009).");
        }

        var so = await LockSalesOrderAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, ct);
        if (so is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_not_found", "Sales order not found in active legal entity.");
        }

        var rev = await LockRevisionAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, cmd.RevisionId, ct);
        if (rev is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_revision_not_found", "Sales order revision not found.");
        }

        if (rev.IsFrozen || rev.Status != SalesOrderRevisionStatuses.Draft)
        {
            return SalesWorkflowOutcome.Fail(409, "so_revision_frozen_immutable", "Frozen or decided revisions cannot be modified.");
        }

        var validated = await ValidateAndBuildLinesAsync(
            db, cmd.TenantId, cmd.LegalEntityId, so.SalesOrderId, rev.RevisionId,
            cmd.CustomerPartnerId, cmd.OrderDate, cmd.Lines, ct);

        if (validated.Failure is not null)
        {
            return validated.Failure;
        }

        try
        {
            if (so.CurrentApprovedRevisionId is null)
            {
                so.UpdatePreApprovalHeader(
                    cmd.CustomerPartnerId,
                    cmd.OrderDate,
                    cmd.RequestedDeliveryDate,
                    cmd.DeliveryAddressSnapshot,
                    cmd.PaymentTermsDays,
                    cmd.CustomerReference,
                    cmd.NowUtc);
            }

            if (so.CurrentApprovedRevisionId is { } approvedRevId)
            {
                var priorLines = await db.SalesOrderLines.AsNoTracking()
                    .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == approvedRevId)
                    .ToListAsync(ct);

                var fulfillmentGuard = VerifyAmendmentDoesNotCutFulfilledAsync(so, validated.Subtotal, validated.TotalBaseQty, validated.Lines, priorLines);
                if (fulfillmentGuard is not null)
                {
                    return fulfillmentGuard;
                }

                CarryForwardCounters(validated.Lines, priorLines);
            }

            var existingLines = await db.SalesOrderLines
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
                .ToListAsync(ct);

            db.SalesOrderLines.RemoveRange(existingLines);
            await db.SaveChangesAsync(ct);

            rev.UpdateDraftDetailsAndTotals(
                validated.Customer!.PartnerId,
                validated.Customer.PartnerCode,
                validated.Customer.LegalName,
                validated.Customer.TaxRegistrationNpwp,
                validated.Customer.TaxPkpStatus,
                cmd.OrderDate,
                cmd.RequestedDeliveryDate,
                cmd.DeliveryAddressSnapshot,
                cmd.PaymentTermsDays,
                cmd.CustomerReference,
                cmd.ChangeReason,
                cmd.Notes,
                validated.Subtotal,
                validated.OutputTaxTotal,
                validated.GrandTotal,
                validated.TotalBaseQty,
                validated.Lines,
                cmd.NowUtc);

            db.SalesOrderLines.AddRange(validated.Lines);
            await db.SaveChangesAsync(ct);
            return SalesWorkflowOutcome.Success(so, rev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<SalesWorkflowOutcome> CreateAmendmentAsync(
        AtheraDbContext db,
        CreateSoAmendmentCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.ChangeReason))
        {
            return SalesWorkflowOutcome.Fail(400, "change_reason_required", "A change reason is required when revising a sales order.");
        }

        var so = await LockSalesOrderAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, ct);
        if (so is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_not_found", "Sales order not found in active legal entity.");
        }

        var openRevisionExists = await db.SalesOrderRevisions.AsNoTracking()
            .AnyAsync(
                r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.SalesOrderId == so.SalesOrderId
                     && (r.Status == SalesOrderRevisionStatuses.Draft || r.Status == SalesOrderRevisionStatuses.PendingApproval),
                ct);

        if (openRevisionExists)
        {
            return SalesWorkflowOutcome.Fail(
                409,
                "so_open_revision_exists",
                "Complete or cancel the existing open draft/pending revision before creating another amendment.");
        }

        var sourceRevisionId = so.CurrentApprovedRevisionId
            ?? await db.SalesOrderRevisions.AsNoTracking()
                .Where(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.SalesOrderId == so.SalesOrderId)
                .OrderByDescending(r => r.RevisionNumber)
                .Select(r => (Guid?)r.RevisionId)
                .FirstOrDefaultAsync(ct);

        if (sourceRevisionId is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_revision_not_found", "No baseline revision exists for this sales order.");
        }

        var baseline = await db.SalesOrderRevisions.AsNoTracking()
            .SingleAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.RevisionId == sourceRevisionId.Value, ct);

        var baselineLines = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == baseline.RevisionId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        int nextRevNumber;
        try
        {
            nextRevNumber = so.AllocateNextRevisionNumber(cmd.NowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return MapDomainFailure(ex);
        }

        var newRevId = Guid.NewGuid();
        var newRev = SalesOrderRevision.CreateDraft(
            newRevId,
            cmd.TenantId,
            cmd.LegalEntityId,
            so.SalesOrderId,
            nextRevNumber,
            baseline.Status == SalesOrderRevisionStatuses.Approved ? baseline.RevisionId : baseline.SupersedesRevisionId,
            baseline.CustomerPartnerId,
            baseline.CustomerCodeSnapshot,
            baseline.CustomerLegalNameSnapshot,
            baseline.CustomerNpwpSnapshot,
            baseline.CustomerPkpStatusSnapshot,
            baseline.CurrencyCode,
            baseline.OrderDate,
            baseline.RequestedDeliveryDate,
            baseline.DeliveryAddressSnapshot,
            baseline.PaymentTermsDays,
            baseline.CustomerReference,
            cmd.ChangeReason.Trim(),
            baseline.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        var copiedLines = baselineLines
            .Select(l =>
            {
                var copy = SalesOrderLine.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    so.SalesOrderId,
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
                    l.DiscountAmount,
                    l.TaxCodeId,
                    l.TaxCodeSnapshot,
                    l.TaxTreatmentClassSnapshot,
                    l.TaxRatePercentSnapshot,
                    l.TaxableBaseAmount,
                    l.TaxAmount,
                    l.RequestedDeliveryDate,
                    l.LineDescription);
                copy.CarryForwardFulfillmentCounters(l.ReservedBaseQuantity, l.DispatchedBaseQuantity, l.BilledBaseQuantity);
                return copy;
            })
            .ToList();

        newRev.ApplyTotalsAndHash(
            baseline.SubtotalAmount,
            baseline.OutputTaxTotalAmount,
            baseline.GrandTotalAmount,
            baseline.TotalOrderedBaseQuantity,
            copiedLines,
            cmd.NowUtc);

        db.SalesOrderRevisions.Add(newRev);
        db.SalesOrderLines.AddRange(copiedLines);
        RecordAuditAndOutbox(
            db, so, newRev, cmd.ActorUserId,
            "SO_REVISION_CREATED",
            $"Created amendment rev {newRev.RevisionNumber} for {so.SoNumber ?? so.DraftCode}: {cmd.ChangeReason.Trim()}",
            "sales.order.revised.v1",
            $"so:revised:{newRev.RevisionId:N}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return SalesWorkflowOutcome.Success(so, newRev, 201);
    }

    public static async Task<SalesWorkflowOutcome> SubmitRevisionAsync(
        AtheraDbContext db,
        SubmitSoRevisionCommand cmd,
        CancellationToken ct)
    {
        var so = await LockSalesOrderAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, ct);
        if (so is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_not_found", "Sales order not found in active legal entity.");
        }

        var rev = await LockRevisionAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, cmd.RevisionId, ct);
        if (rev is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_revision_not_found", "Sales order revision not found.");
        }

        if (rev.IsFrozen || rev.Status != SalesOrderRevisionStatuses.Draft)
        {
            return SalesWorkflowOutcome.Fail(409, "so_revision_not_draft", $"Revision {rev.RevisionNumber} is '{rev.Status}' and cannot be submitted.");
        }

        var revLines = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (revLines.Count == 0)
        {
            return SalesWorkflowOutcome.Fail(422, "so_empty_lines", "At least one commercial line is required before submitting.");
        }

        var eligibilityFailure = await RevalidateMasterEligibilityAndTaxAsync(
            db, cmd.TenantId, cmd.LegalEntityId, rev.CustomerPartnerId, rev.OrderDate, revLines, ct);
        if (eligibilityFailure is not null)
        {
            return eligibilityFailure;
        }

        if (so.CurrentApprovedRevisionId is { } approvedRevId)
        {
            var priorLines = await db.SalesOrderLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == approvedRevId)
                .ToListAsync(ct);

            var guard = VerifyAmendmentDoesNotCutFulfilledAsync(so, rev.SubtotalAmount, rev.TotalOrderedBaseQuantity, revLines, priorLines);
            if (guard is not null)
            {
                return guard;
            }
        }

        var policy = await EnsureSoApprovalPolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
        var docApproval = DocumentApproval.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            policy.PolicyId,
            "SALES",
            "SALES_ORDER_REVISION",
            so.SalesOrderId,
            rev.RevisionId,
            rev.RevisionNumber,
            $"Sales Order {so.DraftCode} Rev {rev.RevisionNumber} ({rev.CustomerCodeSnapshot}, {rev.GrandTotalAmount:0.00} {rev.CurrencyCode})",
            cmd.ActorUserId,
            cmd.NowUtc);

        var hash = SalesOrderHash.ComputeRevisionHash(rev, revLines);

        try
        {
            rev.FreezeAndMarkSubmitted(cmd.ActorUserId, docApproval.ApprovalId, hash, cmd.NowUtc);
            so.MarkSubmitted(cmd.NowUtc);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }

        db.DocumentApprovals.Add(docApproval);
        RecordAuditAndOutbox(
            db, so, rev, cmd.ActorUserId,
            "SO_REVISION_SUBMITTED",
            $"Submitted {so.SoNumber ?? so.DraftCode} rev {rev.RevisionNumber} for independent four-eyes approval",
            "sales.order.submitted.v1",
            $"so:submitted:{rev.RevisionId:N}",
            cmd.NowUtc);

        await db.SaveChangesAsync(ct);
        return SalesWorkflowOutcome.Success(so, rev);
    }

    public static async Task<SalesWorkflowOutcome> DecideRevisionAsync(
        AtheraDbContext db,
        DecideSoRevisionCommand cmd,
        CancellationToken ct)
    {
        string normalizedDecision;
        try
        {
            var raw = cmd.Decision?.Trim().ToUpperInvariant() ?? string.Empty;
            if (raw != ApprovalDecisions.Approved && raw != ApprovalDecisions.Rejected)
            {
                return SalesWorkflowOutcome.Fail(400, "invalid_decision", "Decision must be APPROVED or REJECTED.");
            }

            normalizedDecision = raw;
        }
        catch
        {
            return SalesWorkflowOutcome.Fail(400, "invalid_decision", "Decision must be APPROVED or REJECTED.");
        }

        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            return SalesWorkflowOutcome.Fail(400, "decision_reason_required", "A decision reason is required.");
        }

        var so = await LockSalesOrderAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, ct);
        if (so is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_not_found", "Sales order not found in active legal entity.");
        }

        var rev = await LockRevisionAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, cmd.RevisionId, ct);
        if (rev is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_revision_not_found", "Sales order revision not found.");
        }

        if (rev.Status != SalesOrderRevisionStatuses.PendingApproval || rev.ApprovalRecordId is not { } approvalId)
        {
            return SalesWorkflowOutcome.Fail(409, "so_revision_not_pending", $"Revision {rev.RevisionNumber} is '{rev.Status}' and has no active pending approval.");
        }

        var approval = (await db.DocumentApprovals
            .FromSqlInterpolated(
                $"SELECT * FROM approval.document_approvals WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND approval_id = {approvalId} FOR UPDATE")
            .ToListAsync(ct))
            .Single();

        if (approval.Status != DocumentApprovalStatuses.Pending)
        {
            return SalesWorkflowOutcome.Fail(409, "approval_already_decided", "This sales order approval request has already been decided.");
        }

        if (approval.RequestedByUserId == cmd.ActorUserId || rev.SubmittedByUserId == cmd.ActorUserId)
        {
            return SalesWorkflowOutcome.Fail(
                403,
                "four_eyes_self_approval_forbidden",
                "Four-eyes policy (OD-02) forbids approving or deciding your own submitted sales order revision.");
        }

        var policy = await db.ApprovalPolicies.AsNoTracking()
            .SingleAsync(p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.PolicyId == approval.PolicyId, ct);
        var actorUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == cmd.ActorUserId, ct);
        if (policy.RequiresApproverMfa && !actorUser.TwoFactorEnabled)
        {
            return SalesWorkflowOutcome.Fail(403, "mfa_required", "This approval policy requires TOTP MFA enrollment before deciding.");
        }

        var isApproved = normalizedDecision == ApprovalDecisions.Approved;
        string? assignedSoNumber = so.SoNumber;

        if (isApproved)
        {
            var revLines = await db.SalesOrderLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == rev.RevisionId)
                .ToListAsync(ct);

            var eligibilityCheck = await RevalidateMasterEligibilityAndTaxAsync(
                db, cmd.TenantId, cmd.LegalEntityId, rev.CustomerPartnerId, rev.OrderDate, revLines, ct);
            if (eligibilityCheck is not null)
            {
                return eligibilityCheck;
            }

            if (so.CurrentApprovedRevisionId is { } approvedRevId && approvedRevId != rev.RevisionId)
            {
                var priorLines = await db.SalesOrderLines.AsNoTracking()
                    .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.RevisionId == approvedRevId)
                    .ToListAsync(ct);

                var guard = VerifyAmendmentDoesNotCutFulfilledAsync(so, rev.SubtotalAmount, rev.TotalOrderedBaseQuantity, revLines, priorLines);
                if (guard is not null)
                {
                    return guard;
                }
            }

            if (string.IsNullOrWhiteSpace(assignedSoNumber))
            {
                if (!string.IsNullOrWhiteSpace(cmd.SoNumberOverride))
                {
                    assignedSoNumber = cmd.SoNumberOverride.Trim().ToUpperInvariant();
                }
                else
                {
                    var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                        db, "sales.sales_order_sequences", cmd.TenantId, cmd.LegalEntityId, rev.OrderDate.Year, ct);
                    assignedSoNumber = $"SO-{rev.OrderDate.Year:D4}-{seq:D5}";
                }
            }
        }

        try
        {
            if (isApproved && so.CurrentApprovedRevisionId is { } prevRevId && prevRevId != rev.RevisionId)
            {
                var prevRev = (await db.SalesOrderRevisions
                    .FromSqlInterpolated(
                        $"SELECT * FROM sales.sales_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND revision_id = {prevRevId} FOR UPDATE")
                    .ToListAsync(ct))
                    .Single();

                if (prevRev.Status == SalesOrderRevisionStatuses.Approved)
                {
                    prevRev.MarkSuperseded(cmd.NowUtc);
                }
            }

            approval.RecordDecision(cmd.ActorUserId, normalizedDecision, cmd.Reason, actorUser.TwoFactorEnabled, cmd.NowUtc);
            rev.ApplyDecision(cmd.ActorUserId, isApproved, cmd.Reason, cmd.NowUtc);
            so.ApplyRevisionDecision(
                rev.RevisionId,
                rev.RevisionNumber,
                isApproved,
                assignedSoNumber,
                rev.CustomerPartnerId,
                rev.OrderDate,
                rev.RequestedDeliveryDate,
                rev.DeliveryAddressSnapshot,
                rev.PaymentTermsDays,
                rev.CustomerReference,
                cmd.NowUtc);

            RecordAuditAndOutbox(
                db, so, rev, cmd.ActorUserId,
                isApproved ? "SO_REVISION_APPROVED" : "SO_REVISION_REJECTED",
                $"{normalizedDecision} {so.SoNumber ?? so.DraftCode} rev {rev.RevisionNumber}: {cmd.Reason.Trim()}",
                isApproved ? "sales.order.revision.approved.v1" : "sales.order.rejected.v1",
                $"so:decision:{rev.RevisionId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return SalesWorkflowOutcome.Success(so, rev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    public static async Task<SalesWorkflowOutcome> CloseOrCancelAsync(
        AtheraDbContext db,
        CloseOrCancelSoCommand cmd,
        bool isClose,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            return SalesWorkflowOutcome.Fail(400, "reason_required", "A reason is required.");
        }

        var so = await LockSalesOrderAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SalesOrderId, ct);
        if (so is null)
        {
            return SalesWorkflowOutcome.Fail(404, "so_not_found", "Sales order not found in active legal entity.");
        }

        var revisions = await db.SalesOrderRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_order_revisions WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND sales_order_id = {cmd.SalesOrderId} ORDER BY revision_number FOR UPDATE")
            .ToListAsync(ct);

        var latestRev = revisions.Last();

        try
        {
            if (isClose)
            {
                var hasOpenRev = revisions.Any(r =>
                    r.Status is SalesOrderRevisionStatuses.Draft or SalesOrderRevisionStatuses.PendingApproval);
                so.Close(cmd.ActorUserId, cmd.Reason, hasOpenRev, cmd.NowUtc);
            }
            else
            {
                so.Cancel(cmd.ActorUserId, cmd.Reason, cmd.NowUtc);
                foreach (var openRev in revisions.Where(r => r.Status is SalesOrderRevisionStatuses.Draft or SalesOrderRevisionStatuses.PendingApproval))
                {
                    if (openRev.ApprovalRecordId is { } pendingAppId)
                    {
                        var app = (await db.DocumentApprovals
                            .FromSqlInterpolated(
                                $"SELECT * FROM approval.document_approvals WHERE tenant_id = {cmd.TenantId} AND legal_entity_id = {cmd.LegalEntityId} AND approval_id = {pendingAppId} FOR UPDATE")
                            .ToListAsync(ct))
                            .SingleOrDefault();
                        if (app is not null && app.Status == DocumentApprovalStatuses.Pending)
                        {
                            app.Cancel(cmd.NowUtc);
                        }
                    }

                    openRev.Cancel(cmd.NowUtc);
                }
            }

            RecordAuditAndOutbox(
                db, so, latestRev, cmd.ActorUserId,
                isClose ? "SO_SHORT_CLOSED" : "SO_CANCELLED",
                $"{(isClose ? "Closed" : "Cancelled")} {so.SoNumber ?? so.DraftCode}: {cmd.Reason.Trim()}",
                isClose ? "sales.order.closed.v1" : "sales.order.cancelled.v1",
                $"so:{(isClose ? "close" : "cancel")}:{so.SalesOrderId:N}",
                cmd.NowUtc);

            await db.SaveChangesAsync(ct);
            return SalesWorkflowOutcome.Success(so, latestRev);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MapDomainFailure(ex);
        }
    }

    private sealed record ValidatedSoLines(
        SalesWorkflowOutcome? Failure,
        Partner? Customer,
        IReadOnlyList<SalesOrderLine> Lines,
        decimal Subtotal,
        decimal OutputTaxTotal,
        decimal GrandTotal,
        decimal TotalBaseQty);

    private static async Task<ValidatedSoLines> ValidateAndBuildLinesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid salesOrderId,
        Guid revisionId,
        Guid customerPartnerId,
        DateOnly orderDate,
        IReadOnlyList<SoLineInput> inputs,
        CancellationToken ct)
    {
        if (inputs is null || inputs.Count == 0)
        {
            return new ValidatedSoLines(
                SalesWorkflowOutcome.Fail(422, "so_empty_lines", "At least one commercial line is required."),
                null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
        }

        var customer = await db.Partners.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == customerPartnerId, ct);
        if (customer is null)
        {
            return new ValidatedSoLines(
                SalesWorkflowOutcome.Fail(404, "customer_not_found", "Customer partner does not exist in this tenant."),
                null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
        }

        var profile = await db.PartnerEntityProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PartnerId == customerPartnerId, ct);

        if (!PartnerEligibilityEvaluator.Evaluate(customer, profile).CustomerEligible)
        {
            return new ValidatedSoLines(
                SalesWorkflowOutcome.Fail(409, "customer_not_eligible", $"Partner '{customer.PartnerCode}' is inactive or not eligible as a Customer for this legal entity."),
                null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
        }

        var itemIds = inputs.Select(x => x.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        var policies = await db.ItemEntityPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && itemIds.Contains(p.ItemId))
            .ToDictionaryAsync(p => p.ItemId, ct);

        var conversions = await db.ItemUomConversions.AsNoTracking()
            .Where(c => c.TenantId == tenantId && itemIds.Contains(c.ItemId))
            .ToListAsync(ct);

        var uomIds = inputs.Select(x => x.OrderedUomId).Concat(items.Values.Select(i => i.BaseUomId)).Distinct().ToList();
        var uoms = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
            .ToDictionaryAsync(u => u.UomId, ct);

        var taxCodeIds = inputs.Where(x => x.TaxCodeId.HasValue).Select(x => x.TaxCodeId!.Value).Distinct().ToList();
        var taxCodes = taxCodeIds.Count == 0
            ? new Dictionary<Guid, TaxCode>()
            : await db.TaxCodes.AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && taxCodeIds.Contains(t.TaxCodeId))
                .ToDictionaryAsync(t => t.TaxCodeId, ct);

        var lines = new List<SalesOrderLine>(inputs.Count);
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        decimal subtotal = 0m;
        decimal outputTaxTotal = 0m;
        decimal totalBaseQty = 0m;
        var hasCustomerNpwp = !string.IsNullOrWhiteSpace(customer.TaxRegistrationNpwp);

        for (var idx = 0; idx < inputs.Count; idx++)
        {
            var input = inputs[idx];
            if (input.DiscountAmount != 0m)
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(422, "od09_sales_discount_policy_unresolved", $"Line {idx + 1}: OD-09 safe default forbids unapproved sales discounts until Owner sign-off (ATH-CR-009)."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            if (input.OrderedQuantity <= 0m || input.UnitPrice <= 0m)
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(422, "invalid_quantity_or_price", $"Line {idx + 1}: Ordered quantity and unit price must be strictly positive."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            if (input.RequestedDeliveryDate is { } lineReq && lineReq < orderDate)
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(422, "invalid_delivery_date", $"Line {idx + 1}: Requested delivery date cannot precede OrderDate."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            if (!items.TryGetValue(input.ItemId, out var item))
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(404, "item_not_found", $"Line {idx + 1}: Catalogue item was not found in this tenant."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            policies.TryGetValue(item.ItemId, out var itemPolicy);
            if (!ItemEligibilityEvaluator.Evaluate(item, itemPolicy).SalesAllowed)
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(409, "item_not_sales_eligible", $"Line {idx + 1}: Item '{item.ItemCode}' is inactive or restricted from sales in this legal entity."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            if (!uoms.TryGetValue(item.BaseUomId, out var baseUom) || !uoms.TryGetValue(input.OrderedUomId, out var orderedUom) || orderedUom.Status != CatalogStatuses.Active)
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(409, "uom_not_active", $"Line {idx + 1}: Selected unit of measure is missing or inactive."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            var conversion = conversions.FirstOrDefault(c => c.ItemId == item.ItemId && c.UomId == orderedUom.UomId);
            ItemTransactionSnapshot uomSnapshot;
            try
            {
                uomSnapshot = UomConverter.CaptureSnapshot(item, baseUom, orderedUom, conversion, input.OrderedQuantity, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new ValidatedSoLines(MapDomainFailure(ex), null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            var lineSubtotal = decimal.Round(
                decimal.Round(input.OrderedQuantity, 6, MidpointRounding.AwayFromZero) * decimal.Round(input.UnitPrice, 4, MidpointRounding.AwayFromZero),
                4,
                MidpointRounding.AwayFromZero);

            string? taxCodeSnap = null;
            string? taxTreatmentSnap = null;
            decimal? taxRateSnap = null;
            decimal taxableBase = lineSubtotal;
            decimal taxAmount = 0m;

            if (input.TaxCodeId is { } tcId)
            {
                if (!taxCodes.TryGetValue(tcId, out var taxCode))
                {
                    return new ValidatedSoLines(
                        SalesWorkflowOutcome.Fail(404, "tax_code_not_found", $"Line {idx + 1}: Selected tax code was not found in this legal entity."),
                        null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
                }

                if (taxCode.Direction != TaxDirections.Output)
                {
                    return new ValidatedSoLines(
                        SalesWorkflowOutcome.Fail(409, "tax_code_direction_not_output", $"Line {idx + 1}: Tax code '{taxCode.Code}' has direction '{taxCode.Direction}'; Sales Orders require OUTPUT tax codes."),
                        null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
                }

                TaxDeterminationEnvelope taxEnv;
                try
                {
                    taxEnv = TaxDeterminationEvaluator.Evaluate(
                        taxCode,
                        lineSubtotal,
                        orderDate,
                        hasCustomerNpwp,
                        evidenceProvided: true,
                        currencyScale: 0);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    return new ValidatedSoLines(MapDomainFailure(ex), null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
                }

                if (taxEnv.IsBlocked)
                {
                    return new ValidatedSoLines(
                        SalesWorkflowOutcome.Fail(409, taxEnv.BlockedReason ?? "tax_rule_blocked", $"Line {idx + 1}: Tax code '{taxCode.Code}' is blocked ({taxEnv.BlockedReason})."),
                        null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
                }

                taxCodeSnap = taxCode.Code;
                taxTreatmentSnap = taxCode.TreatmentClass;
                taxRateSnap = taxEnv.EffectiveRatePercent;
                taxableBase = taxEnv.TaxableBaseAmount;
                taxAmount = taxEnv.TaxAmount;
                outputTaxTotal += taxAmount;
            }

            var lineageKey = string.IsNullOrWhiteSpace(input.LineageLineKey)
                ? $"SO-L{idx + 1:D3}"
                : GuardValidation.RequireMasterCode(input.LineageLineKey, nameof(input.LineageLineKey), 2, 48);

            if (!usedKeys.Add(lineageKey))
            {
                return new ValidatedSoLines(
                    SalesWorkflowOutcome.Fail(400, "duplicate_lineage_line_key", $"Duplicate lineage line key '{lineageKey}' within revision."),
                    null, Array.Empty<SalesOrderLine>(), 0m, 0m, 0m, 0m);
            }

            var soLine = SalesOrderLine.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                salesOrderId,
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
                input.DiscountAmount,
                input.TaxCodeId,
                taxCodeSnap,
                taxTreatmentSnap,
                taxRateSnap,
                taxableBase,
                taxAmount,
                input.RequestedDeliveryDate,
                input.LineDescription);

            lines.Add(soLine);
            subtotal += soLine.LineSubtotalAmount;
            totalBaseQty += soLine.OrderedBaseQuantity;
        }

        return new ValidatedSoLines(null, customer, lines, subtotal, outputTaxTotal, subtotal + outputTaxTotal, totalBaseQty);
    }

    private static async Task<SalesWorkflowOutcome?> RevalidateMasterEligibilityAndTaxAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid customerPartnerId,
        DateOnly orderDate,
        IReadOnlyList<SalesOrderLine> lines,
        CancellationToken ct)
    {
        var customer = await db.Partners.AsNoTracking().SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == customerPartnerId, ct);
        if (customer is null)
        {
            return SalesWorkflowOutcome.Fail(404, "customer_not_found", "Customer partner does not exist in this tenant.");
        }

        var profile = await db.PartnerEntityProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PartnerId == customerPartnerId, ct);
        if (!PartnerEligibilityEvaluator.Evaluate(customer, profile).CustomerEligible)
        {
            return SalesWorkflowOutcome.Fail(409, "customer_not_eligible", $"Customer '{customer.PartnerCode}' is no longer eligible in this legal entity.");
        }

        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking().Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId)).ToDictionaryAsync(i => i.ItemId, ct);
        var policies = await db.ItemEntityPolicies.AsNoTracking().Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && itemIds.Contains(p.ItemId)).ToDictionaryAsync(p => p.ItemId, ct);

        foreach (var line in lines)
        {
            if (line.DiscountAmount != 0m)
            {
                return SalesWorkflowOutcome.Fail(422, "od09_sales_discount_policy_unresolved", "OD-09 forbids non-zero sales discounts until Owner sign-off.");
            }

            if (!items.TryGetValue(line.ItemId, out var item))
            {
                return SalesWorkflowOutcome.Fail(404, "item_not_found", $"Item '{line.ItemCodeSnapshot}' not found.");
            }

            policies.TryGetValue(item.ItemId, out var pol);
            if (!ItemEligibilityEvaluator.Evaluate(item, pol).SalesAllowed)
            {
                return SalesWorkflowOutcome.Fail(409, "item_not_sales_eligible", $"Item '{item.ItemCode}' is no longer sales-eligible in this legal entity.");
            }

            if (line.TaxCodeId is { } tcId)
            {
                var tc = await db.TaxCodes.AsNoTracking()
                    .SingleOrDefaultAsync(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.TaxCodeId == tcId, ct);
                if (tc is null || tc.Direction != TaxDirections.Output || !tc.IsEffectiveOn(orderDate))
                {
                    return SalesWorkflowOutcome.Fail(409, "tax_code_not_effective_on_date", $"Tax code on line {line.LineNumber} is no longer effective or valid for sales.");
                }
                if (tc.RequiresCounterpartyNpwp && string.IsNullOrWhiteSpace(customer.TaxRegistrationNpwp))
                {
                    return SalesWorkflowOutcome.Fail(409, "counterparty_npwp_required_for_rate", $"Tax code '{tc.Code}' on line {line.LineNumber} requires customer NPWP.");
                }
            }
        }

        return null;
    }

    private static SalesWorkflowOutcome? VerifyAmendmentDoesNotCutFulfilledAsync(
        SalesOrder so,
        decimal _,
        decimal newTotalBaseQty,
        IReadOnlyList<SalesOrderLine> newLines,
        IReadOnlyList<SalesOrderLine> approvedLines)
    {
        var minTotalRequired = Math.Max(so.TotalReservedBaseQuantity, Math.Max(so.TotalDispatchedBaseQuantity, so.TotalBilledBaseQuantity));
        if (newTotalBaseQty < minTotalRequired)
        {
            return SalesWorkflowOutcome.Fail(
                409,
                "so_has_downstream_fulfillment",
                $"Amended total base quantity ({newTotalBaseQty:F4}) cannot be reduced below already fulfilled quantity ({minTotalRequired:F4}).");
        }

        var byKey = newLines.ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);
        foreach (var prev in approvedLines)
        {
            var fulfilledFloor = Math.Max(prev.ReservedBaseQuantity + prev.DispatchedBaseQuantity, prev.BilledBaseQuantity);
            if (fulfilledFloor <= 0m) continue;

            if (!byKey.TryGetValue(prev.LineageLineKey, out var nextLine) || nextLine.OrderedBaseQuantity < fulfilledFloor)
            {
                return SalesWorkflowOutcome.Fail(
                    409,
                    "so_has_downstream_fulfillment",
                    $"Line '{prev.LineageLineKey}' cannot be removed or reduced below fulfilled base quantity ({fulfilledFloor:F4}) under OD-09 safe defaults.");
            }
        }

        return null;
    }

    private static void CarryForwardCounters(IReadOnlyList<SalesOrderLine> targetLines, IReadOnlyList<SalesOrderLine> approvedLines)
    {
        var prevByKey = approvedLines.ToDictionary(l => l.LineageLineKey, StringComparer.Ordinal);
        foreach (var line in targetLines)
        {
            if (prevByKey.TryGetValue(line.LineageLineKey, out var prev))
            {
                line.CarryForwardFulfillmentCounters(prev.ReservedBaseQuantity, prev.DispatchedBaseQuantity, prev.BilledBaseQuantity);
            }
        }
    }

    private static async Task<SalesOrder?> LockSalesOrderAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid salesOrderId,
        CancellationToken ct) =>
        (await db.SalesOrders
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_orders WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId} AND sales_order_id = {salesOrderId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

    private static async Task<SalesOrderRevision?> LockRevisionAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid salesOrderId,
        Guid revisionId,
        CancellationToken ct) =>
        (await db.SalesOrderRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM sales.sales_order_revisions WHERE tenant_id = {tenantId} AND legal_entity_id = {entityId} AND sales_order_id = {salesOrderId} AND revision_id = {revisionId} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();

    private static async Task<ApprovalPolicy> EnsureSoApprovalPolicyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var existing = await db.ApprovalPolicies.SingleOrDefaultAsync(
            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyCode == SalesPolicyDefaults.SalesOrderApprovalPolicyCode,
            ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = ApprovalPolicy.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            SalesPolicyDefaults.SalesOrderApprovalPolicyCode,
            "Sales Order Commercial Commitment Four-Eyes Approval",
            requiresFourEyes: true,
            requiresApproverMfa: false,
            nowUtc);
        db.ApprovalPolicies.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private static void RecordAuditAndOutbox(
        AtheraDbContext db,
        SalesOrder so,
        SalesOrderRevision rev,
        Guid actorUserId,
        string auditEventType,
        string summary,
        string? eventType,
        string? idempotencyKey,
        DateTimeOffset nowUtc)
    {
        var payload = JsonSerializer.Serialize(new
        {
            so.SalesOrderId,
            so.SoNumber,
            so.DraftCode,
            rev.RevisionId,
            rev.RevisionNumber,
            so.CustomerPartnerId,
            rev.CustomerCodeSnapshot,
            rev.GrandTotalAmount,
            rev.TotalOrderedBaseQuantity,
            rev.SnapshotHash,
            ActorUserId = actorUserId,
            OccurredAtUtc = nowUtc
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            so.TenantId,
            so.LegalEntityId,
            actorUserId,
            auditEventType,
            "SALES_ORDER",
            so.SalesOrderId.ToString("D"),
            summary,
            payload,
            nowUtc));

        if (!string.IsNullOrWhiteSpace(eventType) && !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                so.TenantId,
                so.LegalEntityId,
                eventType,
                idempotencyKey,
                payload,
                nowUtc));
        }
    }

    public static SalesWorkflowOutcome MapDomainFailure(Exception ex)
    {
        var message = ex.Message ?? "sales_domain_error";
        var colon = message.IndexOf(':');
        if (colon > 0 && colon < 64)
        {
            var code = message[..colon].Trim();
            var detail = message[(colon + 1)..].Trim();
            var status = code switch
            {
                "four_eyes_self_approval_forbidden" => 403,
                "od09_sales_discount_policy_unresolved" or "od09_credit_limit_policy_unresolved" or "invalid_quantity_or_price" => 422,
                _ => 409
            };
            return SalesWorkflowOutcome.Fail(status, code, string.IsNullOrEmpty(detail) ? message : detail);
        }

        return SalesWorkflowOutcome.Fail(400, "sales_validation_error", message);
    }
}
