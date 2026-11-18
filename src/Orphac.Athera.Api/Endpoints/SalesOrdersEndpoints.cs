using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Endpoints;

public static class SalesOrdersEndpoints
{
    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.SalesRead,
        PermissionCodes.SalesManage,
        PermissionCodes.SalesApprove
    ];

    public static IEndpointRouteBuilder MapSalesOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        MapReferenceRoutes(app);
        MapReadRoutes(app);
        MapMutationRoutes(app);
        return app;
    }

    private static void MapReferenceRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/reference-data", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteSalesScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var customers = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.IsCustomer && p.Status == PartnerStatuses.Active)
                        .OrderBy(p => p.PartnerCode)
                        .ToListAsync(innerCt);

                    var customerProfiles = await db.PartnerEntityProfiles.AsNoTracking()
                        .Where(pr => pr.TenantId == tenantId && pr.LegalEntityId == entityId)
                        .ToDictionaryAsync(pr => pr.PartnerId, innerCt);

                    var eligibleCustomers = new List<SalesCustomerOptionDto>();
                    foreach (var c in customers)
                    {
                        customerProfiles.TryGetValue(c.PartnerId, out var profile);
                        var (_, custOk) = PartnerEligibilityEvaluator.Evaluate(c, profile);
                        if (custOk)
                        {
                            eligibleCustomers.Add(new SalesCustomerOptionDto(
                                c.PartnerId,
                                c.PartnerCode,
                                c.LegalName,
                                c.DisplayName,
                                c.TaxRegistrationNpwp,
                                c.TaxPkpStatus,
                                profile?.DefaultPaymentTermsDays ?? 30,
                                c.EntityScopeMode));
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
                    var availabilityByItem = await InventoryAvailabilityService.GetItemAvailabilityMapAsync(db, tenantId, entityId, innerCt);

                    var eligibleItems = new List<SalesItemOptionDto>();
                    foreach (var item in items)
                    {
                        itemPolicies.TryGetValue(item.ItemId, out var policy);
                        var (_, salesOk) = ItemEligibilityEvaluator.Evaluate(item, policy);
                        if (!salesOk || !uoms.TryGetValue(item.BaseUomId, out var baseUom))
                        {
                            continue;
                        }

                        var allowedUoms = new List<SalesItemUomOptionDto>
                        {
                            new(baseUom.UomId, baseUom.UomCode, baseUom.Name, 1m, baseUom.QuantityPrecisionScale)
                        };

                        if (convByItem.TryGetValue(item.ItemId, out var itemConvs))
                        {
                            foreach (var conv in itemConvs)
                            {
                                if (conv.UomId != baseUom.UomId && uoms.TryGetValue(conv.UomId, out var u))
                                {
                                    allowedUoms.Add(new(u.UomId, u.UomCode, u.Name, conv.FactorToBase, u.QuantityPrecisionScale));
                                }
                            }
                        }

                        availabilityByItem.TryGetValue(item.ItemId, out var avail);
                        var availDto = avail is null
                            ? new SalesItemAvailabilityDto(0m, 0m, 0m, 0m)
                            : new SalesItemAvailabilityDto(
                                avail.UnrestrictedBaseQuantity,
                                avail.ReservedBaseQuantity,
                                avail.AvailableBaseQuantity,
                                avail.TotalValuedOnHandBaseQuantity);

                        eligibleItems.Add(new SalesItemOptionDto(
                            item.ItemId,
                            item.ItemCode,
                            item.Name,
                            item.ItemKind,
                            baseUom.UomId,
                            baseUom.UomCode,
                            item.DefaultSalesUomId,
                            allowedUoms.OrderBy(u => u.FactorToBase).ThenBy(u => u.UomCode).ToList(),
                            availDto));
                    }

                    var outputTaxCodes = await db.TaxCodes.AsNoTracking()
                        .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.Status == MasterStatuses.Active && t.Direction == TaxDirections.Output)
                        .OrderBy(t => t.Code)
                        .Select(t => new SalesTaxOptionDto(
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
                            t.ReviewStatus))
                        .ToListAsync(innerCt);

                    return Results.Ok(new SalesOrdersReferenceDataResponse(
                        eligibleCustomers,
                        eligibleItems,
                        outputTaxCodes,
                        "OD-09 Safe Default (ATH-CR-009): Positive ordered quantities and unit prices required; discounts (> 0) and credit-limit overrides remain blocked pending Owner sign-off.",
                        "OD-02 Four-Eyes Policy (ATH-CR-001): Submitting a sales order freezes the commercial snapshot without posting inventory/AR; independent approval (Submitter != Approver) commits SO- numbering."));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetSalesOrdersReferenceData")
        .Produces<SalesOrdersReferenceDataResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapReadRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders", async (
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
            return await ExecuteSalesScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var query = db.SalesOrders.AsNoTracking()
                        .Where(o => o.TenantId == tenantId && o.LegalEntityId == entityId);

                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        var normStatus = status.Trim().ToUpperInvariant();
                        query = query.Where(o => o.Status == normStatus);
                    }

                    var orders = await query
                        .OrderByDescending(o => o.UpdatedAtUtc)
                        .Take(100)
                        .ToListAsync(innerCt);

                    var customerIds = orders.Select(o => o.CustomerPartnerId).Distinct().ToList();
                    var customers = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && customerIds.Contains(p.PartnerId))
                        .ToDictionaryAsync(p => p.PartnerId, innerCt);

                    var orderIds = orders.Select(o => o.SalesOrderId).ToList();
                    var revisions = orderIds.Count == 0
                        ? new List<SalesOrderRevision>()
                        : await db.SalesOrderRevisions.AsNoTracking()
                            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && orderIds.Contains(r.SalesOrderId))
                            .ToListAsync(innerCt);

                    var revByOrder = revisions.GroupBy(r => r.SalesOrderId).ToDictionary(g => g.Key, g => g.ToList());
                    var term = search?.Trim();
                    var summaries = new List<SalesOrderSummaryDto>(orders.Count);

                    foreach (var so in orders)
                    {
                        customers.TryGetValue(so.CustomerPartnerId, out var cust);
                        if (!string.IsNullOrWhiteSpace(term))
                        {
                            var match = (so.SoNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                                || so.DraftCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || (so.CustomerReference?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                                || (cust?.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                                || (cust?.PartnerCode.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
                            if (!match) continue;
                        }

                        revByOrder.TryGetValue(so.SalesOrderId, out var soRevs);
                        soRevs ??= [];

                        var approvedRev = so.CurrentApprovedRevisionId is { } appId
                            ? soRevs.FirstOrDefault(r => r.RevisionId == appId)
                            : null;
                        var pendingRev = soRevs.FirstOrDefault(r => r.Status == SalesOrderRevisionStatuses.PendingApproval);
                        var openRevExists = soRevs.Any(r =>
                            r.Status is SalesOrderRevisionStatuses.Draft or SalesOrderRevisionStatuses.PendingApproval);
                        var latestRev = soRevs.OrderByDescending(r => r.RevisionNumber).FirstOrDefault();

                        var displayRev = approvedRev ?? latestRev;
                        var approvedBaseQty = approvedRev?.TotalOrderedBaseQuantity ?? 0m;
                        var progress = SalesFulfillmentProgressStatuses.Derive(
                            so.Status,
                            approvedRev is not null,
                            approvedBaseQty,
                            so.TotalReservedBaseQuantity,
                            so.TotalDispatchedBaseQuantity);

                        summaries.Add(new SalesOrderSummaryDto(
                            so.SalesOrderId,
                            so.DraftCode,
                            so.SoNumber,
                            so.Status,
                            progress,
                            so.CustomerPartnerId,
                            cust?.PartnerCode ?? displayRev?.CustomerCodeSnapshot ?? string.Empty,
                            cust?.DisplayName ?? displayRev?.CustomerLegalNameSnapshot ?? string.Empty,
                            so.CurrencyCode,
                            so.OrderDate,
                            so.RequestedDeliveryDate,
                            so.DeliveryAddressSnapshot,
                            so.PaymentTermsDays,
                            so.CustomerReference,
                            so.CurrentApprovedRevisionNumber,
                            so.LatestRevisionNumber,
                            openRevExists,
                            pendingRev?.RevisionId,
                            pendingRev?.SubmittedByUserId,
                            displayRev?.SubtotalAmount ?? 0m,
                            displayRev?.OutputTaxTotalAmount ?? 0m,
                            displayRev?.GrandTotalAmount ?? 0m,
                            displayRev?.TotalOrderedBaseQuantity ?? 0m,
                            so.TotalReservedBaseQuantity,
                            so.TotalDispatchedBaseQuantity,
                            so.TotalBilledBaseQuantity,
                            so.UpdatedAtUtc,
                            pendingRev?.GrandTotalAmount));
                    }

                    return Results.Ok(new SalesOrderListResponse(summaries));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListSalesOrders")
        .Produces<SalesOrderListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteSalesScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await LoadSalesOrderDetailDtoAsync(db, tenantId, entityId, salesOrderId, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "so_not_found", "Sales order was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetSalesOrderDetail")
        .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapMutationRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders", (
            Guid tenantId,
            Guid entityId,
            CreateSalesOrderRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: true,
                (scope, actorId, innerCt) => SalesOrderCoordinator.CreateDraftAsync(
                    db,
                    new CreateSoDraftCommand(
                        tenantId,
                        entityId,
                        scope.Entity.FunctionalCurrencyCode,
                        request.CurrencyCode,
                        request.CustomerPartnerId,
                        request.OrderDate,
                        request.RequestedDeliveryDate,
                        request.DeliveryAddressSnapshot,
                        request.PaymentTermsDays,
                        request.CustomerReference,
                        request.CreditLimitOverrideRequested,
                        request.Notes,
                        MapLineInputs(request.Lines),
                        actorId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("CreateSalesOrder")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/revisions/{revisionId:guid}", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            Guid revisionId,
            UpdateSalesOrderRevisionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorId, innerCt) => SalesOrderCoordinator.UpdateDraftAsync(
                    db,
                    new UpdateSoDraftCommand(
                        tenantId,
                        entityId,
                        salesOrderId,
                        revisionId,
                        request.CustomerPartnerId,
                        request.OrderDate,
                        request.RequestedDeliveryDate,
                        request.DeliveryAddressSnapshot,
                        request.PaymentTermsDays,
                        request.CustomerReference,
                        request.CreditLimitOverrideRequested,
                        request.ChangeReason,
                        request.Notes,
                        MapLineInputs(request.Lines),
                        actorId,
                        DateTimeOffset.UtcNow),
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("UpdateSalesOrderRevision")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/revisions", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            CreateSoAmendmentRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: true,
                (_, actorId, innerCt) => SalesOrderCoordinator.CreateAmendmentAsync(
                    db,
                    new CreateSoAmendmentCommand(tenantId, entityId, salesOrderId, request.ChangeReason, actorId, DateTimeOffset.UtcNow),
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("CreateSalesOrderAmendment")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/revisions/{revisionId:guid}/submit", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            Guid revisionId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorId, innerCt) => SalesOrderCoordinator.SubmitRevisionAsync(
                    db,
                    new SubmitSoRevisionCommand(tenantId, entityId, salesOrderId, revisionId, actorId, DateTimeOffset.UtcNow),
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("SubmitSalesOrderRevision")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/revisions/{revisionId:guid}/decide", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            Guid revisionId,
            DecideSoRevisionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesApprove],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorId, innerCt) => SalesOrderCoordinator.DecideRevisionAsync(
                    db,
                    new DecideSoRevisionCommand(tenantId, entityId, salesOrderId, revisionId, request.Decision, request.Reason, request.SoNumberOverride, actorId, DateTimeOffset.UtcNow),
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("DecideSalesOrderRevision")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/cancel", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            CloseOrCancelSoRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorId, innerCt) => SalesOrderCoordinator.CloseOrCancelAsync(
                    db,
                    new CloseOrCancelSoCommand(tenantId, entityId, salesOrderId, request.Reason, actorId, DateTimeOffset.UtcNow),
                    isClose: false,
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("CancelSalesOrder")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}/close", (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            CloseOrCancelSoRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            ExecuteSalesMutationAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                outboxSignal,
                db,
                httpContext,
                returnCreated: false,
                (_, actorId, innerCt) => SalesOrderCoordinator.CloseOrCancelAsync(
                    db,
                    new CloseOrCancelSoCommand(tenantId, entityId, salesOrderId, request.Reason, actorId, DateTimeOffset.UtcNow),
                    isClose: true,
                    innerCt),
                ct))
            .RequireAuthorization()
            .WithName("CloseSalesOrder")
            .Produces<SalesOrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapDelete("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/sales-orders/{salesOrderId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid salesOrderId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteSalesScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.SalesManage],
                userManager,
                scopeExecutor,
                httpContext,
                (_, _, _) => Task.FromResult(EndpointSupport.Conflict(
                    httpContext,
                    "so_delete_forbidden",
                    "Sales orders are governed by an immutable audit trail and cannot be deleted; cancel or short-close instead.")),
                ct);
        })
        .RequireAuthorization()
        .WithName("DeleteSalesOrderForbidden")
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ExecuteSalesScopeAsync(
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
                        "Your active grants for this legal entity do not permit this sales order operation.");
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        return !result.IsAllowed
            ? EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode)
            : result.Value!;
    }

    private static async Task<IResult> ExecuteSalesMutationAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        AtheraDbContext db,
        HttpContext httpContext,
        bool returnCreated,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<SalesWorkflowOutcome>> action,
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
                            "Your active grants for this legal entity do not permit this sales order operation."),
                            Committed: false);
                    }

                    var outcome = await action(scope, actorUserId, innerCt);
                    if (!outcome.Succeeded || outcome.SalesOrder is null)
                    {
                        throw new AbortedWorkflowException(outcome);
                    }

                    var detail = await LoadSalesOrderDetailDtoAsync(db, tenantId, entityId, outcome.SalesOrder.SalesOrderId, innerCt);
                    var okResult = returnCreated
                        ? Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/sales-orders/{outcome.SalesOrder.SalesOrderId}",
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

    private sealed class AbortedWorkflowException(SalesWorkflowOutcome outcome) : Exception
    {
        public SalesWorkflowOutcome Outcome { get; } = outcome;
    }

    private static IReadOnlyList<SoLineInput> MapLineInputs(IReadOnlyList<SoLineInputDto>? dtos) =>
        (dtos ?? Array.Empty<SoLineInputDto>())
        .Select(d => new SoLineInput(
            d.LineageLineKey,
            d.ItemId,
            d.OrderedUomId,
            d.OrderedQuantity,
            d.UnitPrice,
            d.DiscountAmount,
            d.TaxCodeId,
            d.RequestedDeliveryDate,
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
                "This sales order or revision was modified concurrently; reload and retry.");
        }

        if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return pgEx.ConstraintName switch
            {
                "ux_sales_orders_entity_so_number" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_so_number",
                    "This SO number is already assigned in this legal entity."),
                "ux_document_approvals_source_revision" or "ux_so_revisions_approval_record" => EndpointSupport.Conflict(
                    httpContext,
                    "approval_already_decided",
                    "An active or decided approval already exists for this SO revision."),
                _ => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_so_conflict",
                    "Duplicate sales order conflict detected.")
            };
        }

        return EndpointSupport.Conflict(httpContext, "so_conflict", "Sales order conflict occurred.");
    }

    private static IResult MapWorkflowFailure(HttpContext httpContext, SalesWorkflowOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "so_workflow_error";
        var detail = outcome.ErrorDetail ?? "Sales order operation failed.";
        return outcome.HttpStatus switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            StatusCodes.Status422UnprocessableEntity => ProblemResults.Create(httpContext, StatusCodes.Status422UnprocessableEntity, code, "Policy validation failed", detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static async Task<SalesOrderDetailDto?> LoadSalesOrderDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid salesOrderId,
        CancellationToken ct)
    {
        var so = await db.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.TenantId == tenantId && o.LegalEntityId == entityId && o.SalesOrderId == salesOrderId, ct);
        if (so is null)
        {
            return null;
        }

        var customer = await db.Partners.AsNoTracking()
            .SingleAsync(p => p.TenantId == tenantId && p.PartnerId == so.CustomerPartnerId, ct);

        var revisions = await db.SalesOrderRevisions.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.SalesOrderId == so.SalesOrderId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync(ct);

        var revIds = revisions.Select(r => r.RevisionId).ToList();
        var lines = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && revIds.Contains(l.RevisionId))
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var approvalIds = revisions
            .Where(r => r.ApprovalRecordId.HasValue)
            .Select(r => r.ApprovalRecordId!.Value)
            .Distinct()
            .ToList();

        var approvals = approvalIds.Count == 0
            ? new Dictionary<Guid, DocumentApproval>()
            : await db.DocumentApprovals.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && approvalIds.Contains(a.ApprovalId))
                .ToDictionaryAsync(a => a.ApprovalId, ct);

        var actorIds = approvals.Values
            .Select(a => a.RequestedByUserId)
            .Concat(approvals.Values.Where(a => a.DecidedByUserId.HasValue).Select(a => a.DecidedByUserId!.Value))
            .Distinct()
            .ToList();

        var userEmails = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id.ToString("D"), ct);

        var linesByRev = lines.GroupBy(l => l.RevisionId).ToDictionary(g => g.Key, g => (IReadOnlyList<SalesOrderLine>)g.ToList());
        var revMap = revisions.ToDictionary(r => r.RevisionId);
        var revDtos = new List<SalesOrderRevisionDto>(revisions.Count);

        foreach (var rev in revisions)
        {
            linesByRev.TryGetValue(rev.RevisionId, out var revLines);
            revLines ??= Array.Empty<SalesOrderLine>();

            DocumentApprovalDto? appDto = null;
            if (rev.ApprovalRecordId is { } aid && approvals.TryGetValue(aid, out var app))
            {
                userEmails.TryGetValue(app.RequestedByUserId, out var reqEmail);
                string? decEmail = null;
                if (app.DecidedByUserId is { } did)
                {
                    userEmails.TryGetValue(did, out decEmail);
                }

                appDto = new DocumentApprovalDto(
                    app.ApprovalId,
                    app.PolicyId,
                    app.Status,
                    app.RequestSummary,
                    app.RequestedByUserId,
                    reqEmail ?? app.RequestedByUserId.ToString("D"),
                    app.RequestedAtUtc,
                    app.DecidedByUserId,
                    decEmail,
                    app.DecidedAtUtc,
                    app.DecisionReason);
            }

            SoRevisionDiffReportDto? diffDto = null;
            if (rev.SupersedesRevisionId is { } baseId && revMap.TryGetValue(baseId, out var baseRev))
            {
                linesByRev.TryGetValue(baseRev.RevisionId, out var baseLines);
                var diff = SalesOrderDiffCalculator.Compute(rev, revLines, baseRev, baseLines);
                diffDto = new SoRevisionDiffReportDto(
                    diff.TargetRevisionNumber,
                    diff.BaselineRevisionNumber,
                    diff.HeaderChanges.Select(h => new SoHeaderFieldDiffDto(h.Field, h.PreviousValue, h.CurrentValue)).ToList(),
                    diff.LineChanges.Select(l => new SoLineDiffItemDto(
                        l.LineageLineKey, l.ChangeKind, l.PreviousItemCode, l.CurrentItemCode,
                        l.PreviousQuantity, l.CurrentQuantity, l.PreviousUomCode, l.CurrentUomCode,
                        l.PreviousUnitPrice, l.CurrentUnitPrice, l.PreviousSubtotal, l.CurrentSubtotal)).ToList(),
                    diff.PreviousGrandTotal,
                    diff.CurrentGrandTotal,
                    diff.GrandTotalDelta,
                    diff.PreviousOrderedBaseQty,
                    diff.CurrentOrderedBaseQty,
                    diff.OrderedBaseQtyDelta);
            }

            revDtos.Add(new SalesOrderRevisionDto(
                rev.RevisionId,
                rev.RevisionNumber,
                rev.SupersedesRevisionId,
                rev.Status,
                rev.IsFrozen,
                rev.CustomerPartnerId,
                rev.CustomerCodeSnapshot,
                rev.CustomerLegalNameSnapshot,
                rev.CustomerNpwpSnapshot,
                rev.CustomerPkpStatusSnapshot,
                rev.CurrencyCode,
                rev.OrderDate,
                rev.RequestedDeliveryDate,
                rev.DeliveryAddressSnapshot,
                rev.PaymentTermsDays,
                rev.CustomerReference,
                rev.ChangeReason,
                rev.Notes,
                rev.SubtotalAmount,
                rev.OutputTaxTotalAmount,
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
                revLines.Select(l => new SalesOrderLineDto(
                    l.SoLineId,
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
                    l.ReservedBaseQuantity,
                    l.DispatchedBaseQuantity,
                    l.BilledBaseQuantity,
                    l.UnitPrice,
                    l.DiscountAmount,
                    l.LineSubtotalAmount,
                    l.TaxCodeId,
                    l.TaxCodeSnapshot,
                    l.TaxTreatmentClassSnapshot,
                    l.TaxRatePercentSnapshot,
                    l.TaxableBaseAmount,
                    l.TaxAmount,
                    l.LineTotalAmount,
                    l.RequestedDeliveryDate,
                    l.LineDescription)).ToList(),
                appDto,
                diffDto));
        }

        var approvedRev = so.CurrentApprovedRevisionId is { } appRevId && revMap.TryGetValue(appRevId, out var aRev) ? aRev : null;
        var approvedBaseQty = approvedRev?.TotalOrderedBaseQuantity ?? 0m;
        var progress = SalesFulfillmentProgressStatuses.Derive(
            so.Status,
            approvedRev is not null,
            approvedBaseQty,
            so.TotalReservedBaseQuantity,
            so.TotalDispatchedBaseQuantity);

        return new SalesOrderDetailDto(
            so.SalesOrderId,
            so.DraftCode,
            so.SoNumber,
            so.Status,
            progress,
            so.CustomerPartnerId,
            customer.PartnerCode,
            customer.DisplayName,
            so.CurrencyCode,
            so.OrderDate,
            so.RequestedDeliveryDate,
            so.DeliveryAddressSnapshot,
            so.PaymentTermsDays,
            so.CustomerReference,
            so.CurrentApprovedRevisionId,
            so.CurrentApprovedRevisionNumber,
            so.LatestRevisionNumber,
            approvedBaseQty,
            so.TotalReservedBaseQuantity,
            so.TotalDispatchedBaseQuantity,
            so.TotalBilledBaseQuantity,
            so.ClosedByUserId,
            so.ClosedAtUtc,
            so.ClosureReason,
            so.CancelledByUserId,
            so.CancelledAtUtc,
            so.CancellationReason,
            so.CreatedByUserId,
            so.CreatedAtUtc,
            so.UpdatedAtUtc,
            so.Version,
            revDtos);
    }
}
