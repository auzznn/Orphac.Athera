using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;

namespace Orphac.Athera.Api.Endpoints;

public static class FulfillmentEndpoints
{
    private const string Od10NoticeText =
        "OD-10 Safe Default (ATH-CR-005 / ATH-CR-010): Reservations and dispatches strictly require UNRESTRICTED stock in STORAGE/DISPATCH locations; QUALITY_INSPECTION, BLOCKED, expired lots, and negative balances are prohibited.";

    private const string Od09NoticeText =
        "OD-09 Safe Default (ATH-CR-009 / ATH-CR-010): Sales orders with active reservations or dispatched shipments cannot be cancelled or reduced below fulfilled quantities.";

    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.InventoryRead,
        PermissionCodes.InventoryManage,
        PermissionCodes.SalesRead,
        PermissionCodes.SalesManage,
        PermissionCodes.SalesApprove
    ];

    private static readonly string[] ManagePermissions =
    [
        PermissionCodes.InventoryManage
    ];

    public static IEndpointRouteBuilder MapFulfillmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/fulfillment")
            .WithTags("Fulfillment")
            .RequireAuthorization();

        group.MapGet("/overview", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(
                        db, tenantId, entityId, DateTimeOffset.UtcNow, innerCt);

                    var overview = await BuildFulfillmentOverviewAsync(db, tenantId, entityId, innerCt);
                    return Results.Ok(overview);
                },
                ct);
        })
        .WithName("GetFulfillmentOverview")
        .Produces<FulfillmentOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/reservations", async (
            Guid tenantId,
            Guid entityId,
            ReserveSalesOrderLineRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.ReserveSalesOrderLineAsync(
                        db,
                        new ReserveSalesOrderLineCommand(
                            tenantId,
                            entityId,
                            request.SalesOrderId,
                            request.SoLineId,
                            request.LocationId,
                            request.StockLotId,
                            request.QuantityBase,
                            request.IdempotencyKey,
                            request.Notes,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Reservation is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_reservation_failed", outcome.ErrorDetail ?? "Could not reserve stock for sales order line.");
                    }

                    var dto = await LoadReservationDtoAsync(db, tenantId, entityId, outcome.Reservation.StockReservationId, innerCt);
                    return outcome.HttpStatus == StatusCodes.Status201Created
                        ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/fulfillment/reservations/{dto.StockReservationId}", dto)
                        : Results.Ok(dto);
                },
                ct);
        })
        .WithName("ReserveSalesOrderLine")
        .Produces<SalesReservationDto>(StatusCodes.Status200OK)
        .Produces<SalesReservationDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/reservations/{reservationId:guid}/release", async (
            Guid tenantId,
            Guid entityId,
            Guid reservationId,
            ReleaseSalesReservationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.ReleaseSalesOrderReservationAsync(
                        db,
                        new ReleaseSalesOrderReservationCommand(
                            tenantId,
                            entityId,
                            reservationId,
                            request.ReleaseBaseQuantity,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Reservation is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_reservation_release_failed", outcome.ErrorDetail ?? "Could not release sales order stock reservation.");
                    }

                    var dto = await LoadReservationDtoAsync(db, tenantId, entityId, outcome.Reservation.StockReservationId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .WithName("ReleaseSalesOrderReservation")
        .Produces<SalesReservationDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/picks", async (
            Guid tenantId,
            Guid entityId,
            CreatePickOrderRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.CreatePickOrderAsync(
                        db,
                        new CreatePickOrderCommand(
                            tenantId,
                            entityId,
                            request.SalesOrderId,
                            request.ConfirmImmediately,
                            request.IdempotencyKey,
                            request.Notes,
                            (request.Lines ?? Array.Empty<PickLineInputDto>())
                                .Select(l => new PickLineInput(l.ReservationId, l.RequestedPickBaseQuantity, l.PickedBaseQuantity))
                                .ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.PickOrder is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_pick_failed", outcome.ErrorDetail ?? "Could not create warehouse pick order.");
                    }

                    var dto = await LoadPickOrderDtoAsync(db, tenantId, entityId, outcome.PickOrder.PickOrderId, innerCt);
                    return outcome.HttpStatus == StatusCodes.Status201Created
                        ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/fulfillment/picks/{dto.PickOrderId}", dto)
                        : Results.Ok(dto);
                },
                ct);
        })
        .WithName("CreatePickOrder")
        .Produces<PickOrderDto>(StatusCodes.Status200OK)
        .Produces<PickOrderDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/picks/{pickOrderId:guid}/confirm", async (
            Guid tenantId,
            Guid entityId,
            Guid pickOrderId,
            ConfirmPickOrderRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.ConfirmPickOrderAsync(
                        db,
                        new ConfirmPickOrderCommand(
                            tenantId,
                            entityId,
                            pickOrderId,
                            (request.Lines ?? Array.Empty<ConfirmPickLineInputDto>())
                                .Select(l => new ConfirmPickLineInput(l.PickLineId, l.PickedBaseQuantity))
                                .ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.PickOrder is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_pick_confirm_failed", outcome.ErrorDetail ?? "Could not confirm warehouse pick order.");
                    }

                    var dto = await LoadPickOrderDtoAsync(db, tenantId, entityId, outcome.PickOrder.PickOrderId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .WithName("ConfirmPickOrder")
        .Produces<PickOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/shipments", async (
            Guid tenantId,
            Guid entityId,
            DispatchSalesShipmentRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.DispatchShipmentAsync(
                        db,
                        new DispatchSalesShipmentCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.PickOrderId,
                            request.DispatchDate,
                            request.DeliveryNoteReference,
                            request.CarrierName,
                            request.TrackingOrVehicleNumber,
                            request.IdempotencyKey,
                            request.Notes,
                            request.Lines?
                                .Select(l => new DispatchShipmentLineInput(l.PickLineId, l.DispatchedBaseQuantity))
                                .ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Shipment is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_shipment_dispatch_failed", outcome.ErrorDetail ?? "Could not dispatch sales shipment.");
                    }

                    var dto = await LoadShipmentDtoAsync(db, tenantId, entityId, outcome.Shipment.ShipmentId, innerCt);
                    return outcome.HttpStatus == StatusCodes.Status201Created
                        ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/fulfillment/shipments/{dto.ShipmentId}", dto)
                        : Results.Ok(dto);
                },
                ct);
        })
        .WithName("DispatchSalesShipment")
        .Produces<SalesShipmentDto>(StatusCodes.Status200OK)
        .Produces<SalesShipmentDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/shipments/{shipmentId:guid}/confirm-delivery", async (
            Guid tenantId,
            Guid entityId,
            Guid shipmentId,
            ConfirmShipmentDeliveryRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                ManagePermissions,
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await SalesFulfillmentCoordinator.ConfirmShipmentDeliveryAsync(
                        db,
                        new ConfirmShipmentDeliveryCommand(
                            tenantId,
                            entityId,
                            shipmentId,
                            request.ConfirmedByName,
                            request.EvidenceReference,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Shipment is null)
                    {
                        throw new AbortedFulfillmentException(outcome.HttpStatus, outcome.ErrorCode ?? "fulfillment_delivery_confirm_failed", outcome.ErrorDetail ?? "Could not confirm shipment delivery evidence.");
                    }

                    var dto = await LoadShipmentDtoAsync(db, tenantId, entityId, outcome.Shipment.ShipmentId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .WithName("ConfirmSalesShipmentDelivery")
        .Produces<SalesShipmentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ExecuteScopeAsync(
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
                        "Your active grants for this legal entity do not permit this fulfillment operation.");
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        return !result.IsAllowed
            ? EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode)
            : result.Value!;
    }

    private static async Task<IResult> ExecuteMutationAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<IResult>> action,
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
                            "Your active grants for this legal entity do not permit this fulfillment operation."),
                            Committed: false);
                    }

                    var okResult = await action(scope, actorUserId, innerCt);
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
        catch (AbortedFulfillmentException aborted)
        {
            return aborted.Status switch
            {
                StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, aborted.Code, aborted.Detail),
                StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, aborted.Code, aborted.Detail),
                StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, aborted.Code, aborted.Detail),
                _ => EndpointSupport.BadRequest(httpContext, aborted.Code, aborted.Detail)
            };
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsUniqueConstraintViolation(ex))
        {
            return EndpointSupport.Conflict(
                httpContext,
                "fulfillment_concurrency_conflict",
                "This fulfillment record or stock balance was modified concurrently; reload and retry.");
        }
    }

    private sealed class AbortedFulfillmentException(int status, string code, string detail) : Exception
    {
        public int Status { get; } = status;
        public string Code { get; } = code;
        public string Detail { get; } = detail;
    }

    private static bool IsUniqueConstraintViolation(Exception ex) =>
        ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;

    private static async Task<FulfillmentOverviewResponse> BuildFulfillmentOverviewAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var warehouses = await db.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.LegalEntityId == entityId)
            .ToDictionaryAsync(w => w.WarehouseId, ct);

        var locations = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .ToDictionaryAsync(l => l.LocationId, ct);

        var lots = await db.StockLots.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .ToDictionaryAsync(l => l.StockLotId, ct);

        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .ToDictionaryAsync(i => i.ItemId, ct);

        var pools = await db.ValuationPools.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
            .ToDictionaryAsync(p => p.ItemId, ct);

        var unrestrictedBalances = await db.StockLocationBalances.AsNoTracking()
            .Where(b =>
                b.TenantId == tenantId &&
                b.LegalEntityId == entityId &&
                b.StockStatus == StockStatuses.Unrestricted &&
                b.OnHandBaseQuantity > 0m)
            .ToListAsync(ct);

        var eligibleBalancesByItem = unrestrictedBalances
            .Where(b =>
                locations.TryGetValue(b.LocationId, out var loc) &&
                loc.Status == MasterStatuses.Active &&
                (loc.ZoneType == LocationZoneTypes.Storage || loc.ZoneType == LocationZoneTypes.Dispatch))
            .GroupBy(b => b.ItemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<FulfillmentEligibleBalanceDto>)g
                    .Select(b =>
                    {
                        var wh = warehouses.GetValueOrDefault(b.WarehouseId);
                        var loc = locations[b.LocationId];
                        var lot = b.StockLotId is { } lid ? lots.GetValueOrDefault(lid) : null;
                        return new FulfillmentEligibleBalanceDto(
                            b.StockLocationBalanceId,
                            b.WarehouseId,
                            wh?.WarehouseCode ?? "WH-MAIN",
                            b.LocationId,
                            loc.LocationCode,
                            loc.ZoneType,
                            b.StockLotId,
                            lot?.LotNumber,
                            lot?.ExpiryDate,
                            b.OnHandBaseQuantity,
                            b.ReservedBaseQuantity,
                            b.AvailableBaseQuantity);
                    })
                    .OrderByDescending(x => x.AvailableBaseQuantity)
                    .ThenBy(x => x.LocationCode, StringComparer.Ordinal)
                    .ToList());

        var approvedOrders = await db.SalesOrders.AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId &&
                o.LegalEntityId == entityId &&
                (o.Status == SalesOrderStatuses.Approved || o.Status == SalesOrderStatuses.Closed) &&
                o.CurrentApprovedRevisionId != null &&
                o.SoNumber != null)
            .OrderByDescending(o => o.UpdatedAtUtc)
            .ToListAsync(ct);

        var approvedRevIds = approvedOrders
            .Where(o => o.CurrentApprovedRevisionId.HasValue)
            .Select(o => o.CurrentApprovedRevisionId!.Value)
            .Distinct()
            .ToList();

        var approvedRevs = await db.SalesOrderRevisions.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && approvedRevIds.Contains(r.RevisionId))
            .ToDictionaryAsync(r => r.RevisionId, ct);

        var approvedLines = await db.SalesOrderLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && approvedRevIds.Contains(l.RevisionId))
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var linesByRevId = approvedLines
            .GroupBy(l => l.RevisionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var fulfillableOrderDtos = new List<FulfillableSalesOrderDto>(approvedOrders.Count);
        foreach (var so in approvedOrders)
        {
            var revId = so.CurrentApprovedRevisionId!.Value;
            if (!approvedRevs.TryGetValue(revId, out var rev))
            {
                continue;
            }

            var soLines = linesByRevId.GetValueOrDefault(revId) ?? new List<SalesOrderLine>();
            var lineDtos = soLines.Select(l =>
            {
                var item = items.GetValueOrDefault(l.ItemId);
                var pool = pools.GetValueOrDefault(l.ItemId);
                var remToReserve = Math.Max(0m, decimal.Round(l.OrderedBaseQuantity - l.DispatchedBaseQuantity - l.ReservedBaseQuantity, 6, MidpointRounding.AwayFromZero));
                var remToDispatch = Math.Max(0m, decimal.Round(l.OrderedBaseQuantity - l.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero));
                var eligible = eligibleBalancesByItem.GetValueOrDefault(l.ItemId) ?? Array.Empty<FulfillmentEligibleBalanceDto>();
                return new FulfillableSalesOrderLineDto(
                    l.SoLineId,
                    l.LineNumber,
                    l.LineageLineKey,
                    l.ItemId,
                    l.ItemCodeSnapshot,
                    l.ItemNameSnapshot,
                    item?.LotTrackingPolicy ?? LotTrackingPolicies.None,
                    l.OrderedUomCodeSnapshot,
                    l.OrderedQuantity,
                    l.BaseUomCodeSnapshot,
                    l.OrderedBaseQuantity,
                    l.ReservedBaseQuantity,
                    l.DispatchedBaseQuantity,
                    remToReserve,
                    remToDispatch,
                    pool?.MovingAverageUnitCost ?? 0m,
                    eligible);
            }).ToList();

            var progress = SalesFulfillmentProgressStatuses.Derive(
                so.Status,
                hasApprovedRevision: true,
                rev.TotalOrderedBaseQuantity,
                so.TotalReservedBaseQuantity,
                so.TotalDispatchedBaseQuantity);

            var remUnfulfilled = Math.Max(
                0m,
                decimal.Round(rev.TotalOrderedBaseQuantity - so.TotalDispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero));

            fulfillableOrderDtos.Add(new FulfillableSalesOrderDto(
                so.SalesOrderId,
                so.SoNumber!,
                so.Status,
                progress,
                so.CustomerPartnerId,
                rev.CustomerCodeSnapshot,
                rev.CustomerLegalNameSnapshot,
                rev.DeliveryAddressSnapshot,
                so.OrderDate,
                so.RequestedDeliveryDate,
                revId,
                so.CurrentApprovedRevisionNumber,
                rev.TotalOrderedBaseQuantity,
                so.TotalReservedBaseQuantity,
                so.TotalDispatchedBaseQuantity,
                remUnfulfilled,
                lineDtos));
        }

        var pickOrders = await db.PickOrders.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync(ct);

        var pickLines = await db.PickLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var openPickAllocatedByRsvId = pickLines
            .Join(
                pickOrders.Where(po => po.Status == PickOrderStatuses.Open || po.Status == PickOrderStatuses.Picked),
                pl => pl.PickOrderId,
                po => po.PickOrderId,
                (pl, po) => new
                {
                    pl.StockReservationId,
                    Allocated = po.Status == PickOrderStatuses.Open
                        ? pl.RequestedPickBaseQuantity
                        : pl.PickedBaseQuantity - pl.DispatchedBaseQuantity
                })
            .GroupBy(x => x.StockReservationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Allocated));

        var salesReservations = await db.StockReservations.AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.LegalEntityId == entityId &&
                r.SourceType == ReservationSourceTypes.SalesOrder)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ToListAsync(ct);

        var reservationsById = salesReservations.ToDictionary(r => r.StockReservationId);
        var soLinesByIdAll = approvedLines.ToDictionary(l => l.SoLineId);

        var reservationDtos = salesReservations.Select(r =>
        {
            var item = items.GetValueOrDefault(r.ItemId);
            var wh = warehouses.GetValueOrDefault(r.WarehouseId);
            var loc = locations.GetValueOrDefault(r.LocationId);
            var lot = r.StockLotId is { } lid ? lots.GetValueOrDefault(lid) : null;
            var soLine = r.SourceLineId is { } slid ? soLinesByIdAll.GetValueOrDefault(slid) : null;
            var allocPick = openPickAllocatedByRsvId.GetValueOrDefault(r.StockReservationId, 0m);
            var unalloc = Math.Max(0m, decimal.Round(r.RemainingReservedBaseQuantity - allocPick, 6, MidpointRounding.AwayFromZero));

            return new SalesReservationDto(
                r.StockReservationId,
                r.ReservationNumber,
                r.SourceDocumentId ?? Guid.Empty,
                r.SourceDocumentNumber,
                r.SourceLineId ?? Guid.Empty,
                soLine?.LineageLineKey ?? "SO-L001",
                r.ItemId,
                soLine?.ItemCodeSnapshot ?? item?.ItemCode ?? "ITEM",
                soLine?.ItemNameSnapshot ?? item?.Name ?? "Item",
                soLine?.BaseUomCodeSnapshot ?? "PCS",
                r.WarehouseId,
                wh?.WarehouseCode ?? "WH-MAIN",
                r.LocationId,
                loc?.LocationCode ?? "LOC-MAIN",
                r.StockLotId,
                lot?.LotNumber,
                r.ReservedBaseQuantity,
                r.ConsumedBaseQuantity,
                r.ReleasedBaseQuantity,
                r.RemainingReservedBaseQuantity,
                allocPick,
                unalloc,
                r.Status,
                r.Notes,
                r.CreatedAtUtc,
                r.UpdatedAtUtc);
        }).ToList();

        var pickLinesByOrderId = pickLines
            .GroupBy(l => l.PickOrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pickOrderDtos = pickOrders.Select(po =>
        {
            var pLines = pickLinesByOrderId.GetValueOrDefault(po.PickOrderId) ?? new List<PickLine>();
            var lineDtos = pLines.Select(pl =>
            {
                var rsv = reservationsById.GetValueOrDefault(pl.StockReservationId);
                var remDisp = Math.Max(0m, decimal.Round(pl.PickedBaseQuantity - pl.DispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero));
                return new PickLineDto(
                    pl.PickLineId,
                    pl.LineNumber,
                    pl.SoLineId,
                    pl.LineageLineKey,
                    pl.StockReservationId,
                    rsv?.ReservationNumber ?? "RSV",
                    pl.ItemId,
                    pl.ItemCodeSnapshot,
                    pl.ItemNameSnapshot,
                    pl.BaseUomCodeSnapshot,
                    pl.WarehouseId,
                    pl.LocationId,
                    pl.LocationCodeSnapshot,
                    pl.StockLotId,
                    pl.LotNumberSnapshot,
                    pl.RequestedPickBaseQuantity,
                    pl.PickedBaseQuantity,
                    pl.DispatchedBaseQuantity,
                    remDisp);
            }).ToList();

            var remOrderDisp = Math.Max(
                0m,
                decimal.Round(po.TotalPickedBaseQuantity - po.TotalDispatchedBaseQuantity, 6, MidpointRounding.AwayFromZero));

            return new PickOrderDto(
                po.PickOrderId,
                po.PickNumber,
                po.SalesOrderId,
                po.SalesOrderNumberSnapshot,
                po.RevisionId,
                po.WarehouseId,
                po.WarehouseCodeSnapshot,
                po.Status,
                po.TotalRequestedBaseQuantity,
                po.TotalPickedBaseQuantity,
                po.TotalDispatchedBaseQuantity,
                remOrderDisp,
                po.Notes,
                po.CreatedAtUtc,
                po.PickedAtUtc,
                po.UpdatedAtUtc,
                lineDtos);
        }).ToList();

        var pickOrdersById = pickOrders.ToDictionary(p => p.PickOrderId);
        var shipments = await db.SalesShipments.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.LegalEntityId == entityId)
            .OrderByDescending(s => s.DispatchedAtUtc)
            .ToListAsync(ct);

        var shipmentLines = await db.SalesShipmentLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var shipmentLinesByShipmentId = shipmentLines
            .GroupBy(l => l.ShipmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var shipmentDtos = shipments.Select(s =>
        {
            var po = pickOrdersById.GetValueOrDefault(s.PickOrderId);
            var sLines = shipmentLinesByShipmentId.GetValueOrDefault(s.ShipmentId) ?? new List<SalesShipmentLine>();
            var lineDtos = sLines.Select(l => new SalesShipmentLineDto(
                l.ShipmentLineId,
                l.LineNumber,
                l.PickLineId,
                l.SoLineId,
                l.LineageLineKey,
                l.StockReservationId,
                l.ItemId,
                l.ItemCodeSnapshot,
                l.ItemNameSnapshot,
                l.BaseUomCodeSnapshot,
                l.LocationId,
                l.LocationCodeSnapshot,
                l.StockLotId,
                l.LotNumberSnapshot,
                l.DispatchedBaseQuantity,
                l.UnitCostApplied,
                l.LineCogsAmount,
                l.MovementLineId,
                l.ValuationEntryId)).ToList();

            return new SalesShipmentDto(
                s.ShipmentId,
                s.ShipmentNumber,
                s.PickOrderId,
                po?.PickNumber ?? "PCK",
                s.SalesOrderId,
                s.SalesOrderNumberSnapshot,
                s.CustomerPartnerId,
                s.CustomerCodeSnapshot,
                s.CustomerNameSnapshot,
                s.DeliveryAddressSnapshot,
                s.DispatchDate,
                s.Status,
                s.CarrierName,
                s.TrackingOrVehicleNumber,
                s.DeliveryNoteReference,
                s.DeliveryConfirmedByName,
                s.DeliveryEvidenceReference,
                s.DeliveredAtUtc,
                s.StockMovementId,
                s.StockMovementNumber,
                s.GlJournalId,
                s.GlJournalNumber,
                s.CurrencyCode,
                s.TotalDispatchedBaseQuantity,
                s.TotalCogsAmount,
                s.Notes,
                s.DispatchedAtUtc,
                lineDtos);
        }).ToList();

        return new FulfillmentOverviewResponse(
            fulfillableOrderDtos,
            reservationDtos,
            pickOrderDtos,
            shipmentDtos,
            Od10NoticeText,
            Od09NoticeText);
    }

    private static async Task<SalesReservationDto> LoadReservationDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid reservationId,
        CancellationToken ct)
    {
        var overview = await BuildFulfillmentOverviewAsync(db, tenantId, entityId, ct);
        return overview.Reservations.Single(r => r.StockReservationId == reservationId);
    }

    private static async Task<PickOrderDto> LoadPickOrderDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid pickOrderId,
        CancellationToken ct)
    {
        var overview = await BuildFulfillmentOverviewAsync(db, tenantId, entityId, ct);
        return overview.PickOrders.Single(p => p.PickOrderId == pickOrderId);
    }

    private static async Task<SalesShipmentDto> LoadShipmentDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid shipmentId,
        CancellationToken ct)
    {
        var overview = await BuildFulfillmentOverviewAsync(db, tenantId, entityId, ct);
        return overview.Shipments.Single(s => s.ShipmentId == shipmentId);
    }
}
