using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

public static class ReportsEndpoints
{
    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.FinanceRead,
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.FinanceConfigManage
    ];

    private static readonly string[] ClosePermissions =
    [
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.FinanceConfigManage
    ];

    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/reports")
            .RequireAuthorization();

        group.MapGet("/overview", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] Guid? periodId,
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
                        return (Allowed: false, Payload: (AccountingReportsOverviewResponse?)null);
                    }

                    var canClose = scope.EffectivePermissions.Any(p => ClosePermissions.Contains(p, StringComparer.Ordinal));
                    var canLock = scope.EffectivePermissions.Contains(PermissionCodes.FinanceConfigManage, StringComparer.Ordinal);
                    var effectiveAsOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

                    var snap = await AccountingReportsService.BuildSnapshotAsync(db, tenantId, entityId, periodId, effectiveAsOf, innerCt);
                    var dto = new AccountingReportsOverviewResponse(
                        canClose,
                        canLock,
                        new TrialBalanceReportDto(
                            snap.TrialBalance.PeriodId,
                            snap.TrialBalance.PeriodCode,
                            snap.TrialBalance.StartDate,
                            snap.TrialBalance.EndDate,
                            snap.TrialBalance.TotalOpeningDebit,
                            snap.TrialBalance.TotalOpeningCredit,
                            snap.TrialBalance.TotalPeriodDebit,
                            snap.TrialBalance.TotalPeriodCredit,
                            snap.TrialBalance.TotalEndingDebit,
                            snap.TrialBalance.TotalEndingCredit,
                            snap.TrialBalance.IsBalanced,
                            snap.TrialBalance.Rows.Select(r => new TrialBalanceRowDto(
                                r.AccountId, r.AccountCode, r.AccountName, r.AccountType, r.NormalBalance,
                                r.OpeningDebit, r.OpeningCredit, r.PeriodDebit, r.PeriodCredit, r.EndingDebit, r.EndingCredit)).ToList()),
                        new ProfitAndLossReportDto(
                            snap.ProfitAndLoss.TotalRevenue,
                            snap.ProfitAndLoss.TotalExpense,
                            snap.ProfitAndLoss.NetIncome,
                            snap.ProfitAndLoss.RevenueLines.Select(l => new StatementAccountLineDto(l.AccountId, l.AccountCode, l.AccountName, l.AccountType, l.Amount)).ToList(),
                            snap.ProfitAndLoss.ExpenseLines.Select(l => new StatementAccountLineDto(l.AccountId, l.AccountCode, l.AccountName, l.AccountType, l.Amount)).ToList()),
                        new BalanceSheetReportDto(
                            snap.BalanceSheet.TotalAssets,
                            snap.BalanceSheet.TotalLiabilities,
                            snap.BalanceSheet.TotalEquity,
                            snap.BalanceSheet.CurrentEarnings,
                            snap.BalanceSheet.TotalLiabilitiesAndEquity,
                            snap.BalanceSheet.IsBalanced,
                            snap.BalanceSheet.AssetLines.Select(l => new StatementAccountLineDto(l.AccountId, l.AccountCode, l.AccountName, l.AccountType, l.Amount)).ToList(),
                            snap.BalanceSheet.LiabilityLines.Select(l => new StatementAccountLineDto(l.AccountId, l.AccountCode, l.AccountName, l.AccountType, l.Amount)).ToList(),
                            snap.BalanceSheet.EquityLines.Select(l => new StatementAccountLineDto(l.AccountId, l.AccountCode, l.AccountName, l.AccountType, l.Amount)).ToList()),
                        snap.Reconciliations.Select(r => new ControlReconciliationRowDto(
                            r.PurposeCode, r.ControlLabel, r.AccountCode, r.AccountName, r.AccountId,
                            r.GlBalance, r.SubledgerBalance, r.VarianceAmount, r.Status, r.OpenItemCount, r.Explanation)).ToList(),
                        new AgingBucketTotalsDto(
                            snap.ApAging.CurrentAmount, snap.ApAging.Days1To30Amount, snap.ApAging.Days31To60Amount,
                            snap.ApAging.Days61To90Amount, snap.ApAging.Over90Amount, snap.ApAging.TotalOpenAmount, snap.ApAging.OpenItemCount),
                        new AgingBucketTotalsDto(
                            snap.ArAging.CurrentAmount, snap.ArAging.Days1To30Amount, snap.ArAging.Days31To60Amount,
                            snap.ArAging.Days61To90Amount, snap.ArAging.Over90Amount, snap.ArAging.TotalOpenAmount, snap.ArAging.OpenItemCount),
                        snap.Periods.Select(p => new ReportPeriodDto(p.PeriodId, p.PeriodCode, p.StartDate, p.EndDate, p.Status, p.CloseNote, p.ClosedAtUtc)).ToList(),
                        snap.CloseReviews.Select(r => new PeriodCloseReviewDto(
                            r.CloseReviewId, r.PeriodId, r.PeriodCodeSnapshot, r.ActionType,
                            r.TrialBalanceDebits, r.TrialBalanceCredits, r.IsTrialBalanceBalanced,
                            r.ReconciliationVarianceCount, r.TotalVarianceAmount, r.UnbilledGrniAmount, r.Reason, r.CreatedAtUtc)).ToList());

                    return (Allowed: true, Payload: (AccountingReportsOverviewResponse?)dto);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            return scoped.Value.Allowed
                ? Results.Ok(scoped.Value.Payload)
                : ProblemResults.Create(httpContext, 403, "reports_read_denied", "Reports access denied", "Requires finance.read permission on this legal entity.");
        })
        .WithName("GetAccountingReportsOverview")
        .Produces<AccountingReportsOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/accounts/{accountId:guid}/ledger", async (
            Guid tenantId,
            Guid entityId,
            Guid accountId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
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
                        return (Status: 403, Payload: (AccountLedgerDrilldownResponse?)null);
                    }

                    var acct = await db.GlAccounts.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == accountId, innerCt);
                    if (acct is null) return (Status: 404, Payload: null);

                    var entries = await AccountingReportsService.GetAccountLedgerAsync(db, tenantId, entityId, accountId, fromDate, toDate, innerCt);
                    var dto = new AccountLedgerDrilldownResponse(
                        acct.AccountId,
                        acct.AccountCode,
                        acct.Name,
                        acct.NormalBalance,
                        entries.Select(e => new AccountLedgerEntryDto(
                            e.JournalId, e.JournalNumber, e.PostingDate, e.EntryKind,
                            e.SourceModule, e.SourceDocumentType, e.SourceDocumentId, e.Description,
                            e.DebitAmount, e.CreditAmount, e.RunningBalance)).ToList());
                    return (Status: 200, Payload: dto);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            return scoped.Value.Status switch
            {
                200 => Results.Ok(scoped.Value.Payload),
                404 => ProblemResults.Create(httpContext, 404, "account_not_found", "Account not found", "GL account not found in this entity."),
                _ => ProblemResults.Create(httpContext, 403, "reports_read_denied", "Reports access denied", "Requires finance.read permission.")
            };
        })
        .WithName("GetAccountLedgerDrilldown")
        .Produces<AccountLedgerDrilldownResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/trial-balance/export.csv", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] Guid? periodId,
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
                        return (Allowed: false, PeriodCode: "", Csv: "");
                    }

                    var asOf = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                    var snap = await AccountingReportsService.BuildSnapshotAsync(db, tenantId, entityId, periodId, asOf, innerCt);
                    return (Allowed: true, PeriodCode: snap.TrialBalance.PeriodCode, Csv: AccountingReportsService.BuildTrialBalanceCsv(snap.TrialBalance));
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "reports_read_denied", "Reports access denied", "Requires finance.read permission.");
            }

            return Results.File(
                Encoding.UTF8.GetBytes(scoped.Value.Csv),
                "text/csv; charset=utf-8",
                $"trial-balance-{scoped.Value.PeriodCode}.csv");
        })
        .WithName("ExportTrialBalanceCsv")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/periods/{periodId:guid}/close", async (
            Guid tenantId,
            Guid entityId,
            Guid periodId,
            [FromBody] ClosePeriodRequest? request,
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

            PeriodCloseOutcome? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: null,
                    async (scope, innerCt) =>
                    {
                        if (!scope.EffectivePermissions.Any(p => ClosePermissions.Contains(p, StringComparer.Ordinal)))
                        {
                            capturedFailure = PeriodCloseOutcome.Fail(403, "period_close_denied", "Period close denied", "Requires finance.journal.post or finance.config.manage.");
                            throw new ReportsAbortException();
                        }

                        var res = await AccountingReportsService.ClosePeriodAsync(
                            db, tenantId, entityId, periodId, request?.Note, request?.BlockOnVariance ?? true, actorUserId, DateTimeOffset.UtcNow, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReportsAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                outboxSignal.NotifyTenant(tenantId);
                var r = scoped.Value!.Review!;
                return Results.Ok(new PeriodCloseReviewDto(
                    r.CloseReviewId, r.PeriodId, r.PeriodCodeSnapshot, r.ActionType,
                    r.TrialBalanceDebits, r.TrialBalanceCredits, r.IsTrialBalanceBalanced,
                    r.ReconciliationVarianceCount, r.TotalVarianceAmount, r.UnbilledGrniAmount, r.Reason, r.CreatedAtUtc));
            }
            catch (ReportsAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("CloseReportPeriod")
        .Produces<PeriodCloseReviewDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/periods/{periodId:guid}/lock", async (
            Guid tenantId,
            Guid entityId,
            Guid periodId,
            [FromBody] LockPeriodRequest? request,
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

            PeriodCloseOutcome? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceConfigManage,
                    async (_, innerCt) =>
                    {
                        var res = await AccountingReportsService.LockPeriodPermanentlyAsync(
                            db, tenantId, entityId, periodId, request?.Note, actorUserId, DateTimeOffset.UtcNow, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReportsAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                var r = scoped.Value!.Review!;
                return Results.Ok(new PeriodCloseReviewDto(
                    r.CloseReviewId, r.PeriodId, r.PeriodCodeSnapshot, r.ActionType,
                    r.TrialBalanceDebits, r.TrialBalanceCredits, r.IsTrialBalanceBalanced,
                    r.ReconciliationVarianceCount, r.TotalVarianceAmount, r.UnbilledGrniAmount, r.Reason, r.CreatedAtUtc));
            }
            catch (ReportsAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("LockReportPeriod")
        .Produces<PeriodCloseReviewDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/periods/{periodId:guid}/reopen", async (
            Guid tenantId,
            Guid entityId,
            Guid periodId,
            [FromBody] ReopenPeriodRequest request,
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

            PeriodCloseOutcome? capturedFailure = null;
            try
            {
                var scoped = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    requiredPermission: PermissionCodes.FinanceConfigManage,
                    async (_, innerCt) =>
                    {
                        var res = await AccountingReportsService.ReopenPeriodAsync(
                            db, tenantId, entityId, periodId, request.Reason, actorUserId, DateTimeOffset.UtcNow, innerCt);
                        if (!res.Succeeded)
                        {
                            capturedFailure = res;
                            throw new ReportsAbortException();
                        }

                        return res;
                    },
                    ct);

                if (!scoped.IsAllowed)
                {
                    return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
                }

                var r = scoped.Value!.Review!;
                return Results.Ok(new PeriodCloseReviewDto(
                    r.CloseReviewId, r.PeriodId, r.PeriodCodeSnapshot, r.ActionType,
                    r.TrialBalanceDebits, r.TrialBalanceCredits, r.IsTrialBalanceBalanced,
                    r.ReconciliationVarianceCount, r.TotalVarianceAmount, r.UnbilledGrniAmount, r.Reason, r.CreatedAtUtc));
            }
            catch (ReportsAbortException) when (capturedFailure is not null)
            {
                return ProblemResults.Create(httpContext, capturedFailure.StatusCode, capturedFailure.ErrorCode!, capturedFailure.Title!, capturedFailure.Detail!);
            }
        })
        .WithName("ReopenReportPeriod")
        .Produces<PeriodCloseReviewDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private sealed class ReportsAbortException : Exception { }
}
