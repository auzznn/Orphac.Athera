namespace Orphac.Athera.Modules.Integration;

public static class OutboxStatuses
{
    public const string Pending = "PENDING";
    public const string Processed = "PROCESSED";
    public const string Failed = "FAILED";

    public static readonly IReadOnlyList<string> All = [Pending, Processed, Failed];
}

/// <summary>
/// Transactionally written outbox event guaranteed atomic with its business write.
/// </summary>
public sealed class OutboxMessage
{
    public Guid MessageId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string SourceKey { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public string Status { get; private set; } = OutboxStatuses.Pending;
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public long Version { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(
        Guid messageId,
        Guid tenantId,
        Guid legalEntityId,
        string topic,
        string sourceKey,
        string payloadJson,
        DateTimeOffset nowUtc)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("MessageId must not be empty.", nameof(messageId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));

        return new OutboxMessage
        {
            MessageId = messageId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Topic = topic.Trim().ToLowerInvariant(),
            SourceKey = sourceKey.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            Status = OutboxStatuses.Pending,
            AttemptCount = 0,
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        Status = OutboxStatuses.Processed;
        ProcessedAtUtc = processedAtUtc;
        LastError = null;
        Version++;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        AttemptCount++;
        Status = OutboxStatuses.Failed;
        var clipped = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error.Trim();
        LastError = clipped.Length > 500 ? clipped[..500] : clipped;
        NextAttemptAtUtc = nowUtc.AddSeconds(Math.Min(300, 15 * AttemptCount));
        Version++;
    }

    public void RequeueForRetry(DateTimeOffset nowUtc)
    {
        Status = OutboxStatuses.Pending;
        NextAttemptAtUtc = nowUtc;
        LastError = null;
        Version++;
    }
}

/// <summary>
/// Consumer deduplication receipt preventing duplicate side effects across outbox retries.
/// </summary>
public sealed class InboxReceipt
{
    public Guid ReceiptId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public Guid MessageId { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private InboxReceipt()
    {
    }

    public static InboxReceipt Record(
        Guid receiptId,
        Guid tenantId,
        string consumerName,
        Guid messageId,
        DateTimeOffset processedAtUtc)
    {
        return new InboxReceipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            ConsumerName = consumerName.Trim().ToLowerInvariant(),
            MessageId = messageId,
            ProcessedAtUtc = processedAtUtc
        };
    }
}

/// <summary>
/// Server idempotency record deduplicating retried state-changing commands per tenant and entity.
/// </summary>
public sealed class CommandIdempotency
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string OperationCode { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ResourceId { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private CommandIdempotency()
    {
    }

    public static CommandIdempotency Create(
        Guid id,
        Guid tenantId,
        Guid legalEntityId,
        string operationCode,
        string idempotencyKey,
        Guid actorUserId,
        string resourceId,
        int statusCode,
        DateTimeOffset nowUtc)
    {
        return new CommandIdempotency
        {
            Id = id,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            OperationCode = operationCode.Trim().ToUpperInvariant(),
            IdempotencyKey = idempotencyKey.Trim(),
            ActorUserId = actorUserId,
            ResourceId = resourceId.Trim(),
            StatusCode = statusCode,
            CreatedAtUtc = nowUtc
        };
    }
}
