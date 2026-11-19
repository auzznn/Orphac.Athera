using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Receivables;

namespace Orphac.Athera.Api.Endpoints;

public static class ReceivablesEndpoints
{
    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.FinanceRead,
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.SalesRead,
        PermissionCodes.SalesManage,
        PermissionCodes.SalesApprove
    ];

    public static void MapReceivablesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/receivables")
            .RequireAuthorization();

        group.MapGet("/overview", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] DateOnly? asOfDate,
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
                    if (!scope.EffectivePermissions.Any(p => ReadPermissions.Contains(p, StringComparer.Ordinal)))
                    {
                        return ReceivablesOutcome<ReceivablesOverviewResponse>.Fail(
                            403, "receivables_read_denied", "Receivables access denied",
                            "You need finance or sales read permissions on this legal entity.");
                    }

                    var canManage = scope.EffectivePermissions.Contains(PermissionCodes.FinanceJournalPost, StringComparer.Ordinal);
                    var effectiveAsOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

                    var bankAccounts = await db.BankAccounts.AsNoTracking()
                        .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && b.Status == MasterStatuses.Active)
                        .OrderBy(b => b.AccountCode)
                        .ToListAsync(innerCt);
                    var bankCodeById = bankAccounts.ToDictionary(b => b.BankAccountId, b => b.AccountCode);

                    var inboundLines = await db.BankStatementLines.AsNoTracking()
                        .Where(l => l.TenantId == tenantId
                            && l.LegalEntityId == entityId
                            && l.Direction == BankStatementLineDirections.Credit)
                        .OrderByDescending(l => l.ValueDate)
                        .ThenByDescending(l => l.LineNumber)
                        .Take(100)
                        .ToListAsync(innerCt);

                    var customers = await db.Partners.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.IsCustomer && p.Status == MasterStatuses.Active)
                        .OrderBy(p => p.PartnerCode)
                        .Select(p => new ReceivablesCustomerOptionDto(p.PartnerId, p.PartnerCode, p.LegalName))
                        .ToListAsync(innerCt);

                    var receivables = await db.CustomerReceivables.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId)
                        .OrderBy(r => r.DueDate)
                        .ThenBy(r => r.ReceivableNumber)
                        .ToListAsync(innerCt);

                    var receipts = await db.CustomerReceipts.AsNoTracking()
                        .Where(r => r.TenantId == tenantId && r.LegalEntityId == entityId)
                        .OrderByDescending(r => r.CreatedAtUtc)
                        .ToListAsync(innerCt);

                    var receiptIds = receipts.Select(r => r.ReceiptId).ToList();
                    var allocations = receiptIds.Count == 0
                        ? []
                        : await db.ReceiptAllocations.AsNoTracking()
                            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && receiptIds.Contains(a.ReceiptId))
                            .OrderBy(a => a.AllocatedAtUtc)
                            .ToListAsync(innerCt);
                    var allocsByReceiptId = allocations.GroupBy(a => a.ReceiptId).ToDictionary(g => g.Key, g => g.ToList());

                    // Compute GL 1200 AR Control net debit balance
                    var arMapping = await db.AccountMappings.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.PurposeCode == GlMappingPurposes.ArTradeControl, innerCt);

                    decimal glArBalance = 0m;
                    if (arMapping is not null)
                    {
                        var glTotals = await (
                            from jl in db.JournalLines.AsNoTracking()
                            join je in db.JournalEntries.AsNoTracking()
                                on new { jl.TenantId, jl.LegalEntityId, jl.JournalId } equals new { je.TenantId, je.LegalEntityId, je.JournalId }
                            where jl.TenantId == tenantId
                                && jl.LegalEntityId == entityId
                                && jl.AccountId == arMapping.AccountId
                            select new { jl.DebitAmount, jl.CreditAmount })
                            .ToListAsync(innerCt);

                        glArBalance = decimal.Round(glTotals.Sum(x => x.DebitAmount - x.CreditAmount), 2, MidpointRounding.AwayFromZero);
                    }

                    // Build AR Aging summary per customer
                    var unappliedByCustomer = receipts
                        .Where(r => r.Status != CustomerReceiptStatuses.Reversed && r.UnappliedAmount > 0m)
                        .GroupBy(r => r.CustomerPartnerId)
                        .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.UnappliedAmount), 2, MidpointRounding.AwayFromZero));

                    var customerLookup = receivables
                        .GroupBy(r => r.CustomerPartnerId)
                        .ToDictionary(g => g.Key, g => (g.First().CustomerCodeSnapshot, g.First().CustomerNameSnapshot));
                    foreach (var rct in receipts.Where(r => r.Status != CustomerReceiptStatuses.Reversed && r.UnappliedAmount > 0m))
                    {
                        customerLookup.TryAdd(rct.CustomerPartnerId, (rct.CustomerCodeSnapshot, rct.CustomerNameSnapshot));
                    }

                    var agingRows = new List<ArAgingCustomerRowDto>();
                    decimal curTot = 0m, d130Tot = 0m, d3160Tot = 0m, d6190Tot = 0m, o90Tot = 0m, openTot = 0m, unapTot = 0m;

                    foreach (var (custId, (custCode, custName)) in customerLookup.OrderBy(kv => kv.Value.Item1, StringComparer.Ordinal))
                    {
                        decimal c0 = 0m, c1 = 0m, c2 = 0m, c3 = 0m, c4 = 0m;
                        foreach (var rec in receivables.Where(r => r.CustomerPartnerId == custId && r.OpenAmount > 0m))
                        {
                            var (bucket, _) = ArAgingBuckets.Classify(rec.DueDate, effectiveAsOf);
                            switch (bucket)
                            {
                                case ArAgingBuckets.Current: c0 += rec.OpenAmount; break;
                                case ArAgingBuckets.Days1To30: c1 += rec.OpenAmount; break;
                                case ArAgingBuckets.Days31To60: c2 += rec.OpenAmount; break;
                                case ArAgingBuckets.Days61To90: c3 += rec.OpenAmount; break;
                                default: c4 += rec.OpenAmount; break;
                            }
                        }

                        var custOpen = decimal.Round(c0 + c1 + c2 + c3 + c4, 2, MidpointRounding.AwayFromZero);
                        var custUnap = unappliedByCustomer.GetValueOrDefault(custId, 0m);
                        if (custOpen == 0m && custUnap == 0m) continue;

                        var custNet = decimal.Round(custOpen - custUnap, 2, MidpointRounding.AwayFromZero);
                        agingRows.Add(new ArAgingCustomerRowDto(custId, custCode, custName, c0, c1, c2, c3, c4, custOpen, custUnap, custNet));
                        curTot += c0;
                        d130Tot += c1;
                        d3160Tot += c2;
                        d6190Tot += c3;
                        o90Tot += c4;
                        openTot += custOpen;
                        unapTot += custUnap;
                    }

                    var netSubledger = decimal.Round(openTot - unapTot, 2, MidpointRounding.AwayFromZero);
                    var agingSummary = new ArAgingSummaryDto(
                        effectiveAsOf,
                        decimal.Round(curTot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(d130Tot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(d3160Tot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(d6190Tot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(o90Tot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(openTot, 2, MidpointRounding.AwayFromZero),
                        decimal.Round(unapTot, 2, MidpointRounding.AwayFromZero),
                        netSubledger,
                        glArBalance,
                        netSubledger == glArBalance,
                        agingRows);

                    return ReceivablesOutcome<ReceivablesOverviewResponse>.Ok(new ReceivablesOverviewResponse(
                        canManage,
                        agingSummary,
                        bankAccounts.Select(b => new ReceivablesBankAccountDto(b.BankAccountId, b.AccountCode, b.BankName, b.AccountNumber, b.CurrencyCode)).ToList(),
                        inboundLines.Select(l => new InboundBankCreditLineDto(
                            l.BankStatementLineId,
                            l.BankStatementId,
                            l.BankAccountId,
                            bankCodeById.GetValueOrDefault(l.BankAccountId, "BANK"),
                            l.ValueDate,
                            l.Amount,
                            l.MatchedAmount,
                            l.UnmatchedAmount,
                            l.BankReference,
                            l.CounterpartyName,
                            l.Description,
                            l.Status)).ToList(),
                        customers,
                        receivables.Where(r => r.OpenAmount > 0m).Select(r => new CustomerReceivableDto(
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
                            r.CreatedAtUtc)).ToList(),
                        receipts.Select(r => MapReceiptDto(r, allocsByReceiptId.GetValueOrDefault(r.ReceiptId, []))).ToList()));
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
        .WithName("GetReceivablesOverview")
        .Produces<ReceivablesOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/bank-statements/import", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] ImportInboundBankStatementRequest request,
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

            BankStatementImportOutcome? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await BankReconciliationCoordinator.ImportBankStatementAsync(
                            db,
                            new ImportBankStatementCommand(
                                tenantId,
                                entityId,
                                request.BankAccountId,
                                request.StatementReference,
                                request.StatementDate,
                                request.OpeningBalance,
                                request.ClosingBalance,
                                request.CsvContent,
                                null,
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded || res.Statement is null)
                        {
                            capturedFailure = res;
                            throw new ReceivablesAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                var imp = scoped.Value!;
                return Results.Created(
                    $"/api/workspaces/{tenantId}/entities/{entityId}/receivables/overview",
                    new ImportInboundBankStatementResponse(imp.Statement!.BankStatementId, imp.Statement.StatementNumber, imp.Lines.Count, 0));
            }
            catch (ReceivablesAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.FailureHttpStatus, capturedFailure.ErrorCode ?? "bank_import_failed", "Bank statement import rejected", capturedFailure.ErrorDetail ?? "Failed to import CSV statement.");
            }
        })
        .WithName("ImportInboundBankStatement")
        .Produces<ImportInboundBankStatementResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/receipts", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] ConfirmCustomerReceiptRequest request,
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

            ReceivablesOutcome<ReceiptWithAllocations>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await CustomerReceiptsCoordinator.ConfirmReceiptAsync(
                            db,
                            new ConfirmCustomerReceiptCommand(
                                tenantId,
                                entityId,
                                request.BankStatementLineId,
                                request.CustomerPartnerId,
                                request.ReceiptDate,
                                request.IdempotencyKey,
                                request.Notes,
                                request.InitialAllocations?.Select(a => new ReceiptAllocationInput(a.ReceivableId, a.AllocatedAmount)).ToList(),
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReceivablesAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                outboxSignal.NotifyTenant(tenantId);
                var dto = MapReceiptDto(scoped.Value!.Value!.Receipt, scoped.Value.Value.Allocations);
                return scoped.Value.StatusCode == 201
                    ? Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/receivables/receipts/{dto.ReceiptId}", dto)
                    : Results.Ok(dto);
            }
            catch (ReceivablesAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("ConfirmCustomerReceipt")
        .Produces<CustomerReceiptDto>(StatusCodes.Status201Created)
        .Produces<CustomerReceiptDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/receipts/{receiptId:guid}/allocate", async (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            [FromBody] AllocateCustomerReceiptRequest request,
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

            ReceivablesOutcome<ReceiptWithAllocations>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await CustomerReceiptsCoordinator.AllocateReceiptAsync(
                            db,
                            new AllocateCustomerReceiptCommand(
                                tenantId,
                                entityId,
                                receiptId,
                                request.Allocations.Select(a => new ReceiptAllocationInput(a.ReceivableId, a.AllocatedAmount)).ToList(),
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReceivablesAbortException();
                        }

                        return res.Value!;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                return Results.Ok(MapReceiptDto(scoped.Value!.Receipt, scoped.Value.Allocations));
            }
            catch (ReceivablesAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("AllocateCustomerReceipt")
        .Produces<CustomerReceiptDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/receipts/{receiptId:guid}/reverse", async (
            Guid tenantId,
            Guid entityId,
            Guid receiptId,
            [FromBody] ReverseCustomerReceiptRequest request,
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

            ReceivablesOutcome<ReceiptWithAllocations>? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceJournalPost,
                    async (_, innerCt) =>
                    {
                        var res = await CustomerReceiptsCoordinator.ReverseReceiptAsync(
                            db,
                            new ReverseCustomerReceiptCommand(
                                tenantId,
                                entityId,
                                receiptId,
                                request.ReversalDate,
                                request.Reason,
                                actorUserId,
                                DateTimeOffset.UtcNow),
                            innerCt);

                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReceivablesAbortException();
                        }

                        return res.Value!;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                outboxSignal.NotifyTenant(tenantId);
                return Results.Ok(MapReceiptDto(scoped.Value!.Receipt, scoped.Value.Allocations));
            }
            catch (ReceivablesAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("ReverseCustomerReceipt")
        .Produces<CustomerReceiptDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static CustomerReceiptDto MapReceiptDto(CustomerReceipt r, IReadOnlyList<ReceiptAllocation> allocs) =>
        new(
            r.ReceiptId,
            r.ReceiptNumber,
            r.BankAccountId,
            r.BankStatementLineId,
            r.CustomerPartnerId,
            r.CustomerCodeSnapshot,
            r.CustomerNameSnapshot,
            r.ReceiptDate,
            r.BankReferenceSnapshot,
            r.CurrencyCode,
            r.ReceiptAmount,
            r.AllocatedAmount,
            r.UnappliedAmount,
            r.Status,
            r.GlJournalId,
            r.GlJournalNumber,
            r.ReversalGlJournalNumber,
            r.ReversalReason,
            r.Notes,
            r.CreatedAtUtc,
            r.ReversedAtUtc,
            allocs.Select(a => new ReceiptAllocationDto(
                a.AllocationId,
                a.ReceiptId,
                a.ReceivableId,
                a.InvoiceId,
                a.InvoiceNumberSnapshot,
                a.AllocatedAmount,
                a.Status,
                a.AllocatedAtUtc,
                a.ReversedAtUtc)).ToList());

    private sealed class ReceivablesAbortException : Exception { }
}
