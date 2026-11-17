using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Platform;

public enum ScopeDenialReason
{
    None = 0,
    TenantNotFound = 1,
    EntityNotFound = 2,
    InsufficientPermission = 3
}

public sealed record ScopeResult<T>(
    bool IsAllowed,
    ScopeDenialReason DenialReason,
    string? ErrorCode,
    T? Value)
{
    public static ScopeResult<T> Success(T value) => new(true, ScopeDenialReason.None, null, value);

    public static ScopeResult<T> Denied(ScopeDenialReason reason, string errorCode) =>
        new(false, reason, errorCode, default);
}

public sealed record TenantScopeContext(
    Tenant Tenant,
    Guid ActorUserId,
    DateTimeOffset EvaluatedAtUtc);

public sealed record AuthorizedEntityScope(
    Tenant Tenant,
    LegalEntity Entity,
    IReadOnlyList<string> EffectivePermissions,
    DateTimeOffset EvaluatedAtUtc);

public sealed record AuthorizedEntitySummary(
    LegalEntity Entity,
    IReadOnlyList<string> Permissions);

public sealed record AuthorizedTenantSummary(
    Tenant Tenant,
    IReadOnlyList<AuthorizedEntitySummary> Entities);

/// <summary>
/// Server-authoritative runner that resolves actor membership before setting
/// transaction-local PostgreSQL session context (app.tenant_id / app.actor_user_id).
/// </summary>
public sealed class TenantScopeExecutor(AtheraDbContext db, TimeProvider? timeProvider = null)
{
    private readonly AtheraDbContext _db = db;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<AuthorizedTenantSummary>> DiscoverWorkspacesForUserAsync(
        Guid userId,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var accountActive = await _db.UserAccounts
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId && u.IsActive, ct);

        if (!accountActive)
        {
            await tx.RollbackAsync(ct);
            return [];
        }

        await SetLocalScopeAsync(userId, tenantId: null, ct);

        var nowUtc = _clock.GetUtcNow();

        var activeTenants = await (
            from membership in _db.TenantMemberships.AsNoTracking()
            join tenant in _db.Tenants.AsNoTracking()
                on membership.TenantId equals tenant.TenantId
            where membership.UserId == userId
                && membership.Status == MembershipStatuses.Active
                && tenant.Status == TenantStatuses.Active
            orderby tenant.Code
            select tenant)
            .ToListAsync(ct);

        var summaries = new List<AuthorizedTenantSummary>(activeTenants.Count);

        foreach (var tenant in activeTenants)
        {
            await SetLocalScopeAsync(userId, tenant.TenantId, ct);

            var rows = await (
                from grant in _db.EntityAccessGrants.AsNoTracking()
                join entity in _db.LegalEntities.AsNoTracking()
                    on new { grant.TenantId, grant.LegalEntityId } equals new { entity.TenantId, entity.LegalEntityId }
                where grant.TenantId == tenant.TenantId
                    && grant.UserId == userId
                    && grant.Status == GrantStatuses.Active
                    && grant.ValidFromUtc <= nowUtc
                    && (grant.ValidUntilUtc == null || grant.ValidUntilUtc > nowUtc)
                    && entity.Status == LegalEntityStatuses.Active
                select new { Entity = entity, grant.PermissionCode })
                .ToListAsync(ct);

            var grouped = rows
                .GroupBy(x => x.Entity.LegalEntityId)
                .Select(g => new AuthorizedEntitySummary(
                    g.First().Entity,
                    g.Select(x => x.PermissionCode)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList()))
                .OrderBy(e => e.Entity.Code, StringComparer.Ordinal)
                .ToList();

            if (grouped.Count > 0)
            {
                summaries.Add(new AuthorizedTenantSummary(tenant, grouped));
            }
        }

