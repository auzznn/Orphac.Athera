using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

internal static class PaymentsAndBankingEndpoints
{
    private const string Od06DefaultNotice =
        "OD-06 safe defaults active (ATH-CR-008): generating simulated payment files never transfers funds or clears AP/Bank; four-eyes release with explicit reference posts Dr 2100 / Cr 1120, and exact bank statement matching posts Dr 1120 / Cr 1110.";

    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.ApRead,
        PermissionCodes.ApManage,
        PermissionCodes.ApApprove,
        PermissionCodes.ApPost,
        PermissionCodes.FinanceRead,
        PermissionCodes.FinanceConfigManage,
        PermissionCodes.FinanceJournalPost
    ];

    private static readonly string[] PreparePermissions =
    [
        PermissionCodes.ApManage,
        PermissionCodes.ApPost,
        PermissionCodes.FinanceJournalPost
    ];

    private static readonly string[] ReleasePermissions =
    [
        PermissionCodes.ApApprove,
        PermissionCodes.ApPost,
        PermissionCodes.FinanceJournalPost
    ];

    private static readonly string[] ReconcilePermissions =
    [
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.ApPost
    ];

    public static IEndpointRouteBuilder MapPaymentsAndBankingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/overview", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReadScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                "Your active grants for this legal entity do not permit viewing supplier payments and bank reconciliation.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    return Results.Ok(overview);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetPaymentsOverview")
        .Produces<PaymentsOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteReadScopeAsync(
                tenantId,
                entityId,
                ReadPermissions,
                "Your active grants for this legal entity do not permit viewing supplier payment details.",
                userManager,
                scopeExecutor,
                httpContext,
                async (_, _, innerCt) =>
                {
                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, paymentId, false, innerCt);
                    return detail is null
                        ? EndpointSupport.NotFound(httpContext, "payment_not_found", "Supplier payment was not found in this legal entity.")
                        : Results.Ok(detail);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetSupplierPaymentDetail")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/bank-accounts", async (
            Guid tenantId,
            Guid entityId,
            UpsertBankAccountRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                [PermissionCodes.FinanceConfigManage],
                "Your active grants for this legal entity require finance.config.manage to configure bank accounts.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.UpsertBankAccountAsync(
                        db,
                        new UpsertBankAccountCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.AccountCode,
                            request.BankName,
                            request.BranchName,
                            request.AccountNumber,
                            request.AccountHolderName,
                            request.GlAccountId,
                            request.ClearingGlAccountId,
                            request.Status,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.BankAccount is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    var dto = overview.BankAccounts.Single(b => b.BankAccountId == outcome.BankAccount.BankAccountId);
                    return (Results.Ok(dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpsertBankAccount")
        .Produces<BankAccountDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments", async (
            Guid tenantId,
            Guid entityId,
            CreateSupplierPaymentProposalRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                PreparePermissions,
                "Your active grants for this legal entity do not permit preparing supplier payment proposals.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var allocs = (request.Allocations ?? Array.Empty<PaymentAllocationInputRequest>())
                        .Select(a => new PaymentAllocationInput(a.PayableItemId, a.AllocatedAmount, a.Notes))
                        .ToList();
                    var resolvedSupplierId = request.SupplierPartnerId;
                    if (resolvedSupplierId == Guid.Empty && allocs.Count > 0)
                    {
                        var firstPayable = await db.PayableItems.AsNoTracking()
                            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PayableItemId == allocs[0].PayableItemId)
                            .Select(p => (Guid?)p.SupplierPartnerId)
                            .SingleOrDefaultAsync(innerCt);
                        if (!firstPayable.HasValue)
                        {
                            throw new AbortedPaymentEndpointException(StatusCodes.Status404NotFound, "payable_not_found", "One or more payables do not belong to this legal entity.");
                        }
                        resolvedSupplierId = firstPayable.Value;
                    }

                    var outcome = await SupplierSettlementCoordinator.CreatePaymentProposalAsync(
                        db,
                        new CreatePaymentProposalCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.BankAccountId,
                            resolvedSupplierId,
                            request.BeneficiaryBankName,
                            request.BeneficiaryAccountNumber,
                            request.BeneficiaryAccountHolder,
                            request.PaymentMethod,
                            request.PaymentDate,
                            request.Notes,
                            allocs,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, false, innerCt);
                    return (Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/payments/{outcome.Payment.SupplierPaymentId}", detail), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateSupplierPaymentProposal")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            UpdateSupplierPaymentProposalRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                PreparePermissions,
                "Your active grants for this legal entity do not permit editing supplier payment proposals.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.UpdatePaymentProposalAsync(
                        db,
                        new UpdatePaymentProposalCommand(
                            tenantId,
                            entityId,
                            paymentId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.BankAccountId,
                            request.BeneficiaryBankName,
                            request.BeneficiaryAccountNumber,
                            request.BeneficiaryAccountHolder,
                            request.PaymentMethod,
                            request.PaymentDate,
                            request.Notes,
                            (request.Allocations ?? Array.Empty<PaymentAllocationInputRequest>())
                                .Select(a => new PaymentAllocationInput(a.PayableItemId, a.AllocatedAmount, a.Notes))
                                .ToList(),
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, false, innerCt);
                    return (Results.Ok(detail), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateSupplierPaymentProposal")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}/simulate-file", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            SimulatePaymentInstructionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                PreparePermissions,
                "Your active grants for this legal entity do not permit simulating payment instruction files.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.SimulatePaymentInstructionFileAsync(
                        db,
                        new SimulatePaymentFileCommand(
                            tenantId,
                            entityId,
                            paymentId,
                            request.FormatCode ?? PaymentInstructionFormats.SimulatedCsvV1,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, false, innerCt);
                    return (Results.Ok(detail), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("SimulateSupplierPaymentFile")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}/submit", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                PreparePermissions,
                "Your active grants for this legal entity do not permit submitting supplier payments for release.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.SubmitPaymentForReleaseAsync(
                        db,
                        new SubmitPaymentCommand(tenantId, entityId, paymentId, actorId, DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, false, innerCt);
                    return (Results.Ok(detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("SubmitSupplierPaymentForRelease")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}/release", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            ReleaseSupplierPaymentRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                ReleasePermissions,
                "Your active grants for this legal entity do not permit authorizing supplier payment release.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.ReleasePaymentAsync(
                        db,
                        new ReleasePaymentCommand(
                            tenantId,
                            entityId,
                            paymentId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.PostingDate,
                            request.ReleaseReference,
                            request.IdempotencyKey,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, outcome.IdempotentReplay, innerCt);
                    return (Results.Ok(detail), !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ReleaseSupplierPayment")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/{paymentId:guid}/reject-released", async (
            Guid tenantId,
            Guid entityId,
            Guid paymentId,
            RejectReleasedPaymentRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                ReconcilePermissions,
                "Your active grants for this legal entity do not permit recording bank rejections on released payments.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await SupplierSettlementCoordinator.RejectReleasedPaymentAsync(
                        db,
                        new RejectReleasedPaymentCommand(
                            tenantId,
                            entityId,
                            paymentId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.ReversalPostingDate,
                            request.Reason,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Payment is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var detail = await BuildPaymentDetailAsync(db, tenantId, entityId, outcome.Payment.SupplierPaymentId, false, innerCt);
                    return (Results.Ok(detail), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("RejectReleasedSupplierPayment")
        .Produces<SupplierPaymentDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/bank-statements", async (
            Guid tenantId,
            Guid entityId,
            ImportBankStatementRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                ReconcilePermissions,
                "Your active grants for this legal entity do not permit importing bank statements.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (_, actorId, innerCt) =>
                {
                    var outcome = await BankReconciliationCoordinator.ImportBankStatementAsync(
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
                            request.Lines?.Select(l => new BankStatementLineInput(l.ValueDate, l.Direction, l.Amount, l.BankReference, l.CounterpartyName, l.Description)).ToList(),
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Statement is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    var dto = overview.BankStatements.Single(s => s.BankStatementId == outcome.Statement.BankStatementId);
                    return (Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/payments/bank-statements/{dto.BankStatementId}", dto), false);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ImportBankStatement")
        .Produces<BankStatementDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/reconcile", async (
            Guid tenantId,
            Guid entityId,
            ReconcileBankStatementLineRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                ReconcilePermissions,
                "Your active grants for this legal entity do not permit posting bank statement reconciliations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await BankReconciliationCoordinator.ReconcileStatementLineAsync(
                        db,
                        new ReconcileBankLineCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            request.BankStatementLineId,
                            request.SupplierPaymentId,
                            request.PostingDate,
                            request.MatchedAmount,
                            request.IdempotencyKey,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Match is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    var matchDto = overview.Matches.Single(m => m.BankPaymentMatchId == outcome.Match.BankPaymentMatchId);
                    return (Results.Ok(matchDto), !outcome.IdempotentReplay);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ReconcileBankStatementLine")
        .Produces<BankPaymentMatchDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/payments/matches/{matchId:guid}/reverse", async (
            Guid tenantId,
            Guid entityId,
            Guid matchId,
            ReverseBankMatchRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutatingScopeAsync(
                tenantId,
                entityId,
                ReconcilePermissions,
                "Your active grants for this legal entity do not permit reversing bank reconciliations.",
                userManager,
                scopeExecutor,
                outboxSignal,
                httpContext,
                async (scope, actorId, innerCt) =>
                {
                    var outcome = await BankReconciliationCoordinator.ReverseBankMatchAsync(
                        db,
                        new ReverseBankMatchCommand(
                            tenantId,
                            entityId,
                            scope.Entity.FunctionalCurrencyCode,
                            matchId,
                            request.ReversalPostingDate,
                            request.Reason,
                            actorId,
                            DateTimeOffset.UtcNow),
                        innerCt);

                    if (!outcome.Succeeded || outcome.Match is null)
                    {
                        throw new AbortedPaymentEndpointException(outcome.FailureHttpStatus, outcome.ErrorCode!, outcome.ErrorDetail!);
                    }

                    var overview = await BuildOverviewAsync(db, tenantId, entityId, innerCt);
                    var matchDto = overview.Matches.Single(m => m.BankPaymentMatchId == outcome.Match.BankPaymentMatchId);
                    return (Results.Ok(matchDto), true);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ReverseBankPaymentMatch")
        .Produces<BankPaymentMatchDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<PaymentsOverviewResponse> BuildOverviewAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var accountsRaw = await db.GlAccounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.Status == MasterStatuses.Active && a.AllowPosting)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);
        var accountLookup = accountsRaw.ToDictionary(a => a.AccountId);

        var bankAccountsRaw = await db.BankAccounts.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId)
            .OrderBy(b => b.AccountCode)
            .ToListAsync(ct);

        var bankDtos = bankAccountsRaw.Select(b =>
        {
            var glAcc = accountLookup.GetValueOrDefault(b.GlAccountId);
            var clAcc = accountLookup.GetValueOrDefault(b.ClearingGlAccountId);
            return new BankAccountDto(
                b.BankAccountId,
                b.AccountCode,
                b.BankName,
                b.BranchName,
                b.AccountNumber,
                b.AccountHolderName,
                b.CurrencyCode,
                b.GlAccountId,
                glAcc?.AccountCode ?? "1110",
                glAcc?.Name ?? "Cash & Bank Operating",
                b.ClearingGlAccountId,
                clAcc?.AccountCode ?? "1120",
                clAcc?.Name ?? "Bank Outbound Clearing",
                b.Status);
        }).ToList();

        var payablesRaw = await db.PayableItems.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId &&
                        (p.Status == PayableStatuses.Open || p.Status == PayableStatuses.PartiallyPaid))
            .OrderBy(p => p.DueDate)
            .ThenBy(p => p.PayableNumber)
            .Take(150)
            .ToListAsync(ct);

        var billIds = payablesRaw.Select(p => p.SupplierBillId).Distinct().ToList();
        var billNums = billIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.SupplierBills.AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.LegalEntityId == entityId && billIds.Contains(b.SupplierBillId))
                .ToDictionaryAsync(b => b.SupplierBillId, b => b.BillNumber ?? b.DraftCode, ct);

        var paymentsRaw = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var statementsRaw = await db.BankStatements.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.LegalEntityId == entityId)
            .OrderByDescending(s => s.StatementDate)
            .ThenByDescending(s => s.ImportedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        var statementLinesRaw = await db.BankStatementLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId)
            .OrderByDescending(l => l.ValueDate)
            .ThenBy(l => l.LineNumber)
            .Take(150)
            .ToListAsync(ct);

        var matchesRaw = await db.BankPaymentMatches.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId)
            .OrderByDescending(m => m.PostingDate)
            .ThenByDescending(m => m.MatchedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var jrnIds = payablesRaw.Select(p => p.GlJournalId)
            .Concat(paymentsRaw.Where(p => p.ReleaseJournalId.HasValue).Select(p => p.ReleaseJournalId!.Value))
            .Concat(paymentsRaw.Where(p => p.RejectionReversalJournalId.HasValue).Select(p => p.RejectionReversalJournalId!.Value))
            .Concat(matchesRaw.Select(m => m.ClearingGlJournalId))
            .Concat(matchesRaw.Where(m => m.ReversalGlJournalId.HasValue).Select(m => m.ReversalGlJournalId!.Value))
            .Distinct()
            .ToList();
        var jrnNums = jrnIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        var userIds = paymentsRaw.SelectMany(p => new Guid?[] { p.PreparedByUserId, p.SubmittedByUserId, p.ReleasedByUserId })
            .Concat(statementsRaw.Select(s => (Guid?)s.ImportedByUserId))
            .Concat(matchesRaw.Select(m => (Guid?)m.MatchedByUserId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        var payableDtos = payablesRaw.Select(p => new PayableItemDto(
            p.PayableItemId,
            p.PayableNumber,
            p.SupplierBillId,
            billNums.GetValueOrDefault(p.SupplierBillId, "-"),
            p.SupplierCodeSnapshot,
            p.SupplierLegalNameSnapshot,
            p.SupplierInvoiceNumber,
            p.GlJournalId,
            jrnNums.GetValueOrDefault(p.GlJournalId, "-"),
            p.PostingDate,
            p.DueDate,
            p.CurrencyCode,
            p.GrossBillAmount,
            p.WithholdingTaxAmount,
            p.NetPayableAmount,
            p.PaidAmount,
            p.CreditedAmount,
            p.RemainingOpenAmount,
            p.Status,
            p.PaymentBlockStatus,
            p.PaymentBlockReason)).ToList();

        var paymentDtos = paymentsRaw.Select(p => ToPaymentSummaryDto(p, jrnNums, emails)).ToList();
        var bankCodeMap = bankAccountsRaw.ToDictionary(b => b.BankAccountId, b => b.AccountCode);
        var stmtNumMap = statementsRaw.ToDictionary(s => s.BankStatementId, s => s.StatementNumber);
        var stmtLineRefMap = statementLinesRaw.ToDictionary(l => l.BankStatementLineId, l => l.BankReference);
        var payNumMap = paymentsRaw.ToDictionary(p => p.SupplierPaymentId, p => p.PaymentNumber ?? p.DraftCode);

        var stmtDtos = statementsRaw.Select(s => new BankStatementDto(
            s.BankStatementId,
            s.StatementNumber,
            s.BankAccountId,
            s.BankAccountCodeSnapshot,
            s.StatementReference,
            s.StatementDate,
            s.CurrencyCode,
            s.OpeningBalance,
            s.ClosingBalance,
            s.TotalDebitAmount,
            s.TotalCreditAmount,
            s.LineCount,
            s.SourceFormat,
            emails.GetValueOrDefault(s.ImportedByUserId),
            s.ImportedAtUtc)).ToList();

        var stmtLineDtos = statementLinesRaw.Select(l => new BankStatementLineDto(
            l.BankStatementLineId,
            l.BankStatementId,
            stmtNumMap.GetValueOrDefault(l.BankStatementId, "-"),
            l.BankAccountId,
            bankCodeMap.GetValueOrDefault(l.BankAccountId, "-"),
            l.LineNumber,
            l.ValueDate,
            l.Direction,
            l.Amount,
            l.MatchedAmount,
            l.UnmatchedAmount,
            l.BankReference,
            l.CounterpartyName,
            l.Description,
            l.Status)).ToList();

        var matchDtos = matchesRaw.Select(m => new BankPaymentMatchDto(
            m.BankPaymentMatchId,
            m.MatchNumber,
            m.BankAccountId,
            bankCodeMap.GetValueOrDefault(m.BankAccountId, "-"),
            m.BankStatementId,
            stmtNumMap.GetValueOrDefault(m.BankStatementId, "-"),
            m.BankStatementLineId,
            stmtLineRefMap.GetValueOrDefault(m.BankStatementLineId, "-"),
            m.SupplierPaymentId,
            payNumMap.GetValueOrDefault(m.SupplierPaymentId, "-"),
            m.PostingDate,
            m.MatchedAmount,
            m.Status,
            m.ClearingGlJournalId,
            jrnNums.GetValueOrDefault(m.ClearingGlJournalId, "-"),
            m.ReversalGlJournalId,
            m.ReversalGlJournalId is { } rj ? jrnNums.GetValueOrDefault(rj) : null,
            m.ReversalReason,
            emails.GetValueOrDefault(m.MatchedByUserId),
            m.MatchedAtUtc)).ToList();

        var clearingGlAccountIds = accountsRaw
            .Where(a => a.AccountCategory == GlAccountCategories.BankOutboundClearing)
            .Select(a => a.AccountId)
            .ToList();
        var cashGlAccountIds = accountsRaw
            .Where(a => a.AccountCategory == GlAccountCategories.CashBank)
            .Select(a => a.AccountId)
            .ToList();

        var glClearingCreditNet = clearingGlAccountIds.Count == 0
            ? 0m
            : await db.JournalLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && clearingGlAccountIds.Contains(l.AccountId))
                .SumAsync(l => (decimal?)(l.CreditAmount - l.DebitAmount), ct) ?? 0m;

        var glCashNet = cashGlAccountIds.Count == 0
            ? 0m
            : await db.JournalLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && cashGlAccountIds.Contains(l.AccountId))
                .SumAsync(l => (decimal?)(l.DebitAmount - l.CreditAmount), ct) ?? 0m;

        var subledgerOpenClearing = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId &&
                        (p.Status == SupplierPaymentStatuses.Released || p.Status == SupplierPaymentStatuses.PartiallyReconciled))
            .SumAsync(p => (decimal?)p.RemainingClearingAmount, ct) ?? 0m;

        var discrepancy = decimal.Round(glClearingCreditNet - subledgerOpenClearing, 4, MidpointRounding.AwayFromZero);
        var glOptions = accountsRaw
            .Where(a => a.AccountCategory == GlAccountCategories.CashBank || a.AccountCategory == GlAccountCategories.BankOutboundClearing)
            .Select(a => new PaymentsGlAccountOptionDto(a.AccountId, a.AccountCode, a.Name, a.AccountCategory))
            .ToList();

        return new PaymentsOverviewResponse(
            Od06DefaultNotice,
            bankDtos,
            glOptions,
            payableDtos,
            paymentDtos,
            stmtDtos,
            stmtLineDtos,
            matchDtos,
            new ClearingReconciliationProofDto(
                decimal.Round(glClearingCreditNet, 4, MidpointRounding.AwayFromZero),
                decimal.Round(subledgerOpenClearing, 4, MidpointRounding.AwayFromZero),
                discrepancy,
                discrepancy == 0m,
                decimal.Round(glCashNet, 4, MidpointRounding.AwayFromZero)),
            DateTimeOffset.UtcNow);
    }

    private static async Task<SupplierPaymentDetailResponse?> BuildPaymentDetailAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid paymentId,
        bool idempotentReplay,
        CancellationToken ct)
    {
        var payment = await db.SupplierPayments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.SupplierPaymentId == paymentId, ct);
        if (payment is null)
        {
            return null;
        }

        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && a.SupplierPaymentId == paymentId)
            .OrderBy(a => a.LineNumber)
            .ToListAsync(ct);

        var instructions = await db.PaymentInstructions.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.LegalEntityId == entityId && i.SupplierPaymentId == paymentId)
            .OrderByDescending(i => i.GeneratedAtUtc)
            .ToListAsync(ct);

        var matches = await db.BankPaymentMatches.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == entityId && m.SupplierPaymentId == paymentId)
            .OrderByDescending(m => m.MatchedAtUtc)
            .ToListAsync(ct);

        var stmtIds = matches.Select(m => m.BankStatementId).Distinct().ToList();
        var lineIds = matches.Select(m => m.BankStatementLineId).Distinct().ToList();
        var stmtNums = stmtIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.BankStatements.AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.LegalEntityId == entityId && stmtIds.Contains(s.BankStatementId))
                .ToDictionaryAsync(s => s.BankStatementId, s => s.StatementNumber, ct);
        var lineRefs = lineIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.BankStatementLines.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.LegalEntityId == entityId && lineIds.Contains(l.BankStatementLineId))
                .ToDictionaryAsync(l => l.BankStatementLineId, l => l.BankReference, ct);

        var jrnIds = new[] { payment.ReleaseJournalId, payment.RejectionReversalJournalId }
            .Concat(matches.Select(m => (Guid?)m.ClearingGlJournalId))
            .Concat(matches.Select(m => m.ReversalGlJournalId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var jrnNums = jrnIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.JournalEntries.AsNoTracking()
                .Where(j => j.TenantId == tenantId && j.LegalEntityId == entityId && jrnIds.Contains(j.JournalId))
                .ToDictionaryAsync(j => j.JournalId, j => j.JournalNumber, ct);

        var userIds = new Guid?[] { payment.PreparedByUserId, payment.SubmittedByUserId, payment.ReleasedByUserId }
            .Concat(instructions.Select(i => (Guid?)i.GeneratedByUserId))
            .Concat(matches.Select(m => (Guid?)m.MatchedByUserId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var emails = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id.ToString("D"), ct);

        return new SupplierPaymentDetailResponse(
            ToPaymentSummaryDto(payment, jrnNums, emails),
            allocations.Select(a => new PaymentAllocationDto(
                a.PaymentAllocationId,
                a.LineNumber,
                a.PayableItemId,
                a.PayableNumberSnapshot,
                a.SupplierBillId,
                a.SupplierInvoiceNumberSnapshot,
                a.PayableOpenBeforeAmount,
                a.AllocatedAmount,
                a.PayableRemainingAfterAmount,
                a.Notes)).ToList(),
            instructions.Select(i => new PaymentInstructionDto(
                i.PaymentInstructionId,
                i.InstructionNumber,
                i.FormatCode,
                i.SimulationOnly,
                i.TotalAmount,
                i.CurrencyCode,
                i.PayloadText,
                i.PayloadSha256,
                emails.GetValueOrDefault(i.GeneratedByUserId),
                i.GeneratedAtUtc)).ToList(),
            matches.Select(m => new BankPaymentMatchDto(
                m.BankPaymentMatchId,
                m.MatchNumber,
                m.BankAccountId,
                payment.BankAccountCodeSnapshot,
                m.BankStatementId,
                stmtNums.GetValueOrDefault(m.BankStatementId, "-"),
                m.BankStatementLineId,
                lineRefs.GetValueOrDefault(m.BankStatementLineId, "-"),
                m.SupplierPaymentId,
                payment.PaymentNumber ?? payment.DraftCode,
                m.PostingDate,
                m.MatchedAmount,
                m.Status,
                m.ClearingGlJournalId,
                jrnNums.GetValueOrDefault(m.ClearingGlJournalId, "-"),
                m.ReversalGlJournalId,
                m.ReversalGlJournalId is { } rj ? jrnNums.GetValueOrDefault(rj) : null,
                m.ReversalReason,
                emails.GetValueOrDefault(m.MatchedByUserId),
                m.MatchedAtUtc)).ToList(),
            idempotentReplay);
    }

    private static SupplierPaymentSummaryDto ToPaymentSummaryDto(
        SupplierPayment p,
        IReadOnlyDictionary<Guid, string> jrnNums,
        IReadOnlyDictionary<Guid, string> emails) =>
        new(
            p.SupplierPaymentId,
            p.DraftCode,
            p.PaymentNumber,
            p.BankAccountId,
            p.BankAccountCodeSnapshot,
            p.BankNameSnapshot,
            p.SupplierPartnerId,
            p.SupplierCodeSnapshot,
            p.SupplierLegalNameSnapshot,
            p.BeneficiaryBankName,
            p.BeneficiaryAccountNumber,
            p.BeneficiaryAccountHolder,
            p.CurrencyCode,
            p.PaymentMethod,
            p.PaymentDate,
            p.TotalAllocatedAmount,
            p.ReconciledAmount,
            p.RemainingClearingAmount,
            p.Status,
            p.SimulatedInstructionCount,
            p.LatestInstructionId,
            p.LatestInstructionCode,
            p.ReleaseReference,
            p.ReleaseJournalId,
            p.ReleaseJournalId is { } rjid ? jrnNums.GetValueOrDefault(rjid) : null,
            p.RejectionReversalJournalId,
            p.RejectionReversalJournalId is { } revj ? jrnNums.GetValueOrDefault(revj) : null,
            p.RejectionReason,
            p.Notes,
            emails.GetValueOrDefault(p.PreparedByUserId),
            p.SubmittedByUserId is { } sid ? emails.GetValueOrDefault(sid) : null,
            p.ReleasedByUserId is { } relid ? emails.GetValueOrDefault(relid) : null,
            p.CreatedAtUtc,
            p.UpdatedAtUtc);

    private static async Task<IResult> ExecuteReadScopeAsync(
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

        return result.IsAllowed ? result.Value! : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static async Task<IResult> ExecuteMutatingScopeAsync(
        Guid tenantId,
        Guid entityId,
        string[] allowedPermissions,
        string deniedDetail,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, Guid, CancellationToken, Task<(IResult Result, bool NotifyOutbox)>> action,
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
                        throw new AbortedPaymentEndpointException(StatusCodes.Status403Forbidden, "insufficient_entity_permission", deniedDetail);
                    }

                    return await action(scope, actorUserId, innerCt);
                },
                ct);

            if (!result.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            if (result.Value.NotifyOutbox)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return result.Value.Result;
        }
        catch (AbortedPaymentEndpointException abEx)
        {
            return abEx.StatusCode switch
            {
                StatusCodes.Status403Forbidden => EndpointSupport.Forbidden(httpContext, abEx.ErrorCode, abEx.ErrorDetail),
                StatusCodes.Status404NotFound => EndpointSupport.NotFound(httpContext, abEx.ErrorCode, abEx.ErrorDetail),
                StatusCodes.Status409Conflict => EndpointSupport.Conflict(httpContext, abEx.ErrorCode, abEx.ErrorDetail),
                _ => EndpointSupport.BadRequest(httpContext, abEx.ErrorCode, abEx.ErrorDetail)
            };
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation))
        {
            if (ex is DbUpdateConcurrencyException)
            {
                return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Concurrent payment or bank reconciliation update detected; reload and retry.");
            }

            var pgEx = (PostgresException)((DbUpdateException)ex).InnerException!;
            return pgEx.ConstraintName switch
            {
                "ux_bank_statements_reference" => EndpointSupport.Conflict(httpContext, "duplicate_bank_statement_reference", "This bank statement reference was already imported for this bank account."),
                "ux_bank_statement_lines_fingerprint" => EndpointSupport.Conflict(httpContext, "duplicate_bank_statement_line", "This bank statement transaction was already imported."),
                _ => EndpointSupport.Conflict(httpContext, "cash_persistence_conflict", "Payment or bank reconciliation conflict occurred.")
            };
        }
    }

    private sealed class AbortedPaymentEndpointException(int statusCode, string errorCode, string errorDetail) : Exception(errorDetail)
    {
        public int StatusCode { get; } = statusCode;
        public string ErrorCode { get; } = errorCode;
        public string ErrorDetail { get; } = errorDetail;
    }
}
