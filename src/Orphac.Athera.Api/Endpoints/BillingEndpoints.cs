using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Endpoints;

public static class BillingEndpoints
{
    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.FinanceRead,
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.SalesRead,
        PermissionCodes.SalesManage,
        PermissionCodes.SalesApprove
    ];

    private static readonly string[] DraftPermissions =
    [
        PermissionCodes.SalesManage,
        PermissionCodes.FinanceJournalPost
    ];

    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/billing")
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
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            var scoped = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission: null,
                async (scope, innerCt) =>
                {
                    var canRead = scope.EffectivePermissions.Any(p => ReadPermissions.Contains(p, StringComparer.Ordinal));
                    if (!canRead)
                    {
                        return BillingOutcome<BillingOverviewResponse>.Fail(
                            403, "billing_read_denied", "Billing access denied",
                            "You need finance or sales read permissions on this legal entity.");
                    }

                    var canDraft = scope.EffectivePermissions.Any(p => DraftPermissions.Contains(p, StringComparer.Ordinal));
                    var canPost = scope.EffectivePermissions.Contains(PermissionCodes.FinanceJournalPost, StringComparer.Ordinal);

                    var taxCodes = await db.TaxCodes.AsNoTracking()
                        .Where(t => t.TenantId == tenantId
                            && t.LegalEntityId == entityId
                            && t.Direction == TaxDirections.Output
                            && t.Status == MasterStatuses.Active)
                        .OrderBy(t => t.Code)
                        .ToListAsync(innerCt);

                    var taxCodeDtos = taxCodes.Select(t =>
                    {
                        var eff = decimal.Round(t.NominalRatePercent * ((decimal)t.TaxableBaseRatioNumerator / t.TaxableBaseRatioDenominator), 4, MidpointRounding.AwayFromZero);
                        return new BillingOutputTaxCodeDto(
                            t.TaxCodeId,
                            t.Code,
                            t.Name,
                            t.TreatmentClass,
                            t.NominalRatePercent,
                            t.TaxableBaseRatioNumerator,
                            t.TaxableBaseRatioDenominator,
                            eff,
                            BillingTaxCalculator.RequiresEvidence(t),
                            t.EffectiveFrom,
                            t.EffectiveTo);
                    }).ToList();

                    // Load eligible shipments & shipment lines
                    var shipments = await db.SalesShipments.AsNoTracking()
                        .Where(s => s.TenantId == tenantId
                            && s.LegalEntityId == entityId
                            && (s.Status == ShipmentStatuses.Dispatched || s.Status == ShipmentStatuses.Delivered))
                        .OrderByDescending(s => s.DispatchedAtUtc)
                        .ToListAsync(innerCt);

                    var shipmentMap = shipments.ToDictionary(s => s.ShipmentId);
                    var shipmentIds = shipments.Select(s => s.ShipmentId).ToList();

                    var shipmentLines = shipmentIds.Count == 0
                        ? []
                        : await db.SalesShipmentLines.AsNoTracking()
                            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && shipmentIds.Contains(l.ShipmentId))
                            .OrderBy(l => l.LineNumber)
                            .ToListAsync(innerCt);

                    var shipmentLineIds = shipmentLines.Select(l => l.ShipmentLineId).ToList();
                    var billedByShipmentLine = await BillingAndReceivablesCoordinator.GetActiveBilledQuantitiesByShipmentLineAsync(
                        db, tenantId, entityId, shipmentLineIds, excludeInvoiceId: null, innerCt);

                    var soIds = shipments.Select(s => s.SalesOrderId).Distinct().ToList();
                    var salesOrders = soIds.Count == 0
                        ? new Dictionary<Guid, Modules.Sales.SalesOrder>()
                        : await db.SalesOrders.AsNoTracking()
                            .Where(so => so.TenantId == tenantId && so.LegalEntityId == entityId && soIds.Contains(so.SalesOrderId))
                            .ToDictionaryAsync(so => so.SalesOrderId, innerCt);

                    var customerIds = shipments.Select(s => s.CustomerPartnerId).Distinct().ToList();
                    var customers = customerIds.Count == 0
                        ? new Dictionary<Guid, Modules.Partners.Partner>()
                        : await db.Partners.AsNoTracking()
                            .Where(p => p.TenantId == tenantId && customerIds.Contains(p.PartnerId))
                            .ToDictionaryAsync(p => p.PartnerId, innerCt);

                    var soLineIds = shipmentLines.Select(l => l.SoLineId).Distinct().ToList();
                    var soLines = soLineIds.Count == 0
                        ? new Dictionary<Guid, Modules.Sales.SalesOrderLine>()
                        : await db.SalesOrderLines.AsNoTracking()
                            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && soLineIds.Contains(l.SoLineId))
                            .ToDictionaryAsync(l => l.SoLineId, innerCt);

                    var billableDtos = new List<BillableShipmentLineDto>();
                    foreach (var sl in shipmentLines)
                    {
                        var shp = shipmentMap[sl.ShipmentId];
                        var alreadyBilled = billedByShipmentLine.GetValueOrDefault(sl.ShipmentLineId, 0m);
                        var remaining = decimal.Round(sl.DispatchedBaseQuantity - alreadyBilled, 6, MidpointRounding.AwayFromZero);
                        if (remaining <= 0m) continue;

                        soLines.TryGetValue(sl.SoLineId, out var soLine);
                        salesOrders.TryGetValue(shp.SalesOrderId, out var so);
                        customers.TryGetValue(shp.CustomerPartnerId, out var cust);

                        var unitPriceBase = soLine is not null && soLine.UomFactorToBaseSnapshot > 0m
                            ? decimal.Round(soLine.UnitPrice / soLine.UomFactorToBaseSnapshot, 6, MidpointRounding.AwayFromZero)
                            : (soLine?.UnitPrice ?? 0m);

                        billableDtos.Add(new BillableShipmentLineDto(
                            sl.ShipmentLineId,
                            sl.ShipmentId,
                            shp.ShipmentNumber,
                            shp.DispatchDate,
                            shp.Status,
                            shp.SalesOrderId,
                            shp.SalesOrderNumberSnapshot,
                            shp.CustomerPartnerId,
                            shp.CustomerCodeSnapshot,
                            shp.CustomerNameSnapshot,
                            cust?.TaxRegistrationNpwp,
                            so?.PaymentTermsDays ?? 30,
                            sl.SoLineId,
                            sl.LineageLineKey,
                            sl.ItemId,
                            sl.ItemCodeSnapshot,
                            sl.ItemNameSnapshot,
                            sl.BaseUomCodeSnapshot,
                            sl.DispatchedBaseQuantity,
                            alreadyBilled,
                            remaining,
                            unitPriceBase,
                            soLine?.TaxCodeId,
                            soLine?.TaxCodeSnapshot));
                    }

                    // Load invoices, lines, and receivables
                    var invoices = await db.CustomerInvoices.AsNoTracking()
                        .Where(i => i.TenantId == tenantId && i.LegalEntityId == entityId)
                        .OrderByDescending(i => i.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    var invIds = invoices.Select(i => i.InvoiceId).ToList();
                    var allInvLines = invIds.Count == 0
                        ? []
                        : await db.CustomerInvoiceLines.AsNoTracking()
                            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && invIds.Contains(l.InvoiceId))
                            .OrderBy(l => l.LineNumber)
                            .ToListAsync(innerCt);

                    var postedCnIds = invoices
                        .Where(i => i.DocumentType == InvoiceDocumentTypes.CreditNote && i.Status == CustomerInvoiceStatuses.Posted)
                        .Select(i => i.InvoiceId)
                        .ToHashSet();

                    var creditedByOrigLine = allInvLines
                        .Where(l => postedCnIds.Contains(l.InvoiceId) && l.OriginalInvoiceLineId.HasValue)
                        .GroupBy(l => l.OriginalInvoiceLineId!.Value)
                        .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.BilledBaseQuantity), 6, MidpointRounding.AwayFromZero));

                    var linesByInvId = allInvLines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

                    var invoiceDtos = invoices.Select(inv =>
                    {
                        var invLines = linesByInvId.GetValueOrDefault(inv.InvoiceId, []);
                        return new CustomerInvoiceDto(
                            inv.InvoiceId,
                            inv.InvoiceNumber,
                            inv.DocumentType,
                            inv.OriginalInvoiceId,
                            inv.OriginalInvoiceNumberSnapshot,
                            inv.SalesOrderId,
                            inv.SalesOrderNumberSnapshot,
                            inv.CustomerPartnerId,
                            inv.CustomerCodeSnapshot,
                            inv.CustomerNameSnapshot,
                            inv.CustomerNpwpSnapshot,
                            inv.InvoiceDate,
                            inv.DueDate,
                            inv.CurrencyCode,
                            inv.Status,
                            inv.SubtotalAmount,
                            inv.TaxAmount,
                            inv.GrandTotalAmount,
                            inv.TaxCodeId,
                            inv.TaxCodeSnapshot,
                            inv.TaxTreatmentClassSnapshot,
                            inv.TaxRatePercentSnapshot,
                            inv.RequiresTaxEvidenceSnapshot,
                            inv.TaxEvidenceReference,
                            inv.TaxDeterminationSummary,
                            inv.GlJournalId,
                            inv.GlJournalNumber,
                            inv.ArReceivableId,
                            inv.Notes,
                            inv.CreatedAtUtc,
                            inv.SubmittedAtUtc,
                            inv.PostedAtUtc,
                            invLines.Select(l =>
                            {
                                var credited = inv.DocumentType == InvoiceDocumentTypes.Invoice
                                    ? creditedByOrigLine.GetValueOrDefault(l.InvoiceLineId, 0m)
                                    : 0m;
                                var remCredit = inv.DocumentType == InvoiceDocumentTypes.Invoice
                                    ? Math.Max(0m, decimal.Round(l.BilledBaseQuantity - credited, 6, MidpointRounding.AwayFromZero))
                                    : 0m;
                                return new CustomerInvoiceLineDto(
                                    l.InvoiceLineId,
                                    l.LineNumber,
                                    l.ShipmentId,
                                    l.ShipmentNumberSnapshot,
                                    l.ShipmentLineId,
                                    l.SoLineId,
                                    l.LineageLineKey,
                                    l.ItemId,
                                    l.ItemCodeSnapshot,
                                    l.ItemNameSnapshot,
                                    l.BaseUomCodeSnapshot,
                                    l.BilledBaseQuantity,
                                    credited,
                                    remCredit,
                                    l.UnitPrice,
                                    l.LineSubtotalAmount,
                                    l.TaxCodeSnapshot,
                                    l.TaxRatePercent,
                                    l.LineTaxAmount,
                                    l.LineTotalAmount,
                                    l.OriginalInvoiceLineId);
                            }).ToList());
                    }).ToList();

                    var receivables = await db.CustomerReceivables.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId)
                        .OrderByDescending(r => r.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    var receivableDtos = receivables.Select(r => new CustomerReceivableDto(
                        r.ReceivableId,
                        r.ReceivableNumber,
                        r.InvoiceId,
                        r.InvoiceNumberSnapshot,
                        r.CustomerPartnerId,
                        r.CustomerCodeSnapshot,
                        r.CustomerNameSnapshot,
                        r.InvoiceDate,
                        r.DueDate,
                        r.CurrencyCode,
                        r.OriginalAmount,
                        r.CreditedAmount,
                        r.ReceivedAmount,
                        r.OpenAmount,
                        r.Status,
                        r.GlJournalId,
                        r.GlJournalNumber,
                        r.CreatedAtUtc)).ToList();

                    var unbilledQty = decimal.Round(billableDtos.Sum(x => x.RemainingBillableBaseQuantity), 6, MidpointRounding.AwayFromZero);
                    var openDrafts = invoices.Count(i => i.Status is CustomerInvoiceStatuses.Draft or CustomerInvoiceStatuses.Submitted);
                    var postedRev = decimal.Round(
                        invoices.Where(i => i.Status == CustomerInvoiceStatuses.Posted)
                            .Sum(i => i.DocumentType == InvoiceDocumentTypes.CreditNote ? -i.SubtotalAmount : i.SubtotalAmount),
                        2,
                        MidpointRounding.AwayFromZero);
                    var openAr = decimal.Round(receivables.Sum(r => r.OpenAmount), 2, MidpointRounding.AwayFromZero);

                    return BillingOutcome<BillingOverviewResponse>.Ok(new BillingOverviewResponse(
                        canDraft,
                        canPost,
                        new BillingSummaryStatsDto(unbilledQty, openDrafts, postedRev, openAr),
                        taxCodeDtos,
                        billableDtos,
                        invoiceDtos,
                        receivableDtos));
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            var outcome = scoped.Value!;
            return outcome.Succeeded
                ? Results.Ok(outcome.Value)
                : ProblemResults.Create(httpContext, outcome.StatusCode, outcome.ErrorCode!, outcome.Title!, outcome.Detail!);
        })
        .WithName("GetBillingOverview")
        .Produces<BillingOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/invoices", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] CreateCustomerInvoiceRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            BillingOutcome<(CustomerInvoice Invoice, IReadOnlyList<CustomerInvoiceLine> Lines)>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: null,
                    async (scope, innerCt) =>
                    {
                        var canDraft = scope.EffectivePermissions.Any(p => DraftPermissions.Contains(p, StringComparer.Ordinal));
                        if (!canDraft)
                        {
                            capturedFailure = BillingOutcome<(CustomerInvoice, IReadOnlyList<CustomerInvoiceLine>)>.Fail(
                                403, "billing_draft_denied", "Invoice draft denied", "Requires sales.manage or finance.journal.post permission.");
                            throw new BillingAbortException();
                        }

                        var cmd = new CreateCustomerInvoiceCommand(
                            tenantId,
                            entityId,
                            request.SalesOrderId,
                            request.TaxCodeId,
                            request.InvoiceDate,
                            request.DueDate,
                            request.TaxEvidenceReference,
                            request.IdempotencyKey,
                            request.Notes,
                            request.Lines.Select(l => new BillingLineInput(l.ShipmentLineId, l.BilledBaseQuantity)).ToList(),
                            actorUserId,
                            DateTimeOffset.UtcNow);

                        var res = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(db, cmd, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new BillingAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                var (inv, lines) = scoped.Value!.Value;
                var dto = MapInvoiceDto(inv, lines);
                return scoped.Value!.StatusCode == 201
                    ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/billing/invoices/{inv.InvoiceId}", dto)
                    : Results.Ok(dto);
            }
            catch (BillingAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("CreateCustomerInvoiceDraft")
        .Produces<CustomerInvoiceDto>(StatusCodes.Status201Created)
        .Produces<CustomerInvoiceDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/invoices/{invoiceId:guid}/submit", async (
            Guid tenantId,
            Guid entityId,
            Guid invoiceId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            BillingOutcome<CustomerInvoice>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: null,
                    async (scope, innerCt) =>
                    {
                        var canDraft = scope.EffectivePermissions.Any(p => DraftPermissions.Contains(p, StringComparer.Ordinal));
                        if (!canDraft)
                        {
                            capturedFailure = BillingOutcome<CustomerInvoice>.Fail(403, "billing_submit_denied", "Submit denied", "Requires sales.manage or finance.journal.post permission.");
                            throw new BillingAbortException();
                        }

                        var res = await BillingAndReceivablesCoordinator.SubmitInvoiceAsync(db, tenantId, entityId, invoiceId, actorUserId, DateTimeOffset.UtcNow, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new BillingAbortException();
                        }

                        return res.Value!;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                return Results.Ok(new { scoped.Value!.InvoiceId, scoped.Value.InvoiceNumber, scoped.Value.Status });
            }
            catch (BillingAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("SubmitCustomerInvoice")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/invoices/{invoiceId:guid}/cancel", async (
            Guid tenantId,
            Guid entityId,
            Guid invoiceId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            BillingOutcome<CustomerInvoice>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: null,
                    async (scope, innerCt) =>
                    {
                        var canDraft = scope.EffectivePermissions.Any(p => DraftPermissions.Contains(p, StringComparer.Ordinal));
                        if (!canDraft)
                        {
                            capturedFailure = BillingOutcome<CustomerInvoice>.Fail(403, "billing_cancel_denied", "Cancel denied", "Requires sales.manage or finance.journal.post permission.");
                            throw new BillingAbortException();
                        }

                        var res = await BillingAndReceivablesCoordinator.CancelInvoiceAsync(db, tenantId, entityId, invoiceId, actorUserId, DateTimeOffset.UtcNow, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new BillingAbortException();
                        }

                        return res.Value!;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                return Results.Ok(new { scoped.Value!.InvoiceId, scoped.Value.InvoiceNumber, scoped.Value.Status });
            }
            catch (BillingAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("CancelCustomerInvoice")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/invoices/{invoiceId:guid}/post", async (
            Guid tenantId,
            Guid entityId,
            Guid invoiceId,
            [FromBody] PostCustomerInvoiceRequest? request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            ITenantOutboxSignal outboxSignal,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            BillingOutcome<InvoicePostingResult>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await BillingAndReceivablesCoordinator.PostInvoiceAsync(
                            db,
                            new PostCustomerInvoiceCommand(
                                tenantId,
                                entityId,
                                invoiceId,
                                request?.TaxEvidenceReference,
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new BillingAbortException();
                        }

                        return res.Value!;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                outboxSignal.NotifyTenant(tenantId);
                return Results.Ok(MapInvoiceDto(scoped.Value!.Invoice, scoped.Value.Lines));
            }
            catch (BillingAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("PostCustomerInvoice")
        .Produces<CustomerInvoiceDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/credit-notes", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] CreateCreditNoteRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            ITenantOutboxSignal outboxSignal,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            BillingOutcome<InvoicePostingResult>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await BillingAndReceivablesCoordinator.CreateAndPostCreditNoteAsync(
                            db,
                            new CreateAndPostCreditNoteCommand(
                                tenantId,
                                entityId,
                                request.OriginalInvoiceId,
                                request.CreditDate,
                                request.TaxEvidenceReference,
                                request.IdempotencyKey,
                                request.Notes,
                                request.Lines.Select(l => new CreditNoteLineInput(l.OriginalInvoiceLineId, l.CreditedBaseQuantity)).ToList(),
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new BillingAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                outboxSignal.NotifyTenant(tenantId);
                var dto = MapInvoiceDto(scoped.Value!.Value!.Invoice, scoped.Value.Value.Lines);
                return scoped.Value.StatusCode == 201
                    ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/billing/invoices/{dto.InvoiceId}", dto)
                    : Results.Ok(dto);
            }
            catch (BillingAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("CreateAndPostCreditNote")
        .Produces<CustomerInvoiceDto>(StatusCodes.Status201Created)
        .Produces<CustomerInvoiceDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static CustomerInvoiceDto MapInvoiceDto(CustomerInvoice inv, IReadOnlyList<CustomerInvoiceLine> lines) =>
        new(
            inv.InvoiceId,
            inv.InvoiceNumber,
            inv.DocumentType,
            inv.OriginalInvoiceId,
            inv.OriginalInvoiceNumberSnapshot,
            inv.SalesOrderId,
            inv.SalesOrderNumberSnapshot,
            inv.CustomerPartnerId,
            inv.CustomerCodeSnapshot,
            inv.CustomerNameSnapshot,
            inv.CustomerNpwpSnapshot,
            inv.InvoiceDate,
            inv.DueDate,
            inv.CurrencyCode,
            inv.Status,
            inv.SubtotalAmount,
            inv.TaxAmount,
            inv.GrandTotalAmount,
            inv.TaxCodeId,
            inv.TaxCodeSnapshot,
            inv.TaxTreatmentClassSnapshot,
            inv.TaxRatePercentSnapshot,
            inv.RequiresTaxEvidenceSnapshot,
            inv.TaxEvidenceReference,
            inv.TaxDeterminationSummary,
            inv.GlJournalId,
            inv.GlJournalNumber,
            inv.ArReceivableId,
            inv.Notes,
            inv.CreatedAtUtc,
            inv.SubmittedAtUtc,
            inv.PostedAtUtc,
            lines.Select(l => new CustomerInvoiceLineDto(
                l.InvoiceLineId,
                l.LineNumber,
                l.ShipmentId,
                l.ShipmentNumberSnapshot,
                l.ShipmentLineId,
                l.SoLineId,
                l.LineageLineKey,
                l.ItemId,
                l.ItemCodeSnapshot,
                l.ItemNameSnapshot,
                l.BaseUomCodeSnapshot,
                l.BilledBaseQuantity,
                0m,
                inv.DocumentType == InvoiceDocumentTypes.Invoice ? l.BilledBaseQuantity : 0m,
                l.UnitPrice,
                l.LineSubtotalAmount,
                l.TaxCodeSnapshot,
                l.TaxRatePercent,
                l.LineTaxAmount,
                l.LineTotalAmount,
                l.OriginalInvoiceLineId)).ToList());

    private sealed class BillingAbortException : Exception { }
}
