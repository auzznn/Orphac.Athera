namespace Orphac.Athera.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    string Service,
    DateTimeOffset TimestampUtc);

public sealed record CsrfTokenResponse(
    string HeaderName,
    string RequestToken);

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string PreferredLocale,
    DateTimeOffset? LastSignedInAtUtc,
    string WorkspaceAccessStatus,
    int AuthorizedWorkspaceCount);

public sealed record AuthSessionResponse(
    CurrentUserResponse User,
    string CsrfToken);

public sealed record LogoutResponse(
    bool SignedOut,
    string CsrfToken);
