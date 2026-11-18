using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.Api.Endpoints;

internal static class ReturnsEndpoints
{
    private const string Od05DefaultNotice =
        "OD-05 safe defaults active (ATH-CR-007): M1 unvalued dock rejections (replacement/close), M2 valued unbilled returns (OPEN period, GRNI reversal), and M3 unpaid billed credit notes are supported. Billed replacements (M4) and paid-bill auto-credits (M5) fail closed pending Product Owner policy sign-off.";

    private const string Od07DefaultNotice =
        "OD-07 safe defaults active (ATH-CR-007): M3 PPN input tax reversal strictly requires an explicit Tax Return Note (Nota Retur) reference; billed lines with PPh 23 withholding (M6) fail closed pending Bukti Potong amendment policy.";

    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.ProcurementRead,
        PermissionCodes.ProcurementManage,
        PermissionCodes.ProcurementApprove,
        PermissionCodes.ReceivingRead,
        PermissionCodes.ReceivingManage,
        PermissionCodes.ReceivingAccept,
        PermissionCodes.InventoryRead,
        PermissionCodes.InventoryManage,
        PermissionCodes.QualityInspect,
        PermissionCodes.ApRead,
        PermissionCodes.ApManage,
        PermissionCodes.ApApprove,
        PermissionCodes.ApPost
    ];

    public static IEndpointRouteBuilder MapReturnsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/overview", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReadScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                "Your active grants for this legal entity do not permit viewing supplier returns and credit notes.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) => Results.Ok(await BuildOverviewAsync(db, tenantId, entityId, innerCt)),
                ct);
        })
        .RequireAuthorization()
        .WithName("GetReturnsOverview")
        .Produces<ReturnsOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/{returnId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid returnId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReadScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                "Your active grants for this legal entity do not permit viewing supplier return details.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var detail = await LoadReturnDetailAsync(db, tenantId, entityId, returnId, includeLivePreview: true, wasIdempotentReplay: false, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "return_not_found", "Supplier return was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetSupplierReturnDetail")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/preview", async (
            Guid tenantId,
            Guid entityId,
            PreviewSupplierReturnRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReadScopeAsync(
                tenantId,
                entityId,
                [
                    PermissionCodes.ReceivingManage,
                    PermissionCodes.ProcurementManage,
                    PermissionCodes.InventoryManage,
                    PermissionCodes.ApManage
                ],
                "Your active grants for this legal entity do not permit previewing supplier return matrix outcomes.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    if (!CanAuthorCase(scope.EffectivePermissions, request.ReturnCaseType))
                    {
                        return EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Your active grants do not permit authoring '{request.ReturnCaseType}' returns.");
                    }

                    var outcome = await SupplierReturnCoordinator.PreviewReturnAsync(
                        db,
                        new PreviewSupplierReturnCommand(
                            tenantId,
                            entityId,
                            request.ReturnCaseType,
                            request.ResolutionMode,
                            request.PurchaseOrderId,
                            request.GoodsReceiptId,
                            request.ReceiptAccrualId,
                            request.SupplierBillId,
                            request.ReturnDate,
                            request.TaxReturnNoteNumber,
                            MapLines(request.Lines),
                            request.ExcludeSupplierReturnId),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Matrix is null)
                    {
                        return MapFailure(httpContext, outcome);
                    }

                    return Results.Ok(MapMatrix(outcome.Matrix));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("PreviewSupplierReturn")
        .Produces<SupplierReturnPreviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns", async (
            Guid tenantId,
            Guid entityId,
            CreateSupplierReturnRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    if (!CanAuthorCase(scope.EffectivePermissions, request.ReturnCaseType))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Your active grants for this legal entity do not permit drafting '{request.ReturnCaseType}' returns."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.CreateDraftAsync(
                        db,
                        new CreateSupplierReturnDraftCommand(
                            tenantId,
                            entityId,
                            request.ReturnCaseType,
                            request.ResolutionMode,
                            request.PurchaseOrderId,
                            request.GoodsReceiptId,
                            request.ReceiptAccrualId,
                            request.SupplierBillId,
                            request.ReturnDate,
                            request.ReasonCode,
                            request.CarrierOrDispatchRef,
                            request.TaxReturnNoteNumber,
                            request.Notes,
                            MapLines(request.Lines),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.ReturnHeader is null)
                    {
                        throw new AbortedReturnException(outcome);
                    }

                    var detail = await LoadReturnDetailAsync(
                        db, tenantId, entityId, outcome.ReturnHeader.SupplierReturnId, includeLivePreview: true, wasIdempotentReplay: false, innerCt);
                    return (Results.Created(
                        $"/api/workspaces/{tenantId}/entities/{entityId}/returns/{outcome.ReturnHeader.SupplierReturnId}",
                        detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateSupplierReturnDraft")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/{returnId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid returnId,
            UpdateSupplierReturnRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var existing = await db.SupplierReturns.AsNoTracking()
                        .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.SupplierReturnId == returnId, innerCt);
                    if (existing is null)
                    {
                        return (EndpointSupport.NotFound(httpContext, "return_not_found", "Supplier return was not found in this legal entity."), false);
                    }

                    if (!CanAuthorCase(scope.EffectivePermissions, existing.ReturnCaseType))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Your active grants for this legal entity do not permit editing '{existing.ReturnCaseType}' returns."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.UpdateDraftAsync(
                        db,
                        new UpdateSupplierReturnDraftCommand(
                            tenantId,
                            entityId,
                            returnId,
                            request.ResolutionMode,
                            request.ReturnDate,
                            request.ReasonCode,
                            request.CarrierOrDispatchRef,
                            request.TaxReturnNoteNumber,
                            request.Notes,
                            MapLines(request.Lines),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.ReturnHeader is null)
                    {
                        throw new AbortedReturnException(outcome);
                    }

                    var detail = await LoadReturnDetailAsync(
                        db, tenantId, entityId, returnId, includeLivePreview: true, wasIdempotentReplay: false, innerCt);
                    return (Results.Ok(detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateSupplierReturnDraft")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/{returnId:guid}/submit", async (
            Guid tenantId,
            Guid entityId,
            Guid returnId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var existing = await db.SupplierReturns.AsNoTracking()
                        .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.SupplierReturnId == returnId, innerCt);
                    if (existing is null)
                    {
                        return (EndpointSupport.NotFound(httpContext, "return_not_found", "Supplier return was not found in this legal entity."), false);
                    }

                    if (!CanAuthorCase(scope.EffectivePermissions, existing.ReturnCaseType))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Your active grants for this legal entity do not permit submitting '{existing.ReturnCaseType}' returns."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.SubmitAsync(
                        db,
                        new SubmitSupplierReturnCommand(tenantId, entityId, returnId, actorUserId, DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.ReturnHeader is null)
                    {
                        throw new AbortedReturnException(outcome);
                    }

                    var detail = await LoadReturnDetailAsync(
                        db, tenantId, entityId, returnId, includeLivePreview: true, wasIdempotentReplay: false, innerCt);
                    return (Results.Ok(detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("SubmitSupplierReturn")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/{returnId:guid}/decision", async (
            Guid tenantId,
            Guid entityId,
            Guid returnId,
            DecideSupplierReturnRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var existing = await db.SupplierReturns.AsNoTracking()
                        .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.SupplierReturnId == returnId, innerCt);
                    if (existing is null)
                    {
                        return (EndpointSupport.NotFound(httpContext, "return_not_found", "Supplier return was not found in this legal entity."), false);
                    }

                    if (!CanApproveCase(scope.EffectivePermissions, existing.ReturnCaseType))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Four-eyes approval of '{existing.ReturnCaseType}' requires approval authority in this legal entity."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.DecideAsync(
                        db,
                        new DecideSupplierReturnCommand(tenantId, entityId, returnId, request.Decision, request.Reason, actorUserId, DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.ReturnHeader is null)
                    {
                        throw new AbortedReturnException(outcome);
                    }

                    var detail = await LoadReturnDetailAsync(
                        db, tenantId, entityId, returnId, includeLivePreview: true, wasIdempotentReplay: false, innerCt);
                    return (Results.Ok(detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("DecideSupplierReturn")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/{returnId:guid}/execute", async (
            Guid tenantId,
            Guid entityId,
            Guid returnId,
            ExecuteSupplierReturnRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var existing = await db.SupplierReturns.AsNoTracking()
                        .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.SupplierReturnId == returnId, innerCt);
                    if (existing is null)
                    {
                        return (EndpointSupport.NotFound(httpContext, "return_not_found", "Supplier return was not found in this legal entity."), false);
                    }

                    if (!CanExecuteCase(scope.EffectivePermissions, existing.ReturnCaseType))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            $"Executing '{existing.ReturnCaseType}' returns requires execution authority for this legal entity."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.ExecuteAsync(
                        db,
                        new ExecuteSupplierReturnCommand(
                            tenantId,
                            entityId,
                            returnId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.PostingDate,
                            request.IdempotencyKey,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.ReturnHeader is null)
                    {
                        throw new AbortedReturnException(outcome);
                    }

                    var detail = await LoadReturnDetailAsync(
                        db, tenantId, entityId, returnId, includeLivePreview: false, wasIdempotentReplay: outcome.IdempotentReplay, innerCt);
                    return (Results.Ok(detail), !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ExecuteSupplierReturn")
        .Produces<SupplierReturnDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/returns/replacements/{authorizationId:guid}/receive", async (
            Guid tenantId,
            Guid entityId,
            Guid authorizationId,
            ReceiveReplacementRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationScopeAsync(
                tenantId,
                entityId,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, PermissionCodes.ReceivingManage))
                    {
                        return (EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            "Recording linked replacement arrivals requires receiving.manage permission."), false);
                    }

                    var outcome = await SupplierReturnCoordinator.ReceiveReplacementAsync(
                        db,
                        new ReceiveReplacementCommand(
                            tenantId,
                            entityId,
                            authorizationId,
                            request.ReceivedUomId,
                            request.PhysicallyDeliveredQuantity,
                            request.ReceivedDate,
                            request.SupplierDeliveryNoteNumber,
                            request.IdempotencyKey,
                            request.ReceivingStation,
                            request.VehicleOrSealRef,
                            request.DocumentEvidenceRef,
                            request.Notes,
                            request.FlagForInspection,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Receipt is null)
                    {
                        throw new AbortedReceivingWorkflowException(outcome);
                    }

                    var dto = await ReceivingEndpoints.LoadGoodsReceiptDetailDtoAsync(
                        db, tenantId, entityId, outcome.Receipt.GoodsReceiptId, outcome.WasIdempotentReplay, innerCt);

                    var ok = outcome.HttpStatus == StatusCodes.Status201Created
                        ? Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{outcome.Receipt.GoodsReceiptId}",
                            dto)
                        : Results.Ok(dto);
                    return (ok, !outcome.WasIdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ReceiveLinkedReplacement")
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status201Created)
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }

    private static bool CanAuthorCase(IReadOnlyList<string> permissions, string returnCaseType)
    {
        var normalized = returnCaseType?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized == SupplierReturnCaseTypes.UnvaluedDockRejection
            ? EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ReceivingManage, PermissionCodes.ProcurementManage)
            : EndpointSupport.HasAnyPermission(permissions, PermissionCodes.InventoryManage, PermissionCodes.ApManage);
    }

    private static bool CanApproveCase(IReadOnlyList<string> permissions, string returnCaseType)
    {
        if (!EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ApprovalDecide))
        {
            return false;
        }

        return returnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn
            ? EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ApApprove)
            : EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ProcurementApprove);
    }

    private static bool CanExecuteCase(IReadOnlyList<string> permissions, string returnCaseType) =>
        returnCaseType switch
        {
            SupplierReturnCaseTypes.UnvaluedDockRejection =>
                EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ReceivingManage),
            SupplierReturnCaseTypes.ValuedUnbilledReturn =>
                EndpointSupport.HasAnyPermission(permissions, PermissionCodes.InventoryManage, PermissionCodes.ReceivingAccept),
            SupplierReturnCaseTypes.ValuedBilledReturn =>
                EndpointSupport.HasAnyPermission(permissions, PermissionCodes.ApPost),
            _ => false
        };

    private static IReadOnlyList<ReturnLineDraftInput> MapLines(IReadOnlyList<ReturnLineInputRequest> lines) =>
        lines.Select(l => new ReturnLineDraftInput(
            l.GoodsReceiptLineId,
            l.ValuedAcceptanceLineId,
            l.ReceiptAccrualLineId,
            l.SupplierBillLineId,
            l.ReturnedBaseQuantity,
            l.SourceLocationId,
            l.SourceStockLotId,
            l.SourceStockStatus,
            l.LineNotes)).ToList();

    private static SupplierReturnPreviewResponse MapMatrix(SupplierReturnMatrixResult m) =>
        new(
            m.MatrixCode,
            m.Disposition,
            m.BlockingCode,
            m.BlockingReason,
            m.RequiresPeriodOpen,
            m.IsPeriodOpen,
            m.RequiresNotaRetur,
            m.TotalReturnedBaseQuantity,
            m.TotalInventoryCreditAmount,
            m.TotalGrniReversalAmount,
            m.TotalCreditSubtotalAmount,
            m.TotalPpnReversalAmount,
            m.TotalCreditNoteAmount,
            m.TotalPpvDifferenceAmount,
            m.Lines.Select(l => new SupplierReturnEvaluatedLineDto(
                l.GoodsReceiptLineId,
                l.ValuedAcceptanceLineId,
                l.ReceiptAccrualLineId,
                l.SupplierBillLineId,
                l.PoLineId,
                l.LineageLineKey,
                l.ItemId,
                l.ItemCode,
                l.ItemName,
                l.BaseUomId,
                l.BaseUomCode,
                l.SourceLocationId,
                l.SourceStockLotId,
                l.SourceStockStatus,
                l.ReturnedBaseQuantity,
                l.AuthorizedUnitCostBase,
                l.InventoryCreditAmount,
                l.GrniReversalAmount,
                l.CreditSubtotalAmount,
                l.PpnReversalAmount,
                l.TotalCreditAmount,
                l.PpvDifferenceAmount,
                l.CreatesReplacement,
                l.BlockingReason)).ToList(),
            m.JournalSuggestions.Select(j => new SupplierReturnJournalSuggestionDto(
                j.Side, j.AccountCode, j.AccountName, j.Amount, j.Description)).ToList(),
            Od05DefaultNotice,
            Od07DefaultNotice);

    private static async Task<IResult> ExecuteReadScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        string forbiddenDetail,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, CancellationToken, Task<IResult>> action,
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
                    return EndpointSupport.Forbidden(httpContext, "insufficient_entity_permission", forbiddenDetail);
                }

                return await action(scope, innerCt);
            },
            ct);

        return result.IsAllowed
            ? result.Value!
            : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static async Task<IResult> ExecuteMutationScopeAsync(
        Guid tenantId,
        Guid entityId,
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
                async (scope, innerCt) => await action(scope, actorUserId, innerCt),
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
        catch (AbortedReturnException aborted)
        {
            return MapFailure(httpContext, aborted.Outcome);
        }
        catch (AbortedReceivingWorkflowException abortedRcv)
        {
            return abortedRcv.Outcome.HttpStatus switch
            {
                StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, abortedRcv.Outcome.ErrorCode ?? "forbidden", abortedRcv.Outcome.ErrorDetail ?? "Forbidden."),
                StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, abortedRcv.Outcome.ErrorCode ?? "not_found", abortedRcv.Outcome.ErrorDetail ?? "Not found."),
                StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, abortedRcv.Outcome.ErrorCode ?? "conflict", abortedRcv.Outcome.ErrorDetail ?? "Conflict."),
                _ => EndpointSupport.BadRequest(httpContext, abortedRcv.Outcome.ErrorCode ?? "bad_request", abortedRcv.Outcome.ErrorDetail ?? "Validation error.")
            };
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || (ex is DbUpdateException dbe && dbe.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation))
        {
            if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx)
            {
                return pgEx.ConstraintName switch
                {
                    "ux_goods_receipts_supplier_delivery_note" => EndpointSupport.Conflict(httpContext, "duplicate_supplier_delivery_note", "This supplier delivery note number has already been used for this supplier."),
                    "ux_goods_receipts_idempotency" => EndpointSupport.Conflict(httpContext, "duplicate_receipt_idempotency_conflict", "Receipt idempotency key already committed with a different payload."),
                    "ux_supplier_returns_execute_idempotency" => EndpointSupport.Conflict(httpContext, "return_already_executed", "Return idempotency key conflict."),
                    _ => EndpointSupport.Conflict(httpContext, "return_conflict", "Concurrent or duplicate return conflict occurred.")
                };
            }

            return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Record was modified concurrently; reload and retry.");
        }
    }

    private sealed class AbortedReturnException(ReturnCommandOutcome outcome) : Exception
    {
        public ReturnCommandOutcome Outcome { get; } = outcome;
    }

    private sealed class AbortedReceivingWorkflowException(ReceivingWorkflowOutcome outcome) : Exception
    {
        public ReceivingWorkflowOutcome Outcome { get; } = outcome;
    }

    private static IResult MapFailure(HttpContext httpContext, ReturnCommandOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "return_workflow_error";
        var detail = outcome.ErrorDetail ?? "Supplier return operation failed.";
        return outcome.FailureHttpStatus switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static async Task<ReturnsOverviewResponse> BuildOverviewAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var returnsRaw = await db.SupplierReturns.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var replacementsRaw = await db.ReplacementAuthorizations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var creditsRaw = await db.SupplierCredits.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.LegalEntityId == entityId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        // 1. Queued M1 unvalued dock rejections
        var dockRejectedGrLines = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.DockRejectedBaseQuantity > 0m)
            .ToListAsync(ct);

        var claimedM1Map = await (
            from rl in db.SupplierReturnLines.AsNoTracking()
            join r in db.SupplierReturns.AsNoTracking()
                on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
            where rl.TenantId == tenantId &&
                  rl.LegalEntityId == entityId &&
                  r.ReturnCaseType == SupplierReturnCaseTypes.UnvaluedDockRejection &&
                  (r.Status == SupplierReturnStatuses.Executed || r.Status == SupplierReturnStatuses.Approved || r.Status == SupplierReturnStatuses.PendingApproval)
            group rl by rl.GoodsReceiptLineId into g
            select new { Id = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Qty, ct);

        // 2. Queued M2 valued unbilled accrual lines
        var openAccrualLines = await db.ReceiptAccrualLines.AsNoTracking()
            .Where(al => al.TenantId == tenantId && al.LegalEntityId == entityId && al.RemainingUnbilledBaseQuantity > 0m)
            .ToListAsync(ct);

        var inFlightM2Map = await (
            from rl in db.SupplierReturnLines.AsNoTracking()
            join r in db.SupplierReturns.AsNoTracking()
                on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
            where rl.TenantId == tenantId &&
                  rl.LegalEntityId == entityId &&
                  r.ReturnCaseType == SupplierReturnCaseTypes.ValuedUnbilledReturn &&
                  (r.Status == SupplierReturnStatuses.Approved || r.Status == SupplierReturnStatuses.PendingApproval) &&
                  rl.ReceiptAccrualLineId != null
            group rl by rl.ReceiptAccrualLineId!.Value into g
            select new { Id = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Qty, ct);

        // 3. Queued M3/M5/M6 valued billed lines
        var postedBills = await db.SupplierBills.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.Status == SupplierBillStatuses.Posted)
            .ToListAsync(ct);
        var postedBillIds = postedBills.Select(b => b.SupplierBillId).ToList();
        var billedLinesRaw = postedBillIds.Count == 0
            ? []
            : await db.SupplierBillLines.AsNoTracking()
                .Where(bl => bl.TenantId == tenantId && bl.LegalEntityId == entityId && postedBillIds.Contains(bl.SupplierBillId))
                .ToListAsync(ct);

        var billLineIds = billedLinesRaw.Select(l => l.SupplierBillLineId).ToList();
        var creditedMap = billLineIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.SupplierCreditLines.AsNoTracking()
                .Where(cl => cl.TenantId == tenantId && cl.LegalEntityId == entityId && billLineIds.Contains(cl.SupplierBillLineId))
                .GroupBy(cl => cl.SupplierBillLineId)
                .Select(g => new { Id = g.Key, Qty = g.Sum(x => x.CreditedBaseQuantity) })
                .ToDictionaryAsync(x => x.Id, x => x.Qty, ct);

        var inFlightM3Map = billLineIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await (
                from rl in db.SupplierReturnLines.AsNoTracking()
                join r in db.SupplierReturns.AsNoTracking()
                    on new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnId }
                    equals new { r.TenantId, r.LegalEntityId, r.SupplierReturnId }
                where rl.TenantId == tenantId &&
                      rl.LegalEntityId == entityId &&
                      r.ReturnCaseType == SupplierReturnCaseTypes.ValuedBilledReturn &&
                      (r.Status == SupplierReturnStatuses.Approved || r.Status == SupplierReturnStatuses.PendingApproval) &&
                      rl.SupplierBillLineId != null &&
                      billLineIds.Contains(rl.SupplierBillLineId.Value)
                group rl by rl.SupplierBillLineId!.Value into g
                select new { Id = g.Key, Qty = g.Sum(x => x.ReturnedBaseQuantity) })
                .ToDictionaryAsync(x => x.Id, x => x.Qty, ct);

        // Bulk load referenced master & document maps cleanly
        var grIds = dockRejectedGrLines.Select(l => l.GoodsReceiptId)
            .Concat(openAccrualLines.Select(l => l.GoodsReceiptLineId)) // loaded below
            .Distinct()
            .ToList();

        var allQueuedGrLineIds = dockRejectedGrLines.Select(l => l.GoodsReceiptLineId)
            .Concat(openAccrualLines.Select(l => l.GoodsReceiptLineId))
            .Concat(billedLinesRaw.Select(l => l.GoodsReceiptLineId))
            .Distinct()
            .ToList();

        var grLinesLookup = allQueuedGrLineIds.Count == 0
            ? new Dictionary<Guid, GoodsReceiptLine>()
            : await db.GoodsReceiptLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && allQueuedGrLineIds.Contains(l.GoodsReceiptLineId))
                .ToDictionaryAsync(l => l.GoodsReceiptLineId, ct);

        var allGrIds = dockRejectedGrLines.Select(l => l.GoodsReceiptId)
            .Concat(grLinesLookup.Values.Select(l => l.GoodsReceiptId))
            .Distinct()
            .ToList();
        var grMap = allGrIds.Count == 0
            ? new Dictionary<Guid, GoodsReceipt>()
            : await db.GoodsReceipts.AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && allGrIds.Contains(r.GoodsReceiptId))
                .ToDictionaryAsync(r => r.GoodsReceiptId, ct);

        var accrualIds = openAccrualLines.Select(l => l.ReceiptAccrualId).Distinct().ToList();
        var accrualMap = accrualIds.Count == 0
            ? new Dictionary<Guid, ReceiptAccrual>()
            : await db.ReceiptAccruals.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accrualIds.Contains(a.ReceiptAccrualId))
                .ToDictionaryAsync(a => a.ReceiptAccrualId, ct);

        var poIds = dockRejectedGrLines.Select(l => l.PurchaseOrderId)
            .Concat(accrualMap.Values.Select(a => a.PurchaseOrderId))
            .Concat(postedBills.Select(b => b.PurchaseOrderId))
            .Concat(replacementsRaw.Select(r => r.PurchaseOrderId))
            .Distinct()
            .ToList();
        var poMap = poIds.Count == 0
            ? new Dictionary<Guid, PurchaseOrder>()
            : await db.PurchaseOrders.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && poIds.Contains(p.PurchaseOrderId))
                .ToDictionaryAsync(p => p.PurchaseOrderId, ct);

        var poLineIds = dockRejectedGrLines.Select(l => l.PoLineId).Distinct().ToList();
        var poLineMap = poLineIds.Count == 0
            ? new Dictionary<Guid, PurchaseOrderLine>()
            : await db.PurchaseOrderLines.AsNoTracking()
                .Where(pl => pl.TenantId == tenantId && pl.LegalEntityId == entityId && poLineIds.Contains(pl.PoLineId))
                .ToDictionaryAsync(pl => pl.PoLineId, ct);

        var partnerIds = poMap.Values.Select(p => p.SupplierPartnerId).Distinct().ToList();
        var partnerMap = partnerIds.Count == 0
            ? new Dictionary<Guid, Partner>()
            : await db.Partners.AsNoTracking()
                .Where(p => p.TenantId == tenantId && partnerIds.Contains(p.PartnerId))
                .ToDictionaryAsync(p => p.PartnerId, ct);

        var itemIds = dockRejectedGrLines.Select(l => l.ItemId)
            .Concat(openAccrualLines.Select(l => l.ItemId))
            .Concat(billedLinesRaw.Select(l => l.ItemId))
            .Concat(replacementsRaw.Select(r => r.ItemId))
            .Distinct()
            .ToList();
        var itemMap = itemIds.Count == 0
            ? new Dictionary<Guid, Item>()
            : await db.Items.AsNoTracking()
                .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
                .ToDictionaryAsync(i => i.ItemId, ct);
        var uomIds = itemMap.Values.Select(i => i.BaseUomId).Concat(replacementsRaw.Select(r => r.BaseUomId)).Distinct().ToList();
        var uomMap = uomIds.Count == 0
            ? new Dictionary<Guid, UnitOfMeasure>()
            : await db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
                .ToDictionaryAsync(u => u.UomId, ct);

        var poolMap = itemIds.Count == 0
            ? new Dictionary<Guid, ValuationPool>()
            : await db.ValuationPools.AsNoTracking()
                .Where(vp => vp.TenantId == tenantId && vp.LegalEntityId == entityId && vp.CurrencyCode == "IDR" && vp.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode && itemIds.Contains(vp.ItemId))
                .ToDictionaryAsync(vp => vp.ItemId, ct);

        var locMap = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .ToDictionaryAsync(l => l.LocationId, ct);
        var lotMap = await db.StockLots.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .ToDictionaryAsync(l => l.StockLotId, ct);
        var balances = itemIds.Count == 0
            ? []
            : await db.StockLocationBalances.AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && itemIds.Contains(b.ItemId) && (b.StockStatus == StockStatuses.Unrestricted || b.StockStatus == StockStatuses.Blocked))
                .ToListAsync(ct);

        var stockOptionsByItem = balances
            .Select(b =>
            {
                var elig = b.StockStatus == StockStatuses.Unrestricted ? b.AvailableBaseQuantity : b.OnHandBaseQuantity;
                var loc = locMap.GetValueOrDefault(b.LocationId);
                var lot = b.StockLotId.HasValue ? lotMap.GetValueOrDefault(b.StockLotId.Value) : null;
                return (b.ItemId, Opt: new ReturnStockBalanceOptionDto(
                    b.LocationId,
                    loc?.LocationCode ?? "-",
                    loc?.Name ?? "-",
                    loc?.ZoneType ?? LocationZoneTypes.Storage,
                    b.StockLotId,
                    lot?.LotNumber,
                    b.StockStatus,
                    elig));
            })
            .Where(x => x.Opt.EligibleQuantity > 0m)
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReturnStockBalanceOptionDto>)g.Select(x => x.Opt).OrderByDescending(o => o.EligibleQuantity).ToList());

        var queuedM1 = dockRejectedGrLines
            .Select(gl =>
            {
                var claimed = claimedM1Map.GetValueOrDefault(gl.GoodsReceiptLineId, 0m);
                var elig = Math.Max(0m, decimal.Round(gl.DockRejectedBaseQuantity - claimed, 6, MidpointRounding.AwayFromZero));
                var gr = grMap[gl.GoodsReceiptId];
                var po = poMap[gl.PurchaseOrderId];
                var poLine = poLineMap[gl.PoLineId];
                var partner = partnerMap[po.SupplierPartnerId];
                var authCost = poLine.OrderedBaseQuantity > 0m
                    ? decimal.Round(poLine.LineSubtotalAmount / poLine.OrderedBaseQuantity, 6, MidpointRounding.AwayFromZero)
                    : poLine.UnitPrice;
                return new QueuedUnvaluedRejectionDto(
                    gl.GoodsReceiptLineId,
                    gl.GoodsReceiptId,
                    gr.ReceiptNumber,
                    gr.ReceivedDate,
                    po.PurchaseOrderId,
                    po.PoNumber ?? po.DraftCode,
                    partner.PartnerId,
                    partner.PartnerCode,
                    partner.LegalName,
                    gl.PoLineId,
                    gl.ItemId,
                    gl.ItemCodeSnapshot,
                    gl.ItemNameSnapshot,
                    gl.BaseUomCodeSnapshot,
                    gl.DockRejectedBaseQuantity,
                    claimed,
                    elig,
                    authCost,
                    gl.DiscrepancyReasonCode);
            })
            .Where(x => x.EligibleQuantity > 0m)
            .OrderByDescending(x => x.ReceivedDate)
            .ToList();

        var queuedM2 = openAccrualLines
            .Select(al =>
            {
                var acc = accrualMap[al.ReceiptAccrualId];
                var po = poMap[acc.PurchaseOrderId];
                var partner = partnerMap[acc.SupplierPartnerId];
                var gr = grMap.GetValueOrDefault(acc.GoodsReceiptId);
                var item = itemMap[al.ItemId];
                var uom = uomMap[item.BaseUomId];
                var pool = poolMap.GetValueOrDefault(al.ItemId);
                var opts = stockOptionsByItem.GetValueOrDefault(al.ItemId, Array.Empty<ReturnStockBalanceOptionDto>());
                var stockCap = opts.Sum(o => o.EligibleQuantity);
                var remAfterInFlight = Math.Max(0m, decimal.Round(al.RemainingUnbilledBaseQuantity - inFlightM2Map.GetValueOrDefault(al.ReceiptAccrualLineId, 0m), 6, MidpointRounding.AwayFromZero));
                var elig = Math.Min(remAfterInFlight, stockCap);
                return new QueuedValuedUnbilledLineDto(
                    al.ReceiptAccrualLineId,
                    al.ReceiptAccrualId,
                    acc.AccrualNumber,
                    al.ValuedAcceptanceLineId,
                    al.GoodsReceiptLineId,
                    acc.GoodsReceiptId,
                    gr?.ReceiptNumber ?? "-",
                    acc.PostingDate,
                    po.PurchaseOrderId,
                    po.PoNumber ?? po.DraftCode,
                    partner.PartnerId,
                    partner.PartnerCode,
                    partner.LegalName,
                    al.ItemId,
                    item.ItemCode,
                    item.Name,
                    uom.UomCode,
                    al.RemainingUnbilledBaseQuantity,
                    elig,
                    al.UnitCostBase,
                    pool?.MovingAverageUnitCost ?? 0m,
                    pool?.ValuedBaseQuantity ?? 0m,
                    opts);
            })
            .Where(x => x.RemainingUnbilledBaseQuantity > 0m)
            .OrderByDescending(x => x.AccrualPostingDate)
            .ToList();

        var billMap = postedBills.ToDictionary(b => b.SupplierBillId);
        var payableByBill = postedBillIds.Count == 0
            ? new Dictionary<Guid, PayableItem>()
            : await db.PayableItems.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && postedBillIds.Contains(p.SupplierBillId))
                .ToDictionaryAsync(p => p.SupplierBillId, ct);

        var queuedM3 = billedLinesRaw
            .Select(bl =>
            {
                var bill = billMap[bl.SupplierBillId];
                var pay = payableByBill[bl.SupplierBillId];
                var grLine = grLinesLookup.GetValueOrDefault(bl.GoodsReceiptLineId);
                var gr = grLine is not null ? grMap.GetValueOrDefault(grLine.GoodsReceiptId) : null;
                var po = poMap[bill.PurchaseOrderId];
                var priorCred = creditedMap.GetValueOrDefault(bl.SupplierBillLineId, 0m);
                var inFlightCred = inFlightM3Map.GetValueOrDefault(bl.SupplierBillLineId, 0m);
                var remQty = Math.Max(0m, decimal.Round(bl.BilledBaseQuantity - priorCred - inFlightCred, 6, MidpointRounding.AwayFromZero));
                var opts = stockOptionsByItem.GetValueOrDefault(bl.ItemId, Array.Empty<ReturnStockBalanceOptionDto>());
                var pool = poolMap.GetValueOrDefault(bl.ItemId);

                var (matrixCode, blockReason) = pay.PaidAmount > 0m
                    ? ("M5", $"OD-05: Paid/partially paid invoice (Paid={pay.PaidAmount:N0} IDR) blocked.")
                    : bl.WithholdingTaxAmount > 0m
                        ? ("M6", "OD-07: Line with PPh 23 withholding blocked pending Bukti Potong policy.")
                        : ("M3", (string?)null);

                return new QueuedValuedBilledLineDto(
                    bl.SupplierBillLineId,
                    bill.SupplierBillId,
                    bill.BillNumber ?? bill.DraftCode,
                    bill.SupplierInvoiceNumber,
                    pay.PayableItemId,
                    pay.PayableNumber,
                    pay.PaidAmount,
                    pay.RemainingOpenAmount,
                    bl.GoodsReceiptLineId,
                    gr?.GoodsReceiptId,
                    gr?.ReceiptNumber,
                    pay.PostingDate,
                    po.PurchaseOrderId,
                    po.PoNumber ?? po.DraftCode,
                    bill.SupplierPartnerId,
                    bill.SupplierCodeSnapshot,
                    bill.SupplierLegalNameSnapshot,
                    bl.ItemId,
                    bl.ItemCodeSnapshot,
                    bl.ItemNameSnapshot,
                    bl.BaseUomCodeSnapshot,
                    bl.BilledBaseQuantity,
                    priorCred,
                    remQty,
                    bl.InvoiceUnitPriceBase,
                    bl.PpnTaxAmount,
                    bl.WithholdingTaxAmount,
                    bl.PpnTaxCodeSnapshot,
                    pool?.MovingAverageUnitCost ?? 0m,
                    pool?.ValuedBaseQuantity ?? 0m,
                    matrixCode,
                    blockReason,
                    opts);
            })
            .Where(x => x.RemainingCreditableQuantity > 0m)
            .OrderByDescending(x => x.BillPostingDate)
            .ToList();

        // Map return summaries, replacements, and credits
        var movIds = returnsRaw.Where(r => r.StockMovementId.HasValue).Select(r => r.StockMovementId!.Value)
            .Concat(creditsRaw.Select(c => c.StockMovementId)).Distinct().ToList();
        var jrnIds = returnsRaw.Where(r => r.GlJournalId.HasValue).Select(r => r.GlJournalId!.Value)
            .Concat(creditsRaw.Select(c => c.GlJournalId)).Distinct().ToList();
        var userIds = returnsRaw.Where(r => r.SubmittedByUserId.HasValue).Select(r => r.SubmittedByUserId!.Value)
            .Concat(returnsRaw.Where(r => r.ApprovedByUserId.HasValue).Select(r => r.ApprovedByUserId!.Value)).Distinct().ToList();

        var movNums = movIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockMovements.AsNoTracking().Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movIds.Contains(m.StockMovementId))
                .ToDictionaryAsync(m => m.StockMovementId, m => m.MovementNumber, ct);
        var jrnNums = jrnIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking().Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);
        var credNumById = creditsRaw.ToDictionary(c => c.SupplierCreditId, c => c.CreditNumber);
        var returnNumById = returnsRaw.ToDictionary(r => r.SupplierReturnId, r => r.ReturnNumber ?? r.DraftCode);
        var creditBillIds = creditsRaw.Select(c => c.SupplierBillId).Distinct().ToList();
        var creditPayableMap = creditBillIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.PayableItems.AsNoTracking().Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && creditBillIds.Contains(p.SupplierBillId))
                .ToDictionaryAsync(p => p.PayableItemId, p => p.PayableNumber, ct);

        var returnSummaries = returnsRaw.Select(r => MapSummary(r, movNums, jrnNums, credNumById, emails)).ToList();

        var replacementDtos = replacementsRaw.Select(a =>
        {
            var po = poMap.GetValueOrDefault(a.PurchaseOrderId);
            var item = itemMap.GetValueOrDefault(a.ItemId);
            var uom = uomMap.GetValueOrDefault(a.BaseUomId);
            return new ReplacementAuthorizationDto(
                a.ReplacementAuthorizationId,
                a.AuthorizationNumber,
                a.SupplierReturnId,
                returnNumById.GetValueOrDefault(a.SupplierReturnId, "-"),
                a.SourceCaseType,
                a.PurchaseOrderId,
                po?.PoNumber ?? po?.DraftCode ?? "-",
                a.ItemId,
                item?.ItemCode ?? "-",
                item?.Name ?? "-",
                a.BaseUomId,
                uom?.UomCode ?? a.BaseUomCodeSnapshot,
                a.AuthorizedBaseQuantity,
                a.DeliveredBaseQuantity,
                a.AcceptedBaseQuantity,
                a.RemainingBaseQuantity,
                a.CommercialIncrementalUnitPrice,
                a.AuthorizedInventoryUnitCostBase,
                a.Status,
                a.CreatedAtUtc,
                a.UpdatedAtUtc);
        }).ToList();

        var creditDtos = creditsRaw.Select(c => new SupplierCreditDto(
            c.SupplierCreditId,
            c.CreditNumber,
            c.SupplierReturnId,
            c.ReturnNumberSnapshot,
            c.SupplierBillId,
            c.BillNumberSnapshot,
            c.PayableItemId,
            creditPayableMap.GetValueOrDefault(c.PayableItemId, "-"),
            c.SupplierCodeSnapshot,
            c.SupplierLegalNameSnapshot,
            c.SupplierNpwpSnapshot,
            c.TaxReturnNoteNumber,
            c.PostingDate,
            c.CurrencyCode,
            c.SubtotalCreditAmount,
            c.PpnReversalAmount,
            c.TotalCreditAmount,
            c.InventoryCreditAmount,
            c.PpvDifferenceAmount,
            c.GlJournalId,
            jrnNums.GetValueOrDefault(c.GlJournalId, "-"),
            c.StockMovementId,
            movNums.GetValueOrDefault(c.StockMovementId, "-"),
            c.CreatedAtUtc)).ToList();

        var openReplacements = replacementDtos.Where(a =>
            (a.Status == ReplacementAuthorizationStatuses.Active || a.Status == ReplacementAuthorizationStatuses.PartiallyReceived) &&
            a.RemainingBaseQuantity > 0m).ToList();

        return new ReturnsOverviewResponse(
            returnsRaw.Count(r => r.Status == SupplierReturnStatuses.PendingApproval),
            openReplacements.Count,
            openReplacements.Sum(a => a.RemainingBaseQuantity),
            returnsRaw.Where(r => r.Status == SupplierReturnStatuses.Executed && r.ReturnCaseType == SupplierReturnCaseTypes.ValuedUnbilledReturn).Sum(r => r.TotalGrniReversalAmount),
            creditsRaw.Sum(c => c.TotalCreditAmount),
            Od05DefaultNotice,
            Od07DefaultNotice,
            BuildMatrixRules(),
            queuedM1,
            queuedM2,
            queuedM3,
            returnSummaries,
            replacementDtos,
            creditDtos);
    }

    private static async Task<SupplierReturnDetailResponse?> LoadReturnDetailAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid returnId,
        bool includeLivePreview,
        bool wasIdempotentReplay,
        CancellationToken ct)
    {
        var r = await db.SupplierReturns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.SupplierReturnId == returnId, ct);
        if (r is null)
        {
            return null;
        }

        var lines = await db.SupplierReturnLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.SupplierReturnId == returnId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var replacements = await db.ReplacementAuthorizations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.SupplierReturnId == returnId)
            .OrderBy(a => a.AuthorizationNumber)
            .ToListAsync(ct);

        var credit = r.SupplierCreditId.HasValue
            ? await db.SupplierCredits.AsNoTracking()
                .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.LegalEntityId == entityId && c.SupplierCreditId == r.SupplierCreditId.Value, ct)
            : null;

        var taxDets = credit is not null
            ? await db.TaxDeterminations.AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.SourceDocumentId == credit.SupplierCreditId)
                .OrderBy(t => t.TaxRole)
                .ToListAsync(ct)
            : [];

        var locIds = lines.Where(l => l.SourceLocationId.HasValue).Select(l => l.SourceLocationId!.Value).Distinct().ToList();
        var locCodes = locIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.WarehouseLocations.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && locIds.Contains(l.LocationId))
                .ToDictionaryAsync(l => l.LocationId, l => l.LocationCode, ct);

        var lotIds = lines.Where(l => l.SourceStockLotId.HasValue).Select(l => l.SourceStockLotId!.Value).Distinct().ToList();
        var lotNums = lotIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockLots.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && lotIds.Contains(l.StockLotId))
                .ToDictionaryAsync(l => l.StockLotId, l => l.LotNumber, ct);

        var movNums = new Dictionary<Guid, string>();
        if (r.StockMovementId.HasValue)
        {
            var mn = await db.StockMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.StockMovementId == r.StockMovementId.Value)
                .Select(m => m.MovementNumber)
                .SingleOrDefaultAsync(ct);
            if (mn is not null) movNums[r.StockMovementId.Value] = mn;
        }

        var jrnNums = new Dictionary<Guid, string>();
        if (r.GlJournalId.HasValue)
        {
            var jn = await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && j.JournalId == r.GlJournalId.Value)
                .Select(j => j.JournalNumber)
                .SingleOrDefaultAsync(ct);
            if (jn is not null) jrnNums[r.GlJournalId.Value] = jn;
        }

        var credNumMap = new Dictionary<Guid, string>();
        if (credit is not null)
        {
            credNumMap[credit.SupplierCreditId] = credit.CreditNumber;
        }

        var userIds = new[] { r.SubmittedByUserId, r.ApprovedByUserId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var lineDtos = lines.Select(l => new SupplierReturnLineDto(
            l.SupplierReturnLineId,
            l.LineNumber,
            l.GoodsReceiptLineId,
            l.ValuedAcceptanceLineId,
            l.ReceiptAccrualLineId,
            l.SupplierBillLineId,
            l.PoLineId,
            l.ItemId,
            l.ItemCodeSnapshot,
            l.ItemNameSnapshot,
            l.BaseUomCodeSnapshot,
            l.SourceLocationId,
            l.SourceLocationId.HasValue ? locCodes.GetValueOrDefault(l.SourceLocationId.Value) : null,
            l.SourceStockLotId,
            l.SourceStockLotId.HasValue ? lotNums.GetValueOrDefault(l.SourceStockLotId.Value) : null,
            l.SourceStockStatus,
            l.ReturnedBaseQuantity,
            l.AuthorizedUnitCostBase,
            l.InventoryCreditAmount,
            l.GrniReversalAmount,
            l.CreditSubtotalAmount,
            l.PpnReversalAmount,
            l.TotalCreditAmount,
            l.PpvDifferenceAmount,
            l.LineNotes)).ToList();

        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var itemMap = itemIds.Count == 0
            ? new Dictionary<Guid, Item>()
            : await db.Items.AsNoTracking().Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId)).ToDictionaryAsync(i => i.ItemId, ct);

        var rpaDtos = replacements.Select(a =>
        {
            var item = itemMap.GetValueOrDefault(a.ItemId);
            return new ReplacementAuthorizationDto(
                a.ReplacementAuthorizationId,
                a.AuthorizationNumber,
                a.SupplierReturnId,
                r.ReturnNumber ?? r.DraftCode,
                a.SourceCaseType,
                a.PurchaseOrderId,
                r.PoNumberSnapshot,
                a.ItemId,
                item?.ItemCode ?? "-",
                item?.Name ?? "-",
                a.BaseUomId,
                a.BaseUomCodeSnapshot,
                a.AuthorizedBaseQuantity,
                a.DeliveredBaseQuantity,
                a.AcceptedBaseQuantity,
                a.RemainingBaseQuantity,
                a.CommercialIncrementalUnitPrice,
                a.AuthorizedInventoryUnitCostBase,
                a.Status,
                a.CreatedAtUtc,
                a.UpdatedAtUtc);
        }).ToList();

        SupplierCreditDto? creditDto = null;
        if (credit is not null)
        {
            var payNumber = await db.PayableItems.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PayableItemId == credit.PayableItemId)
                .Select(p => p.PayableNumber)
                .SingleOrDefaultAsync(ct) ?? "-";

            creditDto = new SupplierCreditDto(
                credit.SupplierCreditId,
                credit.CreditNumber,
                credit.SupplierReturnId,
                credit.ReturnNumberSnapshot,
                credit.SupplierBillId,
                credit.BillNumberSnapshot,
                credit.PayableItemId,
                payNumber,
                credit.SupplierCodeSnapshot,
                credit.SupplierLegalNameSnapshot,
                credit.SupplierNpwpSnapshot,
                credit.TaxReturnNoteNumber,
                credit.PostingDate,
                credit.CurrencyCode,
                credit.SubtotalCreditAmount,
                credit.PpnReversalAmount,
                credit.TotalCreditAmount,
                credit.InventoryCreditAmount,
                credit.PpvDifferenceAmount,
                credit.GlJournalId,
                jrnNums.GetValueOrDefault(credit.GlJournalId, "-"),
                credit.StockMovementId,
                movNums.GetValueOrDefault(credit.StockMovementId, "-"),
                credit.CreatedAtUtc);
        }

        var taxDtos = taxDets.Select(t => new TaxDeterminationDto(
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

        SupplierReturnPreviewResponse? livePreview = null;
        if (includeLivePreview && r.Status != SupplierReturnStatuses.Executed && r.Status != SupplierReturnStatuses.Cancelled && lines.Count > 0)
        {
            var previewOutcome = await SupplierReturnCoordinator.PreviewReturnAsync(
                db,
                new PreviewSupplierReturnCommand(
                    tenantId,
                    entityId,
                    r.ReturnCaseType,
                    r.ResolutionMode,
                    r.PurchaseOrderId,
                    r.GoodsReceiptId,
                    r.ReceiptAccrualId,
                    r.SupplierBillId,
                    r.ReturnDate,
                    r.TaxReturnNoteNumber,
                    lines.Select(l => new ReturnLineDraftInput(
                        l.GoodsReceiptLineId,
                        l.ValuedAcceptanceLineId,
                        l.ReceiptAccrualLineId,
                        l.SupplierBillLineId,
                        l.ReturnedBaseQuantity,
                        l.SourceLocationId,
                        l.SourceStockLotId,
                        l.SourceStockStatus,
                        l.LineNotes)).ToList(),
                    r.SupplierReturnId),
                ct);
            if (previewOutcome.Succeeded && previewOutcome.Matrix is not null)
            {
                livePreview = MapMatrix(previewOutcome.Matrix);
            }
        }

        return new SupplierReturnDetailResponse(
            MapSummary(r, movNums, jrnNums, credNumMap, emails),
            lineDtos,
            rpaDtos,
            creditDto,
            taxDtos,
            livePreview,
            wasIdempotentReplay);
    }

    private static SupplierReturnSummaryDto MapSummary(
        SupplierReturn r,
        Dictionary<Guid, string> movNums,
        Dictionary<Guid, string> jrnNums,
        Dictionary<Guid, string> credNums,
        Dictionary<Guid, string> emails) =>
        new(
            r.SupplierReturnId,
            r.DraftCode,
            r.ReturnNumber,
            r.ReturnCaseType,
            r.ResolutionMode,
            r.MatrixDisposition,
            r.MatrixBlockingCode,
            r.MatrixBlockingReason,
            r.PurchaseOrderId,
            r.PoNumberSnapshot,
            r.GoodsReceiptId,
            r.ReceiptNumberSnapshot,
            r.ReceiptAccrualId,
            r.SupplierBillId,
            r.BillNumberSnapshot,
            r.SupplierCodeSnapshot,
            r.SupplierLegalNameSnapshot,
            r.ReturnDate,
            r.ReasonCode,
            r.TaxReturnNoteNumber,
            r.Status,
            r.TotalReturnedBaseQuantity,
            r.TotalInventoryCreditAmount,
            r.TotalGrniReversalAmount,
            r.TotalCreditSubtotalAmount,
            r.TotalPpnReversalAmount,
            r.TotalCreditNoteAmount,
            r.TotalPpvDifferenceAmount,
            r.StockMovementId,
            r.StockMovementId is { } mid ? movNums.GetValueOrDefault(mid) : null,
            r.GlJournalId,
            r.GlJournalId is { } jid ? jrnNums.GetValueOrDefault(jid) : null,
            r.SupplierCreditId,
            r.SupplierCreditId is { } cid ? credNums.GetValueOrDefault(cid) : null,
            r.SubmittedByUserId is { } sid ? emails.GetValueOrDefault(sid) : null,
            r.ApprovedByUserId is { } aid ? emails.GetValueOrDefault(aid) : null,
            r.CreatedAtUtc,
            r.UpdatedAtUtc);

    private static IReadOnlyList<ReturnMatrixRuleDto> BuildMatrixRules() =>
    [
        new("M1", SupplierReturnCaseTypes.UnvaluedDockRejection, "REPLACEMENT_ONLY / CLOSE_WITHOUT_REPLACEMENT", "Dock-rejected quantity at Gate 1 (not valued-accepted)", "Zero GL/inventory/GRNI; optional ReplacementAuthorization (Commercial=0, InventoryCost=PO)", ReturnMatrixDispositions.Supported),
        new("M2", SupplierReturnCaseTypes.ValuedUnbilledReturn, "CLOSE_WITHOUT_REPLACEMENT / REPLACEMENT_ONLY", "Open unbilled GRNI + unreserved UNRESTRICTED/BLOCKED stock, OPEN period, OD-02 approved", "Outbound movement Cr 1300 (MAC) + Dr 2110 GRNI +/- 5200 PPV + PO & optional Replacement", ReturnMatrixDispositions.Supported),
        new("M3", SupplierReturnCaseTypes.ValuedBilledReturn, ReturnResolutionModes.ApCreditNote, "Unpaid POSTED bill (Paid=0), PPh=0, Nota Retur if PPN>0, OPEN period, OD-02 ap.approve", "Outbound Cr 1300 + Append-only SCN Credit + Dr 2100 AP + Cr 1410 PPN + +/-5200 PPV", ReturnMatrixDispositions.Supported),
        new("M4", SupplierReturnCaseTypes.ValuedBilledReturn, ReturnResolutionModes.ReplacementOnly, "Any billed invoice requesting replacement without credit memo", "Blocked fail-closed (409 od05_billed_replacement_policy_blocked)", ReturnMatrixDispositions.BlockedPolicyOd05),
        new("M5", SupplierReturnCaseTypes.ValuedBilledReturn, ReturnResolutionModes.ApCreditNote, "Billed invoice already paid/partially settled (PaidAmount > 0)", "Blocked fail-closed (409 od05_paid_bill_return_policy_blocked)", ReturnMatrixDispositions.BlockedPolicyOd05),
        new("M6", SupplierReturnCaseTypes.ValuedBilledReturn, ReturnResolutionModes.ApCreditNote, "Billed invoice line carries PPh withholding tax > 0", "Blocked fail-closed (409 od07_withholding_return_reversal_blocked)", ReturnMatrixDispositions.BlockedPolicyOd07),
        new("M7", "VALUED_*", "ANY", "Target fiscal period is CLOSED or LOCKED", "Blocked fail-closed on execution (409 period_closed)", ReturnMatrixDispositions.BlockedClosedPeriod)
    ];
}
