using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.Api.Endpoints;

internal static class InventoryEndpoints
{
    private const string Od04DefaultNotice =
        "OD-04 strict safe defaults active (ATH-CR-004): moving-average weighted cost in entity currency (IDR), zero/negative valuation entries rejected, and anomalous pools blocked automatically.";

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        MapGateTwoAcceptanceRoutes(app);
        MapInventoryOverviewRoutes(app);
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
                                .Select(l => new AcceptReceiptLineInput(l.GoodsReceiptLineId, l.AcceptedBaseQuantity))
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
                            l.BlockingReason)).ToList(),
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
                                    .Select(l => new AcceptReceiptLineInput(l.GoodsReceiptLineId, l.AcceptedBaseQuantity))
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
                [PermissionCodes.InventoryRead],
                "Your active grants for this legal entity do not permit viewing inventory valuation and GRNI accruals.",
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
                        ? new Dictionary<Guid, (string Code, string Name)>()
                        : await db.Items.AsNoTracking()
                            .Where(i => i.TenantId == tenantId && posItemIds.Contains(i.ItemId))
                            .ToDictionaryAsync(i => i.ItemId, i => (i.ItemCode, i.Name), innerCt);

                    var uomIds = positions.Select(p => p.BaseUomId).Distinct().ToList();
                    var uomCodes = uomIds.Count == 0
                        ? new Dictionary<Guid, string>()
                        : await db.UnitsOfMeasure.AsNoTracking()
                            .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UomId))
                            .ToDictionaryAsync(u => u.UomId, u => u.UomCode, innerCt);

                    var allPositionDtos = positions.Select(p =>
                    {
                        pools.TryGetValue(p.ItemId, out var pool);
                        var itemInfo = items.GetValueOrDefault(p.ItemId, ("ITEM", "Catalog Item"));
                        return new StockPositionOverviewDto(
                            p.StockPositionId,
                            p.ItemId,
                            itemInfo.Item1,
                            itemInfo.Item2,
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
                                g => (g.Count(), g.Sum(x => x.BaseQuantityDelta), g.Sum(x => x.LineValuationAmount)));

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

                    return Results.Ok(new InventoryOverviewResponse(
                        filteredPositions.ToList(),
                        movementDtos,
                        accrualDtos.ToList(),
                        totalStockedWithBalance,
                        totalCarryingValue,
                        totalOpenGrni,
                        restrictedCount,
                        Od04DefaultNotice));
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
                [PermissionCodes.InventoryRead],
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

                    var movIds = entries.Select(e => e.StockMovementId).Distinct().ToList();
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
                        movNums.GetValueOrDefault(e.StockMovementId, "-"),
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
                _ => EndpointSupport.Conflict(httpContext, "inventory_acceptance_conflict", "Concurrent acceptance conflict occurred.")
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
}