        await tx.CommitAsync(ct);
        return summaries;
    }

    public async Task<ScopeResult<T>> ExecuteInTenantAsync<T>(
        Guid userId,
        Guid requestedTenantId,
        Func<TenantScopeContext, CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        if (userId == Guid.Empty || requestedTenantId == Guid.Empty)
        {
            return ScopeResult<T>.Denied(ScopeDenialReason.TenantNotFound, "workspace_not_found");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var accountActive = await _db.UserAccounts
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId && u.IsActive, ct);

        if (!accountActive)
        {
            await tx.RollbackAsync(ct);
            return ScopeResult<T>.Denied(ScopeDenialReason.TenantNotFound, "workspace_not_found");
        }

        await SetLocalScopeAsync(userId, tenantId: null, ct);

        var tenant = await (
            from membership in _db.TenantMemberships.AsNoTracking()
            join t in _db.Tenants.AsNoTracking()
                on membership.TenantId equals t.TenantId
            where membership.TenantId == requestedTenantId
                && membership.UserId == userId
                && membership.Status == MembershipStatuses.Active
                && t.Status == TenantStatuses.Active
            select t)
            .SingleOrDefaultAsync(ct);

        if (tenant is null)
        {
            await tx.RollbackAsync(ct);
            return ScopeResult<T>.Denied(ScopeDenialReason.TenantNotFound, "workspace_not_found");
        }

        await SetLocalScopeAsync(userId, tenant.TenantId, ct);

        var scopeContext = new TenantScopeContext(tenant, userId, _clock.GetUtcNow());
        var value = await action(scopeContext, ct);

        await tx.CommitAsync(ct);
        return ScopeResult<T>.Success(value);
    }

    public Task<ScopeResult<AuthorizedEntityScope>> ExecuteInEntityAsync(
        Guid userId,
        Guid requestedTenantId,
        Guid requestedEntityId,
        string? requiredPermission,
        CancellationToken ct) =>
        ExecuteInEntityAsync(
            userId,
            requestedTenantId,
            requestedEntityId,
            requiredPermission,
            static (scope, _) => Task.FromResult(scope),
            ct);

    public async Task<ScopeResult<T>> ExecuteInEntityAsync<T>(
        Guid userId,
        Guid requestedTenantId,
        Guid requestedEntityId,
        string? requiredPermission,
        Func<AuthorizedEntityScope, CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        if (requestedEntityId == Guid.Empty)
        {
            return ScopeResult<T>.Denied(ScopeDenialReason.EntityNotFound, "entity_not_found");
        }

        var innerResult = await ExecuteInTenantAsync(
            userId,
            requestedTenantId,
            async (tenantScope, innerCt) =>
            {
                var entity = await _db.LegalEntities
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        e => e.TenantId == tenantScope.Tenant.TenantId
                            && e.LegalEntityId == requestedEntityId
                            && e.Status == LegalEntityStatuses.Active,
                        innerCt);

                if (entity is null)
                {
                    return ScopeResult<T>.Denied(ScopeDenialReason.EntityNotFound, "entity_not_found");
                }

                var nowUtc = tenantScope.EvaluatedAtUtc;
                var effectivePermissions = await _db.EntityAccessGrants
                    .AsNoTracking()
                    .Where(g => g.TenantId == tenantScope.Tenant.TenantId
                        && g.LegalEntityId == requestedEntityId
                        && g.UserId == userId
                        && g.Status == GrantStatuses.Active
                        && g.ValidFromUtc <= nowUtc
                        && (g.ValidUntilUtc == null || g.ValidUntilUtc > nowUtc))
                    .Select(g => g.PermissionCode)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToListAsync(innerCt);

                if (effectivePermissions.Count == 0)
                {
                    return ScopeResult<T>.Denied(ScopeDenialReason.EntityNotFound, "entity_not_found");
                }

                if (!string.IsNullOrWhiteSpace(requiredPermission))
                {
                    var normalizedPermission = PermissionCodes.Normalize(requiredPermission);
                    if (!effectivePermissions.Contains(normalizedPermission, StringComparer.Ordinal))
                    {
                        return ScopeResult<T>.Denied(
                            ScopeDenialReason.InsufficientPermission,
                            "insufficient_entity_permission");
                    }
                }

                var entityScope = new AuthorizedEntityScope(
                    tenantScope.Tenant,
                    entity,
                    effectivePermissions,
                    nowUtc);

                var value = await action(entityScope, innerCt);
                return ScopeResult<T>.Success(value);
            },
            ct);

        return innerResult.IsAllowed
            ? innerResult.Value!
            : ScopeResult<T>.Denied(innerResult.DenialReason, innerResult.ErrorCode!);
    }

    private Task SetLocalScopeAsync(Guid userId, Guid? tenantId, CancellationToken ct)
    {
        var userIdStr = userId.ToString("D");
        var tenantIdStr = tenantId?.ToString("D") ?? string.Empty;

        return _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.actor_user_id', {userIdStr}, true), set_config('app.tenant_id', {tenantIdStr}, true)",
            ct);
    }
}
