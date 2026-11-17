namespace Orphac.Athera.Modules.Platform;

/// <summary>
/// Canonical lifecycle statuses for tenants and legal entities.
/// </summary>
public static class TenantStatuses
{
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Archived = "ARCHIVED";

    public static readonly IReadOnlyList<string> All = [Active, Suspended, Archived];
}

/// <summary>
/// Alias for legal-entity lifecycle statuses sharing tenant status semantics.
/// </summary>
public static class LegalEntityStatuses
{
    public const string Active = TenantStatuses.Active;
    public const string Suspended = TenantStatuses.Suspended;
    public const string Archived = TenantStatuses.Archived;

    public static readonly IReadOnlyList<string> All = TenantStatuses.All;
}

internal static class GuardValidation
{
    public static string RequireCode(string? raw, string paramName, int maxLength = 64)
    {
        var trimmed = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be between 1 and {maxLength} characters.", paramName);
        }

        return trimmed;
    }

    public static string RequireName(string? raw, string paramName, int maxLength = 200)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be between 1 and {maxLength} characters.", paramName);
        }

        return trimmed;
    }

    public static string RequireExactIsoCode(string? raw, string defaultValue, int expectedLength, string paramName)
    {
        var value = string.IsNullOrWhiteSpace(raw)
            ? defaultValue
            : raw.Trim().ToUpperInvariant();

        if (value.Length != expectedLength || !value.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"{paramName} must be a {expectedLength}-letter ISO code.", paramName);
        }

        return value;
    }
}

/// <summary>
/// Top-level customer isolation boundary owning legal entities and memberships.
/// </summary>
public sealed class Tenant
{
    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = TenantStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public bool IsActive => Status == TenantStatuses.Active;

    private Tenant()
    {
    }

    public static Tenant Create(Guid tenantId, string code, string name, DateTimeOffset nowUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        return new Tenant
        {
            TenantId = tenantId,
            Code = GuardValidation.RequireCode(code, nameof(code)),
            Name = GuardValidation.RequireName(name, nameof(name)),
            Status = TenantStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void Activate(DateTimeOffset atUtc) => Transition(TenantStatuses.Active, atUtc);
    public void Suspend(DateTimeOffset atUtc) => Transition(TenantStatuses.Suspended, atUtc);
    public void Archive(DateTimeOffset atUtc) => Transition(TenantStatuses.Archived, atUtc);

    private void Transition(string nextStatus, DateTimeOffset atUtc)
    {
        if (Status == nextStatus)
        {
            return;
        }

        Status = nextStatus;
        UpdatedAtUtc = atUtc;
        Version++;
    }
}
