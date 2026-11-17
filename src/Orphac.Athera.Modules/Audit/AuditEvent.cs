namespace Orphac.Athera.Modules.Audit;

/// <summary>
/// Append-only security, administration, and governance audit event row.
/// Runtime role is granted SELECT and INSERT only (UPDATE/DELETE denied by PostgreSQL).
/// </summary>
public sealed class AuditEvent
{
    public Guid EventId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string SubjectType { get; private set; } = string.Empty;
    public string SubjectId { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AuditEvent()
    {
    }

    public static AuditEvent Append(
        Guid eventId,
        Guid tenantId,
        Guid legalEntityId,
        Guid actorUserId,
        string eventType,
        string subjectType,
        string subjectId,
        string summary,
        string payloadJson,
        DateTimeOffset occurredAtUtc)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId must not be empty.", nameof(eventId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId must not be empty.", nameof(actorUserId));

        return new AuditEvent
        {
            EventId = eventId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ActorUserId = actorUserId,
            EventType = eventType.Trim().ToUpperInvariant(),
            SubjectType = subjectType.Trim(),
            SubjectId = subjectId.Trim(),
            Summary = summary.Trim().Length > 300 ? summary.Trim()[..300] : summary.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
