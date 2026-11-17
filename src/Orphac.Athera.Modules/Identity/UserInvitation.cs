using System.Security.Cryptography;
using System.Text;

namespace Orphac.Athera.Modules.Identity;

public static class InvitationStatuses
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Revoked = "REVOKED";
    public const string Expired = "EXPIRED";

    public static readonly IReadOnlyList<string> All = [Pending, Accepted, Revoked, Expired];
}

/// <summary>
/// Single-use, expiry-bound, entity-scoped invitation whose raw secret is hashed with SHA-256.
/// </summary>
public sealed class UserInvitation
{
    public Guid InvitationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string InvitedEmail { get; private set; } = string.Empty;
    public string RoleTemplateCode { get; private set; } = RoleTemplates.ProcurementOfficer;
    public string TokenHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = InvitationStatuses.Pending;
    public Guid InvitedByUserId { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    private UserInvitation()
    {
    }

    public static (UserInvitation Invitation, string RawToken) Create(
        Guid invitationId,
        Guid tenantId,
        Guid legalEntityId,
        string invitedEmail,
        string roleTemplateCode,
        Guid invitedByUserId,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset nowUtc)
    {
        if (invitationId == Guid.Empty) throw new ArgumentException("InvitationId must not be empty.", nameof(invitationId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (legalEntityId == Guid.Empty) throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        if (invitedByUserId == Guid.Empty) throw new ArgumentException("InvitedByUserId must not be empty.", nameof(invitedByUserId));

        var email = invitedEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(email) || email.Length > 256 || !email.Contains('@'))
        {
            throw new ArgumentException("InvitedEmail must be a valid email address.", nameof(invitedEmail));
        }

        if (!RoleTemplates.IsKnown(roleTemplateCode))
        {
            throw new ArgumentException($"Unknown role template '{roleTemplateCode}'.", nameof(roleTemplateCode));
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new ArgumentException("ExpiresAtUtc must be strictly after creation time.", nameof(expiresAtUtc));
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var invitation = new UserInvitation
        {
            InvitationId = invitationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            InvitedEmail = email,
            RoleTemplateCode = roleTemplateCode.Trim().ToUpperInvariant(),
            TokenHash = ComputeTokenHash(rawToken),
            Status = InvitationStatuses.Pending,
            InvitedByUserId = invitedByUserId,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };

        return (invitation, rawToken);
    }

    public static string ComputeTokenHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Invitation token cannot be blank.", nameof(rawToken));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexStringLower(bytes);
    }

    public bool IsExpiredAt(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;

    public void Accept(Guid actorUserId, DateTimeOffset acceptedAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("ActorUserId must not be empty.", nameof(actorUserId));
        }

        if (Status != InvitationStatuses.Pending)
        {
            throw new InvalidOperationException($"Invitation in status '{Status}' cannot be accepted.");
        }

        if (IsExpiredAt(acceptedAtUtc))
        {
            throw new InvalidOperationException("Expired invitation cannot be accepted.");
        }

        Status = InvitationStatuses.Accepted;
        AcceptedByUserId = actorUserId;
        AcceptedAtUtc = acceptedAtUtc;
        UpdatedAtUtc = acceptedAtUtc;
        Version++;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (Status != InvitationStatuses.Pending)
        {
            throw new InvalidOperationException($"Invitation in status '{Status}' cannot be revoked.");
        }

        Status = InvitationStatuses.Revoked;
        RevokedAtUtc = revokedAtUtc;
        UpdatedAtUtc = revokedAtUtc;
        Version++;
    }
}
