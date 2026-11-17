using Microsoft.AspNetCore.Identity;

namespace Orphac.Athera.Modules.Identity;

/// <summary>
/// Authoritative ASP.NET Core Identity user record (credential store only).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
}

/// <summary>
/// Authoritative ASP.NET Core Identity role record.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
}
