using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Orphac.Athera.Api.Security;

public sealed class SecurityOptions
{
    public string[] AllowedBrowserOrigins { get; init; } = [];
}

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task InvokeAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IOptions<SecurityOptions> securityOptions)
    {
        if (context.Request.Path.StartsWithSegments("/api") &&
            UnsafeMethods.Contains(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (!OriginValidator.IsAllowed(
                    origin,
                    context.Request.Host.Value,
                    securityOptions.Value.AllowedBrowserOrigins))
            {
                await WriteCsrfProblemAsync(context, "Request origin is not permitted.");
                return;
            }

            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await WriteCsrfProblemAsync(context, "Missing or invalid CSRF token.");
                return;
            }
        }

        await next(context);
    }

    private static Task WriteCsrfProblemAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.CacheControl = "no-store, no-cache, private";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "CSRF validation failed",
            Detail = detail,
            Extensions =
            {
                ["errorCode"] = "csrf_validation_failed",
                ["traceId"] = context.TraceIdentifier
            }
        };

        return context.Response.WriteAsJsonAsync(problem);
    }
}
