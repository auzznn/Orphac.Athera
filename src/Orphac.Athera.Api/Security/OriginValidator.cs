namespace Orphac.Athera.Api.Security;

public static class OriginValidator
{
    public static bool IsAllowed(string? originHeader, string? requestHost, IEnumerable<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(originHeader))
        {
            return true;
        }

        if (!Uri.TryCreate(originHeader.Trim(), UriKind.Absolute, out var originUri))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requestHost) &&
            string.Equals(originUri.Authority, requestHost.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var candidate in allowedOrigins)
        {
            if (Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var allowedUri) &&
                string.Equals(originUri.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(originUri.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                (allowedUri.IsDefaultPort || originUri.Port == allowedUri.Port))
            {
                return true;
            }
        }

        return false;
    }
}
