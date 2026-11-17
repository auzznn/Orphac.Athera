namespace Orphac.Athera.Modules.Identity;

/// <summary>
/// Supported UI localization codes for Orphac Athera.
/// </summary>
public static class SupportedLocales
{
    public const string Indonesian = "id-ID";
    public const string English = "en-US";

    public static string NormalizeOrDefault(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Indonesian;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "id" or "id-id" => Indonesian,
            "en" or "en-us" => English,
            _ => throw new ArgumentException($"Unsupported locale '{raw}'.", nameof(raw))
        };
    }
}
