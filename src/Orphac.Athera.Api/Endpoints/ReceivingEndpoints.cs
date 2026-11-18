using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.Api.Endpoints;

public static class ReceivingEndpoints
{
    public static IEndpointRouteBuilder MapReceivingEndpoints(this IEndpointRouteBuilder app)
    {
        MapEligiblePoRoutes(app);
        MapGoodsReceiptReadRoutes(app);
        MapGoodsReceiptMutationRoutes(app);
        return app;
    }

    private static void MapEligiblePoRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/eligible-pos", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReceivingScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var pos = await db.PurchaseOrders.AsNoTracking()
                        .Where(p =>
                            p.TenantId == tenantId &&
                            p.LegalEntityId == entityId &&
                            p.Status == PurchaseOrderStatuses.Approved &&
                            p.CurrentApprovedRevisionId != null)
                        .OrderByDescending(p => p.OrderDate)
                        .ThenByDescending(p => p.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    if (pos.Count == 0)
                    {
                        return Results.Ok(new EligibleReceivingPosResponse(
                            Array.Empty<EligiblePurchaseOrderForReceivingDto>(),
                            "OD-01 active safe default (ATH-CR-003): Physical excess quantity is held unvalued in Gate 1 custody and cannot be released without approved PO revision."));
                    }

                    var revIds = pos.Select(p => p.CurrentApprovedRevisionId!.Value).ToList();
                    var revisions = await db.PurchaseOrderRevisions.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && revIds.Contains(r.RevisionId))
                        .ToDictionaryAsync(r => r.RevisionId, innerCt);

