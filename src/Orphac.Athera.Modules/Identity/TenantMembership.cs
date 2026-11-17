namespace Orphac.Athera.Modules.Identity;

/// <summary>
/// Canonical lifecycle statuses for tenant memberships and entity access grants.
/// </summary>
public static class MembershipStatuses
{
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Revoked = "REVOKED";

    public static readonly IReadOnlyList<string> All = [Active, Suspended, Revoked];
}

/// <summary>
/// Authoritative link granting a global user identity entry into a tenant.
/// </summary>
public sealed class TenantMembership
{
    public Guid MembershipId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Status { get; private set; } = MembershipStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    public bool IsActive => Status == MembershipStatuses.Active;

    private TenantMembership()
    {
    }

    public static TenantMembership Create(Guid membershipId, Guid tenantId, Guid userId, DateTimeOffset nowUtc)
    {
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("MembershipId must not be empty.", nameof(membershipId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        return new TenantMembership
        {
            MembershipId = membershipId,
            TenantId = tenantId,
            UserId = userId,
            Status = MembershipStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void Suspend(DateTimeOffset atUtc)
    {
        if (Status == MembershipStatuses.Suspended)
        {
            return;
        }

        Status = MembershipStatuses.Suspended;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    public void Revoke(DateTimeOffset atUtc)
    {
        if (Status == MembershipStatuses.Revoked)
        {
            return;
        }

        Status = MembershipStatuses.Revoked;
        RevokedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    public void Reactivate(DateTimeOffset atUtc)
    {
        if (Status == MembershipStatuses.Active)
        {
            return;
        }

        Status = MembershipStatuses.Active;
        RevokedAtUtc = null;
        UpdatedAtUtc = atUtc;
        Version++;
    }
}
