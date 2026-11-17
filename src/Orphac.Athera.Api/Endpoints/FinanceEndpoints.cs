using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.Api.Endpoints;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        MapAccountRoutes(app);
        MapPeriodRoutes(app);
        MapAccountMappingRoutes(app);
        MapTaxRoutes(app);
        MapJournalRoutes(app);
        return app;
    }

    private static void MapAccountRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/accounts", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? type,
            [FromQuery] string? status,
            [FromQuery] bool? postableOnly,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var query = db.GlAccounts
                        .AsNoTracking()
                        .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId);

                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        var normType = type.Trim().ToUpperInvariant();
                        query = query.Where(a => a.AccountType == normType);
                    }

                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        var normStatus = status.Trim().ToUpperInvariant();
                        query = query.Where(a => a.Status == normStatus);
                    }

                    if (postableOnly == true)
                    {
                        query = query.Where(a => a.AllowPosting);
                    }

                    var rows = await query
                        .OrderBy(a => a.AccountCode)
                        .ToListAsync(innerCt);

                    return Results.Ok(new GlAccountListResponse(rows.Select(ToAccountDto).ToList()));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListGlAccounts")
        .Produces<GlAccountListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/accounts", async (
            Guid tenantId,
            Guid entityId,
            CreateGlAccountRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceConfigManage],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        if (request.ParentAccountId is { } parentId)
                        {
                            var parentExists = await db.GlAccounts
                                .AsNoTracking()
                                .AnyAsync(
                                    a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == parentId,
                                    innerCt);
                            if (!parentExists)
                            {
                                return EndpointSupport.BadRequest(httpContext, "parent_account_not_found", "Parent GL account was not found in this legal entity.");
                            }
                        }

                        GlAccount account;
                        try
                        {
                            account = GlAccount.Create(
                                Guid.NewGuid(),
                                tenantId,
                                entityId,
                                request.AccountCode,
                                request.Name,
                                request.AccountType,
                                request.AccountCategory,
                                request.ParentAccountId,
                                request.AllowPosting,
                                request.IsControlAccount,
                                scope.EvaluatedAtUtc);
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                        {
                            return MapDomainError(httpContext, ex, "invalid_gl_account");
                        }

                        db.GlAccounts.Add(account);
                        EndpointSupport.RecordAuditAndOutbox(
                            db,
                            tenantId,
                            entityId,
                            actorUserId,
                            "GL_ACCOUNT_CREATED",
                            "GL_ACCOUNT",
                            account.AccountId.ToString("D"),
                            $"Created GL account {account.AccountCode} ({account.Name})",
                            "finance.account.created.v1",
                            $"gl:account:create:{account.AccountId:D}",
                            new { account.AccountId, account.AccountCode, account.AccountType, account.AccountCategory, account.AllowPosting },
                            scope.EvaluatedAtUtc);

                        await db.SaveChangesAsync(innerCt);
                        return Results.Created(
                            $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/finance/accounts/{account.AccountId:D}",
                            ToAccountDto(account));
                    },
                    ct);
            }
            catch (Exception ex)
            {
                var mapped = EndpointSupport.MapPersistenceConflict(
                    httpContext,
                    ex,
                    "gl_account_code_conflict",
                    "A GL account with this code already exists in this legal entity.");
                if (mapped is not null)
                {
                    return mapped;
                }

                throw;
            }
        })
        .RequireAuthorization()
        .WithName("CreateGlAccount")
        .Produces<GlAccountDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/accounts/{accountId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid accountId,
            UpdateGlAccountRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceConfigManage],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        var account = await db.GlAccounts
                            .SingleOrDefaultAsync(
                                a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == accountId,
                                innerCt);
                        if (account is null)
                        {
                            return EndpointSupport.NotFound(httpContext, "gl_account_not_found", "GL account not found in this legal entity.");
                        }

                        if (account.Version != request.ExpectedVersion)
                        {
                            return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "The GL account was modified concurrently; reload and retry.");
                        }

                        if (request.ParentAccountId is { } parentId)
                        {
                            var parentExists = await db.GlAccounts
                                .AsNoTracking()
                                .AnyAsync(
                                    a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == parentId,
                                    innerCt);
                            if (!parentExists)
                            {
                                return EndpointSupport.BadRequest(httpContext, "parent_account_not_found", "Parent GL account was not found in this legal entity.");
                            }
                        }

                        try
                        {
                            account.UpdateDetails(
                                request.Name,
                                request.AccountCategory,
                                request.ParentAccountId,
                                request.AllowPosting,
                                request.IsControlAccount,
                                scope.EvaluatedAtUtc);

                            if (!string.IsNullOrWhiteSpace(request.Status))
                            {
                                var targetStatus = request.Status.Trim().ToUpperInvariant();
                                if (targetStatus == MasterStatuses.Inactive)
                                {
                                    account.Deactivate(scope.EvaluatedAtUtc);
                                }
                                else if (targetStatus == MasterStatuses.Active)
                                {
                                    account.Activate(scope.EvaluatedAtUtc);
                                }
                                else
                                {
                                    return EndpointSupport.BadRequest(httpContext, "invalid_gl_account", $"Unsupported status '{request.Status}'.");
                                }
                            }
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                        {
                            return MapDomainError(httpContext, ex, "invalid_gl_account");
                        }

                        EndpointSupport.RecordAuditAndOutbox(
                            db,
                            tenantId,
                            entityId,
                            actorUserId,
                            "GL_ACCOUNT_UPDATED",
                            "GL_ACCOUNT",
                            account.AccountId.ToString("D"),
                            $"Updated GL account {account.AccountCode} ({account.Status})",
                            "finance.account.updated.v1",
                            $"gl:account:update:{account.AccountId:D}:{account.Version}",
                            new { account.AccountId, account.AccountCode, account.Status, account.AllowPosting, account.Version },
                            scope.EvaluatedAtUtc);

                        await db.SaveChangesAsync(innerCt);
                        return Results.Ok(ToAccountDto(account));
                    },
                    ct);
            }
            catch (Exception ex)
            {
                var mapped = EndpointSupport.MapPersistenceConflict(
                    httpContext,
                    ex,
                    "gl_account_code_conflict",
                    "Concurrent GL account modification detected.");
                if (mapped is not null)
                {
                    return mapped;
                }

                throw;
            }
        })
        .RequireAuthorization()
        .WithName("UpdateGlAccount")
        .Produces<GlAccountDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static void MapPeriodRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/periods", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var periods = await db.FinancialPeriods
                        .AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
                        .OrderByDescending(p => p.FiscalYear)
                        .ThenByDescending(p => p.PeriodNumber)
                        .ToListAsync(innerCt);

                    return Results.Ok(new FinancialPeriodListResponse(periods.Select(ToPeriodDto).ToList()));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListFinancialPeriods")
        .Produces<FinancialPeriodListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/periods", async (
            Guid tenantId,
            Guid entityId,
            CreateFinancialPeriodRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceConfigManage],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        var overlapping = await db.FinancialPeriods
                            .AsNoTracking()
                            .AnyAsync(
                                p => p.TenantId == tenantId
                                    && p.LegalEntityId == entityId
                                    && p.StartDate <= request.EndDate
                                    && p.EndDate >= request.StartDate,
                                innerCt);
                        if (overlapping)
                        {
                            return EndpointSupport.Conflict(
                                httpContext,
                                "overlapping_period_dates",
                                "Fiscal period date range overlaps with an existing period in this legal entity.");
                        }

                        FinancialPeriod period;
                        try
                        {
                            period = FinancialPeriod.Create(
                                Guid.NewGuid(),
                                tenantId,
                                entityId,
                                request.PeriodCode,
                                request.FiscalYear,
                                request.PeriodNumber,
                                request.StartDate,
                                request.EndDate,
                                FinancialPeriodStatuses.Open,
                                scope.EvaluatedAtUtc);
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                        {
                            return MapDomainError(httpContext, ex, "invalid_financial_period");
                        }

                        db.FinancialPeriods.Add(period);
                        EndpointSupport.RecordAuditAndOutbox(
                            db,
                            tenantId,
                            entityId,
                            actorUserId,
                            "FINANCIAL_PERIOD_CREATED",
                            "FINANCIAL_PERIOD",
                            period.PeriodId.ToString("D"),
                            $"Opened fiscal period {period.PeriodCode} ({period.StartDate:yyyy-MM-dd}..{period.EndDate:yyyy-MM-dd})",
                            "finance.period.created.v1",
                            $"gl:period:create:{period.PeriodId:D}",
                            new { period.PeriodId, period.PeriodCode, period.FiscalYear, period.PeriodNumber, period.Status },
                            scope.EvaluatedAtUtc);

                        await db.SaveChangesAsync(innerCt);
                        return Results.Created(
                            $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/finance/periods/{period.PeriodId:D}",
                            ToPeriodDto(period));
                    },
                    ct);
            }
            catch (Exception ex)
            {
                var mapped = EndpointSupport.MapPersistenceConflict(
                    httpContext,
                    ex,
                    "financial_period_conflict",
                    "A fiscal period with this code or year/number already exists in this legal entity.");
                if (mapped is not null)
                {
                    return mapped;
                }

                throw;
            }
        })
        .RequireAuthorization()
        .WithName("CreateFinancialPeriod")
        .Produces<FinancialPeriodDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/periods/{periodId:guid}/close", async (
            Guid tenantId,
            Guid entityId,
            Guid periodId,
            CloseFinancialPeriodRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceConfigManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    var period = await db.FinancialPeriods
                        .SingleOrDefaultAsync(
                            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PeriodId == periodId,
                            innerCt);
                    if (period is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "period_not_found", "Fiscal period was not found in this legal entity.");
                    }

                    try
                    {
                        if (request.HardLock)
                        {
                            period.Lock(actorUserId, request.Note, scope.EvaluatedAtUtc);
                        }
                        else
                        {
                            period.Close(actorUserId, request.Note, scope.EvaluatedAtUtc);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        return MapDomainError(httpContext, ex, "period_not_open", StatusCodes.Status409Conflict);
                    }
                    catch (ArgumentException ex)
                    {
                        return MapDomainError(httpContext, ex, "invalid_close_note");
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        request.HardLock ? "FINANCIAL_PERIOD_LOCKED" : "FINANCIAL_PERIOD_CLOSED",
                        "FINANCIAL_PERIOD",
                        period.PeriodId.ToString("D"),
                        $"Period {period.PeriodCode} transitioned to {period.Status}",
                        "finance.period.closed.v1",
                        $"gl:period:close:{period.PeriodId:D}:{period.Version}",
                        new { period.PeriodId, period.PeriodCode, period.Status, period.CloseNote },
                        scope.EvaluatedAtUtc);

                    await db.SaveChangesAsync(innerCt);
                    return Results.Ok(ToPeriodDto(period));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CloseFinancialPeriod")
        .Produces<FinancialPeriodDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static void MapAccountMappingRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/account-mappings", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var items = await (
                        from m in db.AccountMappings.AsNoTracking()
                        join a in db.GlAccounts.AsNoTracking()
                            on new { m.TenantId, m.LegalEntityId, m.AccountId }
                            equals new { a.TenantId, a.LegalEntityId, a.AccountId }
                        where m.TenantId == tenantId && m.LegalEntityId == entityId
                        orderby m.PurposeCode
                        select new AccountMappingDto(
                            m.MappingId,
                            m.PurposeCode,
                            m.AccountId,
                            a.AccountCode,
                            a.Name,
                            a.AccountType,
                            m.Notes,
                            m.UpdatedAtUtc,
                            m.Version))
                        .ToListAsync(innerCt);

                    return Results.Ok(new AccountMappingListResponse(items, GlMappingPurposes.All));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListAccountMappings")
        .Produces<AccountMappingListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/account-mappings", async (
            Guid tenantId,
            Guid entityId,
            UpsertAccountMappingRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceConfigManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, actorUserId, innerCt) =>
                {
                    string purposeCode;
                    try
                    {
                        purposeCode = GlMappingPurposes.Normalize(request.PurposeCode);
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_mapping_purpose", ex.Message);
                    }

                    var account = await db.GlAccounts
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == request.AccountId,
                            innerCt);
                    if (account is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "foreign_or_missing_account", "Target GL account was not found in this legal entity.");
                    }

                    var existing = await db.AccountMappings
                        .SingleOrDefaultAsync(
                            m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.PurposeCode == purposeCode,
                            innerCt);

                    AccountMapping saved;
                    try
                    {
                        if (existing is null)
                        {
                            saved = AccountMapping.Create(
                                Guid.NewGuid(),
                                tenantId,
                                entityId,
                                purposeCode,
                                account,
                                request.Notes,
                                actorUserId,
                                scope.EvaluatedAtUtc);
                            db.AccountMappings.Add(saved);
                        }
                        else
                        {
                            existing.UpdateAccount(account, request.Notes, actorUserId, scope.EvaluatedAtUtc);
                            saved = existing;
                        }
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        return MapDomainError(httpContext, ex, "invalid_account_mapping");
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        "GL_ACCOUNT_MAPPING_SAVED",
                        "ACCOUNT_MAPPING",
                        saved.MappingId.ToString("D"),
                        $"Mapped {saved.PurposeCode} -> {account.AccountCode} ({account.Name})",
                        "finance.account_mapping.saved.v1",
                        $"gl:mapping:{saved.MappingId:D}:{saved.Version}",
                        new { saved.MappingId, saved.PurposeCode, saved.AccountId, account.AccountCode },
                        scope.EvaluatedAtUtc);

                    await db.SaveChangesAsync(innerCt);
                    return Results.Ok(new AccountMappingDto(
                        saved.MappingId,
                        saved.PurposeCode,
                        saved.AccountId,
                        account.AccountCode,
                        account.Name,
                        account.AccountType,
                        saved.Notes,
                        saved.UpdatedAtUtc,
                        saved.Version));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpsertAccountMapping")
        .Produces<AccountMappingDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapTaxRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/tax-codes", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var items = await db.TaxCodes
                        .AsNoTracking()
                        .Where(t => t.TenantId == tenantId && t.LegalEntityId == entityId)
                        .OrderBy(t => t.Code)
                        .ThenByDescending(t => t.EffectiveFrom)
                        .ToListAsync(innerCt);

                    return Results.Ok(new TaxCodeListResponse(items.Select(ToTaxCodeDto).ToList(), TaxDeterminationEvaluator.Od07Disclaimer));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListTaxCodes")
        .Produces<TaxCodeListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/tax-codes", async (
            Guid tenantId,
            Guid entityId,
            CreateTaxCodeRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceConfigManage],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        if (request.TaxAccountId is { } accId)
                        {
                            var acc = await db.GlAccounts
                                .AsNoTracking()
                                .SingleOrDefaultAsync(
                                    a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.AccountId == accId,
                                    innerCt);
                            if (acc is null || !acc.AllowPosting || acc.Status != MasterStatuses.Active)
                            {
                                return EndpointSupport.BadRequest(httpContext, "invalid_tax_account", "Tax account must be an active postable account in this legal entity.");
                            }
                        }

                        TaxCode taxCode;
                        try
                        {
                            taxCode = TaxCode.Create(
                                Guid.NewGuid(),
                                tenantId,
                                entityId,
                                request.Code,
                                request.Name,
                                request.TaxFamily,
                                request.Direction,
                                request.TreatmentClass,
                                request.NominalRatePercent,
                                request.TaxableBaseRatioNumerator,
                                request.TaxableBaseRatioDenominator,
                                request.RequiresDocumentaryEvidence,
                                request.RequiresCounterpartyNpwp,
                                request.TaxAccountId,
                                request.EffectiveFrom,
                                request.EffectiveTo,
                                TaxReviewStatuses.UnreviewedConfigurable,
                                request.StatutoryReferenceNote,
                                scope.EvaluatedAtUtc);
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                        {
                            return MapDomainError(httpContext, ex, "invalid_tax_code");
                        }

                        db.TaxCodes.Add(taxCode);
                        EndpointSupport.RecordAuditAndOutbox(
                            db,
                            tenantId,
                            entityId,
                            actorUserId,
                            "TAX_CODE_CREATED",
                            "TAX_CODE",
                            taxCode.TaxCodeId.ToString("D"),
                            $"Created configurable tax code {taxCode.Code} ({taxCode.NominalRatePercent:F2}% DPP {taxCode.TaxableBaseRatioNumerator}/{taxCode.TaxableBaseRatioDenominator})",
                            "finance.tax_code.created.v1",
                            $"tax:code:create:{taxCode.TaxCodeId:D}",
                            new { taxCode.TaxCodeId, taxCode.Code, taxCode.TaxFamily, taxCode.TreatmentClass, taxCode.NominalRatePercent },
                            scope.EvaluatedAtUtc);

                        await db.SaveChangesAsync(innerCt);
                        return Results.Created(
                            $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/finance/tax-codes/{taxCode.TaxCodeId:D}",
                            ToTaxCodeDto(taxCode));
                    },
                    ct);
            }
            catch (Exception ex)
            {
                var mapped = EndpointSupport.MapPersistenceConflict(
                    httpContext,
                    ex,
                    "tax_code_conflict",
                    "A tax code with the same code and effective date already exists in this legal entity.");
                if (mapped is not null)
                {
                    return mapped;
                }

                throw;
            }
        })
        .RequireAuthorization()
        .WithName("CreateTaxCode")
        .Produces<TaxCodeDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/tax-codes/evaluate-preview", async (
            Guid tenantId,
            Guid entityId,
            EvaluateTaxPreviewRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    TaxCode? taxCode = null;
                    if (request.TaxCodeId is { } tid && tid != Guid.Empty)
                    {
                        taxCode = await db.TaxCodes
                            .AsNoTracking()
                            .SingleOrDefaultAsync(
                                t => t.TenantId == tenantId && t.LegalEntityId == entityId && t.TaxCodeId == tid,
                                innerCt);
                        if (taxCode is null)
                        {
                            return EndpointSupport.NotFound(httpContext, "foreign_or_missing_tax_code", "Tax code not found in this legal entity.");
                        }
                    }

                    try
                    {
                        var env = TaxDeterminationEvaluator.Evaluate(
                            taxCode,
                            request.GrossAmount,
                            request.TransactionDate,
                            request.HasCounterpartyNpwp,
                            request.EvidenceProvided,
                            currencyScale: 0);

                        return Results.Ok(new EvaluateTaxPreviewResponse(
                            env.TaxCodeId,
                            env.Code,
                            env.TaxFamily,
                            env.Direction,
                            env.TreatmentClass,
                            env.GrossAmount,
                            env.TaxableBaseAmount,
                            env.NominalRatePercent,
                            env.EffectiveRatePercent,
                            env.TaxAmount,
                            env.ReviewStatus,
                            env.EvidenceStatus,
                            env.IsBlocked,
                            env.BlockedReason,
                            env.GovernanceDisclaimer));
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        return MapDomainError(httpContext, ex, "invalid_tax_evaluation");
                    }
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("EvaluateTaxPreview")
        .Produces<EvaluateTaxPreviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static void MapJournalRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/journals", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] Guid? periodId,
            [FromQuery] string? entryKind,
            [FromQuery] string? q,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var normalizedPage = page is null or < 1 ? 1 : page.Value;
                    var normalizedSize = pageSize is null or < 1 or > 100 ? 25 : pageSize.Value;
                    var search = q?.Trim().ToUpperInvariant();
                    var kindFilter = entryKind?.Trim().ToUpperInvariant();

                    var query = db.JournalEntries
                        .AsNoTracking()
                        .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId);

                    if (periodId is { } pid && pid != Guid.Empty)
                    {
                        query = query.Where(j => j.PeriodId == pid);
                    }

                    if (!string.IsNullOrEmpty(kindFilter))
                    {
                        query = query.Where(j => j.EntryKind == kindFilter);
                    }

                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(j =>
                            j.JournalNumber.ToUpper().Contains(search)
                            || j.Description.ToUpper().Contains(search)
                            || j.SourceEventKey.ToUpper().Contains(search));
                    }

                    var totalCount = await query.CountAsync(innerCt);
                    var rows = await query
                        .OrderByDescending(j => j.PostedAtUtc)
                        .ThenByDescending(j => j.JournalNumber)
                        .Skip((normalizedPage - 1) * normalizedSize)
                        .Take(normalizedSize)
                        .ToListAsync(innerCt);

                    var periodIds = rows.Select(r => r.PeriodId).Distinct().ToList();
                    var periodMap = await db.FinancialPeriods
                        .AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && periodIds.Contains(p.PeriodId))
                        .ToDictionaryAsync(p => p.PeriodId, p => p.PeriodCode, innerCt);

                    var rowIds = rows.Select(r => r.JournalId).ToList();
                    var reversals = await db.JournalEntries
                        .AsNoTracking()
                        .Where(r => r.TenantId == tenantId
                            && r.LegalEntityId == entityId
                            && r.ReversalOfJournalId != null
                            && rowIds.Contains(r.ReversalOfJournalId.Value))
                        .ToDictionaryAsync(r => r.ReversalOfJournalId!.Value, r => r.JournalId, innerCt);

                    var dtos = rows.Select(j => new JournalListItemDto(
                        j.JournalId,
                        j.PeriodId,
                        periodMap.GetValueOrDefault(j.PeriodId, string.Empty),
                        j.JournalNumber,
                        j.EntryKind,
                        j.PostingDate,
                        j.CurrencyCode,
                        j.Description,
                        j.SourceModule,
                        j.SourceDocumentType,
                        j.SourceDocumentId,
                        j.SourceEventKey,
                        j.ReversalOfJournalId,
                        reversals.ContainsKey(j.JournalId),
                        reversals.TryGetValue(j.JournalId, out var revId) ? revId : null,
                        j.TotalDebit,
                        j.TotalCredit,
                        j.LineCount,
                        j.PostedByUserId,
                        j.PostedAtUtc)).ToList();

                    return Results.Ok(new PagedJournalsResponse(dtos, totalCount, normalizedPage, normalizedSize));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListJournals")
        .Produces<PagedJournalsResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/journals/{journalId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid journalId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteFinanceScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var dto = await LoadJournalDetailDtoAsync(db, tenantId, entityId, journalId, idempotentReplay: false, innerCt);
                    return dto is null
                        ? EndpointSupport.NotFound(httpContext, "journal_not_found", "Journal entry was not found in this legal entity.")
                        : Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetJournalDetail")
        .Produces<JournalDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/journals", async (
            Guid tenantId,
            Guid entityId,
            PostJournalRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var lineInputs = request.Lines?
                .Select(l => new GlJournalLineInput(l.AccountId, l.Description, l.DebitAmount, l.CreditAmount, l.PartnerId, l.ItemId, l.TaxCodeId))
                .ToList() ?? [];

            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceJournalPost],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        var cmd = new GlPostJournalCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.PostingDate,
                            request.CurrencyCode,
                            request.Description,
                            request.SourceModule,
                            request.SourceDocumentType,
                            request.SourceDocumentId,
                            request.SourceEventKey,
                            lineInputs,
                            actorUserId,
                            scope.EvaluatedAtUtc);

                        var outcome = await GlPostingKernel.PostJournalAsync(db, cmd, innerCt);
                        if (!outcome.Succeeded || outcome.Journal is null)
                        {
                            return MapKernelFailure(httpContext, outcome);
                        }

                        var dto = await BuildJournalDetailDtoAsync(
                            db,
                            tenantId,
                            entityId,
                            outcome.Journal,
                            outcome.Lines,
                            outcome.PeriodCode,
                            outcome.IdempotentReplay,
                            innerCt);

                        return outcome.IdempotentReplay
                            ? Results.Ok(dto)
                            : Results.Created(
                                $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/finance/journals/{outcome.Journal.JournalId:D}",
                                dto);
                    },
                    ct);
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex))
            {
                // Cross-period concurrent duplicate race on (source_module, source_event_key): resolve cleanly in fresh read scope.
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceJournalPost],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (_, _, innerCt) =>
                    {
                        var normModule = request.SourceModule?.Trim().ToUpperInvariant() ?? string.Empty;
                        var normKey = request.SourceEventKey?.Trim() ?? string.Empty;
                        var committed = await db.JournalEntries
                            .AsNoTracking()
                            .SingleOrDefaultAsync(
                                j => j.TenantId == tenantId
                                    && j.LegalEntityId == entityId
                                    && j.SourceModule == normModule
                                    && j.SourceEventKey == normKey,
                                innerCt);

                        if (committed is null)
                        {
                            return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Journal conflict detected; please retry.");
                        }

                        var requestHash = GlPostingHash.ComputePayloadHash(
                            request.PostingDate,
                            request.CurrencyCode,
                            JournalEntryKinds.Standard,
                            null,
                            lineInputs);

                        if (!string.Equals(committed.PayloadHash, requestHash, StringComparison.Ordinal))
                        {
                            return EndpointSupport.Conflict(
                                httpContext,
                                "duplicate_posting_source_conflict",
                                $"Source event '{normModule}:{normKey}' was already posted with a different payload.");
                        }

                        var dto = await LoadJournalDetailDtoAsync(db, tenantId, entityId, committed.JournalId, idempotentReplay: true, innerCt);
                        return Results.Ok(dto);
                    },
                    ct);
            }
        })
        .RequireAuthorization()
        .WithName("PostJournal")
        .Produces<JournalDetailDto>(StatusCodes.Status201Created)
        .Produces<JournalDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/finance/journals/{journalId:guid}/reverse", async (
            Guid tenantId,
            Guid entityId,
            Guid journalId,
            ReverseJournalRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                return await ExecuteFinanceScopeAsync(
                    tenantId,
                    entityId,
                    [PermissionCodes.FinanceJournalPost],
                    userManager,
                    scopeExecutor,
                    httpContext,
                    async (scope, actorUserId, innerCt) =>
                    {
                        var outcome = await GlPostingKernel.ReverseJournalAsync(
                            db,
                            new GlReverseJournalCommand(
                                tenantId,
                                entityId,
                                scope.Entity.FunctionalCurrencyCode,
                                journalId,
                                request.ReversalPostingDate,
                                request.Reason,
                                actorUserId,
                                scope.EvaluatedAtUtc),
                            innerCt);

                        if (!outcome.Succeeded || outcome.Journal is null)
                        {
                            return MapKernelFailure(httpContext, outcome);
                        }

                        var dto = await BuildJournalDetailDtoAsync(
                            db,
                            tenantId,
                            entityId,
                            outcome.Journal,
                            outcome.Lines,
                            outcome.PeriodCode,
                            idempotentReplay: false,
                            innerCt);

                        return Results.Created(
                            $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/finance/journals/{outcome.Journal.JournalId:D}",
                            dto);
                    },
                    ct);
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex))
            {
                return EndpointSupport.Conflict(
                    httpContext,
                    "journal_already_reversed",
                    "A compensating reversal already exists for this journal entry.");
            }
        })
        .RequireAuthorization()
        .WithName("ReverseJournal")
        .Produces<JournalDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ExecuteFinanceScopeAsync(
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
                        "Your active grants for this legal entity do not permit this finance operation.");
                }

                return await action(scope, actorUserId, innerCt);
            },
            ct);

        return result.IsAllowed
            ? result.Value!
            : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static bool IsUniqueConstraintViolation(Exception ex) =>
        ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;

    private static IResult MapKernelFailure(HttpContext httpContext, GlPostingOutcome outcome)
    {
        var code = outcome.ErrorCode ?? "gl_posting_error";
        var detail = outcome.ErrorDetail ?? "GL posting failed.";
        return outcome.FailureHttpStatus switch
        {
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static IResult MapDomainError(HttpContext httpContext, Exception ex, string fallbackCode, int statusCode = StatusCodes.Status400BadRequest)
    {
        var msg = ex.Message ?? string.Empty;
        var sepIdx = msg.IndexOf(": ", StringComparison.Ordinal);
        var code = sepIdx > 0 && sepIdx <= 48 && msg[..sepIdx].All(c => char.IsLower(c) || char.IsDigit(c) || c == '_')
            ? msg[..sepIdx]
            : fallbackCode;
        var detail = sepIdx > 0 && code != fallbackCode ? msg[(sepIdx + 2)..] : msg;

        return statusCode switch
        {
            StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, code, detail),
            StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, code, detail),
            _ => EndpointSupport.BadRequest(httpContext, code, detail)
        };
    }

    private static async Task<JournalDetailDto?> LoadJournalDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid journalId,
        bool idempotentReplay,
        CancellationToken ct)
    {
        var entry = await db.JournalEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                j => j.TenantId == tenantId && j.LegalEntityId == entityId && j.JournalId == journalId,
                ct);
        if (entry is null)
        {
            return null;
        }

        var lines = await db.JournalLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && l.JournalId == entry.JournalId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var periodCode = await db.FinancialPeriods
            .AsNoTracking()
            .Where(p => p.PeriodId == entry.PeriodId)
            .Select(p => p.PeriodCode)
            .SingleOrDefaultAsync(ct);

        return await BuildJournalDetailDtoAsync(db, tenantId, entityId, entry, lines, periodCode, idempotentReplay, ct);
    }

    private static async Task<JournalDetailDto> BuildJournalDetailDtoAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        JournalEntry entry,
        IReadOnlyList<JournalLine> lines,
        string? periodCode,
        bool idempotentReplay,
        CancellationToken ct)
    {
        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.GlAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, a => (a.AccountCode, a.Name), ct);

        var reversalId = await db.JournalEntries
            .AsNoTracking()
            .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && j.ReversalOfJournalId == entry.JournalId)
            .Select(j => (Guid?)j.JournalId)
            .SingleOrDefaultAsync(ct);

        var lineDtos = lines
            .OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                var acc = accounts.TryGetValue(l.AccountId, out var pair) ? pair : (AccountCode: string.Empty, Name: string.Empty);
                return new JournalLineDto(
                    l.LineId,
                    l.LineNumber,
                    l.AccountId,
                    acc.AccountCode,
                    acc.Name,
                    l.Description,
                    l.DebitAmount,
                    l.CreditAmount,
                    l.PartnerId,
                    l.ItemId,
                    l.TaxCodeId);
            })
            .ToList();

        return new JournalDetailDto(
            entry.JournalId,
            entry.PeriodId,
            periodCode ?? string.Empty,
            entry.JournalNumber,
            entry.EntryKind,
            entry.PostingDate,
            entry.CurrencyCode,
            entry.Description,
            entry.SourceModule,
            entry.SourceDocumentType,
            entry.SourceDocumentId,
            entry.SourceEventKey,
            entry.PayloadHash,
            entry.ReversalOfJournalId,
            reversalId is not null,
            reversalId,
            entry.TotalDebit,
            entry.TotalCredit,
            entry.LineCount,
            entry.PostedByUserId,
            entry.PostedAtUtc,
            idempotentReplay,
            lineDtos);
    }

    private static GlAccountDto ToAccountDto(GlAccount a) =>
        new(
            a.AccountId,
            a.AccountCode,
            a.Name,
            a.AccountType,
            a.AccountCategory,
            a.NormalBalance,
            a.ParentAccountId,
            a.AllowPosting,
            a.IsControlAccount,
            a.Status,
            a.CreatedAtUtc,
            a.UpdatedAtUtc,
            a.Version);

    private static FinancialPeriodDto ToPeriodDto(FinancialPeriod p) =>
        new(
            p.PeriodId,
            p.PeriodCode,
            p.FiscalYear,
            p.PeriodNumber,
            p.StartDate,
            p.EndDate,
            p.Status,
            p.ClosedAtUtc,
            p.ClosedByUserId,
            p.CloseNote,
            p.UpdatedAtUtc,
            p.Version);

    private static TaxCodeDto ToTaxCodeDto(TaxCode t) =>
        new(
            t.TaxCodeId,
            t.Code,
            t.Name,
            t.TaxFamily,
            t.Direction,
            t.TreatmentClass,
            t.NominalRatePercent,
            t.TaxableBaseRatioNumerator,
            t.TaxableBaseRatioDenominator,
            t.RequiresDocumentaryEvidence,
            t.RequiresCounterpartyNpwp,
            t.TaxAccountId,
            t.EffectiveFrom,
            t.EffectiveTo,
            t.ReviewStatus,
            t.StatutoryReferenceNote,
            t.Status,
            t.UpdatedAtUtc,
            t.Version);
}
