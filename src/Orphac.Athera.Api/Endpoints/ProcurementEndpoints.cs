using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.Api.Endpoints;

public static class ProcurementEndpoints
{
    public static IEndpointRouteBuilder MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        MapReferenceRoutes(app);
        MapPurchaseOrderReadRoutes(app);
        MapPurchaseOrderMutationRoutes(app);
        return app;
    }

    private static void MapReferenceRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/reference-data", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteProcurementScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var suppliers = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.IsSupplier && p.Status == PartnerStatuses.Active)
                        .OrderBy(p => p.PartnerCode)
                        .ToListAsync(innerCt);

                    var supplierProfiles = await db.PartnerEntityProfiles.AsNoTracking()
                        .Where(pr => pr.TenantId == tenantId && pr.LegalEntityId == entityId)
                        .ToDictionaryAsync(pr => pr.PartnerId, innerCt);

                    var eligibleSuppliers = new List<ProcurementSupplierOptionDto>();
                    foreach (var s in suppliers)
                    {
                        supplierProfiles.TryGetValue(s.PartnerId, out var profile);
                        var (supOk, _) = PartnerEligibilityEvaluator.Evaluate(s, profile);
                        if (supOk)
                        {
                            eligibleSuppliers.Add(new ProcurementSupplierOptionDto(
                                s.PartnerId,
                                s.PartnerCode,
                                s.LegalName,
                                s.DisplayName,
                                s.TaxRegistrationNpwp,
                                s.TaxPkpStatus,
                                profile?.DefaultPaymentTermsDays ?? 30,
                                s.EntityScopeMode));
                        }
                    }

                    var items = await db.Items.AsNoTracking()
                        .Where(i => i.TenantId == tenantId && i.Status == CatalogStatuses.Active)
                        .OrderBy(i => i.ItemCode)
                        .ToListAsync(innerCt);

                    var itemPolicies = await db.ItemEntityPolicies.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
                        .ToDictionaryAsync(p => p.ItemId, innerCt);

                    var uoms = await db.UnitsOfMeasure.AsNoTracking()
                        .Where(u => u.TenantId == tenantId && u.Status == CatalogStatuses.Active)
                        .ToDictionaryAsync(u => u.UomId, innerCt);

                    var conversions = await db.ItemUomConversions.AsNoTracking()
                        .Where(c => c.TenantId == tenantId && c.Status == CatalogStatuses.Active)
                        .ToListAsync(innerCt);

                    var convByItem = conversions.GroupBy(c => c.ItemId).ToDictionary(g => g.Key, g => g.ToList());

                    var eligibleItems = new List<ProcurementItemOptionDto>();
                    foreach (var item in items)
                    {
                        itemPolicies.TryGetValue(item.ItemId, out var policy);
                        var (purchaseOk, _) = ItemEligibilityEvaluator.Evaluate(item, policy);
                        if (!purchaseOk || !uoms.TryGetValue(item.BaseUomId, out var baseUom))
                        {
                            continue;
                        }

                        var allowedUoms = new List<ProcurementItemUomOptionDto>
                        {
                            new(baseUom.UomId, baseUom.UomCode, baseUom.Name, 1m, baseUom.QuantityPrecisionScale)
                        };

                        if (convByItem.TryGetValue(item.ItemId, out var itemConvs))
                        {
                            foreach (var c in itemConvs)
                            {
                                if (c.UomId != baseUom.UomId && uoms.TryGetValue(c.UomId, out var u))
                                {
                                    allowedUoms.Add(new(u.UomId, u.UomCode, u.Name, c.FactorToBase, u.QuantityPrecisionScale));
                                }
                            }
                        }

                        eligibleItems.Add(new ProcurementItemOptionDto(
                            item.ItemId,
                            item.ItemCode,
                            item.Name,
                            item.ItemKind,
                            baseUom.UomId,
                            baseUom.UomCode,
                            item.DefaultPurchaseUomId,
                            allowedUoms.OrderBy(u => u.FactorToBase).ThenBy(u => u.UomCode).ToList()));
                    }

                    var taxCodes = await db.TaxCodes.AsNoTracking()
                        .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.Status == MasterStatuses.Active && t.Direction != Modules.Tax.TaxDirections.Output)
                        .OrderBy(t => t.Code)
                        .Select(t => new ProcurementTaxOptionDto(
                            t.TaxCodeId,
                            t.Code,
                            t.Name,
                            t.TaxFamily,
                            t.Direction,
                            t.NominalRatePercent,
                            t.TaxableBaseRatioNumerator,
                            t.TaxableBaseRatioDenominator,
                            t.RequiresCounterpartyNpwp,
                            t.EffectiveFrom,
                            t.EffectiveTo,
                            t.ReviewStatus))
                        .ToListAsync(innerCt);

                    var tolerancePolicies = await db.DeliveryTolerancePolicies.AsNoTracking()
                        .Where(tp => tp.TenantId == tenantId && tp.LegalEntityId == entityId && tp.Status == MasterStatuses.Active)
                        .OrderByDescending(tp => tp.IsDefault)
                        .ThenBy(tp => tp.Code)
                        .Select(tp => new DeliveryTolerancePolicyDto(
                            tp.PolicyId,
                            tp.Code,
                            tp.Name,
                            tp.Mode,
                            tp.OverdeliveryTolerancePercent,
                            tp.OverdeliveryToleranceBaseQty,
                            tp.AllowUnlimitedOverdelivery,
                            tp.ReviewStatus,
                            tp.IsDefault))
                        .ToListAsync(innerCt);

                    return Results.Ok(new ProcurementReferenceDataResponse(
                        eligibleSuppliers,
                        eligibleItems,
                        taxCodes,
                        tolerancePolicies,
                        "OD-01 Safe Default: Exact approved quantity only (0% tolerance, no unlimited overdelivery) until Product Owner sign-off (ATH-CR-003).",
                        "OD-02 Four-Eyes Policy: Every PO commitment and amendment requires an independent authorized approver (Submitter != Approver, ATH-CR-001)."));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetProcurementReferenceData")
        .Produces<ProcurementReferenceDataResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapPurchaseOrderReadRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? status,
            [FromQuery] string? search,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteProcurementScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var query = db.PurchaseOrders.AsNoTracking()
                        .Where(po => po.TenantId == tenantId && po.LegalEntityId == entityId);

                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        var normStatus = status.Trim().ToUpperInvariant();
                        query = query.Where(po => po.Status == normStatus);
                    }

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var term = search.Trim();
                        query = query.Where(po =>
                            EF.Functions.ILike(po.DraftCode, $"%{term}%") ||
                            (po.PoNumber != null && EF.Functions.ILike(po.PoNumber, $"%{term}%")) ||
                            EF.Functions.ILike(po.DeliveryDestination, $"%{term}%"));
                    }

                    var orders = await query
                        .OrderByDescending(po => po.UpdatedAtUtc)
                        .ThenByDescending(po => po.DraftCode)
                        .ToListAsync(innerCt);

                    if (orders.Count == 0)
                    {
                        return Results.Ok(new PurchaseOrderListResponse(Array.Empty<PurchaseOrderSummaryDto>()));
                    }

                    var supplierIds = orders.Select(o => o.SupplierPartnerId).Distinct().ToList();
                    var suppliers = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && supplierIds.Contains(p.PartnerId))
                        .ToDictionaryAsync(p => p.PartnerId, innerCt);

                    var poIds = orders.Select(o => o.PurchaseOrderId).ToList();
                    var revisions = await db.PurchaseOrderRevisions.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && poIds.Contains(r.PurchaseOrderId))
                        .ToListAsync(innerCt);

                    var revsByPo = revisions.GroupBy(r => r.PurchaseOrderId).ToDictionary(g => g.Key, g => g.ToList());

                    var summaries = new List<PurchaseOrderSummaryDto>(orders.Count);
                    foreach (var po in orders)
                    {
                        revsByPo.TryGetValue(po.PurchaseOrderId, out var poRevs);
                        poRevs ??= [];

                        var approvedRev = po.CurrentApprovedRevisionId is { } appRevId
                            ? poRevs.FirstOrDefault(r => r.RevisionId == appRevId)
                            : null;
                        var pendingRev = poRevs
                            .Where(r => r.Status == PurchaseOrderRevisionStatuses.PendingApproval)
                            .OrderByDescending(r => r.RevisionNumber)
                            .FirstOrDefault();
                        var openRevExists = poRevs.Any(r =>
                            r.Status == PurchaseOrderRevisionStatuses.Draft ||
                            r.Status == PurchaseOrderRevisionStatuses.PendingApproval);
                        var latestRev = poRevs.OrderByDescending(r => r.RevisionNumber).FirstOrDefault();

                        var displayRev = approvedRev ?? latestRev;
                        var approvedOrderedQty = approvedRev?.TotalOrderedBaseQuantity ?? 0m;
                        var receivingProgress = ReceivingProgressStatuses.Derive(
                            po.Status,
                            approvedRev is not null,
                            approvedOrderedQty,
                            po.AcceptedBaseQuantityTotal);

                        var remainingOpen = po.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled || approvedRev is null
                            ? 0m
                            : Math.Max(0m, approvedOrderedQty - po.AcceptedBaseQuantityTotal);

                        suppliers.TryGetValue(po.SupplierPartnerId, out var supplier);

                        summaries.Add(new PurchaseOrderSummaryDto(
                            po.PurchaseOrderId,
                            po.DraftCode,
                            po.PoNumber,
                            po.Status,
                            receivingProgress,
                            po.SupplierPartnerId,
                            supplier?.PartnerCode ?? displayRev?.SupplierCodeSnapshot ?? string.Empty,
                            supplier?.DisplayName ?? displayRev?.SupplierLegalNameSnapshot ?? string.Empty,
                            po.CurrencyCode,
                            po.OrderDate,
                            po.ExpectedDeliveryDate,
                            po.DeliveryDestination,
                            po.PaymentTermsDays,
                            po.CurrentApprovedRevisionNumber,
                            po.LatestRevisionNumber,
                            openRevExists,
                            pendingRev?.RevisionId,
                            pendingRev?.SubmittedByUserId,
                            displayRev?.GrandTotalAmount ?? 0m,
                            displayRev?.TotalOrderedBaseQuantity ?? 0m,
                            po.AcceptedBaseQuantityTotal,
                            remainingOpen,
                            po.DownstreamReferenceCount,
                            po.UpdatedAtUtc));
                    }

                    return Results.Ok(new PurchaseOrderListResponse(summaries));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListPurchaseOrders")
        .Produces<PurchaseOrderListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteProcurementScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await LoadPurchaseOrderDetailDtoAsync(db, tenantId, entityId, poId, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "po_not_found", "Purchase order was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetPurchaseOrderDetail")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapPurchaseOrderMutationRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders", (
            Guid tenantId,
            Guid entityId,
            CreatePurchaseOrderRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: true,
                (scope, actorUserId, innerCt) => PurchaseOrderWorkflowService.CreateDraftAsync(
                    db,
                    new CreatePoDraftCommand(
                        tenantId,
                        entityId,
                        scope.Entity.FunctionalCurrencyCode,
                        request.CurrencyCode,
                        request.SupplierPartnerId,
                        request.OrderDate,
                        request.ExpectedDeliveryDate,
                        request.DeliveryDestination,
                        request.PaymentTermsDays,
                        request.TolerancePolicyId,
                        request.Notes,
                        MapLineInputs(request.Lines),
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("CreatePurchaseOrder")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/revisions/{revisionId:guid}", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            Guid revisionId,
            UpdatePurchaseOrderRevisionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.UpdateDraftRevisionAsync(
                    db,
                    new UpdatePoDraftCommand(
                        tenantId,
                        entityId,
                        poId,
                        revisionId,
                        request.SupplierPartnerId,
                        request.OrderDate,
                        request.ExpectedDeliveryDate,
                        request.DeliveryDestination,
                        request.PaymentTermsDays,
                        request.TolerancePolicyId,
                        request.ChangeReason,
                        request.Notes,
                        MapLineInputs(request.Lines),
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("UpdatePurchaseOrderDraftRevision")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/revisions", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            CreatePoAmendmentRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: true,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.CreateAmendmentRevisionAsync(
                    db,
                    new CreatePoAmendmentCommand(
                        tenantId,
                        entityId,
                        poId,
                        request.ChangeReason,
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("CreatePurchaseOrderAmendmentRevision")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/revisions/{revisionId:guid}/submit", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            Guid revisionId,
            SubmitPoRevisionRequest? request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.SubmitRevisionAsync(
                    db,
                    new SubmitPoRevisionCommand(
                        tenantId,
                        entityId,
                        poId,
                        revisionId,
                        request?.PoNumberOverride,
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("SubmitPurchaseOrderRevision")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/revisions/{revisionId:guid}/decide", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            Guid revisionId,
            DecidePoRevisionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.DecideRevisionAsync(
                    db,
                    new DecidePoRevisionCommand(
                        tenantId,
                        entityId,
                        poId,
                        revisionId,
                        request.Decision,
                        request.Reason,
                        actorUserId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("DecidePurchaseOrderRevision")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/cancel", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            CloseOrCancelPoRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.CancelPurchaseOrderAsync(
                    db,
                    new CloseOrCancelPoCommand(tenantId, entityId, poId, request.Reason, actorUserId, DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("CancelPurchaseOrder")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}/short-close", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            CloseOrCancelPoRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorUserId, innerCt) => PurchaseOrderWorkflowService.ShortClosePurchaseOrderAsync(
                    db,
                    new CloseOrCancelPoCommand(tenantId, entityId, poId, request.Reason, actorUserId, DateTimeOffset.UtcNow),
                    innerCt),
                ct))
        .RequireAuthorization()
        .WithName("ShortClosePurchaseOrder")
        .Produces<PurchaseOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapDelete("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/procurement/purchase-orders/{poId:guid}", (
            Guid tenantId,
            Guid entityId,
            Guid poId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteProcurementScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ProcurementManage],
                userManager,
                scopeExecutor,
                httpContext,
                (_, _, _) => Task.FromResult(EndpointSupport.Conflict(
                    httpContext,
                    "po_delete_forbidden",
                    "Purchase orders are immutable commercial records and cannot be deleted; use cancel (no downstream refs) or authorized short-close.")),
                ct))
        .RequireAuthorization()
        .WithName("DeletePurchaseOrderForbidden")
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ExecuteProcurementScopeAsync(
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
            requiredPermission: null,
            async (scope, innerCt) =>
            {
                if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                {
                    return EndpointSupport.Forbidden(
                        httpContext,
                        "insufficient_entity_permission",
                        "Your active grants for this legal entity do not permit this procurement operation.");
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        return result.IsAllowed
            ? result.Value!
            : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static async Task<IResult> ExecuteProcurementMutationAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        AtheraDbContext db,
        HttpContext httpContext,
        bool returnCreated,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<PoWorkflowOutcome>> action,
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
                requiredPermission: null,
                async (scope, innerCt) =>
                {
                    if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                    {
                        return (Result: EndpointSupport.Forbidden(
                            httpContext,
                            "insufficient_entity_permission",
                            "Your active grants for this legal entity do not permit this procurement operation."),
                            Committed: false);
                    }

                    var outcome = await action(scope, actorUserId, innerCt);
                    if (!outcome.Succeeded || outcome.PurchaseOrder is null)
                    {
                        throw new AbortedWorkflowException(outcome);
                    }

                    var detail = await LoadPurchaseOrderDetailDtoAsync(db, tenantId, entityId, outcome.PurchaseOrder.PurchaseOrderId, innerCt);
                    var okResult = returnCreated
                        ? Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/procurement/purchase-orders/{outcome.PurchaseOrder.PurchaseOrderId}",
                            detail)
                        : Results.Ok(detail);

                    return (Result: okResult, Committed: true);
                },
                ct);

            if (!result.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            if (result.Value.Committed)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return result.Value.Result;
        }
        catch (AbortedWorkflowException aborted)
        {
            return MapWorkflowFailure(httpContext, aborted.Outcome);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsUniqueConstraintViolation(ex))
        {
            return MapPersistenceConflict(httpContext, ex);
        }
    }

    private sealed class AbortedWorkflowException(PoWorkflowOutcome outcome) : Exception
    {
        public PoWorkflowOutcome Outcome { get; } = outcome;
    }

    private static IReadOnlyList<PoLineInput> MapLineInputs(IReadOnlyList<PoLineInputDto>? dtos) =>
        (dtos ?? Array.Empty<PoLineInputDto>())
        .Select(d => new PoLineInput(
            d.LineageLineKey,
            d.ItemId,
            d.OrderedUomId,
            d.OrderedQuantity,
            d.UnitPrice,
            d.TaxCodeId,
            d.OverdeliveryTolerancePercent,
            d.OverdeliveryToleranceBaseQty,
            d.AllowUnlimitedOverdelivery,
            d.LineDescription))
        .ToList();

    private static bool IsUniqueConstraintViolation(Exception ex) =>
        ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;

    private static IResult MapPersistenceConflict(HttpContext httpContext, Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return EndpointSupport.Conflict(
                httpContext,
                "concurrency_conflict",
                "This purchase order or revision was modified concurrently; reload and retry.");
        }

        if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return pgEx.ConstraintName switch
            {
                "ux_purchase_orders_entity_po_number" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_po_number",
                    "This PO number is already assigned in this legal entity."),
                "ux_document_approvals_source_revision" or "ux_po_revisions_approval_record" => EndpointSupport.Conflict(
                    httpContext,
                    "approval_already_decided",
                    "An active or decided approval already exists for this PO revision."),
                _ => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_po_conflict",
                    "Duplicate procurement record conflict detected.")
            };
        }

        return EndpointSupport.Conflict(httpContext, "po_conflict", "Procurement conflict occurred.");
    }

    private static IResult MapWorkflowFailure(HttpContext httpContext, PoWorkflowOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "po_workflow_error";
        var detail = outcome.ErrorDetail ?? "Procurement operation failed.";
        return outcome.HttpStatus switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static async Task<PurchaseOrderDetailDto?> LoadPurchaseOrderDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid poId,
        CancellationToken ct)
    {
        var po = await db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PurchaseOrderId == poId, ct);

        if (po is null)
        {
            return null;
        }

        var supplier = await db.Partners.AsNoTracking()
            .SingleAsync(p => p.TenantId == tenantId && p.PartnerId == po.SupplierPartnerId, ct);

        var revisions = await db.PurchaseOrderRevisions.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.PurchaseOrderId == po.PurchaseOrderId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync(ct);

        var revIds = revisions.Select(r => r.RevisionId).ToList();
        var lines = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && revIds.Contains(l.RevisionId))
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var approvals = await db.DocumentApprovals.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.SourceModule == "PROCUREMENT" && a.SourceDocumentId == po.PurchaseOrderId)
            .ToDictionaryAsync(a => a.SourceRevisionId, ct);

        var actorIds = approvals.Values
            .SelectMany(a => new[] { a.RequestedByUserId, a.DecidedByUserId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var emails = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var revMap = revisions.ToDictionary(r => r.RevisionId);
        var linesByRev = lines.GroupBy(l => l.RevisionId).ToDictionary(g => g.Key, g => g.ToList());

        var revisionDtos = new List<PurchaseOrderRevisionDto>(revisions.Count);
        foreach (var rev in revisions)
        {
            linesByRev.TryGetValue(rev.RevisionId, out var revLines);
            revLines ??= [];

            approvals.TryGetValue(rev.RevisionId, out var app);
            DocumentApprovalDto? appDto = app is null
                ? null
                : new DocumentApprovalDto(
                    app.ApprovalId,
                    app.PolicyId,
                    app.Status,
                    app.RequestSummary,
                    app.RequestedByUserId,
                    emails.GetValueOrDefault(app.RequestedByUserId, app.RequestedByUserId.ToString("D")),
                    app.RequestedAtUtc,
                    app.DecidedByUserId,
                    app.DecidedByUserId is { } did ? emails.GetValueOrDefault(did, did.ToString("D")) : null,
                    app.DecidedAtUtc,
                    app.DecisionReason);

            PurchaseOrderRevision? baselineRev = rev.SupersedesRevisionId is { } prevId && revMap.TryGetValue(prevId, out var foundBase)
                ? foundBase
                : revisions.FirstOrDefault(r => r.RevisionNumber == rev.RevisionNumber - 1);

            IReadOnlyList<PurchaseOrderLine>? baselineLines = baselineRev is not null && linesByRev.TryGetValue(baselineRev.RevisionId, out var bl)
                ? bl
                : null;

            var diff = PurchaseOrderDiffCalculator.Compute(rev, revLines, baselineRev, baselineLines);
            var diffDto = new PoRevisionDiffReportDto(
                diff.TargetRevisionNumber,
                diff.BaselineRevisionNumber,
                diff.HeaderChanges.Select(h => new PoHeaderFieldDiffDto(h.FieldKey, h.PreviousValue, h.CurrentValue)).ToList(),
                diff.LineChanges.Select(lc => new PoLineDiffItemDto(
                    lc.LineageLineKey,
                    lc.ChangeKind,
                    lc.PreviousItemCode,
                    lc.CurrentItemCode,
                    lc.PreviousQuantity,
                    lc.CurrentQuantity,
                    lc.PreviousUomCode,
                    lc.CurrentUomCode,
                    lc.PreviousUnitPrice,
                    lc.CurrentUnitPrice,
                    lc.PreviousSubtotal,
                    lc.CurrentSubtotal)).ToList(),
                diff.PreviousGrandTotal,
                diff.CurrentGrandTotal,
                diff.GrandTotalDelta,
                diff.PreviousOrderedBaseQty,
                diff.CurrentOrderedBaseQty,
                diff.OrderedBaseQtyDelta);

            revisionDtos.Add(new PurchaseOrderRevisionDto(
                rev.RevisionId,
                rev.RevisionNumber,
                rev.SupersedesRevisionId,
                rev.Status,
                rev.IsFrozen,
                rev.SupplierPartnerId,
                rev.SupplierCodeSnapshot,
                rev.SupplierLegalNameSnapshot,
                rev.SupplierNpwpSnapshot,
                rev.SupplierPkpStatusSnapshot,
                rev.CurrencyCode,
                rev.OrderDate,
                rev.ExpectedDeliveryDate,
                rev.DeliveryDestination,
                rev.PaymentTermsDays,
                rev.TolerancePolicyId,
                rev.ToleranceModeSnapshot,
                rev.ChangeReason,
                rev.Notes,
                rev.SubtotalAmount,
                rev.PpnTaxTotalAmount,
                rev.WithholdingTaxTotalAmount,
                rev.GrandTotalAmount,
                rev.TotalOrderedBaseQuantity,
                rev.SnapshotHash,
                rev.CreatedByUserId,
                rev.CreatedAtUtc,
                rev.SubmittedByUserId,
                rev.SubmittedAtUtc,
                rev.DecidedByUserId,
                rev.DecidedAtUtc,
                rev.DecisionReason,
                rev.Version,
                revLines.Select(l => new PurchaseOrderLineDto(
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
                    l.UomFactorToBaseSnapshot,
                    l.OrderedQuantity,
                    l.OrderedBaseQuantity,
                    l.UnitPrice,
                    l.LineSubtotalAmount,
                    l.TaxCodeId,
                    l.TaxCodeSnapshot,
                    l.TaxFamilySnapshot,
                    l.TaxRatePercentSnapshot,
                    l.TaxableBaseAmount,
                    l.TaxAmount,
                    l.OverdeliveryTolerancePercent,
                    l.OverdeliveryToleranceBaseQty,
                    l.AllowUnlimitedOverdelivery,
                    l.LineDescription)).ToList(),
                appDto,
                diffDto));
        }

        var approvedRev = po.CurrentApprovedRevisionId is { } appId && revMap.TryGetValue(appId, out var aRev) ? aRev : null;
        var approvedBaseQty = approvedRev?.TotalOrderedBaseQuantity ?? 0m;
        var receivingProgress = ReceivingProgressStatuses.Derive(
            po.Status,
            approvedRev is not null,
            approvedBaseQty,
            po.AcceptedBaseQuantityTotal);

        var remainingOpen = po.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled || approvedRev is null
            ? 0m
            : Math.Max(0m, approvedBaseQty - po.AcceptedBaseQuantityTotal);

        return new PurchaseOrderDetailDto(
            po.PurchaseOrderId,
            po.DraftCode,
            po.PoNumber,
            po.Status,
            receivingProgress,
            po.SupplierPartnerId,
            supplier.PartnerCode,
            supplier.DisplayName,
            po.CurrencyCode,
            po.OrderDate,
            po.ExpectedDeliveryDate,
            po.DeliveryDestination,
            po.PaymentTermsDays,
            po.CurrentApprovedRevisionId,
            po.CurrentApprovedRevisionNumber,
            po.LatestRevisionNumber,
            po.DownstreamReferenceCount,
            approvedBaseQty,
            po.AcceptedBaseQuantityTotal,
            remainingOpen,
            po.ClosedByUserId,
            po.ClosedAtUtc,
            po.ClosureReason,
            po.CancelledByUserId,
            po.CancelledAtUtc,
            po.CancellationReason,
            po.CreatedByUserId,
            po.CreatedAtUtc,
            po.UpdatedAtUtc,
            po.Version,
            revisionDtos);
    }
}
