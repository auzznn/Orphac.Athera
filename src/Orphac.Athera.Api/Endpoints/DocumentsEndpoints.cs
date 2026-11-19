using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Api.Security;
using Orphac.Athera.Modules.Documents;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

public static class DocumentsEndpoints
{
    private static readonly string[] ReadPermissions =
    [
        PermissionCodes.FinanceRead,
        PermissionCodes.FinanceConfigManage,
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.ProcurementRead,
        PermissionCodes.ProcurementManage,
        PermissionCodes.ApRead,
        PermissionCodes.ApManage,
        PermissionCodes.SalesRead,
        PermissionCodes.SalesManage,
        PermissionCodes.InventoryRead,
        PermissionCodes.InventoryManage,
        PermissionCodes.AdminRead,
        PermissionCodes.AdminManage
    ];

    private static readonly string[] UploadPermissions =
    [
        PermissionCodes.FinanceJournalPost,
        PermissionCodes.FinanceConfigManage,
        PermissionCodes.ProcurementManage,
        PermissionCodes.ApManage,
        PermissionCodes.SalesManage,
        PermissionCodes.InventoryManage
    ];

    private static readonly string[] OutboxRetryPermissions =
    [
        PermissionCodes.FinanceConfigManage
    ];

    private static byte[] ResolveDownloadSigningKey(IConfiguration config)
    {
        var configured = config["Security:DocumentDownloadSigningKey"];
        var source = string.IsNullOrWhiteSpace(configured)
            ? "athera-local-dev-document-download-signing-key-2026"
            : configured;
        return SHA256.HashData(Encoding.UTF8.GetBytes(source));
    }

    public static void MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/documents")
            .RequireAuthorization()
            .WithTags("Documents & Evidence");

        group.MapGet("/overview", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? q,
            [FromQuery] string? category,
            [FromQuery] string? sourceType,
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
                        return (Allowed: false, Payload: (DocumentsOverviewResponse?)null);
                    }

                    var snap = await DocumentsService.BuildOverviewAsync(db, tenantId, entityId, q, category, sourceType, innerCt);
                    var canUpload = scope.EffectivePermissions.Any(p => UploadPermissions.Contains(p, StringComparer.Ordinal));
                    var canRetry = scope.EffectivePermissions.Any(p => OutboxRetryPermissions.Contains(p, StringComparer.Ordinal));

                    var dto = new DocumentsOverviewResponse(
                        canUpload,
                        canRetry,
                        snap.TotalDocuments,
                        snap.TotalExceptions,
                        snap.PendingOutboxCount,
                        snap.FailedOutboxCount,
                        snap.PublishedOutboxCount,
                        snap.Documents.Select(d => new BusinessDocumentItemDto(
                            d.DocumentId, d.DocumentNumber, d.FileName, d.ContentType, d.ByteSize,
                            d.Sha256Hash, d.Category, d.ScanStatus, d.Notes,
                            d.SourceModule, d.SourceDocumentType, d.SourceDocumentId, d.SourceDocumentNumber,
                            d.CreatedAtUtc)).ToList(),
                        snap.Exceptions.Select(e => new OperationalExceptionDto(
                            e.ExceptionId, e.Category, e.Severity, e.ReferenceNumber,
                            e.Title, e.Detail, e.OccurredAtUtc)).ToList(),
                        snap.OutboxMessages.Select(o => new OutboxDiagnosticDto(
                            o.MessageId, o.Topic, o.SourceKey, o.Status,
                            o.AttemptCount, o.LastError, o.CreatedAtUtc, o.ProcessedAtUtc)).ToList(),
                        snap.AuditEvents.Select(a => new DocumentAuditEventDto(
                            a.EventId, a.EventType, a.SubjectType, a.SubjectId,
                            a.Summary, a.OccurredAtUtc)).ToList());

