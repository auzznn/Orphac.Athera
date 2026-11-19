namespace Orphac.Athera.Api.Contracts;

public sealed record BusinessDocumentItemDto(
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

public sealed record OperationalExceptionDto(
    string ExceptionId,
    string Category,
    string Severity,
    string ReferenceNumber,
    string Title,
    string Detail,
    DateTimeOffset OccurredAtUtc);

public sealed record OutboxDiagnosticDto(
    Guid MessageId,
    string Topic,
    string SourceKey,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public sealed record DocumentAuditEventDto(
    Guid EventId,
    string EventType,
    string SubjectType,
    string SubjectId,
    string Summary,
    DateTimeOffset OccurredAtUtc);

public sealed record DocumentsOverviewResponse(
    bool CanUpload,
    bool CanRetryOutbox,
    int TotalDocuments,
    int TotalExceptions,
    int PendingOutboxCount,
    int FailedOutboxCount,
    int ProcessedOutboxCount,
    IReadOnlyList<BusinessDocumentItemDto> Documents,
    IReadOnlyList<OperationalExceptionDto> Exceptions,
    IReadOnlyList<OutboxDiagnosticDto> OutboxMessages,
    IReadOnlyList<DocumentAuditEventDto> AuditEvents);

public sealed record UploadBusinessDocumentRequest(
    string FileName,
    string ContentType,
    string ContentBase64,
    string Category,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceDocumentNumber,
    string? Notes);

public sealed record DocumentDownloadTokenResponse(
    Guid DocumentId,
    string Token,
    DateTimeOffset ExpiresAtUtc,
    string DownloadUrl);

public sealed record OutboxRetryResponse(
    Guid MessageId,
    string Status,
    DateTimeOffset RetriedAtUtc);
