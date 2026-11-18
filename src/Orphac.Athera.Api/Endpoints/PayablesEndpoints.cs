using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

internal static class PayablesEndpoints
{
    private const string Od03DefaultNotice =
        "OD-03 safe defaults active (ATH-CR-006): automatic PP/DQ/BD tolerances are 0; DQ overbilling and unconfigured BD differences are blocked, while PP price variances require four-eyes approval and DEC-P2P-013 stock-coverage apportionment.";

    private const string Od08DefaultNotice =
        "OD-08 safe defaults active (ATH-CR-006): only PO-backed three-way matching is active; NON_PO_EXPENSE and SUPPLIER_ADVANCE bills are blocked pending Product Owner approval; tax metadata uses configurable OD-07 rules.";

    public static IEndpointRouteBuilder MapPayablesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/overview", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecutePayablesReadScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost],
                "Your active grants for this legal entity do not permit viewing supplier bills and payables.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, _, innerCt) =>
                {
                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    return Results.Ok(overview);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetPayablesOverview")
        .Produces<PayablesOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecutePayablesReadScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost],
                "Your active grants for this legal entity do not permit viewing supplier bill details.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, _, innerCt) =>
                {
                    var detail = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, false, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "bill_not_found", "Supplier bill was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetSupplierBillDetail")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/preview", async (
            Guid tenantId,
            Guid entityId,
            PreviewSupplierBillRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecutePayablesReadScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost],
                "Your active grants for this legal entity do not permit previewing supplier bill matches.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, _, innerCt) =>
                {
                    var preview = await SupplierBillCoordinator.PreviewBillAsync(
                        db,
                        new PreviewSupplierBillCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.BillType,
                            request.PurchaseOrderId,
                            request.CurrencyCode,
                            request.InvoiceDate,
                            request.DeclaredGrossAmount,
                            (request.Lines ?? Array.Empty<BillLineInputRequest>()).Select(ToLineInput).ToList(),
                            request.PostingDate),
                        innerCt);

                    if (!preview.Succeeded || preview.Computation is null)
                    {
                        return MapFailure(httpContext, preview.FailureHttpStatus, preview.ErrorCode ?? "preview_failed", preview.ErrorDetail ?? "Three-way match preview failed.");
                    }

                    return Results.Ok(ToPreviewDto(preview));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("PreviewSupplierBillMatch")
        .Produces<SupplierBillPreviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills", async (
            Guid tenantId,
            Guid entityId,
            CreateSupplierBillRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApManage],
                "Your active grants for this legal entity do not permit creating supplier bill drafts.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.CreateDraftAsync(
                        db,
                        new CreateSupplierBillCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.BillType,
                            request.PurchaseOrderId,
                            request.SupplierInvoiceNumber,
                            request.TaxInvoiceNumber,
                            request.WithholdingSlipReference,
                            request.InvoiceDate,
                            request.DueDate,
                            request.CurrencyCode,
                            request.DeclaredGrossAmount,
                            request.Notes,
                            (request.Lines ?? Array.Empty<BillLineInputRequest>()).Select(ToLineInput).ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "create_bill_failed", outcome.ErrorDetail ?? "Failed creating bill draft.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, outcome.Bill.SupplierBillId, false, innerCt);
                    return (Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{outcome.Bill.SupplierBillId}", dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateSupplierBillDraft")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            UpdateSupplierBillRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApManage],
                "Your active grants for this legal entity do not permit editing supplier bill drafts.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.UpdateDraftAsync(
                        db,
                        new UpdateSupplierBillCommand(
                            tenantId,
                            entityId,
                            billId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.SupplierInvoiceNumber,
                            request.TaxInvoiceNumber,
                            request.WithholdingSlipReference,
                            request.InvoiceDate,
                            request.DueDate,
                            request.DeclaredGrossAmount,
                            request.Notes,
                            (request.Lines ?? Array.Empty<BillLineInputRequest>()).Select(ToLineInput).ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "update_bill_failed", outcome.ErrorDetail ?? "Failed updating bill draft.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, false, innerCt);
                    return (Results.Ok(dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateSupplierBillDraft")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}/submit", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApManage],
                "Your active grants for this legal entity do not permit submitting supplier bills.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.SubmitBillAsync(
                        db,
                        new SubmitSupplierBillCommand(tenantId, entityId, billId, scope.Entity.FunctionalCurrencyCode, actorUserId, DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "submit_bill_failed", outcome.ErrorDetail ?? "Failed submitting bill.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, false, innerCt);
                    return (Results.Ok(dto), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("SubmitSupplierBill")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}/decision", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            DecideSupplierBillRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApApprove],
                "Your active grants for this legal entity do not permit approving or rejecting supplier bills.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.DecideBillAsync(
                        db,
                        new DecideSupplierBillCommand(
                            tenantId,
                            entityId,
                            billId,
                            request.Decision,
                            request.Reason,
                            request.ApprovePriceVariance,
                            request.ApplyPaymentBlock,
                            request.PaymentBlockReason,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "decide_bill_failed", outcome.ErrorDetail ?? "Failed deciding supplier bill.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, false, innerCt);
                    return (Results.Ok(dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("DecideSupplierBill")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}/post", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            PostSupplierBillRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApPost],
                "Your active grants for this legal entity do not permit posting supplier bills into AP & GL.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.PostBillAsync(
                        db,
                        new PostSupplierBillCommand(
                            tenantId,
                            entityId,
                            billId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.PostingDate,
                            request.IdempotencyKey,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "post_bill_failed", outcome.ErrorDetail ?? "Failed posting supplier bill.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, outcome.IdempotentReplay, innerCt);
                    return (outcome.IdempotentReplay ? Results.Ok(dto) : Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/payables/bills/{billId}", dto), !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("PostSupplierBill")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status201Created)
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/bills/{billId:guid}/payment-block", async (
            Guid tenantId,
            Guid entityId,
            Guid billId,
            UpdateBillPaymentBlockRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApApprove, PermissionCodes.ApPost],
                "Your active grants for this legal entity do not permit blocking or releasing AP payments.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SupplierBillCoordinator.UpdatePaymentBlockAsync(
                        db,
                        new UpdateBillPaymentBlockCommand(tenantId, entityId, billId, request.TargetBlockStatus, request.Reason, actorUserId, DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Bill is null)
                    {
                        throw new AbortedPayablesException(outcome.FailureHttpStatus, outcome.ErrorCode ?? "payment_block_failed", outcome.ErrorDetail ?? "Failed updating AP payment block.");
                    }

                    var dto = await LoadBillDetailAsync(db, tenantId, entityId, scope.Entity.FunctionalCurrencyCode, billId, false, innerCt);
                    return (Results.Ok(dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateSupplierBillPaymentBlock")
        .Produces<SupplierBillDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payables/match-policy", async (
            Guid tenantId,
            Guid entityId,
            UpdateInvoiceMatchPolicyRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingPayablesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ApApprove],
                "Your active grants for this legal entity do not permit updating invoice match tolerance policy.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, _, innerCt) =>
                {
                    try
                    {
                        InvoiceMatchPolicy.EnsurePublicSafeValues(
                            request.PpTolerancePercent,
                            request.PpToleranceAmount,
                            request.DqTolerancePercent,
                            request.DqToleranceBaseQty,
                            request.BdWriteOffLimitAmount,
                            request.BdWriteOffAccountId);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new AbortedPayablesException(StatusCodes.Status400BadRequest, "invalid_tolerance_limit", ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        var idx = ex.Message.IndexOf(':', StringComparison.Ordinal);
                        var code = idx > 0 ? ex.Message[..idx].Trim() : "od03_unreviewed_tolerance_policy_blocked";
                        var detail = idx > 0 ? ex.Message[(idx + 1)..].Trim() : ex.Message;
                        throw new AbortedPayablesException(StatusCodes.Status409Conflict, code, detail);
                    }

                    var policy = await SupplierBillCoordinator.EnsureDefaultMatchPolicyAsync(db, tenantId, entityId, DateTimeOffset.UtcNow, innerCt);
                    return (Results.Ok(ToPolicyDto(policy)), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateInvoiceMatchPolicy")
        .Produces<InvoiceMatchPolicyDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }

    private static BillLineDraftInput ToLineInput(BillLineInputRequest r) =>
        new(r.ReceiptAccrualLineId, r.BilledBaseQuantity, r.InvoiceUnitPriceBase, r.PpnTaxCodeId, r.WithholdingTaxCodeId, r.LineNotes);

    private static InvoiceMatchPolicyDto ToPolicyDto(InvoiceMatchPolicy p) =>
        new(p.PolicyId, p.Code, p.Name, p.ReviewStatus, p.PpTolerancePercent, p.PpToleranceAmount, p.DqTolerancePercent, p.DqToleranceBaseQty, p.BdWriteOffLimitAmount, p.BdWriteOffAccountId);

    private static SupplierBillPreviewResponse ToPreviewDto(BillPreviewOutcome p)
    {
        var c = p.Computation!;
        return new SupplierBillPreviewResponse(
            p.PurchaseOrderId,
            p.PoNumber,
            p.SupplierPartnerId,
            p.SupplierCode,
            p.SupplierLegalName,
            p.CurrencyCode,
            p.InvoiceDate,
            c.DqOutcome,
            c.PpOutcome,
            c.BdOutcome,
            c.OverallMatchStatus,
            c.IsPostingBlocked,
            c.RequiresApproval,
            c.LinesNetSubtotal,
            c.TotalGrniClearAmount,
            c.TotalPriceVarianceAmount,
            c.TotalInventoryAdjAmount,
            c.TotalPnlVarianceAmount,
            c.DeclaredGrossAmount,
            c.ComputedGrossTotal,
            c.BdWriteOffAmount,
            c.PpnTaxTotal,
            c.WithholdingTaxTotal,
            c.NetPayableAmount,
            c.Lines.Select(l => new MatchedLineComparisonDto(
                l.LineNumber,
                l.ReceiptAccrualLineId,
                l.PoLineId,
                l.ItemId,
                l.ItemCode,
                l.ItemName,
                l.BaseUomCode,
                l.OrderedBaseQty,
                l.RemainingUnbilledBaseQty,
                l.BilledBaseQuantity,
                l.PoUnitPriceFunctional,
                l.ReceiptUnitCostBase,
                l.InvoiceUnitPriceBase,
                l.LineNetAmount,
                l.GrniClearAmount,
                l.PriceVarianceAmount,
                l.CurrentPoolOnHandQty,
                l.CurrentPoolCarryingValue,
                l.CurrentPoolMovingAvgCost,
                l.CoveredBaseQuantity,
                l.UncoveredBaseQuantity,
                l.InventoryAdjustmentAmount,
                l.PnlVarianceAmount,
                l.ProjectedMovingAvgCost,
                l.PpnTaxCodeId,
                l.PpnTaxCodeSnapshot,
                l.PpnTaxAmount,
                l.WithholdingTaxCodeId,
                l.WithholdingTaxCodeSnapshot,
                l.WithholdingTaxAmount,
                l.DqOutcome,
                l.PpOutcome,
                l.BlockingReason)).ToList(),
            p.JournalPreview.Select(j => new BillJournalSuggestionDto(j.Side, j.AccountCode, j.AccountName, j.Amount, j.Description)).ToList(),
            p.BlockingReasons,
            Od03DefaultNotice);
    }

    private static async Task<PayablesOverviewResponse> BuildOverviewAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var billsRaw = await db.SupplierBills.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId)
            .OrderByDescending(b => b.InvoiceDate)
            .ThenByDescending(b => b.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var payablesRaw = await db.PayableItems.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
            .OrderByDescending(p => p.PostingDate)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var openAccrualLinesRaw = await db.ReceiptAccrualLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.RemainingUnbilledBaseQuantity > 0m)
            .OrderBy(l => l.ReceiptAccrualId)
            .ThenBy(l => l.LineNumber)
            .ToListAsync(ct);

        var accIds = openAccrualLinesRaw.Select(l => l.ReceiptAccrualId).Distinct().ToList();
        var accrualsMap = accIds.Count == 0
            ? new Dictionary<Guid, Modules.GeneralLedger.ReceiptAccrual>()
            : await db.ReceiptAccruals.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accIds.Contains(a.ReceiptAccrualId))
                .ToDictionaryAsync(a => a.ReceiptAccrualId, ct);

        var totalOpenGrniIdr = await db.ReceiptAccruals.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.RemainingOpenAmount > 0m)
            .SumAsync(a => (decimal?)a.RemainingOpenAmount, ct) ?? 0m;

        var poIds = accrualsMap.Values.Select(a => a.PurchaseOrderId).Distinct().ToList();
        var posMap = poIds.Count == 0
            ? new Dictionary<Guid, Modules.Procurement.PurchaseOrder>()
            : await db.PurchaseOrders.AsNoTracking()
                .Where(po => po.TenantId == tenantId && po.LegalEntityId == entityId && poIds.Contains(po.PurchaseOrderId))
                .ToDictionaryAsync(po => po.PurchaseOrderId, ct);

        var poLineIds = openAccrualLinesRaw.Select(l => l.PoLineId).Distinct().ToList();
        var poLinesMap = poLineIds.Count == 0
            ? new Dictionary<Guid, Modules.Procurement.PurchaseOrderLine>()
            : await db.PurchaseOrderLines.AsNoTracking()
                .Where(pl => pl.TenantId == tenantId && pl.LegalEntityId == entityId && poLineIds.Contains(pl.PoLineId))
                .ToDictionaryAsync(pl => pl.PoLineId, ct);

        var grIds = accrualsMap.Values.Select(a => a.GoodsReceiptId).Distinct().ToList();
        var grNums = grIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.GoodsReceipts.AsNoTracking()
                .Where(g => g.TenantId == tenantId && g.LegalEntityId == entityId && grIds.Contains(g.GoodsReceiptId))
                .ToDictionaryAsync(g => g.GoodsReceiptId, g => g.ReceiptNumber, ct);

        var partnerIds = accrualsMap.Values.Select(a => a.SupplierPartnerId).Distinct().ToList();
        var partnersMap = partnerIds.Count == 0
            ? new Dictionary<Guid, Modules.Partners.Partner>()
            : await db.Partners.AsNoTracking()
                .Where(p => p.TenantId == tenantId && partnerIds.Contains(p.PartnerId))
                .ToDictionaryAsync(p => p.PartnerId, ct);
        var partnerProfiles = partnerIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.PartnerEntityProfiles.AsNoTracking()
                .Where(pf => pf.TenantId == tenantId && pf.LegalEntityId == entityId && partnerIds.Contains(pf.PartnerId))
                .ToDictionaryAsync(pf => pf.PartnerId, pf => pf.DefaultPaymentTermsDays, ct);

        var itemIds = openAccrualLinesRaw.Select(l => l.ItemId).Distinct().ToList();
        var itemsMap = itemIds.Count == 0
            ? new Dictionary<Guid, Modules.Catalog.Item>()
            : await db.Items.AsNoTracking()
                .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
                .ToDictionaryAsync(i => i.ItemId, ct);
        var uomIds = itemsMap.Values.Select(i => i.BaseUomId).Distinct().ToList();
        var uomsMap = uomIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
                .ToDictionaryAsync(u => u.UomId, u => u.UomCode, ct);

        var poolsMap = itemIds.Count == 0
            ? new Dictionary<Guid, ValuationPool>()
            : await db.ValuationPools.AsNoTracking()
                .Where(vp => vp.TenantId == tenantId && vp.LegalEntityId == entityId && vp.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode && itemIds.Contains(vp.ItemId))
                .ToDictionaryAsync(vp => vp.ItemId, ct);

        var jrnIds = billsRaw.Where(b => b.PostedJournalId.HasValue).Select(b => b.PostedJournalId!.Value)
            .Concat(payablesRaw.Select(p => p.GlJournalId))
            .Distinct()
            .ToList();
        var jrnNums = jrnIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        var actorIds = billsRaw.SelectMany(b => new[] { b.SubmittedByUserId, b.ApprovedByUserId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var actorEmails = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var taxCodesRaw = await db.TaxCodes.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.Status == MasterStatuses.Active)
            .OrderBy(t => t.TaxFamily)
            .ThenBy(t => t.Code)
            .ToListAsync(ct);

        var activePolicy = await db.InvoiceMatchPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.Status == MasterStatuses.Active)
            .OrderByDescending(p => p.IsDefault)
            .FirstOrDefaultAsync(ct)
            ?? InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tenantId, entityId, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact Three-Way Match Only (OD-03 Safe Default)", true, DateTimeOffset.UtcNow);

        var recentAdjs = await LoadPriceAdjustmentsAsync(db, tenantId, entityId, null, ct);

        var billNumMap = billsRaw.ToDictionary(b => b.SupplierBillId, b => b.BillNumber ?? b.DraftCode);

        var billDtos = billsRaw.Select(b => new SupplierBillSummaryDto(
            b.SupplierBillId,
            b.DraftCode,
            b.BillNumber,
            b.BillType,
            b.PurchaseOrderId,
            b.PoNumberSnapshot,
            b.SupplierPartnerId,
            b.SupplierCodeSnapshot,
            b.SupplierLegalNameSnapshot,
            b.SupplierInvoiceNumber,
            b.TaxInvoiceNumber,
            b.InvoiceDate,
            b.DueDate,
            b.CurrencyCode,
            b.DeclaredGrossAmount,
            b.TotalGrniClearAmount,
            b.TotalPriceVarianceAmount,
            b.TotalInventoryAdjAmount,
            b.TotalPnlVarianceAmount,
            b.PpnTaxTotal,
            b.WithholdingTaxTotal,
            b.NetPayableAmount,
            b.Status,
            b.MatchStatus,
            b.PaymentBlockStatus,
            b.VarianceApproved,
            b.PostedJournalId,
            b.PostedJournalId is { } jid ? jrnNums.GetValueOrDefault(jid) : null,
            b.SubmittedByUserId is { } sid ? actorEmails.GetValueOrDefault(sid) : null,
            b.ApprovedByUserId is { } aid ? actorEmails.GetValueOrDefault(aid) : null,
            b.CreatedAtUtc,
            b.UpdatedAtUtc)).ToList();

        var payableDtos = payablesRaw.Select(p => new PayableItemDto(
            p.PayableItemId,
            p.PayableNumber,
            p.SupplierBillId,
            billNumMap.GetValueOrDefault(p.SupplierBillId, "-"),
            p.SupplierCodeSnapshot,
            p.SupplierLegalNameSnapshot,
            p.SupplierInvoiceNumber,
            p.GlJournalId,
            jrnNums.GetValueOrDefault(p.GlJournalId, "-"),
            p.PostingDate,
            p.DueDate,
            p.CurrencyCode,
            p.GrossBillAmount,
            p.WithholdingTaxAmount,
            p.NetPayableAmount,
            p.PaidAmount,
            p.RemainingOpenAmount,
            p.Status,
            p.PaymentBlockStatus,
            p.PaymentBlockReason)).ToList();

        var eligibleDtos = openAccrualLinesRaw.Select(l =>
        {
            var acc = accrualsMap[l.ReceiptAccrualId];
            var po = posMap[acc.PurchaseOrderId];
            var poLine = poLinesMap[l.PoLineId];
            var partner = partnersMap[acc.SupplierPartnerId];
            var item = itemsMap[l.ItemId];
            var pool = poolsMap.GetValueOrDefault(l.ItemId);
            var poUnitBase = poLine.OrderedBaseQuantity > 0m
                ? decimal.Round(poLine.LineSubtotalAmount / poLine.OrderedBaseQuantity, 6, MidpointRounding.AwayFromZero)
                : poLine.UnitPrice;

            return new EligibleAccrualLineDto(
                l.ReceiptAccrualLineId,
                acc.ReceiptAccrualId,
                acc.AccrualNumber,
                po.PurchaseOrderId,
                po.PoNumber ?? po.DraftCode,
                acc.ValuedAcceptanceId,
                partner.PartnerId,
                partner.PartnerCode,
                partner.LegalName,
                partner.TaxRegistrationNpwp,
                partnerProfiles.GetValueOrDefault(partner.PartnerId, po.PaymentTermsDays),
                acc.GoodsReceiptId,
                grNums.GetValueOrDefault(acc.GoodsReceiptId, "-"),
                acc.PostingDate,
                l.ValuedAcceptanceLineId,
                l.GoodsReceiptLineId,
                l.PoLineId,
                l.LineNumber,
                item.ItemId,
                item.ItemCode,
                item.Name,
                item.BaseUomId,
                uomsMap.GetValueOrDefault(item.BaseUomId, "PCS"),
                poLine.OrderedBaseQuantity,
                l.AcceptedBaseQuantity,
                l.BilledBaseQuantity,
                l.RemainingUnbilledBaseQuantity,
                l.UnitCostBase,
                poUnitBase,
                l.AccruedLineAmount,
                l.RemainingOpenLineAmount,
                pool?.ValuedBaseQuantity ?? 0m,
                pool?.CarryingValueAmount ?? 0m,
                pool?.MovingAverageUnitCost ?? 0m,
                poLine.TaxCodeId);
        }).ToList();

        var taxDtos = taxCodesRaw.Select(t => new PayableTaxCodeDto(
            t.TaxCodeId,
            t.Code,
            t.Name,
            t.TaxFamily,
            t.Direction,
            t.TreatmentClass,
            t.NominalRatePercent,
            t.TaxableBaseRatioNumerator,
            t.TaxableBaseRatioDenominator,
            t.RequiresCounterpartyNpwp,
            t.EffectiveFrom,
            t.EffectiveTo,
            t.ReviewStatus)).ToList();

        var openPayableTotal = decimal.Round(payablesRaw.Where(p => p.RemainingOpenAmount > 0m).Sum(p => p.RemainingOpenAmount), 4, MidpointRounding.AwayFromZero);
        var blockedCount = payablesRaw.Count(p => p.PaymentBlockStatus == PaymentBlockStatuses.PaymentBlocked);
        var pendingApprovalCount = billsRaw.Count(b => b.Status == SupplierBillStatuses.PendingApproval);

        return new PayablesOverviewResponse(
            billDtos,
            payableDtos,
            eligibleDtos,
            taxDtos,
            ToPolicyDto(activePolicy),
            recentAdjs,
            totalOpenGrniIdr,
            openPayableTotal,
            blockedCount,
            pendingApprovalCount,
            Od03DefaultNotice,
            Od08DefaultNotice);
    }

    private static async Task<SupplierBillDetailResponse?> LoadBillDetailAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        string currencyCode,
        Guid billId,
        bool wasIdempotentReplay,
        CancellationToken ct)
    {
        var bill = await db.SupplierBills.AsNoTracking()
            .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.SupplierBillId == billId, ct);
        if (bill is null) return null;

        var lines = await db.SupplierBillLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.SupplierBillId == billId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var evalsRaw = await db.InvoiceMatchEvaluations.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.LegalEntityId == entityId && e.SupplierBillId == billId)
            .OrderByDescending(e => e.EvaluatedAtUtc)
            .ToListAsync(ct);

        var matchesRaw = await db.SupplierBillReceiptMatches.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.SupplierBillId == billId)
            .ToListAsync(ct);

        var adjs = await LoadPriceAdjustmentsAsync(db, tenantId, entityId, billId, ct);

        var taxRaw = await db.TaxDeterminations.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.SourceDocumentId == billId)
            .OrderBy(t => t.TaxRole)
            .ToListAsync(ct);

        var payable = await db.PayableItems.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.SupplierBillId == billId, ct);

        var userIds = new[] { bill.SubmittedByUserId, bill.ApprovedByUserId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(evalsRaw.Select(e => e.EvaluatedByUserId))
            .Distinct()
            .ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var jrnIds = new List<Guid>();
        if (bill.PostedJournalId.HasValue) jrnIds.Add(bill.PostedJournalId.Value);
        if (payable is not null) jrnIds.Add(payable.GlJournalId);
        jrnIds = jrnIds.Distinct().ToList();
        var jrnNums = jrnIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        SupplierBillPreviewResponse? liveComparison = null;
        if (bill.Status != SupplierBillStatuses.Posted && bill.Status != SupplierBillStatuses.Cancelled)
        {
            var preview = await SupplierBillCoordinator.PreviewBillAsync(
                db,
                new PreviewSupplierBillCommand(
                    tenantId,
                    entityId,
                    currencyCode,
                    bill.BillType,
                    bill.PurchaseOrderId,
                    bill.CurrencyCode,
                    bill.InvoiceDate,
                    bill.DeclaredGrossAmount,
                    lines.Select(l => new BillLineDraftInput(l.ReceiptAccrualLineId, l.BilledBaseQuantity, l.InvoiceUnitPriceBase, l.PpnTaxCodeId, l.WithholdingTaxCodeId, l.LineNotes)).ToList()),
                ct);
            if (preview.Succeeded && preview.Computation is not null)
            {
                liveComparison = ToPreviewDto(preview);
            }
        }

        var summaryDto = new SupplierBillSummaryDto(
            bill.SupplierBillId,
            bill.DraftCode,
            bill.BillNumber,
            bill.BillType,
            bill.PurchaseOrderId,
            bill.PoNumberSnapshot,
            bill.SupplierPartnerId,
            bill.SupplierCodeSnapshot,
            bill.SupplierLegalNameSnapshot,
            bill.SupplierInvoiceNumber,
            bill.TaxInvoiceNumber,
            bill.InvoiceDate,
            bill.DueDate,
            bill.CurrencyCode,
            bill.DeclaredGrossAmount,
            bill.TotalGrniClearAmount,
            bill.TotalPriceVarianceAmount,
            bill.TotalInventoryAdjAmount,
            bill.TotalPnlVarianceAmount,
            bill.PpnTaxTotal,
            bill.WithholdingTaxTotal,
            bill.NetPayableAmount,
            bill.Status,
            bill.MatchStatus,
            bill.PaymentBlockStatus,
            bill.VarianceApproved,
            bill.PostedJournalId,
            bill.PostedJournalId is { } bjid ? jrnNums.GetValueOrDefault(bjid) : null,
            bill.SubmittedByUserId is { } sid ? emails.GetValueOrDefault(sid) : null,
            bill.ApprovedByUserId is { } aid ? emails.GetValueOrDefault(aid) : null,
            bill.CreatedAtUtc,
            bill.UpdatedAtUtc);

        var lineDtos = lines.Select(l => new SupplierBillLineDto(
            l.SupplierBillLineId,
            l.LineNumber,
            l.ReceiptAccrualLineId,
            l.ValuedAcceptanceLineId,
            l.GoodsReceiptLineId,
            l.PoLineId,
            l.ItemId,
            l.ItemCodeSnapshot,
            l.ItemNameSnapshot,
            l.BaseUomCodeSnapshot,
            l.OrderedBaseQtySnapshot,
            l.AcceptedUnbilledQtySnapshot,
            l.BilledBaseQuantity,
            l.PoUnitPriceFunctional,
            l.InvoiceUnitPriceBase,
            l.LineNetAmount,
            l.GrniClearAmount,
            l.PriceVarianceAmount,
            l.PpnTaxCodeId,
            l.PpnTaxCodeSnapshot,
            l.PpnTaxAmount,
            l.WithholdingTaxCodeId,
            l.WithholdingTaxCodeSnapshot,
            l.WithholdingTaxAmount,
            l.DqOutcome,
            l.PpOutcome,
            l.LineNotes)).ToList();

        var evalDtos = evalsRaw.Select(e =>
        {
            var reasons = JsonSerializer.Deserialize<List<string>>(e.BlockingReasonsJson) ?? [];
            return new InvoiceMatchEvaluationDto(
                e.EvaluationId,
                e.EvaluationStage,
                e.DqOutcome,
                e.PpOutcome,
                e.BdOutcome,
                e.OverallMatchStatus,
                e.IsPostingBlocked,
                e.RequiresApproval,
                e.RequiresPaymentBlock,
                reasons,
                emails.GetValueOrDefault(e.EvaluatedByUserId, e.EvaluatedByUserId.ToString("D")),
                e.EvaluatedAtUtc);
        }).ToList();

        var matchDtos = matchesRaw.Select(m => new SupplierBillReceiptMatchDto(
            m.MatchId,
            m.SupplierBillLineId,
            m.ReceiptAccrualId,
            m.ReceiptAccrualLineId,
            m.ItemId,
            m.MatchedBaseQuantity,
            m.ReceiptUnitCostBase,
            m.InvoiceUnitPriceBase,
            m.GrniClearedAmount,
            m.PriceVarianceAmount,
            m.CoveredBaseQuantity,
            m.InventoryAdjustmentAmount,
            m.PnlVarianceAmount)).ToList();

        var taxDtos = taxRaw.Select(t => new TaxDeterminationDto(
            t.DeterminationId,
            t.SourceDocumentLineId,
            t.TaxRole,
            t.TaxCodeSnapshot,
            t.TaxFamily,
            t.Direction,
            t.TreatmentClass,
            t.GrossAmount,
            t.TaxableBaseAmount,
            t.NominalRatePercent,
            t.EffectiveRatePercent,
            t.TaxAmount,
            t.ReviewStatus,
            t.EvidenceStatus,
            t.DocumentaryEvidenceRef,
            t.CounterpartyNpwpSnapshot,
            t.TransactionDate)).ToList();

        var payableDto = payable is null
            ? null
            : new PayableItemDto(
                payable.PayableItemId,
                payable.PayableNumber,
                payable.SupplierBillId,
                bill.BillNumber ?? bill.DraftCode,
                payable.SupplierCodeSnapshot,
                payable.SupplierLegalNameSnapshot,
                payable.SupplierInvoiceNumber,
                payable.GlJournalId,
                jrnNums.GetValueOrDefault(payable.GlJournalId, "-"),
                payable.PostingDate,
                payable.DueDate,
                payable.CurrencyCode,
                payable.GrossBillAmount,
                payable.WithholdingTaxAmount,
                payable.NetPayableAmount,
                payable.PaidAmount,
                payable.RemainingOpenAmount,
                payable.Status,
                payable.PaymentBlockStatus,
                payable.PaymentBlockReason);

        return new SupplierBillDetailResponse(
            summaryDto,
            lineDtos,
            evalDtos,
            matchDtos,
            adjs,
            taxDtos,
            payableDto,
            liveComparison,
            wasIdempotentReplay);
    }

    private static async Task<IReadOnlyList<InvoicePriceAdjustmentDto>> LoadPriceAdjustmentsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid? billId,
        CancellationToken ct)
    {
        var query = db.InvoicePriceAdjustments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId);
        if (billId.HasValue)
        {
            query = query.Where(a => a.SupplierBillId == billId.Value);
        }

        var raw = await query.OrderByDescending(a => a.PostingDate).ThenByDescending(a => a.CreatedAtUtc).Take(50).ToListAsync(ct);
        if (raw.Count == 0) return Array.Empty<InvoicePriceAdjustmentDto>();

        var itemIds = raw.Select(a => a.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, i => (i.ItemCode, i.Name), ct);
        var jrnIds = raw.Select(a => a.GlJournalId).Distinct().ToList();
        var jrns = await db.JournalEntries.AsNoTracking()
            .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
            .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        return raw.Select(a =>
        {
            var it = items.GetValueOrDefault(a.ItemId, ("ITEM", "Catalog Item"));
            return new InvoicePriceAdjustmentDto(
                a.AdjustmentId,
                a.SupplierBillId,
                a.SupplierBillLineId,
                a.BillNumber,
                a.ItemId,
                it.Item1,
                it.Item2,
                a.ValuationPoolId,
                a.GlJournalId,
                jrns.GetValueOrDefault(a.GlJournalId, "-"),
                a.PostingDate,
                a.BilledBaseQuantity,
                a.PoolOnHandBeforeQty,
                a.CoveredBaseQuantity,
                a.UncoveredBaseQuantity,
                a.TotalPriceDiffAmount,
                a.InventoryAdjustmentAmount,
                a.PnlVarianceAmount,
                a.PreviousMovingAverageCost,
                a.NewMovingAverageCost,
                a.CreatedAtUtc);
        }).ToList();
    }

    private static async Task<IResult> ExecutePayablesReadScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        string deniedDetail,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<IResult>> action,
        CancellationToken ct)
    {
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return EndpointSupport.Unauthenticated(httpContext);
        }

        var result = await scopeExecutor.ExecuteInEntityAsync(
            actorUserId,
            tenantId,
            entityId,
            null,
            async (scope, innerCt) =>
            {
                if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                {
                    return EndpointSupport.Forbidden(httpContext, "insufficient_entity_permission", deniedDetail);
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        return result.IsAllowed ? result.Value! : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static async Task<IResult> ExecuteMutatingPayablesScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        string deniedDetail,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<(IResult Result, bool NotifyOutbox)>> action,
        CancellationToken ct)
    {
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return EndpointSupport.Unauthenticated(httpContext);
        }

        try
        {
            var result = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                null,
                async (scope, innerCt) =>
                {
                    if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                    {
                        throw new AbortedPayablesException(StatusCodes.Status403Forbidden, "insufficient_entity_permission", deniedDetail);
                    }

                    return await action(scope, actorUserId, innerCt);
                },
                ct);

            if (!result.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            if (result.Value.NotifyOutbox)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return result.Value.Result;
        }
        catch (AbortedPayablesException abEx)
        {
            return MapFailure(httpContext, abEx.StatusCode, abEx.ErrorCode, abEx.ErrorDetail);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation))
        {
            if (ex is DbUpdateConcurrencyException)
            {
                return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Concurrent supplier bill or accrual update detected; reload and retry.");
            }

            var pgEx = (PostgresException)((DbUpdateException)ex).InnerException!;
            return pgEx.ConstraintName switch
            {
                "ux_supplier_bills_vendor_invoice" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_supplier_invoice_number",
                    "This supplier invoice number already exists for the selected supplier in this legal entity."),
                _ => EndpointSupport.Conflict(httpContext, "ap_persistence_conflict", "Supplier bill persistence conflict occurred.")
            };
        }
    }

    private static IResult MapFailure(HttpContext httpContext, int statusCode, string code, string detail) =>
        statusCode switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };

    private sealed class AbortedPayablesException(int statusCode, string errorCode, string errorDetail) : Exception(errorDetail)
    {
        public int StatusCode { get; } = statusCode;
        public string ErrorCode { get; } = errorCode;
        public string ErrorDetail { get; } = errorDetail;
    }
}
