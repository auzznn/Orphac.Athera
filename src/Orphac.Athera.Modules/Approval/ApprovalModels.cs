using Orphac.Athera.Modules.Identity;

namespace Orphac.Athera.Modules.Approval;

public static class ApprovalCaseStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Pending, Approved, Rejected, Cancelled];
}

public static class ApprovalDecisions
{
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    public static readonly IReadOnlyList<string> All = [Approved, Rejected];
}

public static class ApprovalActionTypes
{
    public const string EntityAccessGrantRequest = "ENTITY_ACCESS_GRANT_REQUEST";
}

/// <summary>
/// Legal-entity approval rule governing four-eyes enforcement and approver MFA requirements.
/// </summary>
public sealed class ApprovalPolicy
{
    public Guid PolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string PolicyCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool RequiresFourEyes { get; private set; } = true;
    public bool RequiresApproverMfa { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ApprovalPolicy()
    {
    }

    public static ApprovalPolicy Create(
        Guid policyId,
        Guid tenantId,
        Guid legalEntityId,
        string policyCode,
        string name,
        bool requiresFourEyes,
        bool requiresApproverMfa,
        DateTimeOffset nowUtc)
    {
        if (policyId == Guid.Empty) throw new ArgumentException("PolicyId must not be empty.", nameof(policyId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));

        var code = policyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(code) || code.Length > 64) throw new ArgumentException("PolicyCode invalid.", nameof(policyCode));

        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > 160) throw new ArgumentException("Name invalid.", nameof(name));

        return new ApprovalPolicy
        {
            PolicyId = policyId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PolicyCode = code,
            Name = trimmedName,
            RequiresFourEyes = requiresFourEyes,
            RequiresApproverMfa = requiresApproverMfa,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

/// <summary>
/// Request awaiting a distinct authorized approver decision within one legal entity.
/// </summary>
public sealed class ApprovalCase
{
    public Guid CaseId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PolicyId { get; private set; }
    public string ActionType { get; private set; } = ApprovalActionTypes.EntityAccessGrantRequest;
    public Guid TargetUserId { get; private set; }
    public string TargetPermissionCode { get; private set; } = string.Empty;
    public string RequestReason { get; private set; } = string.Empty;
    public string Status { get; private set; } = ApprovalCaseStatuses.Pending;
    public Guid RequestedByUserId { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ApprovalCase()
    {
    }

    public static ApprovalCase Create(
        Guid caseId,
        Guid tenantId,
        Guid legalEntityId,
        Guid policyId,
        string actionType,
        Guid targetUserId,
        string targetPermissionCode,
        string requestReason,
        Guid requestedByUserId,
        DateTimeOffset nowUtc)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId must not be empty.", nameof(caseId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        if (policyId == Guid.Empty) throw new ArgumentException("PolicyId must not be empty.", nameof(policyId));
        if (targetUserId == Guid.Empty) throw new ArgumentException("TargetUserId must not be empty.", nameof(targetUserId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("RequestedByUserId must not be empty.", nameof(requestedByUserId));

        var trimmedReason = requestReason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > 500)
        {
            throw new ArgumentException("RequestReason must be between 1 and 500 characters.", nameof(requestReason));
        }

        return new ApprovalCase
        {
            CaseId = caseId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PolicyId = policyId,
            ActionType = actionType.Trim().ToUpperInvariant(),
            TargetUserId = targetUserId,
            TargetPermissionCode = PermissionCodes.NormalizeKnown(targetPermissionCode),
            RequestReason = trimmedReason,
            Status = ApprovalCaseStatuses.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void RecordDecision(Guid decidedByUserId, string decision, string reason, DateTimeOffset decidedAtUtc)
    {
        if (decidedByUserId == Guid.Empty) throw new ArgumentException("DecidedByUserId must not be empty.", nameof(decidedByUserId));
        if (Status != ApprovalCaseStatuses.Pending) throw new InvalidOperationException($"Case '{CaseId}' is already decided ({Status}).");
        if (decidedByUserId == RequestedByUserId) throw new InvalidOperationException("Four-eyes policy forbids approving or deciding your own request.");

        var normalizedDecision = decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedDecision != ApprovalDecisions.Approved && normalizedDecision != ApprovalDecisions.Rejected)
        {
            throw new ArgumentException("Decision must be APPROVED or REJECTED.", nameof(decision));
        }

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > 500)
        {
            throw new ArgumentException("Decision reason must be between 1 and 500 characters.", nameof(reason));
        }

        Status = normalizedDecision == ApprovalDecisions.Approved
            ? ApprovalCaseStatuses.Approved
            : ApprovalCaseStatuses.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
        Version++;
    }
}

/// <summary>
/// Append-only immutable four-eyes approval decision evidence row.
/// </summary>
public sealed class ApprovalDecision
{
    public Guid DecisionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid CaseId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid DecidedByUserId { get; private set; }
    public string Decision { get; private set; } = ApprovalDecisions.Approved;
    public string Reason { get; private set; } = string.Empty;
    public bool ApproverMfaVerified { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }

    private ApprovalDecision()
    {
    }

    public static ApprovalDecision Create(
        Guid decisionId,
        Guid tenantId,
        Guid legalEntityId,
        Guid caseId,
        Guid requestedByUserId,
        Guid decidedByUserId,
        string decision,
        string reason,
        bool approverMfaVerified,
        DateTimeOffset decidedAtUtc)
    {
        if (decisionId == Guid.Empty) throw new ArgumentException("DecisionId must not be empty.", nameof(decisionId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId must not be empty.", nameof(caseId));
        if (requestedByUserId == Guid.Empty || decidedByUserId == Guid.Empty) throw new ArgumentException("Actor identifiers must not be empty.");
        if (requestedByUserId == decidedByUserId) throw new ArgumentException("Four-eyes rule prohibits self-approval.", nameof(decidedByUserId));

        var normalized = decision?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized != ApprovalDecisions.Approved && normalized != ApprovalDecisions.Rejected)
        {
            throw new ArgumentException("Decision must be APPROVED or REJECTED.", nameof(decision));
        }

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > 500)
        {
            throw new ArgumentException("Reason must be between 1 and 500 characters.", nameof(reason));
        }

        return new ApprovalDecision
        {
            DecisionId = decisionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            CaseId = caseId,
            RequestedByUserId = requestedByUserId,
            DecidedByUserId = decidedByUserId,
            Decision = normalized,
            Reason = trimmedReason,
            ApproverMfaVerified = approverMfaVerified,
            DecidedAtUtc = decidedAtUtc
        };
    }
}
