using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Quality;

namespace Orphac.Athera.Api.Endpoints;

internal static class InventoryEndpoints
{
    private const string Od04DefaultNotice =
        "OD-04 strict safe defaults active (ATH-CR-004): moving-average weighted cost in entity currency (IDR), zero/negative valuation entries rejected, and anomalous pools blocked automatically.";

    private const string Od10DefaultNotice =
        "OD-10 strict safe defaults active (ATH-CR-005): negative stock and over-reservation forbidden; quality holds and location transfers preserve carrying value while updating availability.";

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        MapGateTwoAcceptanceRoutes(app);
        MapInventoryOverviewRoutes(app);
        MapInventoryWarehouseAndOperationsRoutes(app);
        return app;
    }

    private static void MapGateTwoAcceptanceRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts/{receiptId:guid}/accept-preview", async (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            PreviewReceiptAcceptanceRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingAccept, PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage],
                "Your active grants for this legal entity do not permit Gate 2 acceptance previews.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var preview = await ReceiptAcceptanceCoordinator.PreviewAcceptanceAsync(
                        db,
                        new PreviewAcceptanceCommand(
                            tenantId,
                            entityId,
                            receiptId,
                            request.AcceptanceDate,
                            request.StockStatus,
                            (request.Lines ?? Array.Empty<AcceptReceiptLineRequest>())
                                .Select(l => new AcceptReceiptLineInput(
                                    l.GoodsReceiptLineId,
                                    l.AcceptedBaseQuantity,
                                    l.DestinationLocationId,
                                    l.LotNumber,
                                    l.SupplierLotReference,
                                    l.ManufacturedDate,
                                    l.ExpiryDate))
                                .ToList()),
                        innerCt);

                    if (!preview.Succeeded)
                    {
                        return preview.FailureHttpStatus == StatusCodes.Status404NotFound
                            ? EndpointSupport.NotFound(httpContext, preview.ErrorCode ?? "not_found", preview.ErrorDetail ?? "Receipt not found.")
                            : EndpointSupport.BadRequest(httpContext, preview.ErrorCode ?? "preview_failed", preview.ErrorDetail ?? "Preview failed.");
                    }

                    return Results.Ok(new AcceptancePreviewDto(
                        preview.GoodsReceiptId,
                        preview.ReceiptNumber,
                        preview.CurrencyCode,
                        preview.AcceptanceDate,
                        preview.StockStatus,
                        preview.CanPost,
                        preview.TotalAcceptedBaseQuantity,
                        preview.TotalAcceptedAmount,
                        Od04DefaultNotice,
                        preview.Lines.Select(l => new AcceptancePreviewLineDto(
                            l.GoodsReceiptLineId,
                            l.ItemId,
                            l.ItemCode,
                            l.ItemName,
                            l.BaseUomCode,
                            l.AcceptedBaseQuantity,
                            l.UnitCostBase,
                            l.AcceptedLineAmount,
                            l.PreviousPoolBaseQuantity,
                            l.PreviousCarryingValue,
                            l.PreviousMovingAverageCost,
                            l.ProjectedPoolBaseQuantity,
                            l.ProjectedCarryingValue,
                            l.ProjectedMovingAverageCost,
                            l.BlockingReason,
                            l.LotTrackingPolicy,
                            l.LotNumber,
                            l.DestinationLocationId,
                            l.DestinationLocationCode)).ToList(),
                        preview.JournalPreview.Select(j => new PreviewJournalSuggestionDto(
                            j.Side,
                            j.AccountCode,
                            j.AccountName,
                            j.Amount,
                            j.Description)).ToList(),
                        preview.BlockingReasons));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("PreviewReceiptAcceptance")
        .Produces<AcceptancePreviewDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/receipts/{receiptId:guid}/accept", async (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            AcceptReceiptRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            try
            {
                var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    null,
                    async (scope, innerCt) =>
                    {
                        if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, PermissionCodes.ReceivingAccept))
                        {
                            return (Result: EndpointSupport.Forbidden(
                                httpContext,
                                "insufficient_entity_permission",
                                "Your active grants for this legal entity do not permit Gate 2 valued acceptance."),
                                ShouldNotifyOutbox: false);
                        }

                        var outcome = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
                            db,
                            new AcceptReceiptCommand(
                                tenantId,
                                entityId,
                                scope.Entity.FunctionalCurrencyCode,
                                receiptId,
                                request.IdempotencyKey,
                                request.AcceptanceDate,
                                request.StockStatus,
                                request.Notes,
                                (request.Lines ?? Array.Empty<AcceptReceiptLineRequest>())
                                    .Select(l => new AcceptReceiptLineInput(
                                        l.GoodsReceiptLineId,
                                        l.AcceptedBaseQuantity,
                                        l.DestinationLocationId,
                                        l.LotNumber,
                                        l.SupplierLotReference,
                                        l.ManufacturedDate,
                                        l.ExpiryDate))
                                    .ToList(),
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!outcome.Succeeded || outcome.Acceptance is null)
                        {
                            throw new AbortedAcceptanceWorkflowException(outcome);
                        }

                        var detail = await LoadValuedAcceptanceDetailDtoAsync(
                            db,
                            tenantId,
                            entityId,
                            outcome.Acceptance.ValuedAcceptanceId,
                            outcome.IdempotentReplay,
                            innerCt);

                        var okResult = !outcome.IdempotentReplay
                            ? Results.Created(
                                $"/api/workspaces/{tenantId}/entities/{entityId}/receiving/acceptances/{outcome.Acceptance.ValuedAcceptanceId}",
                                detail)
                            : Results.Ok(detail);

                        return (Result: okResult, ShouldNotifyOutbox: !outcome.IdempotentReplay);
                    },
                    ct);

                if (!scopeResult.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
                }

                if (scopeResult.Value.ShouldNotifyOutbox)
                {
                    outboxSignal.NotifyTenant(tenantId);
                }

                return scopeResult.Value.Result;
            }
            catch (AbortedAcceptanceWorkflowException aborted)
            {
                return MapFailure(httpContext, aborted.Outcome);
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
            {
                return MapPersistenceConflict(httpContext, ex);
            }
        })
        .RequireAuthorization()
        .WithName("AcceptGoodsReceiptValued")
        .Produces<ValuedAcceptanceDetailDto>(StatusCodes.Status201Created)
        .Produces<ValuedAcceptanceDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/acceptances", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] Guid? receiptId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingRead, PermissionCodes.ReceivingAccept, PermissionCodes.InventoryRead],
                "Your active grants for this legal entity do not permit viewing valued acceptances.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var query = db.ValuedAcceptances.AsNoTracking()
                        .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId);

                    if (receiptId.HasValue && receiptId.Value != Guid.Empty)
                    {
                        query = query.Where(a => a.GoodsReceiptId == receiptId.Value);
                    }

                    var rows = await query
                        .OrderByDescending(a => a.AcceptanceDate)
                        .ThenByDescending(a => a.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    if (rows.Count == 0)
                    {
                        return Results.Ok(new ValuedAcceptanceListResponse(Array.Empty<ValuedAcceptanceSummaryDto>()));
                    }

                    var grIds = rows.Select(r => r.GoodsReceiptId).Distinct().ToList();
                    var poIds = rows.Select(r => r.PurchaseOrderId).Distinct().ToList();
                    var supIds = rows.Select(r => r.SupplierPartnerId).Distinct().ToList();
                    var movIds = rows.Where(r => r.StockMovementId.HasValue).Select(r => r.StockMovementId!.Value).Distinct().ToList();
                    var accIds = rows.Where(r => r.ReceiptAccrualId.HasValue).Select(r => r.ReceiptAccrualId!.Value).Distinct().ToList();
                    var jrnIds = rows.Where(r => r.GlJournalId.HasValue).Select(r => r.GlJournalId!.Value).Distinct().ToList();
                    var userIds = rows.Select(r => r.AcceptedByUserId).Distinct().ToList();

                    var grNums = await db.GoodsReceipts.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && grIds.Contains(r.GoodsReceiptId))
                        .ToDictionaryAsync(r => r.GoodsReceiptId, r => r.ReceiptNumber, innerCt);
                    var poNums = await db.PurchaseOrders.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && poIds.Contains(p.PurchaseOrderId))
                        .ToDictionaryAsync(p => p.PurchaseOrderId, p => p.PoNumber ?? p.DraftCode, innerCt);
                    var partners = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && supIds.Contains(p.PartnerId))
                        .ToDictionaryAsync(p => p.PartnerId, p => (p.PartnerCode, p.LegalName), innerCt);
                    var movNums = movIds.Count == 0 ? new Dictionary<Guid, string>() : await db.StockMovements.AsNoTracking()
                        .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movIds.Contains(m.StockMovementId))
                        .ToDictionaryAsync(m => m.StockMovementId, m => m.MovementNumber, innerCt);
                    var accNums = accIds.Count == 0 ? new Dictionary<Guid, string>() : await db.ReceiptAccruals.AsNoTracking()
                        .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accIds.Contains(a.ReceiptAccrualId))
                        .ToDictionaryAsync(a => a.ReceiptAccrualId, a => a.AccrualNumber, innerCt);
                    var jrnNums = jrnIds.Count == 0 ? new Dictionary<Guid, string>() : await db.JournalEntries.AsNoTracking()
                        .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                        .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, innerCt);
                    var emails = await db.Users.AsNoTracking()
                        .Where(u => userIds.Contains(u.Id))
                        .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), innerCt);

                    var dtos = rows.Select(a =>
                    {
                        var partner = partners.GetValueOrDefault(a.SupplierPartnerId, ("SUP", "Supplier"));
                        return new ValuedAcceptanceSummaryDto(
                            a.ValuedAcceptanceId,
                            a.AcceptanceNumber,
                            a.GoodsReceiptId,
                            grNums.GetValueOrDefault(a.GoodsReceiptId, "-"),
                            a.PurchaseOrderId,
                            poNums.GetValueOrDefault(a.PurchaseOrderId, "-"),
                            partner.Item1,
                            partner.Item2,
                            a.AcceptanceDate,
                            a.StockStatus,
                            a.CurrencyCode,
                            a.TotalAcceptedBaseQuantity,
                            a.TotalAcceptedAmount,
                            a.StockMovementId,
                            a.StockMovementId is { } mid && movNums.TryGetValue(mid, out var mn) ? mn : null,
                            a.ReceiptAccrualId,
                            a.ReceiptAccrualId is { } aid && accNums.TryGetValue(aid, out var an) ? an : null,
                            a.GlJournalId,
                            a.GlJournalId is { } jid && jrnNums.TryGetValue(jid, out var jn) ? jn : null,
                            emails.GetValueOrDefault(a.AcceptedByUserId, a.AcceptedByUserId.ToString("D")),
                            a.CreatedAtUtc);
                    }).ToList();

                    return Results.Ok(new ValuedAcceptanceListResponse(dtos));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListValuedAcceptances")
        .Produces<ValuedAcceptanceListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receiving/acceptances/{acceptanceId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid acceptanceId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.ReceivingRead, PermissionCodes.ReceivingAccept, PermissionCodes.InventoryRead],
                "Your active grants for this legal entity do not permit viewing valued acceptances.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await LoadValuedAcceptanceDetailDtoAsync(db, tenantId, entityId, acceptanceId, wasIdempotentReplay: false, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "valued_acceptance_not_found", "Valued acceptance record was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetValuedAcceptanceDetail")
        .Produces<ValuedAcceptanceDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapInventoryOverviewRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/overview", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? search,
            [FromQuery] string? stockStatus,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.QualityInspect],
                "Your active grants for this legal entity do not permit viewing inventory valuation, availability, and GRNI accruals.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var positions = await db.StockPositions.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode)
                        .ToListAsync(innerCt);

                    var pools = await db.ValuationPools.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode)
                        .ToDictionaryAsync(p => p.ItemId, innerCt);

                    var posItemIds = positions.Select(p => p.ItemId).Distinct().ToList();
                    var items = posItemIds.Count == 0
                        ? new Dictionary<Guid, (string Code, string Name, string LotPolicy)>()
                        : await db.Items.AsNoTracking()
                            .Where(i => i.TenantId == tenantId && posItemIds.Contains(i.ItemId))
                            .ToDictionaryAsync(i => i.ItemId, i => (i.ItemCode, i.Name, i.LotTrackingPolicy), innerCt);

                    var uomIds = positions.Select(p => p.BaseUomId).Distinct().ToList();
                    var uomCodes = uomIds.Count == 0
                        ? new Dictionary<Guid, string>()
                        : await db.UnitsOfMeasure.AsNoTracking()
                            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
                            .ToDictionaryAsync(u => u.UomId, u => u.UomCode, innerCt);

                    var locBalanceCounts = posItemIds.Count == 0
                        ? new Dictionary<Guid, (int LocCount, int LotCount)>()
                        : (await db.StockLocationBalances.AsNoTracking()
                            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.OnHandBaseQuantity > 0m && posItemIds.Contains(b.ItemId))
                            .Select(b => new { b.ItemId, b.LocationId, b.StockLotId })
                            .ToListAsync(innerCt))
                            .GroupBy(b => b.ItemId)
                            .ToDictionary(
                                g => g.Key,
                                g => (
                                    g.Select(x => x.LocationId).Distinct().Count(),
                                    g.Where(x => x.StockLotId.HasValue).Select(x => x.StockLotId!.Value).Distinct().Count()));

                    var pendingInspectionsByItem = posItemIds.Count == 0
                        ? new Dictionary<Guid, int>()
                        : await db.ReceiptInspections.AsNoTracking()
                            .Where(q => q.TenantId == tenantId && q.LegalEntityId == entityId && q.Status != ReceiptInspectionStatuses.Completed && posItemIds.Contains(q.ItemId))
                            .GroupBy(q => q.ItemId)
                            .Select(g => new { ItemId = g.Key, Count = g.Count() })
                            .ToDictionaryAsync(x => x.ItemId, x => x.Count, innerCt);

                    var allPositionDtos = positions.Select(p =>
                    {
                        pools.TryGetValue(p.ItemId, out var pool);
                        var itemInfo = items.GetValueOrDefault(p.ItemId, ("ITEM", "Catalog Item", LotTrackingPolicies.None));
                        var counts = locBalanceCounts.GetValueOrDefault(p.ItemId, (0, 0));
                        return new StockPositionOverviewDto(
                            p.StockPositionId,
                            p.ItemId,
                            itemInfo.Item1,
                            itemInfo.Item2,
                            itemInfo.Item3,
                            p.BaseUomId,
                            uomCodes.GetValueOrDefault(p.BaseUomId, "PCS"),
                            p.ValuationGroupCode,
                            pool?.CurrencyCode ?? "IDR",
                            p.UnrestrictedBaseQuantity,
                            p.QualityInspectionBaseQuantity,
                            p.BlockedBaseQuantity,
                            p.ReservedBaseQuantity,
                            p.TotalValuedOnHandBaseQuantity,
                            p.AvailableBaseQuantity,
                            pool?.CarryingValueAmount ?? 0m,
                            pool?.MovingAverageUnitCost ?? 0m,
                            pool?.Status ?? ValuationPoolStatuses.Active,
                            pool?.LastEntrySequence ?? 0L,
                            counts.Item1,
                            counts.Item2,
                            pendingInspectionsByItem.GetValueOrDefault(p.ItemId, 0),
                            p.UpdatedAtUtc);
                    })
                    .OrderBy(p => p.ItemCode, StringComparer.Ordinal)
                    .ToList();

                    var totalStockedWithBalance = allPositionDtos.Count(p => p.TotalValuedOnHandBaseQuantity > 0m);
                    var totalCarryingValue = decimal.Round(allPositionDtos.Sum(p => p.CarryingValueAmount), 4, MidpointRounding.AwayFromZero);
                    var restrictedCount = allPositionDtos.Count(p => p.QualityInspectionBaseQuantity > 0m || p.BlockedBaseQuantity > 0m);

                    IEnumerable<StockPositionOverviewDto> filteredPositions = allPositionDtos;
                    if (!string.IsNullOrWhiteSpace(stockStatus))
                    {
                        var normStatus = stockStatus.Trim().ToUpperInvariant();
                        filteredPositions = normStatus switch
                        {
                            StockStatuses.Unrestricted => filteredPositions.Where(p => p.UnrestrictedBaseQuantity > 0m),
                            StockStatuses.QualityInspection => filteredPositions.Where(p => p.QualityInspectionBaseQuantity > 0m),
                            StockStatuses.Blocked => filteredPositions.Where(p => p.BlockedBaseQuantity > 0m),
                            _ => filteredPositions
                        };
                    }

                    var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToUpperInvariant();
                    if (term is not null)
                    {
                        filteredPositions = filteredPositions.Where(p =>
                            p.ItemCode.ToUpperInvariant().Contains(term) ||
                            p.ItemName.ToUpperInvariant().Contains(term));
                    }

                    // Load recent stock movements
                    var recentMovementsRaw = await db.StockMovements.AsNoTracking()
                        .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId)
                        .OrderByDescending(m => m.PostingDate)
                        .ThenByDescending(m => m.CreatedAtUtc)
                        .Take(25)
                        .ToListAsync(innerCt);

                    var movIds = recentMovementsRaw.Select(m => m.StockMovementId).ToList();
                    var movLineAgg = movIds.Count == 0
                        ? new Dictionary<Guid, (int Count, decimal Qty, decimal Amt)>()
                        : (await db.MovementLines.AsNoTracking()
                            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && movIds.Contains(l.StockMovementId))
                            .ToListAsync(innerCt))
                            .GroupBy(l => l.StockMovementId)
                            .ToDictionary(
                                g => g.Key,
                                g =>
                                {
                                    var posQty = g.Where(x => x.BaseQuantityDelta > 0m).Sum(x => x.BaseQuantityDelta);
                                    var qty = posQty > 0m ? posQty : g.Sum(x => Math.Abs(x.BaseQuantityDelta));
                                    return (g.Count(), qty, g.Sum(x => x.LineValuationAmount));
                                });

                    var movActorIds = recentMovementsRaw.Select(m => m.CreatedByUserId).Distinct().ToList();
                    var actorEmails = movActorIds.Count == 0
                        ? new Dictionary<Guid, string>()
                        : await db.Users.AsNoTracking()
                            .Where(u => movActorIds.Contains(u.Id))
                            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), innerCt);

                    var movementDtos = recentMovementsRaw.Select(m =>
                    {
                        var agg = movLineAgg.GetValueOrDefault(m.StockMovementId, (0, 0m, 0m));
                        return new StockMovementSummaryDto(
                            m.StockMovementId,
                            m.MovementNumber,
                            m.MovementType,
                            m.PostingDate,
                            m.SourceDocumentNumber,
                            agg.Item1,
                            agg.Item2,
                            agg.Item3,
                            actorEmails.GetValueOrDefault(m.CreatedByUserId, m.CreatedByUserId.ToString("D")),
                            m.CreatedAtUtc);
                    }).ToList();

                    // Load open GRNI accruals
                    var accrualsRaw = await db.ReceiptAccruals.AsNoTracking()
                        .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId)
                        .OrderByDescending(a => a.PostingDate)
                        .ThenByDescending(a => a.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    var totalOpenGrni = decimal.Round(accrualsRaw.Sum(a => a.RemainingOpenAmount), 4, MidpointRounding.AwayFromZero);

                    var accIds = accrualsRaw.Select(a => a.ValuedAcceptanceId).Distinct().ToList();
                    var accGrIds = accrualsRaw.Select(a => a.GoodsReceiptId).Distinct().ToList();
                    var accPoIds = accrualsRaw.Select(a => a.PurchaseOrderId).Distinct().ToList();
                    var accSupIds = accrualsRaw.Select(a => a.SupplierPartnerId).Distinct().ToList();
                    var accJrnIds = accrualsRaw.Select(a => a.JournalId).Distinct().ToList();

                    var vaNums = accIds.Count == 0 ? new Dictionary<Guid, string>() : await db.ValuedAcceptances.AsNoTracking()
                        .Where(v => v.TenantId == tenantId && v.LegalEntityId == entityId && accIds.Contains(v.ValuedAcceptanceId))
                        .ToDictionaryAsync(v => v.ValuedAcceptanceId, v => v.AcceptanceNumber, innerCt);
                    var grNums = accGrIds.Count == 0 ? new Dictionary<Guid, string>() : await db.GoodsReceipts.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && accGrIds.Contains(r.GoodsReceiptId))
                        .ToDictionaryAsync(r => r.GoodsReceiptId, r => r.ReceiptNumber, innerCt);
                    var poNums = accPoIds.Count == 0 ? new Dictionary<Guid, string>() : await db.PurchaseOrders.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && accPoIds.Contains(p.PurchaseOrderId))
                        .ToDictionaryAsync(p => p.PurchaseOrderId, p => p.PoNumber ?? p.DraftCode, innerCt);
                    var supMap = accSupIds.Count == 0 ? new Dictionary<Guid, (string Code, string Name)>() : await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && accSupIds.Contains(p.PartnerId))
                        .ToDictionaryAsync(p => p.PartnerId, p => (p.PartnerCode, p.LegalName), innerCt);
                    var jrnNums = accJrnIds.Count == 0 ? new Dictionary<Guid, string>() : await db.JournalEntries.AsNoTracking()
                        .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && accJrnIds.Contains(j.JournalId))
                        .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, innerCt);

                    IEnumerable<ReceiptAccrualOverviewDto> accrualDtos = accrualsRaw.Select(a =>
                    {
                        var sup = supMap.GetValueOrDefault(a.SupplierPartnerId, ("SUP", "Supplier"));
                        return new ReceiptAccrualOverviewDto(
                            a.ReceiptAccrualId,
                            a.AccrualNumber,
                            a.ValuedAcceptanceId,
                            vaNums.GetValueOrDefault(a.ValuedAcceptanceId, "-"),
                            a.GoodsReceiptId,
                            grNums.GetValueOrDefault(a.GoodsReceiptId, "-"),
                            a.PurchaseOrderId,
                            poNums.GetValueOrDefault(a.PurchaseOrderId, "-"),
                            a.SupplierPartnerId,
                            sup.Item1,
                            sup.Item2,
                            a.JournalId,
                            jrnNums.GetValueOrDefault(a.JournalId, "-"),
                            a.PostingDate,
                            a.CurrencyCode,
                            a.AccruedAmount,
                            a.ClearedAmount,
                            a.RemainingOpenAmount,
                            a.Status,
                            a.CreatedAtUtc);
                    });

                    if (term is not null)
                    {
                        accrualDtos = accrualDtos.Where(a =>
                            a.AccrualNumber.ToUpperInvariant().Contains(term) ||
                            a.AcceptanceNumber.ToUpperInvariant().Contains(term) ||
                            a.ReceiptNumber.ToUpperInvariant().Contains(term) ||
                            a.PoNumber.ToUpperInvariant().Contains(term) ||
                            a.SupplierName.ToUpperInvariant().Contains(term));
                    }

                    var locationDtos = await LoadLocationDtosAsync(db, tenantId, entityId, innerCt);
                    var inspectionDtos = await LoadInspectionsAsync(db, tenantId, entityId, itemIdFilter: null, inspectionIdFilter: null, innerCt);
                    if (term is not null)
                    {
                        inspectionDtos = inspectionDtos
                            .Where(q =>
                                q.InspectionNumber.ToUpperInvariant().Contains(term) ||
                                q.ItemCode.ToUpperInvariant().Contains(term) ||
                                q.ItemName.ToUpperInvariant().Contains(term) ||
                                (q.LotNumber?.ToUpperInvariant().Contains(term) ?? false))
                            .ToList();
                    }

                    return Results.Ok(new InventoryOverviewResponse(
                        filteredPositions.ToList(),
                        movementDtos,
                        accrualDtos.ToList(),
                        locationDtos,
                        inspectionDtos,
                        totalStockedWithBalance,
                        totalCarryingValue,
                        totalOpenGrni,
                        restrictedCount,
                        Od04DefaultNotice,
                        Od10DefaultNotice));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetInventoryOverview")
        .Produces<InventoryOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/items/{itemId:guid}/ledger", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.QualityInspect],
                "Your active grants for this legal entity do not permit viewing item valuation ledgers.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var item = await db.Items.AsNoTracking()
                        .SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, innerCt);
                    if (item is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "item_not_found", "Catalog item was not found in this tenant.");
                    }

                    var baseUomCode = await db.UnitsOfMeasure.AsNoTracking()
                        .Where(u => u.TenantId == tenantId && u.UomId == item.BaseUomId)
                        .Select(u => u.UomCode)
                        .SingleOrDefaultAsync(innerCt) ?? "PCS";

                    var position = await db.StockPositions.AsNoTracking()
                        .SingleOrDefaultAsync(p =>
                            p.TenantId == tenantId &&
                            p.LegalEntityId == entityId &&
                            p.ItemId == itemId &&
                            p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode,
                            innerCt);

                    var pool = await db.ValuationPools.AsNoTracking()
                        .SingleOrDefaultAsync(p =>
                            p.TenantId == tenantId &&
                            p.LegalEntityId == entityId &&
                            p.ItemId == itemId &&
                            p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode,
                            innerCt);

                    var entries = await db.ValuationEntries.AsNoTracking()
                        .Where(e => e.TenantId == tenantId && e.LegalEntityId == entityId && e.ItemId == itemId)
                        .OrderBy(e => e.PoolSequenceNumber)
                        .ToListAsync(innerCt);

                    var movIds = entries.Where(e => e.StockMovementId.HasValue).Select(e => e.StockMovementId!.Value).Distinct().ToList();
                    var jrnIds = entries.Where(e => e.GlJournalId.HasValue).Select(e => e.GlJournalId!.Value).Distinct().ToList();
                    var userIds = entries.Select(e => e.CreatedByUserId).Distinct().ToList();

                    var movNums = movIds.Count == 0 ? new Dictionary<Guid, string>() : await db.StockMovements.AsNoTracking()
                        .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movIds.Contains(m.StockMovementId))
                        .ToDictionaryAsync(m => m.StockMovementId, m => m.MovementNumber, innerCt);
                    var jrnNums = jrnIds.Count == 0 ? new Dictionary<Guid, string>() : await db.JournalEntries.AsNoTracking()
                        .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                        .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, innerCt);
                    var emails = userIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking()
                        .Where(u => userIds.Contains(u.Id))
                        .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), innerCt);

                    var entryDtos = entries.Select(e => new ValuationEntryLedgerDto(
                        e.ValuationEntryId,
                        e.PoolSequenceNumber,
                        e.PostingDate,
                        e.EntryType,
                        e.SourceDocumentType,
                        e.SourceDocumentId,
                        e.SourceDocumentNumber,
                        e.StockMovementId is { } mid ? movNums.GetValueOrDefault(mid, "-") : "-",
                        e.GlJournalId,
                        e.GlJournalId is { } jid && jrnNums.TryGetValue(jid, out var jn) ? jn : null,
                        e.QuantityDelta,
                        e.UnitCostApplied,
                        e.ValueDelta,
                        e.PreviousPoolBaseQuantity,
                        e.PreviousPoolCarryingValue,
                        e.PreviousMovingAverageCost,
                        e.NewPoolBaseQuantity,
                        e.NewPoolCarryingValue,
                        e.NewMovingAverageCost,
                        emails.GetValueOrDefault(e.CreatedByUserId, e.CreatedByUserId.ToString("D")),
                        e.CreatedAtUtc)).ToList();

                    return Results.Ok(new ItemValuationLedgerResponse(
                        item.ItemId,
                        item.ItemCode,
                        item.Name,
                        baseUomCode,
                        pool?.CurrencyCode ?? "IDR",
                        pool?.ValuationGroupCode ?? InventoryValuationDefaults.DefaultValuationGroupCode,
                        pool?.Status ?? ValuationPoolStatuses.Active,
                        pool?.ValuedBaseQuantity ?? 0m,
                        pool?.CarryingValueAmount ?? 0m,
                        pool?.MovingAverageUnitCost ?? 0m,
                        position?.UnrestrictedBaseQuantity ?? 0m,
                        position?.QualityInspectionBaseQuantity ?? 0m,
                        position?.BlockedBaseQuantity ?? 0m,
                        position?.AvailableBaseQuantity ?? 0m,
                        entryDtos));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetItemValuationLedger")
        .Produces<ItemValuationLedgerResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapInventoryWarehouseAndOperationsRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/warehouses", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [
                    PermissionCodes.InventoryRead,
                    PermissionCodes.InventoryManage,
                    PermissionCodes.QualityInspect,
                    PermissionCodes.ReceivingRead,
                    PermissionCodes.ReceivingAccept,
                    PermissionCodes.ReceivingManage
                ],
                "Your active grants for this legal entity do not permit viewing warehouses and locations.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var warehouses = await db.Warehouses.AsNoTracking()
                        .Where(w => w.TenantId == tenantId && w.LegalEntityId == entityId)
                        .OrderByDescending(w => w.IsDefault)
                        .ThenBy(w => w.WarehouseCode)
                        .Select(w => new WarehouseDto(w.WarehouseId, w.WarehouseCode, w.Name, w.IsDefault, w.Status, w.CreatedAtUtc))
                        .ToListAsync(innerCt);

                    var locations = await LoadLocationDtosAsync(db, tenantId, entityId, innerCt);
                    return Results.Ok(new WarehouseListResponse(warehouses, locations));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListInventoryWarehouses")
        .Produces<WarehouseListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/warehouses", async (
            Guid tenantId,
            Guid entityId,
            CreateWarehouseRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit creating warehouses.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantId, entityId, nowUtc, innerCt);

                    var warehouse = Warehouse.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        request.WarehouseCode,
                        request.Name,
                        isDefault: false,
                        nowUtc);

                    var dup = await db.Warehouses.AsNoTracking()
                        .AnyAsync(w => w.TenantId == tenantId && w.LegalEntityId == entityId && w.WarehouseCode == warehouse.WarehouseCode, innerCt);
                    if (dup)
                    {
                        throw new AbortedDomainOperationException(
                            StatusCodes.Status409Conflict,
                            "duplicate_warehouse_code",
                            $"Warehouse code '{warehouse.WarehouseCode}' already exists in this legal entity.");
                    }

                    db.Warehouses.Add(warehouse);

                    var payload = JsonSerializer.Serialize(new { warehouse.WarehouseId, warehouse.WarehouseCode, warehouse.Name });
                    db.AuditEvents.Add(AuditEvent.Append(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        actorUserId,
                        "WAREHOUSE_CREATED",
                        "WAREHOUSE",
                        warehouse.WarehouseId.ToString("D"),
                        $"Created warehouse {warehouse.WarehouseCode} ({warehouse.Name})",
                        payload,
                        nowUtc));

                    db.OutboxMessages.Add(OutboxMessage.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        "inventory.warehouse.created.v1",
                        $"inventory:warehouse:{warehouse.WarehouseId:D}",
                        payload,
                        nowUtc));

                    await db.SaveChangesAsync(innerCt);

                    var dto = new WarehouseDto(
                        warehouse.WarehouseId,
                        warehouse.WarehouseCode,
                        warehouse.Name,
                        warehouse.IsDefault,
                        warehouse.Status,
                        warehouse.CreatedAtUtc);

                    return (Results.Created(
                        $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/warehouses/{warehouse.WarehouseId}",
                        dto), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateWarehouse")
        .Produces<WarehouseDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/warehouses/{warehouseId:guid}/locations", async (
            Guid tenantId,
            Guid entityId,
            Guid warehouseId,
            CreateWarehouseLocationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit creating warehouse locations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var warehouse = await db.Warehouses.AsNoTracking()
                        .SingleOrDefaultAsync(w => w.TenantId == tenantId && w.LegalEntityId == entityId && w.WarehouseId == warehouseId, innerCt);
                    if (warehouse is null)
                    {
                        throw new AbortedDomainOperationException(
                            StatusCodes.Status404NotFound,
                            "warehouse_not_found",
                            "Warehouse was not found in this legal entity.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    var location = WarehouseLocation.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        warehouseId,
                        request.LocationCode,
                        request.Name,
                        request.ZoneType,
                        request.IsDefaultReceiving,
                        nowUtc);

                    var dup = await db.WarehouseLocations.AsNoTracking()
                        .AnyAsync(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.LocationCode == location.LocationCode, innerCt);
                    if (dup)
                    {
                        throw new AbortedDomainOperationException(
                            StatusCodes.Status409Conflict,
                            "duplicate_location_code",
                            $"Location code '{location.LocationCode}' already exists in this legal entity.");
                    }

                    db.WarehouseLocations.Add(location);

                    var payload = JsonSerializer.Serialize(new
                    {
                        location.LocationId,
                        location.WarehouseId,
                        location.LocationCode,
                        location.ZoneType,
                        location.Name
                    });

                    db.AuditEvents.Add(AuditEvent.Append(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        actorUserId,
                        "WAREHOUSE_LOCATION_CREATED",
                        "WAREHOUSE_LOCATION",
                        location.LocationId.ToString("D"),
                        $"Created location {location.LocationCode} [{location.ZoneType}] in warehouse {warehouse.WarehouseCode}",
                        payload,
                        nowUtc));

                    db.OutboxMessages.Add(OutboxMessage.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        "inventory.location.created.v1",
                        $"inventory:location:{location.LocationId:D}",
                        payload,
                        nowUtc));

                    await db.SaveChangesAsync(innerCt);

                    var dto = new WarehouseLocationDto(
                        location.LocationId,
                        warehouse.WarehouseId,
                        warehouse.WarehouseCode,
                        warehouse.Name,
                        location.LocationCode,
                        location.Name,
                        location.ZoneType,
                        location.IsDefaultReceiving,
                        location.Status,
                        location.CreatedAtUtc);

                    return (Results.Created(
                        $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/warehouses/{warehouseId}/locations/{location.LocationId}",
                        dto), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateWarehouseLocation")
        .Produces<WarehouseLocationDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/items/{itemId:guid}/detail", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.QualityInspect],
                "Your active grants for this legal entity do not permit viewing item stock detail.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await LoadItemStockDetailDtoAsync(db, tenantId, entityId, itemId, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "item_not_found", "Catalog item was not found in this tenant.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetItemStockDetail")
        .Produces<ItemStockDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/transfers", async (
            Guid tenantId,
            Guid entityId,
            TransferStockLocationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit transferring stock between locations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await InventoryAvailabilityService.TransferLocationAsync(
                        db,
                        new TransferStockLocationCommand(
                            tenantId,
                            entityId,
                            request.ItemId,
                            request.FromLocationId,
                            request.ToLocationId,
                            request.StockLotId,
                            request.StockStatus,
                            request.Quantity,
                            request.PostingDate,
                            request.IdempotencyKey,
                            request.Notes,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Movement is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "transfer_failed",
                            outcome.ErrorDetail ?? "Location transfer failed.");
                    }

                    var dto = new InventoryMovementResultDto(
                        outcome.Movement.StockMovementId,
                        outcome.Movement.MovementNumber,
                        outcome.Movement.MovementType,
                        outcome.Movement.PostingDate,
                        outcome.IdempotentReplay);

                    return (outcome.IdempotentReplay
                        ? Results.Ok(dto)
                        : Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{request.ItemId}/detail",
                            dto),
                        !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("TransferInventoryLocation")
        .Produces<InventoryMovementResultDto>(StatusCodes.Status201Created)
        .Produces<InventoryMovementResultDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/inspections/{inspectionId:guid}/dispositions", async (
            Guid tenantId,
            Guid entityId,
            Guid inspectionId,
            RecordInspectionDispositionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.QualityInspect],
                "Your active grants for this legal entity do not permit recording quality inspection dispositions.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await QualityInspectionService.RecordDispositionAsync(
                        db,
                        new RecordInspectionDispositionCommand(
                            tenantId,
                            entityId,
                            inspectionId,
                            request.DispositionDate,
                            request.ReleasedBaseQuantity,
                            request.TargetReleaseLocationId,
                            request.BlockedBaseQuantity,
                            request.TargetBlockLocationId,
                            request.ReasonCode,
                            request.EvidenceReference,
                            request.Notes,
                            request.IdempotencyKey,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Inspection is null || outcome.Disposition is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "disposition_failed",
                            outcome.ErrorDetail ?? "Quality inspection disposition failed.");
                    }

                    var summaries = await LoadInspectionsAsync(
                        db,
                        tenantId,
                        entityId,
                        itemIdFilter: null,
                        inspectionIdFilter: outcome.Inspection.ReceiptInspectionId,
                        innerCt);
                    var inspectionDto = summaries.Single();
                    var dispositionDto = inspectionDto.Dispositions
                        .Single(d => d.InspectionDispositionId == outcome.Disposition.InspectionDispositionId);

                    var resultDto = new InspectionDispositionOutcomeDto(
                        inspectionDto,
                        dispositionDto,
                        outcome.StockMovement?.MovementNumber ?? dispositionDto.MovementNumber,
                        outcome.IdempotentReplay);

                    return (outcome.IdempotentReplay
                        ? Results.Ok(resultDto)
                        : Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{inspectionDto.ItemId}/detail",
                            resultDto),
                        !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("RecordInspectionDisposition")
        .Produces<InspectionDispositionOutcomeDto>(StatusCodes.Status201Created)
        .Produces<InspectionDispositionOutcomeDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/status-transitions", async (
            Guid tenantId,
            Guid entityId,
            StockStatusTransitionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.QualityInspect],
                "Your active grants for this legal entity do not permit stock quality or hold status transitions.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await InventoryAvailabilityService.TransitionManualStockStatusAsync(
                        db,
                        new StockStatusTransitionCommand(
                            tenantId,
                            entityId,
                            request.ItemId,
                            request.FromLocationId,
                            request.ToLocationId,
                            request.StockLotId,
                            request.FromStatus,
                            request.ToStatus,
                            request.Quantity,
                            request.PostingDate,
                            request.ReasonCode,
                            request.EvidenceReference,
                            request.Notes,
                            request.IdempotencyKey,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Movement is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "status_transition_failed",
                            outcome.ErrorDetail ?? "Stock status transition failed.");
                    }

                    var dto = new InventoryMovementResultDto(
                        outcome.Movement.StockMovementId,
                        outcome.Movement.MovementNumber,
                        outcome.Movement.MovementType,
                        outcome.Movement.PostingDate,
                        outcome.IdempotentReplay);

                    return (outcome.IdempotentReplay
                        ? Results.Ok(dto)
                        : Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{request.ItemId}/detail",
                            dto),
                        !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("TransitionInventoryStockStatus")
        .Produces<InventoryMovementResultDto>(StatusCodes.Status201Created)
        .Produces<InventoryMovementResultDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/reservations", async (
            Guid tenantId,
            Guid entityId,
            CreateStockReservationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit creating stock reservations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await InventoryAvailabilityService.CreateReservationAsync(
                        db,
                        new CreateStockReservationCommand(
                            tenantId,
                            entityId,
                            request.ItemId,
                            request.LocationId,
                            request.StockLotId,
                            request.SourceType,
                            request.SourceDocumentId,
                            request.SourceDocumentNumber,
                            request.SourceLineId,
                            request.Quantity,
                            request.ExpiresAtUtc,
                            request.IdempotencyKey,
                            request.Notes,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Reservation is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "reservation_failed",
                            outcome.ErrorDetail ?? "Stock reservation creation failed.");
                    }

                    var dto = await LoadSingleReservationDtoAsync(db, tenantId, entityId, outcome.Reservation.StockReservationId, innerCt);
                    var resultDto = new StockReservationOutcomeDto(dto, outcome.IdempotentReplay);

                    return (outcome.IdempotentReplay
                        ? Results.Ok(resultDto)
                        : Results.Created(
                            $"/api/workspaces/{tenantId}/entities/{entityId}/inventory/items/{request.ItemId}/detail",
                            resultDto),
                        !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateStockReservation")
        .Produces<StockReservationOutcomeDto>(StatusCodes.Status201Created)
        .Produces<StockReservationOutcomeDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/reservations/{reservationId:guid}/release", async (
            Guid tenantId,
            Guid entityId,
            Guid reservationId,
            ReleaseStockReservationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit releasing stock reservations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorUserId, innerCt) =>
                {
                    var outcome = await InventoryAvailabilityService.ReleaseReservationAsync(
                        db,
                        tenantId,
                        entityId,
                        reservationId,
                        request.ReleaseBaseQuantity,
                        actorUserId,
                        DateTimeOffset.UtcNow,
                        innerCt);

                    if (!outcome.Succeeded || outcome.Reservation is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "reservation_release_failed",
                            outcome.ErrorDetail ?? "Stock reservation release failed.");
                    }

                    var dto = await LoadSingleReservationDtoAsync(db, tenantId, entityId, outcome.Reservation.StockReservationId, innerCt);
                    return (Results.Ok(new StockReservationOutcomeDto(dto, outcome.IdempotentReplay)), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ReleaseStockReservation")
        .Produces<StockReservationOutcomeDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/inventory/issues", async (
            Guid tenantId,
            Guid entityId,
            CreateOperationalIssueRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingInventoryScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.InventoryManage],
                "Your active grants for this legal entity do not permit recording operational inventory issues.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var outcome = await InventoryValuationService.PostOperationalIssueAsync(
                        db,
                        new PostOperationalIssueCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.ItemId,
                            request.LocationId,
                            request.StockLotId,
                            request.Quantity,
                            request.PostingDate,
                            request.SourceReference,
                            request.IdempotencyKey,
                            request.Notes,
                            actorUserId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Movement is null || outcome.ValuationEntry is null)
                    {
                        throw new AbortedDomainOperationException(
                            outcome.FailureHttpStatus,
                            outcome.ErrorCode ?? "operational_issue_failed",
                            outcome.ErrorDetail ?? "Operational stock issue failed.");
                    }

                    var dto = new OperationalIssueOutcomeDto(
                        outcome.Movement.StockMovementId,
                        outcome.Movement.MovementNumber,
                        outcome.Movement.PostingDate,
                        Math.Abs(outcome.ValuationEntry.QuantityDelta),
                        Math.Abs(outcome.ValuationEntry.ValueDelta),
                        outcome.ValuationEntry.UnitCostApplied,
                        outcome.GlJournal?.JournalId,
                        outcome.GlJournal?.JournalNumber,
                        outcome.IdempotentReplay);
                    return (outcome.IdempotentReplay ? Results.Ok(dto) : Results.Created(string.Empty, dto), !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateOperationalInventoryIssue")
        .Produces<OperationalIssueOutcomeDto>(StatusCodes.Status201Created)
        .Produces<OperationalIssueOutcomeDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ExecuteMutatingInventoryScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        string deniedDetail,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<(IResult Result, bool ShouldNotifyOutbox)>> action,
        CancellationToken ct)
    {
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return EndpointSupport.Unauthenticated(httpContext);
        }

        try
        {
            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                null,
                async (scope, innerCt) =>
                {
                    if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                    {
                        return (Result: EndpointSupport.Forbidden(httpContext, "insufficient_entity_permission", deniedDetail), ShouldNotifyOutbox: false);
                    }

                    return await action(scope, actorUserId, innerCt);
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            if (scopeResult.Value.ShouldNotifyOutbox)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return scopeResult.Value.Result;
        }
        catch (AbortedDomainOperationException aborted)
        {
            return MapDomainFailure(httpContext, aborted.StatusCode, aborted.ErrorCode, aborted.ErrorDetail);
        }
        catch (ArgumentException ex)
        {
            return EndpointSupport.BadRequest(httpContext, "invalid_inventory_request", ex.Message);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
        {
            return MapPersistenceConflict(httpContext, ex);
        }
    }

    private static async Task<IReadOnlyList<WarehouseLocationDto>> LoadLocationDtosAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var warehouses = await db.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.LegalEntityId == entityId)
            .ToDictionaryAsync(w => w.WarehouseId, w => (w.WarehouseCode, w.Name, w.IsDefault), ct);

        var locations = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .OrderBy(l => l.LocationCode)
            .ToListAsync(ct);

        return locations
            .OrderByDescending(l => warehouses.TryGetValue(l.WarehouseId, out var wh) && wh.IsDefault)
            .ThenBy(l => warehouses.TryGetValue(l.WarehouseId, out var wh) ? wh.WarehouseCode : string.Empty)
            .ThenByDescending(l => l.IsDefaultReceiving)
            .ThenBy(l => l.LocationCode)
            .Select(l =>
            {
                var wh = warehouses.GetValueOrDefault(l.WarehouseId, ("WH", "Warehouse", false));
                return new WarehouseLocationDto(
                    l.LocationId,
                    l.WarehouseId,
                    wh.Item1,
                    wh.Item2,
                    l.LocationCode,
                    l.Name,
                    l.ZoneType,
                    l.IsDefaultReceiving,
                    l.Status,
                    l.CreatedAtUtc);
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<ReceiptInspectionSummaryDto>> LoadInspectionsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid? itemIdFilter,
        Guid? inspectionIdFilter,
        CancellationToken ct)
    {
        var query = db.ReceiptInspections.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.LegalEntityId == entityId);

        if (itemIdFilter.HasValue)
        {
            query = query.Where(i => i.ItemId == itemIdFilter.Value);
        }

        if (inspectionIdFilter.HasValue)
        {
            query = query.Where(i => i.ReceiptInspectionId == inspectionIdFilter.Value);
        }

        var inspections = (await query.ToListAsync(ct))
            .OrderBy(i => i.Status == ReceiptInspectionStatuses.Pending ? 0 : (i.Status == ReceiptInspectionStatuses.PartiallyDecided ? 1 : 2))
            .ThenByDescending(i => i.CreatedAtUtc)
            .ToList();

        if (inspections.Count == 0)
        {
            return Array.Empty<ReceiptInspectionSummaryDto>();
        }

        var inspectionIds = inspections.Select(i => i.ReceiptInspectionId).ToList();
        var vaIds = inspections.Select(i => i.ValuedAcceptanceId).Distinct().ToList();
        var grIds = inspections.Select(i => i.GoodsReceiptId).Distinct().ToList();
        var itemIds = inspections.Select(i => i.ItemId).Distinct().ToList();
        var lotIds = inspections.Where(i => i.StockLotId.HasValue).Select(i => i.StockLotId!.Value).Distinct().ToList();

        var dispositions = await db.InspectionDispositions.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.LegalEntityId == entityId && inspectionIds.Contains(d.ReceiptInspectionId))
            .OrderBy(d => d.DecidedAtUtc)
            .ToListAsync(ct);

        var locationIds = inspections.Select(i => i.QuarantineLocationId)
            .Concat(dispositions.Where(d => d.TargetReleaseLocationId.HasValue).Select(d => d.TargetReleaseLocationId!.Value))
            .Concat(dispositions.Where(d => d.TargetBlockLocationId.HasValue).Select(d => d.TargetBlockLocationId!.Value))
            .Distinct()
            .ToList();

        var movIds = dispositions.Select(d => d.StockMovementId).Distinct().ToList();
        var actorIds = dispositions.Select(d => d.DecidedByUserId).Distinct().ToList();

        var vaNums = await db.ValuedAcceptances.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.LegalEntityId == entityId && vaIds.Contains(v.ValuedAcceptanceId))
            .ToDictionaryAsync(v => v.ValuedAcceptanceId, v => v.AcceptanceNumber, ct);

        var grNums = await db.GoodsReceipts.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && grIds.Contains(r.GoodsReceiptId))
            .ToDictionaryAsync(r => r.GoodsReceiptId, r => r.ReceiptNumber, ct);

        var items = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, i => (i.ItemCode, i.Name, i.BaseUomId), ct);

        var uomIds = items.Values.Select(v => v.BaseUomId).Distinct().ToList();
        var uomMap = uomIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.UnitsOfMeasure.AsNoTracking()
                .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
                .ToDictionaryAsync(u => u.UomId, u => u.UomCode, ct);

        var locCodes = locationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.WarehouseLocations.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && locationIds.Contains(l.LocationId))
                .ToDictionaryAsync(l => l.LocationId, l => l.LocationCode, ct);

        var lotNums = lotIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockLots.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && lotIds.Contains(l.StockLotId))
                .ToDictionaryAsync(l => l.StockLotId, l => l.LotNumber, ct);

        var movNums = movIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movIds.Contains(m.StockMovementId))
                .ToDictionaryAsync(m => m.StockMovementId, m => m.MovementNumber, ct);

        var emails = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var dispByInspection = dispositions
            .GroupBy(d => d.ReceiptInspectionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<InspectionDispositionDto>)g.Select(d => new InspectionDispositionDto(
                    d.InspectionDispositionId,
                    d.ReceiptInspectionId,
                    d.StockMovementId,
                    movNums.GetValueOrDefault(d.StockMovementId, "-"),
                    d.DispositionDate,
                    d.ReleasedBaseQuantity,
                    d.TargetReleaseLocationId,
                    d.TargetReleaseLocationId is { } relId ? locCodes.GetValueOrDefault(relId) : null,
                    d.BlockedBaseQuantity,
                    d.TargetBlockLocationId,
                    d.TargetBlockLocationId is { } blkId ? locCodes.GetValueOrDefault(blkId) : null,
                    d.ReasonCode,
                    d.EvidenceReference,
                    d.Notes,
                    emails.GetValueOrDefault(d.DecidedByUserId, d.DecidedByUserId.ToString("D")),
                    d.DecidedAtUtc)).ToList());

        return inspections.Select(i =>
        {
            var itemInfo = items.GetValueOrDefault(i.ItemId, ("ITEM", "Catalog Item", Guid.Empty));
            var baseUomCode = uomMap.GetValueOrDefault(itemInfo.Item3, "PCS");
            return new ReceiptInspectionSummaryDto(
                i.ReceiptInspectionId,
                i.InspectionNumber,
                i.ValuedAcceptanceId,
                vaNums.GetValueOrDefault(i.ValuedAcceptanceId, "-"),
                i.GoodsReceiptId,
                grNums.GetValueOrDefault(i.GoodsReceiptId, "-"),
                i.ItemId,
                itemInfo.Item1,
                itemInfo.Item2,
                baseUomCode,
                i.WarehouseId,
                i.QuarantineLocationId,
                locCodes.GetValueOrDefault(i.QuarantineLocationId, InventoryAvailabilityDefaults.DefaultQuarantineLocationCode),
                i.StockLotId,
                i.StockLotId is { } lotId ? lotNums.GetValueOrDefault(lotId) : null,
                i.InspectedTotalBaseQuantity,
                i.PendingBaseQuantity,
                i.ReleasedBaseQuantity,
                i.BlockedBaseQuantity,
                i.Status,
                i.CreatedAtUtc,
                i.UpdatedAtUtc,
                dispByInspection.GetValueOrDefault(i.ReceiptInspectionId, Array.Empty<InspectionDispositionDto>()));
        }).ToList();
    }

    private static async Task<ItemStockDetailResponse?> LoadItemStockDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid itemId,
        CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking()
            .SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, ct);
        if (item is null)
        {
            return null;
        }

        var baseUomCode = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.UomId == item.BaseUomId)
            .Select(u => u.UomCode)
            .SingleOrDefaultAsync(ct) ?? "PCS";

        var pool = await db.ValuationPools.AsNoTracking()
            .SingleOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.LegalEntityId == entityId &&
                p.ItemId == itemId &&
                p.ValuationGroupCode == InventoryValuationDefaults.DefaultValuationGroupCode,
                ct);

        var recon = await InventoryAvailabilityService.VerifyItemReconciliationAsync(db, tenantId, entityId, itemId, ct);

        var warehouses = await db.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.LegalEntityId == entityId)
            .ToDictionaryAsync(w => w.WarehouseId, w => w.WarehouseCode, ct);

        var locations = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .ToDictionaryAsync(l => l.LocationId, l => (l.LocationCode, l.Name, l.ZoneType), ct);

        var lotsRaw = await db.StockLots.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.ItemId == itemId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);

        var lotVaIds = lotsRaw.Where(l => l.SourceValuedAcceptanceId.HasValue).Select(l => l.SourceValuedAcceptanceId!.Value).Distinct().ToList();
        var vaNums = lotVaIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.ValuedAcceptances.AsNoTracking()
                .Where(v => v.TenantId == tenantId && v.LegalEntityId == entityId && lotVaIds.Contains(v.ValuedAcceptanceId))
                .ToDictionaryAsync(v => v.ValuedAcceptanceId, v => v.AcceptanceNumber, ct);

        var lotMap = lotsRaw.ToDictionary(l => l.StockLotId, l => (l.LotNumber, l.ExpiryDate));
        var lotDtos = lotsRaw.Select(l => new StockLotSummaryDto(
            l.StockLotId,
            l.ItemId,
            l.LotNumber,
            l.SupplierLotReference,
            l.ManufacturedDate,
            l.ExpiryDate,
            l.SourceValuedAcceptanceId,
            l.SourceValuedAcceptanceId is { } vaId ? vaNums.GetValueOrDefault(vaId) : null,
            l.CreatedAtUtc)).ToList();

        var balancesRaw = await db.StockLocationBalances.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.ItemId == itemId && (b.OnHandBaseQuantity > 0m || b.ReservedBaseQuantity > 0m))
            .ToListAsync(ct);

        var balanceDtos = balancesRaw
            .Select(b =>
            {
                var whCode = warehouses.GetValueOrDefault(b.WarehouseId, "WH");
                var locInfo = locations.GetValueOrDefault(b.LocationId, ("LOC", "Location", LocationZoneTypes.Storage));
                var lotInfo = b.StockLotId is { } lid && lotMap.TryGetValue(lid, out var li) ? li : default((string?, DateOnly?));
                return new StockLocationBalanceDto(
                    b.StockLocationBalanceId,
                    b.WarehouseId,
                    whCode,
                    b.LocationId,
                    locInfo.Item1,
                    locInfo.Item2,
                    locInfo.Item3,
                    b.StockLotId,
                    lotInfo.Item1,
                    lotInfo.Item2,
                    b.StockStatus,
                    b.OnHandBaseQuantity,
                    b.ReservedBaseQuantity,
                    b.AvailableBaseQuantity,
                    b.UpdatedAtUtc);
            })
            .OrderBy(b => b.StockStatus == StockStatuses.Unrestricted ? 0 : (b.StockStatus == StockStatuses.QualityInspection ? 1 : 2))
            .ThenBy(b => b.WarehouseCode)
            .ThenBy(b => b.LocationCode)
            .ThenBy(b => b.LotNumber)
            .ToList();

        var inspections = await LoadInspectionsAsync(db, tenantId, entityId, itemIdFilter: itemId, inspectionIdFilter: null, ct);

        var reservationsRaw = await db.StockReservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.ItemId == itemId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var linesRaw = await db.MovementLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.ItemId == itemId)
            .ToListAsync(ct);

        var movIds = linesRaw.Select(l => l.StockMovementId).Distinct().ToList();
        var movements = movIds.Count == 0
            ? new Dictionary<Guid, StockMovement>()
            : await db.StockMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && movIds.Contains(m.StockMovementId))
                .ToDictionaryAsync(m => m.StockMovementId, ct);

        var userIds = reservationsRaw.Select(r => r.CreatedByUserId)
            .Concat(movements.Values.Select(m => m.CreatedByUserId))
            .Distinct()
            .ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var reservationDtos = reservationsRaw.Select(r =>
        {
            var locInfo = locations.GetValueOrDefault(r.LocationId, ("LOC", "Location", LocationZoneTypes.Storage));
            var lotNumber = r.StockLotId is { } lid && lotMap.TryGetValue(lid, out var li) ? li.Item1 : null;
            return new StockReservationDto(
                r.StockReservationId,
                r.ReservationNumber,
                r.StockLocationBalanceId,
                r.ItemId,
                item.ItemCode,
                r.WarehouseId,
                r.LocationId,
                locInfo.Item1,
                r.StockLotId,
                lotNumber,
                r.SourceType,
                r.SourceDocumentId,
                r.SourceDocumentNumber,
                r.ReservedBaseQuantity,
                r.ConsumedBaseQuantity,
                r.ReleasedBaseQuantity,
                r.RemainingReservedBaseQuantity,
                r.Status,
                r.Notes,
                r.ExpiresAtUtc,
                emails.GetValueOrDefault(r.CreatedByUserId, r.CreatedByUserId.ToString("D")),
                r.CreatedAtUtc,
                r.UpdatedAtUtc);
        }).ToList();

        var timelineDtos = linesRaw
            .Select(l =>
            {
                var mov = movements[l.StockMovementId];
                var locInfo = l.LocationId is { } locId && locations.TryGetValue(locId, out var li)
                    ? li
                    : (string.Empty, string.Empty, string.Empty);
                var lotNumber = l.StockLotId is { } lotId && lotMap.TryGetValue(lotId, out var lti) ? lti.Item1 : null;
                return (Movement: mov, Line: l, Dto: new ItemMovementTraceDto(
                    mov.StockMovementId,
                    l.MovementLineId,
                    mov.MovementNumber,
                    mov.MovementType,
                    mov.PostingDate,
                    mov.SourceDocumentType,
                    mov.SourceDocumentNumber,
                    string.IsNullOrEmpty(locInfo.Item1) ? null : locInfo.Item1,
                    string.IsNullOrEmpty(locInfo.Item3) ? null : locInfo.Item3,
                    lotNumber,
                    l.ResultingStockStatus,
                    l.BaseQuantityDelta,
                    l.UnitValuationCost,
                    l.LineValuationAmount,
                    mov.Notes,
                    emails.GetValueOrDefault(mov.CreatedByUserId, mov.CreatedByUserId.ToString("D")),
                    mov.CreatedAtUtc));
            })
            .OrderByDescending(x => x.Movement.PostingDate)
            .ThenByDescending(x => x.Movement.CreatedAtUtc)
            .ThenBy(x => x.Line.LineNumber)
            .Take(200)
            .Select(x => x.Dto)
            .ToList();

        return new ItemStockDetailResponse(
            item.ItemId,
            item.ItemCode,
            item.Name,
            baseUomCode,
            item.LotTrackingPolicy,
            pool?.CurrencyCode ?? "IDR",
            pool?.Status ?? ValuationPoolStatuses.Active,
            pool?.CarryingValueAmount ?? 0m,
            pool?.MovingAverageUnitCost ?? 0m,
            new ItemReconciliationDto(
                recon.IsReconciled,
                recon.PositionOnHandTotal,
                recon.PositionUnrestricted,
                recon.PositionQualityInspection,
                recon.PositionBlocked,
                recon.PositionReserved,
                recon.PositionAvailable,
                recon.LocationBalancesOnHandTotal,
                recon.LocationBalancesUnrestricted,
                recon.LocationBalancesQualityInspection,
                recon.LocationBalancesBlocked,
                recon.LocationBalancesReserved,
                recon.MovementsNetTotal,
                recon.ActiveReservationsRemaining),
            balanceDtos,
            lotDtos,
            inspections,
            reservationDtos,
            timelineDtos);
    }

    private static async Task<StockReservationDto> LoadSingleReservationDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid reservationId,
        CancellationToken ct)
    {
        var r = await db.StockReservations.AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.StockReservationId == reservationId, ct);

        var itemCode = await db.Items.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.ItemId == r.ItemId)
            .Select(i => i.ItemCode)
            .SingleAsync(ct);

        var locationCode = await db.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.LocationId == r.LocationId)
            .Select(l => l.LocationCode)
            .SingleAsync(ct);

        var lotNumber = r.StockLotId is { } lid
            ? await db.StockLots.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.StockLotId == lid)
                .Select(l => l.LotNumber)
                .SingleOrDefaultAsync(ct)
            : null;

        var email = await db.Users.AsNoTracking()
            .Where(u => u.Id == r.CreatedByUserId)
            .Select(u => u.Email ?? u.UserName)
            .SingleOrDefaultAsync(ct) ?? r.CreatedByUserId.ToString("D");

        return new StockReservationDto(
            r.StockReservationId,
            r.ReservationNumber,
            r.StockLocationBalanceId,
            r.ItemId,
            itemCode,
            r.WarehouseId,
            r.LocationId,
            locationCode,
            r.StockLotId,
            lotNumber,
            r.SourceType,
            r.SourceDocumentId,
            r.SourceDocumentNumber,
            r.ReservedBaseQuantity,
            r.ConsumedBaseQuantity,
            r.ReleasedBaseQuantity,
            r.RemainingReservedBaseQuantity,
            r.Status,
            r.Notes,
            r.ExpiresAtUtc,
            email,
            r.CreatedAtUtc,
            r.UpdatedAtUtc);
    }

    private static IResult MapDomainFailure(HttpContext httpContext, int statusCode, string errorCode, string detail) =>
        statusCode switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, errorCode, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, errorCode, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, errorCode, detail),
            _ => EndpointSupport.BadRequest(httpContext, errorCode, detail)
        };

    private static async Task<IResult> ExecuteInventoryScopeAsync(
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

        return result.IsAllowed
            ? result.Value!
            : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static IResult MapFailure(HttpContext httpContext, ReceiptAcceptanceOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "acceptance_failed";
        var detail = outcome.ErrorDetail ?? "Valued receipt acceptance failed.";
        return outcome.FailureHttpStatus switch
        {
            StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;

    private static IResult MapPersistenceConflict(HttpContext httpContext, Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Concurrent valued acceptance or inventory update detected; reload and retry.");
        }

        if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return pgEx.ConstraintName switch
            {
                "ux_valued_acceptances_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_acceptance_idempotency_conflict",
                    "Idempotency key already belongs to a different Gate 2 valued acceptance."),
                "ux_valued_acceptances_number" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_acceptance_number",
                    "Duplicate valued acceptance number detected."),
                "ux_stock_movements_number" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_movement_number",
                    "Duplicate stock movement number detected."),
                "ux_stock_movements_source_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_movement_idempotency_conflict",
                    "Idempotency key already belongs to a different stock movement."),
                "ux_stock_reservations_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_reservation_idempotency_conflict",
                    "Idempotency key already belongs to a different stock reservation."),
                "ux_inspection_dispositions_idempotency" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_disposition_idempotency_conflict",
                    "Idempotency key already belongs to a different quality inspection disposition."),
                "ux_warehouses_code" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_warehouse_code",
                    "Warehouse code already exists in this legal entity."),
                "ux_locations_code" => EndpointSupport.Conflict(
                    httpContext,
                    "duplicate_location_code",
                    "Location code already exists in this legal entity."),
                _ => EndpointSupport.Conflict(httpContext, "inventory_acceptance_conflict", "Concurrent acceptance or stock update conflict occurred.")
            };
        }

        return EndpointSupport.Conflict(httpContext, "inventory_acceptance_conflict", "Concurrent acceptance conflict occurred.");
    }

    private static async Task<ValuedAcceptanceDetailDto?> LoadValuedAcceptanceDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid acceptanceId,
        bool wasIdempotentReplay,
        CancellationToken ct)
    {
        var va = await db.ValuedAcceptances.AsNoTracking()
            .SingleOrDefaultAsync(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.ValuedAcceptanceId == acceptanceId, ct);
        if (va is null)
        {
            return null;
        }

        var receipt = await db.GoodsReceipts.AsNoTracking()
            .SingleAsync(r => r.TenantId == tenantId && r.LegalEntityId == entityId && r.GoodsReceiptId == va.GoodsReceiptId, ct);

        var lines = await db.ValuedAcceptanceLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.ValuedAcceptanceId == va.ValuedAcceptanceId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var itemMap = itemIds.Count == 0
            ? new Dictionary<Guid, (string Code, string Name)>()
            : await db.Items.AsNoTracking()
                .Where(i => i.TenantId == tenantId && itemIds.Contains(i.ItemId))
                .ToDictionaryAsync(i => i.ItemId, i => (i.ItemCode, i.Name), ct);

        var movNum = va.StockMovementId is { } mid
            ? await db.StockMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.StockMovementId == mid)
                .Select(m => m.MovementNumber)
                .SingleOrDefaultAsync(ct)
            : null;

        var accNum = va.ReceiptAccrualId is { } aid
            ? await db.ReceiptAccruals.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.ReceiptAccrualId == aid)
                .Select(a => a.AccrualNumber)
                .SingleOrDefaultAsync(ct)
            : null;

        var jrnNum = va.GlJournalId is { } jid
            ? await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && j.JournalId == jid)
                .Select(j => j.JournalNumber)
                .SingleOrDefaultAsync(ct)
            : null;

        var actorEmail = await db.Users.AsNoTracking()
            .Where(u => u.Id == va.AcceptedByUserId)
            .Select(u => u.Email ?? u.UserName)
            .SingleOrDefaultAsync(ct) ?? va.AcceptedByUserId.ToString("D");

        var lineDtos = lines.Select(l =>
        {
            var item = itemMap.GetValueOrDefault(l.ItemId, ("ITEM", "Catalog Item"));
            return new ValuedAcceptanceLineDto(
                l.ValuedAcceptanceLineId,
                l.LineNumber,
                l.GoodsReceiptLineId,
                l.PoLineId,
                l.ItemId,
                item.Item1,
                item.Item2,
                l.BaseUomId,
                l.BaseUomCodeSnapshot,
                l.AcceptedBaseQuantity,
                l.UnitPriceBaseSnapshot,
                l.AcceptedLineAmount);
        }).ToList();

        return new ValuedAcceptanceDetailDto(
            va.ValuedAcceptanceId,
            va.AcceptanceNumber,
            va.GoodsReceiptId,
            receipt.ReceiptNumber,
            receipt.Status,
            va.PurchaseOrderId,
            receipt.PoNumberSnapshot,
            receipt.PoRevisionNumberSnapshot,
            va.SupplierPartnerId,
            receipt.SupplierCodeSnapshot,
            receipt.SupplierLegalNameSnapshot,
            va.AcceptanceDate,
            va.StockStatus,
            va.CurrencyCode,
            va.TotalAcceptedBaseQuantity,
            va.TotalAcceptedAmount,
            va.StockMovementId,
            movNum,
            va.ReceiptAccrualId,
            accNum,
            va.GlJournalId,
            jrnNum,
            va.Notes,
            va.AcceptedByUserId,
            actorEmail,
            va.CreatedAtUtc,
            wasIdempotentReplay,
            lineDtos);
    }

    private sealed class AbortedAcceptanceWorkflowException(ReceiptAcceptanceOutcome outcome) : Exception
    {
        public ReceiptAcceptanceOutcome Outcome { get; } = outcome;
    }

    private sealed class AbortedDomainOperationException(int statusCode, string errorCode, string errorDetail) : Exception(errorDetail)
    {
        public int StatusCode { get; } = statusCode;
        public string ErrorCode { get; } = errorCode;
        public string ErrorDetail { get; } = errorDetail;
    }
}
