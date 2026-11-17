using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

internal static class EndpointSupport
{
    public static bool TryGetActorUserId(UserManager<ApplicationUser> userManager, HttpContext httpContext, out Guid userId)
    {
        var raw = userManager.GetUserId(httpContext.User);
        return Guid.TryParse(raw, out userId) && userId != Guid.Empty;
    }

    public static IResult Unauthenticated(HttpContext httpContext) =>
        ProblemResults.Create(
            httpContext,
            StatusCodes.Status401Unauthorized,
            "unauthenticated",
            "Authentication required",
            "Sign in to continue.");

    public static IResult Forbidden(HttpContext httpContext, string errorCode, string detail) =>
        ProblemResults.Create(httpContext, StatusCodes.Status403Forbidden, errorCode, "Forbidden", detail);

    public static IResult BadRequest(HttpContext httpContext, string errorCode, string detail) =>
        ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, errorCode, "Validation failed", detail);

    public static IResult NotFound(HttpContext httpContext, string errorCode, string detail) =>
        ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, errorCode, "Not found", detail);

    public static IResult Conflict(HttpContext httpContext, string errorCode, string detail) =>
        ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, errorCode, "Conflict", detail);

    public static IResult MapScopeDenial(HttpContext httpContext, ScopeDenialReason reason, string? errorCode) =>
        reason switch
        {
            ScopeDenialReason.InsufficientPermission => Forbidden(
                httpContext,
                errorCode ?? "insufficient_entity_permission",
                "Your active grants for this legal entity do not permit this action."),
            ScopeDenialReason.EntityNotFound => NotFound(
                httpContext,
                errorCode ?? "entity_not_found",
                "The requested legal entity does not exist or is outside your active scope."),
            _ => NotFound(
                httpContext,
                errorCode ?? "workspace_not_found",
                "The requested tenant workspace does not exist or is outside your active scope.")
        };

    public static bool HasAnyPermission(IReadOnlyList<string> effectivePermissions, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (effectivePermissions.Contains(candidate, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static void RecordAuditAndOutbox(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid actorUserId,
        string eventType,
        string subjectType,
        string subjectId,
        string summary,
        string outboxTopic,
        string sourceKey,
        object payload,
        DateTimeOffset nowUtc)
    {
        var json = JsonSerializer.Serialize(payload);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            tenantId,
            entityId,
            actorUserId,
            eventType,
            subjectType,
            subjectId,
            summary,
            json,
            nowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            outboxTopic,
            sourceKey,
            json,
            nowUtc));
    }

    public static IResult? MapPersistenceConflict(HttpContext httpContext, Exception ex, string duplicateErrorCode, string duplicateMessage)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return Conflict(httpContext, "concurrency_conflict", "The record was modified concurrently; reload and retry.");
        }

        if (ex is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(httpContext, duplicateErrorCode, duplicateMessage);
        }

        return null;
    }
}