                    var poLines = await db.PurchaseOrderLines.AsNoTracking()
                        .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && revIds.Contains(l.RevisionId))
                        .OrderBy(l => l.LineNumber)
                        .ToListAsync(innerCt);

                    var poIds = pos.Select(p => p.PurchaseOrderId).ToList();
                    var receiptLines = await db.GoodsReceiptLines.AsNoTracking()
                        .Where(gl => gl.TenantId == tenantId && gl.LegalEntityId == entityId && poIds.Contains(gl.PurchaseOrderId))
                        .ToListAsync(innerCt);

                    var itemIds = poLines.Select(l => l.ItemId).Distinct().ToList();
                    var uoms = await db.UnitsOfMeasure.AsNoTracking()
                        .Where(u => u.TenantId == tenantId && u.Status == CatalogStatuses.Active)
                        .ToDictionaryAsync(u => u.UomId, innerCt);
                    var conversions = await db.ItemUomConversions.AsNoTracking()
                        .Where(c => c.TenantId == tenantId && c.Status == CatalogStatuses.Active && itemIds.Contains(c.ItemId))
                        .ToListAsync(innerCt);
                    var convByItem = conversions.GroupBy(c => c.ItemId).ToDictionary(g => g.Key, g => g.ToList());

                    var linesByRev = poLines.GroupBy(l => l.RevisionId).ToDictionary(g => g.Key, g => g.ToList());
                    var rcptByPoLineKey = receiptLines
                        .GroupBy(gl => (gl.PurchaseOrderId, gl.LineageLineKey))
                        .ToDictionary(g => g.Key, g => g.ToList());

                    var resultItems = new List<EligiblePurchaseOrderForReceivingDto>(pos.Count);
                    foreach (var po in pos)
                    {
                        if (po.CurrentApprovedRevisionId is not { } revId || !revisions.TryGetValue(revId, out var rev))
                        {
                            continue;
                        }

                        var revLines = linesByRev.GetValueOrDefault(revId, []);
                        var mappedLines = new List<EligiblePoReceivingLineDto>(revLines.Count);

                        foreach (var l in revLines)
                        {
                            var history = rcptByPoLineKey.GetValueOrDefault((po.PurchaseOrderId, l.LineageLineKey), []);
                            var deliveredBase = history.Sum(x => x.PhysicallyDeliveredBaseQuantity);
                            var pendingBase = history.Sum(x => x.PendingCheckBaseQuantity);
                            var heldBase = history.Sum(x => x.DockHeldBaseQuantity);
                            var releasedBase = history.Sum(x => x.DockReleasedBaseQuantity);
                            var rejectedBase = history.Sum(x => x.DockRejectedBaseQuantity);
                            var valuedBase = history.Sum(x => x.ValuedAcceptedBaseQuantity);
                            var remainingEligible = Math.Max(0m, l.OrderedBaseQuantity - (pendingBase + releasedBase));

                            var allowedUoms = new List<EligiblePoReceivingUomDto>();
                            if (uoms.TryGetValue(l.BaseUomId, out var baseUom))
                            {
                                allowedUoms.Add(new EligiblePoReceivingUomDto(
                                    baseUom.UomId,
                                    baseUom.UomCode,
                                    baseUom.Name,
                                    1m,
                                    baseUom.QuantityPrecisionScale));
                            }
                            else
                            {
                                allowedUoms.Add(new EligiblePoReceivingUomDto(
                                    l.BaseUomId,
                                    l.BaseUomCodeSnapshot,
                                    l.BaseUomCodeSnapshot,
                                    1m,
                                    0));
                            }

                            if (convByItem.TryGetValue(l.ItemId, out var itemConvs))
                            {
                                foreach (var c in itemConvs)
                                {
                                    if (c.UomId != l.BaseUomId && uoms.TryGetValue(c.UomId, out var u))
                                    {
                                        allowedUoms.Add(new EligiblePoReceivingUomDto(
                                            u.UomId,
                                            u.UomCode,
                                            u.Name,
                                            c.FactorToBase,
                                            u.QuantityPrecisionScale));
                                    }
                                }
                            }

                            mappedLines.Add(new EligiblePoReceivingLineDto(
                                l.PoLineId,
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
                                l.OrderedBaseQuantity,
                                deliveredBase,
                                pendingBase,
                                heldBase,
                                releasedBase,
                                rejectedBase,
                                valuedBase,
                                remainingEligible,
                                allowedUoms.OrderBy(u => u.FactorToBase).ThenBy(u => u.UomCode).ToList()));
                        }

                        resultItems.Add(new EligiblePurchaseOrderForReceivingDto(
                            po.PurchaseOrderId,
                            po.PoNumber ?? po.DraftCode,
                            rev.RevisionId,
                            rev.RevisionNumber,
                            rev.SnapshotHash,
                            rev.SupplierPartnerId,
                            rev.SupplierCodeSnapshot,
                            rev.SupplierLegalNameSnapshot,
                            rev.OrderDate,
                            rev.ExpectedDeliveryDate,
                            rev.DeliveryDestination,
                            mappedLines));
                    }

                    return Results.Ok(new EligibleReceivingPosResponse(
                        resultItems,
                        "OD-01 active safe default (ATH-CR-003): Excess physical deliveries enter unvalued DOCK_HELD custody and remain blocked from release until an approved PO amendment increases ordered capacity."));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListEligiblePurchaseOrdersForReceiving")
        .Produces<EligibleReceivingPosResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapGoodsReceiptReadRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? status,
            [FromQuery] Guid? purchaseOrderId,
            [FromQuery] string? search,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReceivingScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var query = db.GoodsReceipts.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId);

                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        var normStatus = status.Trim().ToUpperInvariant();
                        query = query.Where(r => r.Status == normStatus);
                    }

                    if (purchaseOrderId is { } poFilter && poFilter != Guid.Empty)
                    {
                        query = query.Where(r => r.PurchaseOrderId == poFilter);
                    }

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var term = search.Trim().ToUpperInvariant();
                        query = query.Where(r =>
                            r.ReceiptNumber.ToUpper().Contains(term) ||
                            r.SupplierDeliveryNoteNumber.ToUpper().Contains(term) ||
                            r.PoNumberSnapshot.ToUpper().Contains(term) ||
                            r.ReceivingStation.ToUpper().Contains(term));
                    }

                    var receipts = await query
                        .OrderByDescending(r => r.ReceivedDate)
                        .ThenByDescending(r => r.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    var receiptIdStrings = receipts.Select(r => r.GoodsReceiptId.ToString("D")).ToList();
                    var linkedJournals = receiptIdStrings.Count == 0
                        ? 0
                        : await db.JournalEntries.AsNoTracking()
                            .CountAsync(j =>
                                j.TenantId == tenantId &&
                                j.LegalEntityId == entityId &&
                                receiptIdStrings.Contains(j.SourceDocumentId),
                                innerCt);

                    var dtos = receipts.Select(r => new GoodsReceiptSummaryDto(
                        r.GoodsReceiptId,
                        r.ReceiptNumber,
                        r.PurchaseOrderId,
                        r.PoNumberSnapshot,
                        r.PoRevisionNumberSnapshot,
                        r.SupplierCodeSnapshot,
                        r.SupplierLegalNameSnapshot,
                        r.SupplierDeliveryNoteNumber,
                        r.ReceivedDate,
                        r.ReceivingStation,
                        r.Status,
                        r.TotalDeliveredBaseQuantity,
                        r.TotalRejectedBaseQuantity,
                        r.TotalValuedAcceptedBaseQuantity,
                        r.TotalUnresolvedCustodyBaseQuantity,
                        r.TotalReleasedBaseQuantity,
                        r.TotalHeldBaseQuantity,
                        r.HasFinancialPosting,
                        r.UpdatedAtUtc)).ToList();

                    return Results.Ok(new GoodsReceiptListResponse(
                        dtos,
                        dtos.Count,
                        dtos.Sum(d => d.TotalDeliveredBaseQuantity),
                        dtos.Sum(d => d.TotalHeldBaseQuantity),
                        dtos.Sum(d => d.TotalReleasedBaseQuantity),
                        dtos.Sum(d => d.TotalRejectedBaseQuantity),
                        linkedJournals));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListGoodsReceipts")
        .Produces<GoodsReceiptListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts/{receiptId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReceivingScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await LoadGoodsReceiptDetailDtoAsync(db, tenantId, entityId, receiptId, wasIdempotentReplay: false, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "goods_receipt_not_found", "Goods receipt was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetGoodsReceiptDetail")
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapGoodsReceiptMutationRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts", (
            Guid tenantId,
            Guid entityId,
            RecordGoodsReceiptRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteReceivingMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                (_, actorUserId, innerCt) => GoodsReceiptWorkflowService.RecordArrivalAsync(
                    db,
                    new RecordGoodsReceiptCommand(
                        tenantId,
                        entityId,
                        request.PurchaseOrderId,
                        request.SupplierDeliveryNoteNumber,
                        request.IdempotencyKey,
                        request.ReceivedDate,
                        request.ReceivingStation,
                        request.VehicleOrSealRef,
                        request.DocumentEvidenceRef,
                        request.Notes,
                        (request.Lines ?? Array.Empty<ArrivalLineRequest>())
                            .Select(l => new ArrivalLineInput(
                                l.PoLineId,
                                l.ReceivedUomId,
                                l.PhysicallyDeliveredQuantity,
                                l.FlagForInspection,
                                l.DiscrepancyReasonCode,
                                l.DiscrepancyNotes,
                                l.ConditionEvidenceRef))
                            .ToList(),
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("RecordGoodsReceiptArrival")
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status201Created)
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts/{receiptId:guid}/lines/{lineId:guid}/dock-checks", (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            Guid lineId,
            RecordDockCheckRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteReceivingMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                (_, actorUserId, innerCt) => GoodsReceiptWorkflowService.RecordDockCheckAsync(
                    db,
                    new RecordDockCheckCommand(
                        tenantId,
                        entityId,
                        receiptId,
                        lineId,
                        request.IdempotencyKey,
                        request.PendingCheckBaseQuantity,
                        request.DockHeldBaseQuantity,
                        request.DockReleasedBaseQuantity,
                        request.DockRejectedBaseQuantity,
                        request.DiscrepancyReasonCode,
                        request.InspectionNotes,
                        request.EvidenceReference,
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("RecordGoodsReceiptDockCheck")
        .Produces<GoodsReceiptDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ExecuteReceivingScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
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
                    return EndpointSupport.Forbidden(
                        httpContext,
                        "insufficient_entity_permission",
                        "Your active grants for this legal entity do not permit accessing receiving records.");
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        if (!result.IsAllowed)
        {
            return EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
        }

        return result.Value!;
    }

    private static async Task<IResult> ExecuteReceivingMutationAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        AtheraDbContext db,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<ReceivingWorkflowOutcome>> action,
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
                        return (Result: EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            "Your active grants for this legal entity do not permit Gate 1 receiving actions."),
                            ShouldNotifyOutbox: false);
                    }

                    var outcome = await action(scope, actorUserId, innerCt);
                    if (!outcome.Succeeded || outcome.Receipt is null)
                    {
                        throw new AbortedReceivingWorkflowException(outcome);
                    }

                    var detail = await LoadGoodsReceiptDetailDtoAsync(
                        db,
                        tenantId,
                        entityId,
                        outcome.Receipt.GoodsReceiptId,
                        outcome.WasIdempotentReplay,
                        innerCt);

                    var okResult = outcome.HttpStatus == StatusCodes.Status201Created
                        ? Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/receiving/receipts/{outcome.Receipt.GoodsReceiptId}",
                            detail)
                        : Results.Ok(detail);

                    return (Result: okResult, ShouldNotifyOutbox: !outcome.WasIdempotentReplay);
                },
                ct);

            if (!result.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            if (result.Value.ShouldNotifyOutbox)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return result.Value.Result;
        }
        catch (AbortedReceivingWorkflowException aborted)
        {
            return MapWorkflowFailure(httpContext, aborted.Outcome);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsUniqueConstraintViolation(ex))
        {
            return MapPersistenceConflict(httpContext, ex);
        }
    }

    private sealed class AbortedReceivingWorkflowException(ReceivingWorkflowOutcome outcome) : Exception
    {
        public ReceivingWorkflowOutcome Outcome { get; } = outcome;
    }

    private static bool IsUniqueConstraintViolation(Exception ex) =>
        ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;

    private static IResult MapPersistenceConflict(HttpContext httpContext, Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Receipt custody record was modified concurrently; reload and retry.");
        }

        if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return pgEx.ConstraintName switch
            {
                "ux_goods_receipts_supplier_delivery_note" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_supplier_delivery_note",
                    "This supplier delivery note number has already been recorded for this supplier and legal entity."),
                "ux_goods_receipts_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_receipt_idempotency_conflict",
                    "This receipt idempotency key was already committed with a different payload."),
                "ux_dock_checks_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_dock_check_idempotency_conflict",
                    "This dock check idempotency key was already committed with a different disposition."),
                "ux_goods_receipts_number" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_receipt_number",
                    "Duplicate receipt number detected in this legal entity."),
                _ => EndpointSupport.Conflict(httpContext, "receiving_conflict", "Duplicate receiving conflict occurred.")
            };
        }

        return EndpointSupport.Conflict(httpContext, "receiving_conflict", "Receiving conflict occurred.");
    }

    private static IResult MapWorkflowFailure(HttpContext httpContext, ReceivingWorkflowOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "receiving_workflow_error";
        var detail = outcome.ErrorDetail ?? "Receiving operation failed.";
        return outcome.HttpStatus switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static async Task<GoodsReceiptDetailDto?> LoadGoodsReceiptDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid receiptId,
        bool wasIdempotentReplay,
        CancellationToken ct)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.GoodsReceiptId == receiptId, ct);

        if (receipt is null)
        {
            return null;
        }

        var lines = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.GoodsReceiptId == receipt.GoodsReceiptId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var dockChecks = await db.DockChecks.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.LegalEntityId == entityId && d.GoodsReceiptId == receipt.GoodsReceiptId)
            .OrderByDescending(d => d.CheckedAtUtc)
            .ToListAsync(ct);

        var po = await db.PurchaseOrders.AsNoTracking()
            .SingleAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PurchaseOrderId == receipt.PurchaseOrderId, ct);

        var approvedLineQtyByKey = po.CurrentApprovedRevisionId is { } appRevId && po.Status != PurchaseOrderStatuses.Cancelled
            ? await db.PurchaseOrderLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.RevisionId == appRevId)
                .ToDictionaryAsync(l => l.LineageLineKey, l => l.OrderedBaseQuantity, ct)
            : new Dictionary<string, decimal>(StringComparer.Ordinal);

        var allPoReceiptLines = await db.GoodsReceiptLines.AsNoTracking()
            .Where(gl => gl.TenantId == tenantId && gl.LegalEntityId == entityId && gl.PurchaseOrderId == receipt.PurchaseOrderId)
            .ToListAsync(ct);

        var actorIds = new HashSet<Guid> { receipt.ReceivedByUserId };
        foreach (var dc in dockChecks)
        {
            actorIds.Add(dc.CheckedByUserId);
        }

        var emails = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var receiptIdStr = receipt.GoodsReceiptId.ToString("D");
        var linkedGlJournals = await db.JournalEntries.AsNoTracking()
            .CountAsync(j =>
                j.TenantId == tenantId &&
                j.LegalEntityId == entityId &&
                j.SourceDocumentId == receiptIdStr,
                ct);

        var valuedAcceptancesRaw = await db.ValuedAcceptances.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.GoodsReceiptId == receipt.GoodsReceiptId)
            .OrderByDescending(a => a.AcceptanceDate)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var movementIds = valuedAcceptancesRaw.Where(a => a.StockMovementId.HasValue).Select(a => a.StockMovementId!.Value).Distinct().ToList();
        var accrualIds = valuedAcceptancesRaw.Where(a => a.ReceiptAccrualId.HasValue).Select(a => a.ReceiptAccrualId!.Value).Distinct().ToList();
        var journalIds = valuedAcceptancesRaw.Where(a => a.GlJournalId.HasValue).Select(a => a.GlJournalId!.Value).Distinct().ToList();

        var movementNumbers = movementIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movementIds.Contains(m.StockMovementId))
                .ToDictionaryAsync(m => m.StockMovementId, m => m.MovementNumber, ct);

        var accrualNumbers = accrualIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.ReceiptAccruals.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accrualIds.Contains(a.ReceiptAccrualId))
                .ToDictionaryAsync(a => a.ReceiptAccrualId, a => a.AccrualNumber, ct);

        var journalNumbers = journalIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && journalIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        var valuedAcceptanceDtos = valuedAcceptancesRaw.Select(a => new GoodsReceiptLinkedAcceptanceDto(
            a.ValuedAcceptanceId,
            a.AcceptanceNumber,
            a.AcceptanceDate,
            a.StockStatus,
            a.TotalAcceptedBaseQuantity,
            a.TotalAcceptedAmount,
            a.StockMovementId,
            a.StockMovementId is { } mid && movementNumbers.TryGetValue(mid, out var mn) ? mn : null,
            a.ReceiptAccrualId,
            a.ReceiptAccrualId is { } aid && accrualNumbers.TryGetValue(aid, out var an) ? an : null,
            a.GlJournalId,
            a.GlJournalId is { } jid && journalNumbers.TryGetValue(jid, out var jn) ? jn : null,
            a.CreatedAtUtc)).ToList();

        var lineDtos = lines.Select(l =>
        {
            var approvedCap = approvedLineQtyByKey.GetValueOrDefault(l.LineageLineKey, 0m);
            var otherReleased = allPoReceiptLines
                .Where(gl => gl.LineageLineKey == l.LineageLineKey && gl.GoodsReceiptLineId != l.GoodsReceiptLineId)
                .Sum(gl => gl.DockReleasedBaseQuantity);
            var maxCapForLine = Math.Max(0m, approvedCap - otherReleased);

            return new GoodsReceiptLineDto(
                l.GoodsReceiptLineId,
                l.LineNumber,
                l.PurchaseOrderId,
                l.PoRevisionId,
                l.PoLineId,
                l.LineageLineKey,
                l.ItemId,
                l.ItemCodeSnapshot,
                l.ItemNameSnapshot,
                l.ItemKindSnapshot,
                l.ReceivedUomId,
                l.ReceivedUomCodeSnapshot,
                l.BaseUomId,
                l.BaseUomCodeSnapshot,
                l.UomFactorToBaseSnapshot,
                l.PoOrderedBaseQuantitySnapshot,
                l.PhysicallyDeliveredQuantity,
                l.PhysicallyDeliveredBaseQuantity,
                l.PendingCheckBaseQuantity,
                l.DockHeldBaseQuantity,
                l.DockReleasedBaseQuantity,
                l.DockRejectedBaseQuantity,
                l.ValuedAcceptedBaseQuantity,
                l.UnresolvedCustodyBaseQuantity,
                l.OverdeliveryExcessBaseQuantity,
                l.CustodyStatus,
                l.DiscrepancyReasonCode,
                l.DiscrepancyNotes,
                l.ConditionEvidenceRef,
                maxCapForLine,
                l.UpdatedAtUtc);
        }).ToList();

        var dockCheckDtos = dockChecks.Select(d => new DockCheckHistoryDto(
            d.DockCheckId,
            d.GoodsReceiptLineId,
            d.PayloadHash,
            d.PreviousCustodyStatus,
            d.ResultingCustodyStatus,
            d.PendingCheckBaseQuantity,
            d.DockHeldBaseQuantity,
            d.DockReleasedBaseQuantity,
            d.DockRejectedBaseQuantity,
            d.DiscrepancyReasonCode,
            d.InspectionNotes,
            d.EvidenceReference,
            d.CheckedByUserId,
            emails.GetValueOrDefault(d.CheckedByUserId, d.CheckedByUserId.ToString("D")),
            d.CheckedAtUtc)).ToList();

        return new GoodsReceiptDetailDto(
            receipt.GoodsReceiptId,
            receipt.ReceiptNumber,
            receipt.PurchaseOrderId,
            receipt.PoRevisionId,
            receipt.PoNumberSnapshot,
            receipt.PoRevisionNumberSnapshot,
            receipt.PoSnapshotHash,
            receipt.SupplierPartnerId,
            receipt.SupplierCodeSnapshot,
            receipt.SupplierLegalNameSnapshot,
            receipt.SupplierDeliveryNoteNumber,
            receipt.IdempotencyKey,
            receipt.ArrivalPayloadHash,
            receipt.ReceivedDate,
            receipt.ReceivingStation,
            receipt.VehicleOrSealRef,
            receipt.DocumentEvidenceRef,
            receipt.Status,
            receipt.TotalDeliveredBaseQuantity,
            receipt.TotalRejectedBaseQuantity,
            receipt.TotalValuedAcceptedBaseQuantity,
            receipt.TotalUnresolvedCustodyBaseQuantity,
            receipt.TotalReleasedBaseQuantity,
            receipt.TotalHeldBaseQuantity,
            receipt.HasFinancialPosting,
            linkedGlJournals,
            receipt.Notes,
            receipt.ReceivedByUserId,
            emails.GetValueOrDefault(receipt.ReceivedByUserId, receipt.ReceivedByUserId.ToString("D")),
            receipt.CreatedAtUtc,
            receipt.UpdatedAtUtc,
            receipt.Version,
            wasIdempotentReplay,
            valuedAcceptanceDtos.Count(a => a.GlJournalId.HasValue),
            valuedAcceptanceDtos,
            lineDtos,
            dockCheckDtos);
    }
}
