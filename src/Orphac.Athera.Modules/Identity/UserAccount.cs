namespace Orphac.Athera.Modules.Identity;

/// <summary>
/// Platform-owned account profile and active lifecycle flag linked 1:1 with ASP.NET Core Identity.
/// </summary>
public sealed class UserAccount
{
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string PreferredLocale { get; private set; } = SupportedLocales.Indonesian;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastSignedInAtUtc { get; private set; }
    public long Version { get; private set; }

    private UserAccount()
    {
    }

    public static UserAccount Create(
        Guid userId,
        string displayName,
        string? preferredLocale,
        DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        var trimmedName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > 160)
        {
            throw new ArgumentException("DisplayName must be between 1 and 160 characters.", nameof(displayName));
        }

        return new UserAccount
        {
            UserId = userId,
            DisplayName = trimmedName,
            PreferredLocale = SupportedLocales.NormalizeOrDefault(preferredLocale),
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void RecordSignIn(DateTimeOffset signedInAtUtc)
    {
        LastSignedInAtUtc = signedInAtUtc;
        UpdatedAtUtc = signedInAtUtc;
        Version++;
    }

    public void Deactivate(DateTimeOffset deactivatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = deactivatedAtUtc;
        Version++;
    }

    public void Reactivate(DateTimeOffset reactivatedAtUtc)
    {
        IsActive = true;
        UpdatedAtUtc = reactivatedAtUtc;
        Version++;
    }
}
