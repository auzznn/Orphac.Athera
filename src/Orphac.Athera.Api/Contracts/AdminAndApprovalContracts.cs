namespace Orphac.Athera.Api.Contracts;

public sealed record EntityMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string MembershipStatus,
    bool MfaEnabled,
    IReadOnlyList<string> Permissions);

public sealed record InvitationDto(
    Guid InvitationId,
    string InvitedEmail,
    string RoleTemplateCode,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record ApprovalPolicyDto(
    Guid PolicyId,
    string PolicyCode,
    string Name,
    bool RequiresFourEyes,
    bool RequiresApproverMfa,
    bool IsActive);

public sealed record AuditEventDto(
    Guid EventId,
    Guid ActorUserId,
    string ActorEmail,
    string EventType,
    string SubjectType,
    string SubjectId,
    string Summary,
    DateTimeOffset OccurredAtUtc);

public sealed record OutboxSummaryDto(
    int PendingCount,
    int ProcessedCount,
    int FailedCount);

public sealed record AdminOverviewResponse(
    IReadOnlyList<EntityMemberDto> Members,
    IReadOnlyList<InvitationDto> Invitations,
    IReadOnlyList<ApprovalPolicyDto> Policies,
    IReadOnlyList<AuditEventDto> AuditEvents,
    OutboxSummaryDto OutboxSummary,
    IReadOnlyList<string> AvailablePermissions,
    IReadOnlyList<string> AvailableRoleTemplates);

public sealed record CreateInvitationRequest(
    string Email,
    string RoleTemplateCode,
    int ExpiresInHours = 48);

public sealed record CreateInvitationResponse(
    Guid InvitationId,
    string InvitedEmail,
    string RoleTemplateCode,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string InviteToken);

public sealed record AcceptInvitationRequest(string Token);

public sealed record AcceptInvitationResponse(
    Guid TenantId,
    Guid LegalEntityId,
    string TenantCode,
    string EntityCode,
    IReadOnlyList<string> GrantedPermissions);

public sealed record GrantPermissionRequest(
    Guid TargetUserId,
    string PermissionCode);

public sealed record MemberStatusResponse(
    Guid UserId,
    string MembershipStatus,
    int AffectedGrants);

public sealed record CreateApprovalCaseRequest(
    string PolicyCode,
    string TargetPermissionCode,
    string Reason,
    Guid? TargetUserId = null);

public sealed record ApprovalCaseDto(
    Guid CaseId,
    Guid TenantId,
    Guid LegalEntityId,
    Guid PolicyId,
    string PolicyCode,
    bool RequiresApproverMfa,
    string ActionType,
    Guid TargetUserId,
    string TargetPermissionCode,
    string RequestReason,
    string Status,
    Guid RequestedByUserId,
    string RequestedByName,
    DateTimeOffset RequestedAtUtc,
    Guid? DecidedByUserId,
    string? DecidedByName,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason,
    bool? ApproverMfaVerified);

public sealed record DecideApprovalCaseRequest(
    string Decision,
    string Reason);

public sealed record ApprovalCasesResponse(
    IReadOnlyList<ApprovalCaseDto> Cases,
    IReadOnlyList<ApprovalPolicyDto> Policies);

public sealed record OutboxDispatchResponse(
    int ProcessedCount,
    int FailedCount,
    OutboxSummaryDto OutboxSummary);