                    return (Allowed: true, Payload: (DocumentsOverviewResponse?)dto);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to view documents in this entity.");
            }

            return Results.Ok(scoped.Value.Payload!);
        })
        .WithName("GetDocumentsOverview")
        .Produces<DocumentsOverviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/upload", async (
            Guid tenantId,
            Guid entityId,
            UploadBusinessDocumentRequest request,
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

            byte[] contentBytes;
            try
            {
                contentBytes = Convert.FromBase64String(request.ContentBase64 ?? string.Empty);
            }
            catch (FormatException)
            {
                return ProblemResults.Create(httpContext, 400, "invalid_base64_content", "Invalid file encoding", "File payload must be valid Base64.");
            }

            var scoped = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission: null,
                async (scope, innerCt) =>
                {
                    if (!scope.EffectivePermissions.Any(p => UploadPermissions.Contains(p, StringComparer.Ordinal)))
                    {
                        return (Allowed: false, Outcome: (DocumentUploadOutcome?)null);
                    }

                    var outcome = await DocumentsService.UploadAndLinkAsync(
                        db,
                        tenantId,
                        entityId,
                        request.FileName,
                        request.ContentType,
                        contentBytes,
                        request.Category,
                        request.SourceModule,
                        request.SourceDocumentType,
                        request.SourceDocumentId,
                        request.SourceDocumentNumber,
                        request.Notes,
                        actorUserId,
                        DateTimeOffset.UtcNow,
                        innerCt);

                    return (Allowed: true, Outcome: (DocumentUploadOutcome?)outcome);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Upload not permitted", "Your active role grants do not permit uploading documents in this entity.");
            }

            var outcome = scoped.Value.Outcome!;
            if (!outcome.Succeeded)
            {
                return ProblemResults.Create(httpContext, outcome.StatusCode, outcome.ErrorCode!, outcome.Title!, outcome.Detail!);
            }

            var d = outcome.Document!;
            return Results.Created(
                $"/api/workspaces/{tenantId}/entities/{entityId}/documents/{d.DocumentId}",
                new BusinessDocumentItemDto(
                    d.DocumentId, d.DocumentNumber, d.FileName, d.ContentType, d.ByteSize,
                    d.Sha256Hash, d.Category, d.ScanStatus, d.Notes,
                    d.SourceModule, d.SourceDocumentType, d.SourceDocumentId, d.SourceDocumentNumber,
                    d.CreatedAtUtc));
        })
        .WithName("UploadBusinessDocument")
        .Produces<BusinessDocumentItemDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{documentId:guid}/download-token", async (
            Guid tenantId,
            Guid entityId,
            Guid documentId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            IConfiguration config,
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
                        return (Allowed: false, Exists: false);
                    }

                    var exists = await db.BusinessDocuments.AsNoTracking()
                        .AnyAsync(d => d.TenantId == tenantId && d.LegalEntityId == entityId && d.DocumentId == documentId, innerCt);
                    return (Allowed: true, Exists: exists);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to download documents in this entity.");
            }

            if (!scoped.Value.Exists)
            {
                return ProblemResults.Create(httpContext, 404, "document_not_found", "Document not found", "The requested document does not exist in this entity.");
            }

            var expires = DateTimeOffset.UtcNow.AddMinutes(5);
            var token = DocumentSecurity.CreateDownloadToken(
                ResolveDownloadSigningKey(config),
                tenantId, entityId, documentId, actorUserId, expires);
            var url = $"/api/workspaces/{tenantId}/entities/{entityId}/documents/{documentId}/download?token={Uri.EscapeDataString(token)}";

            return Results.Ok(new DocumentDownloadTokenResponse(documentId, token, expires, url));
        })
        .WithName("IssueDocumentDownloadToken")
        .Produces<DocumentDownloadTokenResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{documentId:guid}/download", async (
            Guid tenantId,
            Guid entityId,
            Guid documentId,
            [FromQuery] string? token,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            IConfiguration config,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return EndpointSupport.Unauthenticated(httpContext);
            }

            if (!DocumentSecurity.VerifyDownloadToken(
                    ResolveDownloadSigningKey(config),
                    token, tenantId, entityId, documentId, actorUserId, DateTimeOffset.UtcNow))
            {
                return ProblemResults.Create(httpContext, 403, "invalid_or_expired_download_token", "Download token invalid", "Signed download token is missing, expired, or bound to a different document/user/entity.");
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
                        return (Allowed: false, FileName: (string?)null, ContentType: (string?)null, Bytes: (byte[]?)null);
                    }

                    var row = await db.BusinessDocuments.AsNoTracking()
                        .Where(d => d.TenantId == tenantId && d.LegalEntityId == entityId && d.DocumentId == documentId)
                        .Select(d => new { d.FileName, d.ContentType, d.ContentBytes })
                        .SingleOrDefaultAsync(innerCt);

                    return row is null
                        ? (Allowed: true, FileName: (string?)null, ContentType: (string?)null, Bytes: (byte[]?)null)
                        : (Allowed: true, FileName: (string?)row.FileName, ContentType: (string?)row.ContentType, Bytes: (byte[]?)row.ContentBytes);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to download documents in this entity.");
            }

            if (scoped.Value.Bytes is null)
            {
                return ProblemResults.Create(httpContext, 404, "document_not_found", "Document not found", "The requested document does not exist in this entity.");
            }

            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(scoped.Value.Bytes, scoped.Value.ContentType!, scoped.Value.FileName!);
        })
        .WithName("DownloadBusinessDocument")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/evidence-pack/{sourceType}/{sourceId}/summary.pdf", async (
            Guid tenantId,
            Guid entityId,
            string sourceType,
            string sourceId,
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
                        return (Allowed: false, Pdf: (byte[]?)null);
                    }

                    var pdf = await DocumentsService.BuildEvidencePackPdfAsync(
                        db, tenantId, entityId, sourceType, sourceId, DateTimeOffset.UtcNow, innerCt);
                    return (Allowed: true, Pdf: (byte[]?)pdf);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to generate evidence summaries in this entity.");
            }

            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var safeFile = $"evidence-{sourceType.Trim().ToLowerInvariant()}-{sourceId.Trim().Replace("/", "-")}.pdf";
            return Results.File(scoped.Value.Pdf!, "application/pdf", safeFile);
        })
        .WithName("DownloadEvidencePackPdf")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/audit-export.csv", async (
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
                    if (!scope.EffectivePermissions.Any(p => ReadPermissions.Contains(p, StringComparer.Ordinal)))
                    {
                        return (Allowed: false, Csv: (string?)null);
                    }

                    var csv = await DocumentsService.BuildAuditCsvAsync(db, tenantId, entityId, innerCt);
                    return (Allowed: true, Csv: (string?)csv);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to export entity audit trails.");
            }

            return Results.File(
                Encoding.UTF8.GetBytes(scoped.Value.Csv!),
                "text/csv; charset=utf-8",
                $"audit-trail-{entityId:N}.csv");
        })
        .WithName("ExportEntityAuditCsv")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/outbox/{messageId:guid}/retry", async (
            Guid tenantId,
            Guid entityId,
            Guid messageId,
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

            var nowUtc = DateTimeOffset.UtcNow;
            var scoped = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission: null,
                async (scope, innerCt) =>
                {
                    if (!scope.EffectivePermissions.Any(p => OutboxRetryPermissions.Contains(p, StringComparer.Ordinal)))
                    {
                        return (Allowed: false, Result: (Succeeded: false, StatusCode: 403, ErrorCode: (string?)"insufficient_entity_permission", Detail: (string?)"Insufficient permission."));
                    }

                    var res = await DocumentsService.RetryFailedOutboxMessageAsync(
                        db, tenantId, entityId, messageId, actorUserId, nowUtc, innerCt);
                    return (Allowed: true, Result: res);
                },
                ct);

            if (!scoped.IsAllowed)
            {
                return EndpointSupport.MapScopeDenial(httpContext, scoped.DenialReason, scoped.ErrorCode);
            }

            if (!scoped.Value.Allowed)
            {
                return ProblemResults.Create(httpContext, 403, "insufficient_entity_permission", "Access denied", "You do not have permission to retry outbox messages in this entity.");
            }

            var (ok, statusCode, errCode, detail) = scoped.Value.Result;
            if (!ok)
            {
                return ProblemResults.Create(httpContext, statusCode, errCode!, "Outbox retry rejected", detail!);
            }

            return Results.Ok(new OutboxRetryResponse(messageId, "PENDING", nowUtc));
        })
        .WithName("RetryOutboxMessage")
        .Produces<OutboxRetryResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
