using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces", async (
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);

            if (!TryGetActorUserId(userManager, httpContext, out var userId))
            {
                return Unauthenticated(httpContext);
            }

            var summaries = await scopeExecutor.DiscoverWorkspacesForUserAsync(userId, ct);
            var tenants = summaries
                .Select(s => new AuthorizedTenantDto(
                    s.Tenant.TenantId,
                    s.Tenant.Code,
                    s.Tenant.Name,
                    s.Tenant.Status,
                    s.Entities.Select(e => ToLegalEntityDto(e.Entity, e.Permissions)).ToList()))
                .ToList();

            var totalEntities = tenants.Sum(t => t.Entities.Count);
            return Results.Ok(new WorkspacesResponse(tenants, totalEntities, DateTimeOffset.UtcNow));
        })
        .RequireAuthorization()
        .WithName("ListWorkspaces")
        .Produces<WorkspacesResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities", async (
            Guid tenantId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);

            if (!TryGetActorUserId(userManager, httpContext, out var userId))
            {
                return Unauthenticated(httpContext);
            }

            var result = await scopeExecutor.ExecuteInTenantAsync(
                userId,
                tenantId,
                async (scope, innerCt) =>
                {
                    var nowUtc = scope.EvaluatedAtUtc;
                    var rows = await (
                        from grant in db.EntityAccessGrants.AsNoTracking()
                        join entity in db.LegalEntities.AsNoTracking()
                            on new { grant.TenantId, grant.LegalEntityId } equals new { entity.TenantId, entity.LegalEntityId }
                        where grant.TenantId == scope.Tenant.TenantId
                            && grant.UserId == userId
                            && grant.Status == GrantStatuses.Active
                            && grant.ValidFromUtc <= nowUtc
                            && (grant.ValidUntilUtc == null || grant.ValidUntilUtc > nowUtc)
                            && entity.Status == LegalEntityStatuses.Active
                        select new { Entity = entity, grant.PermissionCode })
                        .ToListAsync(innerCt);

                    var entities = rows
                        .GroupBy(x => x.Entity.LegalEntityId)
                        .Select(g => ToLegalEntityDto(
                            g.First().Entity,
                            g.Select(x => x.PermissionCode)
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(p => p, StringComparer.Ordinal)
                                .ToList()))
                        .OrderBy(e => e.Code, StringComparer.Ordinal)
                        .ToList();

                    return new TenantEntitiesResponse(
                        ToTenantSummary(scope.Tenant),
                        entities,
                        nowUtc);
                },
                ct);

            if (!result.IsAllowed)
            {
                return MapDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithName("ListTenantEntities")
        .Produces<TenantEntitiesResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? requirePermission,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);

            if (!TryGetActorUserId(userManager, httpContext, out var userId))
            {
                return Unauthenticated(httpContext);
            }

            if (requirePermission is not null && requirePermission.Trim().Length > 100)
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status400BadRequest,
                    "invalid_permission_query",
                    "Invalid permission parameter",
                    "Permission filter must not exceed 100 characters.");
            }

            var result = await scopeExecutor.ExecuteInEntityAsync(
                userId,
                tenantId,
                entityId,
                string.IsNullOrWhiteSpace(requirePermission) ? null : requirePermission,
                ct);

            if (!result.IsAllowed)
            {
                return MapDenial(httpContext, result.DenialReason, result.ErrorCode);
            }

            var scope = result.Value!;
            var payload = new EntityWorkspaceDetailResponse(
                ToTenantSummary(scope.Tenant),
                ToLegalEntityDto(scope.Entity, scope.EffectivePermissions),
                scope.EffectivePermissions,
                BuildNavigationCapabilities(scope.EffectivePermissions),
                DateTimeOffset.UtcNow);

            return Results.Ok(payload);
        })
        .RequireAuthorization()
        .WithName("GetEntityWorkspaceDetail")
        .Produces<EntityWorkspaceDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool TryGetActorUserId(
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        out Guid userId)
    {
        var raw = userManager.GetUserId(httpContext.User);
        return Guid.TryParse(raw, out userId) && userId != Guid.Empty;
    }

    private static IResult Unauthenticated(HttpContext httpContext) =>
        ProblemResults.Create(
            httpContext,
            StatusCodes.Status401Unauthorized,
            "unauthenticated",
            "Authentication required",
            "Sign in to access Orphac Athera workspaces.");

    private static IResult MapDenial(HttpContext httpContext, ScopeDenialReason reason, string? errorCode) =>
        reason switch
        {
            ScopeDenialReason.InsufficientPermission => ProblemResults.Create(
                httpContext,
                StatusCodes.Status403Forbidden,
                errorCode ?? "insufficient_entity_permission",
                "Insufficient entity permission",
                "Your active grants for this legal entity do not permit this operation."),
            ScopeDenialReason.EntityNotFound => ProblemResults.Create(
                httpContext,
                StatusCodes.Status404NotFound,
                errorCode ?? "entity_not_found",
                "Entity not found",
                "The requested legal entity does not exist or is outside your active scope."),
            _ => ProblemResults.Create(
                httpContext,
                StatusCodes.Status404NotFound,
                errorCode ?? "workspace_not_found",
                "Workspace not found",
                "The requested tenant workspace does not exist or is outside your active scope.")
        };

    private static TenantSummaryDto ToTenantSummary(Tenant tenant) =>
        new(tenant.TenantId, tenant.Code, tenant.Name, tenant.Status);

    private static LegalEntityDto ToLegalEntityDto(LegalEntity entity, IReadOnlyList<string> permissions) =>
        new(
            entity.LegalEntityId,
            entity.TenantId,
            entity.Code,
            entity.Name,
            entity.FunctionalCurrencyCode,
            entity.CountryCode,
            entity.TimeZoneId,
            entity.TaxRegistrationNpwp,
            entity.Status,
            permissions);

    private static IReadOnlyList<ModuleNavigationItemDto> BuildNavigationCapabilities(IReadOnlyList<string> permissions)
    {
        var set = new HashSet<string>(permissions, StringComparer.Ordinal);
        return
        [
            new("overview", "AVAILABLE", true, PermissionCodes.WorkspaceView),
            new("partners", "AVAILABLE", set.Contains(PermissionCodes.PartnersRead) || set.Contains(PermissionCodes.PartnersManage), PermissionCodes.PartnersRead),
            new("catalog", "AVAILABLE", set.Contains(PermissionCodes.CatalogRead) || set.Contains(PermissionCodes.CatalogManage), PermissionCodes.CatalogRead),
            new("approvals", "AVAILABLE", set.Contains(PermissionCodes.WorkspaceView) || set.Contains(PermissionCodes.ApprovalRequest) || set.Contains(PermissionCodes.ApprovalDecide), PermissionCodes.ApprovalRequest),
            new("procurement", "AVAILABLE", set.Contains(PermissionCodes.ProcurementRead) || set.Contains(PermissionCodes.ProcurementManage) || set.Contains(PermissionCodes.ProcurementApprove), PermissionCodes.ProcurementRead),
            new("receiving", "AVAILABLE", set.Contains(PermissionCodes.ReceivingRead) || set.Contains(PermissionCodes.ReceivingManage) || set.Contains(PermissionCodes.ReceivingAccept), PermissionCodes.ReceivingRead),
            new("returns", "AVAILABLE", set.Contains(PermissionCodes.ProcurementRead) || set.Contains(PermissionCodes.ReceivingRead) || set.Contains(PermissionCodes.InventoryRead) || set.Contains(PermissionCodes.ApRead) || set.Contains(PermissionCodes.ProcurementManage) || set.Contains(PermissionCodes.ReceivingManage) || set.Contains(PermissionCodes.InventoryManage) || set.Contains(PermissionCodes.ApManage), PermissionCodes.ProcurementRead),
            new("inventory", "AVAILABLE", set.Contains(PermissionCodes.InventoryRead) || set.Contains(PermissionCodes.InventoryManage) || set.Contains(PermissionCodes.QualityInspect), PermissionCodes.InventoryRead),
            new("payables", "AVAILABLE", set.Contains(PermissionCodes.ApRead) || set.Contains(PermissionCodes.ApManage) || set.Contains(PermissionCodes.ApApprove) || set.Contains(PermissionCodes.ApPost), PermissionCodes.ApRead),
            new("payments", "AVAILABLE", set.Contains(PermissionCodes.ApRead) || set.Contains(PermissionCodes.ApManage) || set.Contains(PermissionCodes.ApApprove) || set.Contains(PermissionCodes.ApPost) || set.Contains(PermissionCodes.FinanceRead) || set.Contains(PermissionCodes.FinanceConfigManage) || set.Contains(PermissionCodes.FinanceJournalPost), PermissionCodes.ApRead),
            new("sales", "AVAILABLE", set.Contains(PermissionCodes.SalesRead) || set.Contains(PermissionCodes.SalesManage) || set.Contains(PermissionCodes.SalesApprove), PermissionCodes.SalesRead),
            new("finance", "AVAILABLE", set.Contains(PermissionCodes.FinanceRead) || set.Contains(PermissionCodes.FinanceConfigManage) || set.Contains(PermissionCodes.FinanceJournalPost), PermissionCodes.FinanceRead),
            new("admin", "AVAILABLE", set.Contains(PermissionCodes.AdminRead) || set.Contains(PermissionCodes.AdminManage), PermissionCodes.AdminRead)
        ];
    }
}
