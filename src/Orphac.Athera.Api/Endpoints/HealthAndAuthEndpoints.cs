using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Api.Endpoints;

public static class HealthAndAuthEndpoints
{
    public static IEndpointRouteBuilder MapHealthAndAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (AtheraDbContext db, HttpContext httpContext, CancellationToken ct) =>
        {
            SetNoStoreHeaders(httpContext);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return Results.Ok(new HealthResponse("ok", "Orphac.Athera.Api", DateTimeOffset.UtcNow));
            }
            catch (Exception)
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "database_unavailable",
                    "Database unavailable",
                    "PostgreSQL health check failed.");
            }
        })
        .WithName("GetHealth")
        .Produces<HealthResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            SetNoStoreHeaders(httpContext);
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new CsrfTokenResponse(
                tokens.HeaderName ?? "X-CSRF-Token",
                tokens.RequestToken ?? string.Empty));
        })
        .WithName("GetCsrfToken")
        .Produces<CsrfTokenResponse>(StatusCodes.Status200OK);

        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AtheraDbContext db,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            SetNoStoreHeaders(httpContext);

            var normalizedEmail = request.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrEmpty(request.Password))
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status400BadRequest,
                    "validation_error",
                    "Missing credentials",
                    "Email and password are required.");
            }

            var user = await userManager.FindByEmailAsync(normalizedEmail);
            if (user is null)
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "invalid_credentials",
                    "Invalid credentials",
                    "The email or password is incorrect.");
            }

            var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status423Locked,
                    "account_locked",
                    "Account locked",
                    "Too many failed sign-in attempts. Wait before trying again.");
            }

            if (!check.Succeeded)
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "invalid_credentials",
                    "Invalid credentials",
                    "The email or password is incorrect.");
            }

            var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
            if (account is not { IsActive: true })
            {
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status403Forbidden,
                    "account_inactive",
                    "Account inactive",
                    "Your account is inactive. Contact an organization administrator.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            account.RecordSignIn(nowUtc);
            await db.SaveChangesAsync(ct);

            await signInManager.SignInAsync(user, isPersistent: request.RememberMe);
            httpContext.User = await signInManager.CreateUserPrincipalAsync(user);

            var rotatedTokens = antiforgery.GetAndStoreTokens(httpContext);
            var currentUser = new CurrentUserResponse(
                user.Id,
                user.Email ?? normalizedEmail,
                account.DisplayName,
                account.PreferredLocale,
                account.LastSignedInAtUtc,
                "S00_DEFAULT_DENY",
                0);

            return Results.Ok(new AuthSessionResponse(
                currentUser,
                rotatedTokens.RequestToken ?? string.Empty));
        })
        .WithName("Login")
        .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status423Locked);

        app.MapPost("/api/auth/logout", async (
            SignInManager<ApplicationUser> signInManager,
            IAntiforgery antiforgery,
            HttpContext httpContext) =>
        {
            SetNoStoreHeaders(httpContext);
            await signInManager.SignOutAsync();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new LogoutResponse(true, tokens.RequestToken ?? string.Empty));
        })
        .WithName("Logout")
        .Produces<LogoutResponse>(StatusCodes.Status200OK);

        app.MapGet("/api/auth/me", async (
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            SetNoStoreHeaders(httpContext);

            var userIdRaw = userManager.GetUserId(httpContext.User);
            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                await signInManager.SignOutAsync();
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "unauthenticated",
                    "Authentication required",
                    "Sign in to access Orphac Athera.");
            }

            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || await userManager.IsLockedOutAsync(user))
            {
                await signInManager.SignOutAsync();
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "unauthenticated",
                    "Authentication required",
                    "Your session is no longer valid.");
            }

            var account = await db.UserAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == userId, ct);

            if (account is not { IsActive: true })
            {
                await signInManager.SignOutAsync();
                return ProblemResults.Create(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "unauthenticated",
                    "Account inactive",
                    "Your account is inactive or missing.");
            }

            return Results.Ok(new CurrentUserResponse(
                user.Id,
                user.Email ?? user.UserName ?? string.Empty,
                account.DisplayName,
                account.PreferredLocale,
                account.LastSignedInAtUtc,
                "S00_DEFAULT_DENY",
                0));
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return app;
    }

    internal static void SetNoStoreHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, private";
        context.Response.Headers.Pragma = "no-cache";
    }
}
