namespace Orphac.Athera.Modules.Identity;

public static class GrantStatuses
{
    public const string Active = MembershipStatuses.Active;
    public const string Suspended = MembershipStatuses.Suspended;
    public const string Revoked = MembershipStatuses.Revoked;

    public static readonly IReadOnlyList<string> All = MembershipStatuses.All;
}

/// <summary>
/// Stable dot-delimited permission codes used across S01 workspaces and downstream modules.
/// </summary>
public static class PermissionCodes
{
    public const string WorkspaceView = "workspace.view";
    public const string ProcurementRead = "procurement.read";
    public const string InventoryRead = "inventory.read";
    public const string SalesRead = "sales.read";
    public const string FinanceRead = "finance.read";
    public const string FinanceJournalPost = "finance.journal.post";
    public const string AdminRead = "admin.read";
    public const string AdminManage = "admin.manage";
    public const string ApprovalRequest = "approval.request";
    public const string ApprovalDecide = "approval.decide";

    public static readonly IReadOnlyList<string> KnownAll =
    [
        WorkspaceView,
        ProcurementRead,
        InventoryRead,
        SalesRead,
        FinanceRead,
        FinanceJournalPost,
        AdminRead,
        AdminManage,
        ApprovalRequest,
        ApprovalDecide
    ];

    private static readonly HashSet<string> KnownSet = new(KnownAll, StringComparer.Ordinal);

    public static string Normalize(string? permissionCode)
    {
        var normalized = permissionCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 100)
        {
            throw new ArgumentException("PermissionCode must be between 1 and 100 characters.", nameof(permissionCode));
        }

        return normalized;
    }

    public static bool IsKnown(string? permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        return KnownSet.Contains(permissionCode.Trim().ToLowerInvariant());
    }

    public static string NormalizeKnown(string? permissionCode)
    {
        var normalized = Normalize(permissionCode);
        if (!KnownSet.Contains(normalized))
        {
            throw new ArgumentException($"Unknown or unapproved permission code '{normalized}'.", nameof(permissionCode));
        }

        return normalized;
    }
}

/// <summary>
/// Scoped permission grant tying a tenant member to a specific legal entity inside the same tenant.
/// </summary>
public sealed class EntityAccessGrant
{
    public Guid GrantId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionCode { get; private set; } = PermissionCodes.WorkspaceView;
    public string Status { get; private set; } = GrantStatuses.Active;
    public DateTimeOffset ValidFromUtc { get; private set; }
    public DateTimeOffset? ValidUntilUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    private EntityAccessGrant()
    {
    }

    public static EntityAccessGrant Create(
        Guid grantId,
        Guid tenantId,
        Guid legalEntityId,
        Guid userId,
        string permissionCode,
        DateTimeOffset validFromUtc,
        DateTimeOffset? validUntilUtc,
        DateTimeOffset nowUtc)
    {
        if (grantId == Guid.Empty)
        {
            throw new ArgumentException("GrantId must not be empty.", nameof(grantId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        if (legalEntityId == Guid.Empty)
        {
            throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        if (validUntilUtc is not null && validUntilUtc <= validFromUtc)
        {
            throw new ArgumentException("ValidUntilUtc must be strictly after ValidFromUtc.", nameof(validUntilUtc));
        }

        return new EntityAccessGrant
        {
            GrantId = grantId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            UserId = userId,
            PermissionCode = PermissionCodes.Normalize(permissionCode),
            Status = GrantStatuses.Active,
            ValidFromUtc = validFromUtc,
            ValidUntilUtc = validUntilUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public bool IsEffectiveAt(DateTimeOffset nowUtc)
    {
        return Status == GrantStatuses.Active
            && ValidFromUtc <= nowUtc
            && (ValidUntilUtc is null || ValidUntilUtc > nowUtc);
    }

    public void Suspend(DateTimeOffset atUtc)
    {
        if (Status == GrantStatuses.Suspended)
        {
            return;
        }

        Status = GrantStatuses.Suspended;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    public void Revoke(DateTimeOffset atUtc)
    {
        if (Status == GrantStatuses.Revoked)
        {
            return;
        }

        Status = GrantStatuses.Revoked;
        RevokedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
        Version++;
    }
}
