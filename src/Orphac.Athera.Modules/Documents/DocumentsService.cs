using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Documents;

public sealed record DocumentItemSummary(
    Guid DocumentId,
    string DocumentNumber,
    string FileName,
    string ContentType,
    long ByteSize,
    string Sha256Hash,
    string Category,
    string ScanStatus,
    string? Notes,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceDocumentNumber,
    DateTimeOffset CreatedAtUtc);

public sealed record OperationalExceptionSummary(
    string ExceptionId,
    string Category,
    string Severity,
    string ReferenceNumber,
    string Title,
    string Detail,
    DateTimeOffset OccurredAtUtc);

public sealed record OutboxDiagnosticItem(
    Guid MessageId,
    string Topic,
    string SourceKey,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public sealed record AuditTrailEntrySummary(
    Guid EventId,
    string EventType,
    string SubjectType,
    string SubjectId,
    string Summary,
    DateTimeOffset OccurredAtUtc);

public sealed record DocumentsOverviewSnapshot(
    int TotalDocuments,
    int TotalExceptions,
    int PendingOutboxCount,
    int FailedOutboxCount,
    int PublishedOutboxCount,
    IReadOnlyList<DocumentItemSummary> Documents,
    IReadOnlyList<OperationalExceptionSummary> Exceptions,
    IReadOnlyList<OutboxDiagnosticItem> OutboxMessages,
    IReadOnlyList<AuditTrailEntrySummary> AuditEvents);

public sealed record DocumentUploadOutcome(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? Title,
    string? Detail,
    DocumentItemSummary? Document)
{
    public static DocumentUploadOutcome Ok(DocumentItemSummary doc) =>
        new(true, 201, null, null, null, doc);

    public static DocumentUploadOutcome Fail(int statusCode, string errorCode, string title, string detail) =>
        new(false, statusCode, errorCode, title, detail, null);
}

public static class DocumentsService
{
    public static async Task<DocumentsOverviewSnapshot> BuildOverviewAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        string? searchQuery,
        string? categoryFilter,
        string? sourceTypeFilter,
        CancellationToken ct)
    {
        var q = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim().ToLowerInvariant();
        var cat = string.IsNullOrWhiteSpace(categoryFilter) ? null : categoryFilter.Trim().ToUpperInvariant();
        var src = string.IsNullOrWhiteSpace(sourceTypeFilter) ? null : sourceTypeFilter.Trim().ToUpperInvariant();

        var docRows = await (
            from d in db.BusinessDocuments.AsNoTracking()
            join l in db.DocumentLinks.AsNoTracking()
                on new { d.TenantId, d.LegalEntityId, d.DocumentId }
                equals new { l.TenantId, l.LegalEntityId, l.DocumentId }
            where d.TenantId == tenantId && d.LegalEntityId == legalEntityId
            orderby d.CreatedAtUtc descending
            select new DocumentItemSummary(
                d.DocumentId,
                d.DocumentNumber,
                d.FileName,
                d.ContentType,
                d.ByteSize,
                d.Sha256Hash,
                d.Category,
                d.ScanStatus,
                d.Notes,
                l.SourceModule,
                l.SourceDocumentType,
                l.SourceDocumentId,
                l.SourceDocumentNumber,
                d.CreatedAtUtc))
            .Take(200)
            .ToListAsync(ct);

        var filteredDocs = docRows
            .Where(d => cat is null || string.Equals(d.Category, cat, StringComparison.OrdinalIgnoreCase))
            .Where(d => src is null || string.Equals(d.SourceDocumentType, src, StringComparison.OrdinalIgnoreCase))
            .Where(d => q is null ||
                        d.DocumentNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        d.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        d.SourceDocumentNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        (d.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        // Live Operational Exceptions (Payment-blocked AP items + Pending Approvals + Failed Outbox)
        var exceptions = new List<OperationalExceptionSummary>();

        var blockedPayables = await db.PayableItems.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == legalEntityId && p.PaymentBlockStatus == PaymentBlockStatuses.PaymentBlocked)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(25)
            .Select(p => new
            {
                p.PayableItemId,
                p.PayableNumber,
                p.RemainingOpenAmount,
                p.PaymentBlockReason,
                p.CreatedAtUtc
            })
            .ToListAsync(ct);

        foreach (var bp in blockedPayables)
        {
            exceptions.Add(new OperationalExceptionSummary(
                $"ap-block-{bp.PayableItemId:N}",
                "AP_PAYMENT_BLOCKED",
                "HIGH",
                bp.PayableNumber,
                $"AP Payment Blocked ({bp.PayableNumber})",
                string.IsNullOrWhiteSpace(bp.PaymentBlockReason)
                    ? $"Open AP balance {bp.RemainingOpenAmount:N2} IDR is held pending review."
                    : bp.PaymentBlockReason,
                bp.CreatedAtUtc));
        }

        var pendingApprovals = await db.ApprovalCases.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == legalEntityId && a.Status == ApprovalCaseStatuses.Pending)
            .OrderByDescending(a => a.RequestedAtUtc)
            .Take(25)
            .Select(a => new
            {
                a.CaseId,
                a.TargetPermissionCode,
                a.RequestReason,
                a.RequestedAtUtc
            })
            .ToListAsync(ct);

        foreach (var ap in pendingApprovals)
        {
            exceptions.Add(new OperationalExceptionSummary(
                $"appr-{ap.CaseId:N}",
                "APPROVAL_PENDING",
                "MEDIUM",
                ap.TargetPermissionCode,
                $"Pending Four-Eyes Approval ({ap.TargetPermissionCode})",
                ap.RequestReason,
                ap.RequestedAtUtc));
        }

        // Outbox diagnostics
        var outboxStatusCounts = await db.OutboxMessages.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.LegalEntityId == legalEntityId)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, StringComparer.Ordinal, ct);

        var recentOutbox = await db.OutboxMessages.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.LegalEntityId == legalEntityId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(40)
            .Select(o => new OutboxDiagnosticItem(
                o.MessageId,
                o.Topic,
                o.SourceKey,
                o.Status,
                o.AttemptCount,
                o.LastError,
                o.CreatedAtUtc,
                o.ProcessedAtUtc))
            .ToListAsync(ct);

        foreach (var failed in recentOutbox.Where(o => o.Status == OutboxStatuses.Failed))
        {
            exceptions.Add(new OperationalExceptionSummary(
                $"outbox-{failed.MessageId:N}",
                "OUTBOX_FAILED",
                "HIGH",
                failed.Topic,
                $"Integration Outbox Failed ({failed.Topic})",
                failed.LastError ?? "Delivery failed; eligible for operator retry.",
                failed.CreatedAtUtc));
        }

        var auditQuery = db.AuditEvents.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == legalEntityId);

        var auditRows = await auditQuery
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(80)
            .Select(a => new AuditTrailEntrySummary(
                a.EventId,
                a.EventType,
                a.SubjectType,
                a.SubjectId,
                a.Summary,
                a.OccurredAtUtc))
            .ToListAsync(ct);

        if (q is not null)
        {
            auditRows = auditRows
                .Where(a =>
                    a.EventType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.SubjectType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.Summary.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new DocumentsOverviewSnapshot(
            filteredDocs.Count,
            exceptions.Count,
            outboxStatusCounts.GetValueOrDefault(OutboxStatuses.Pending, 0),
            outboxStatusCounts.GetValueOrDefault(OutboxStatuses.Failed, 0),
            outboxStatusCounts.GetValueOrDefault(OutboxStatuses.Processed, 0),
            filteredDocs,
            exceptions.OrderByDescending(e => e.OccurredAtUtc).ToList(),
            recentOutbox,
            auditRows);
    }

    public static async Task<DocumentUploadOutcome> UploadAndLinkAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        string fileName,
        string contentType,
        byte[] contentBytes,
        string category,
        string sourceModule,
        string sourceDocumentType,
        string sourceDocumentId,
        string sourceDocumentNumber,
        string? notes,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var (ok, errCode, errMsg) = DocumentSecurity.ValidateFilePayload(fileName, contentType, contentBytes);
        if (!ok)
        {
            var code = errCode == "file_too_large" ? 413 : 422;
            return DocumentUploadOutcome.Fail(code, errCode ?? "invalid_file", "Document validation failed", errMsg ?? "Invalid file.");
        }

        if (string.IsNullOrWhiteSpace(sourceModule) ||
            string.IsNullOrWhiteSpace(sourceDocumentType) ||
            string.IsNullOrWhiteSpace(sourceDocumentId) ||
            string.IsNullOrWhiteSpace(sourceDocumentNumber))
        {
            return DocumentUploadOutcome.Fail(
                400,
                "source_link_required",
                "Source business record required",
                "Every uploaded business document must be linked to an authoritative source module, document type, ID, and number.");
        }

        string normalizedCategory;
        try
        {
            normalizedCategory = DocumentCategories.Normalize(category);
        }
        catch (ArgumentException ex)
        {
            return DocumentUploadOutcome.Fail(400, "invalid_document_category", "Invalid document category", ex.Message);
        }

        var fiscalYear = nowUtc.Year;
        var seq = await db.DocumentSequences
            .FromSqlInterpolated($"""
                SELECT * FROM documents.document_sequences
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {legalEntityId}
                  AND fiscal_year = {fiscalYear}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (seq is null)
        {
            seq = DocumentSequence.Create(tenantId, legalEntityId, fiscalYear, nowUtc);
            db.DocumentSequences.Add(seq);
        }

        var docNumber = seq.NextNumber(nowUtc);
        var docId = Guid.NewGuid();
        var doc = BusinessDocument.Create(
            docId, tenantId, legalEntityId, docNumber,
            fileName, contentType, normalizedCategory,
            contentBytes, notes, actorUserId, nowUtc);

        var link = DocumentLink.Create(
            Guid.NewGuid(), tenantId, legalEntityId, docId,
            sourceModule, sourceDocumentType, sourceDocumentId, sourceDocumentNumber,
            actorUserId, nowUtc);

        db.BusinessDocuments.Add(doc);
        db.DocumentLinks.Add(link);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(), tenantId, legalEntityId, actorUserId,
            "DOCUMENT_EVIDENCE_ATTACHED", "BusinessDocument", docId.ToString("D"),
            $"Attached {doc.DocumentNumber} ({doc.FileName}, SHA-256 {doc.Sha256Hash[..12]}) to {link.SourceDocumentType} {link.SourceDocumentNumber}",
            JsonSerializer.Serialize(new
            {
                doc.DocumentNumber,
                doc.FileName,
                doc.Sha256Hash,
                doc.ByteSize,
                link.SourceModule,
                link.SourceDocumentType,
                link.SourceDocumentId,
                link.SourceDocumentNumber
            }),
            nowUtc));

        await db.SaveChangesAsync(ct);

        return DocumentUploadOutcome.Ok(new DocumentItemSummary(
            doc.DocumentId,
            doc.DocumentNumber,
            doc.FileName,
            doc.ContentType,
            doc.ByteSize,
            doc.Sha256Hash,
            doc.Category,
            doc.ScanStatus,
            doc.Notes,
            link.SourceModule,
            link.SourceDocumentType,
            link.SourceDocumentId,
            link.SourceDocumentNumber,
            doc.CreatedAtUtc));
    }

    public static async Task<byte[]> BuildEvidencePackPdfAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        string sourceType,
        string sourceId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var entity = await db.LegalEntities.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.LegalEntityId == legalEntityId)
            .Select(e => new { e.Code, e.Name })
            .SingleOrDefaultAsync(ct);

        var normalizedType = sourceType.Trim().ToUpperInvariant();
        var docs = await (
            from d in db.BusinessDocuments.AsNoTracking()
            join l in db.DocumentLinks.AsNoTracking()
                on new { d.TenantId, d.LegalEntityId, d.DocumentId }
                equals new { l.TenantId, l.LegalEntityId, l.DocumentId }
            where d.TenantId == tenantId &&
                  d.LegalEntityId == legalEntityId &&
                  l.SourceDocumentType == normalizedType &&
                  l.SourceDocumentId == sourceId
            orderby d.CreatedAtUtc
            select new { d.DocumentNumber, d.FileName, d.Category, d.Sha256Hash, d.ByteSize, l.SourceDocumentNumber, d.CreatedAtUtc })
            .ToListAsync(ct);

        var audits = await db.AuditEvents.AsNoTracking()
            .Where(a => a.TenantId == tenantId &&
                        a.LegalEntityId == legalEntityId &&
                        (a.SubjectId == sourceId || a.Summary.Contains(sourceId)))
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(12)
            .Select(a => new { a.OccurredAtUtc, a.EventType, a.Summary })
            .ToListAsync(ct);

        var lines = new List<string>
        {
            $"Entity: {entity?.Code ?? legalEntityId.ToString("D")} - {entity?.Name ?? "Authorized Entity"}",
            $"Source Record: {normalizedType} / {sourceId}",
            $"Generated UTC: {nowUtc:yyyy-MM-dd HH:mm:ss}Z",
            "------------------------------------------------------------------------",
            $"LINKED EVIDENCE DOCUMENTS ({docs.Count}):"
        };

        if (docs.Count == 0)
        {
            lines.Add("  (No attachments linked yet)");
        }
        else
        {
            foreach (var d in docs)
            {
                lines.Add($"  {d.DocumentNumber} | {d.FileName} | {d.Category} | {d.ByteSize} bytes");
                lines.Add($"    Ref: {d.SourceDocumentNumber} | SHA-256: {d.Sha256Hash}");
            }
        }

        lines.Add("------------------------------------------------------------------------");
        lines.Add($"AUDIT TRAIL SNAPSHOT ({audits.Count}):");
        if (audits.Count == 0)
        {
            lines.Add("  (No direct audit rows matched this source ID)");
        }
        else
        {
            foreach (var a in audits)
            {
                lines.Add($"  {a.OccurredAtUtc:yyyy-MM-dd HH:mm}Z | {a.EventType} | {a.Summary}");
            }
        }

        return DocumentSecurity.BuildSimplePdf($"Orphac Athera Evidence Summary - {normalizedType}", lines);
    }

    public static async Task<string> BuildAuditCsvAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken ct)
    {
        var rows = await db.AuditEvents.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == legalEntityId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(500)
            .Select(a => new { a.OccurredAtUtc, a.EventType, a.SubjectType, a.SubjectId, a.Summary })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("OccurredAtUtc,EventType,SubjectType,SubjectId,Summary");
        foreach (var r in rows)
        {
            var safeSummary = r.Summary.Replace("\"", "\"\"", StringComparison.Ordinal);
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{r.OccurredAtUtc:O},{r.EventType},{r.SubjectType},{r.SubjectId},\"{safeSummary}\""));
        }

        return sb.ToString();
    }

    public static async Task<(bool Succeeded, int StatusCode, string? ErrorCode, string? Detail)> RetryFailedOutboxMessageAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid messageId,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var msg = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM integration.outbox_messages
                WHERE tenant_id = {tenantId}
                  AND (legal_entity_id IS NULL OR legal_entity_id = {legalEntityId})
                  AND message_id = {messageId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (msg is null)
        {
            return (false, 404, "outbox_message_not_found", "Outbox message was not found in this entity.");
        }

        if (msg.Status != OutboxStatuses.Failed)
        {
            return (false, 409, "outbox_message_not_failed", $"Only FAILED outbox messages can be retried (current status: '{msg.Status}').");
        }

        msg.RequeueForRetry(nowUtc);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(), tenantId, legalEntityId, actorUserId,
            "OUTBOX_MESSAGE_RETRIED", "OutboxMessage", msg.MessageId.ToString("D"),
            $"Requeued failed outbox message {msg.Topic} ({msg.SourceKey}) for retry",
            JsonSerializer.Serialize(new { msg.MessageId, msg.Topic, msg.SourceKey }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return (true, 200, null, null);
    }
}
