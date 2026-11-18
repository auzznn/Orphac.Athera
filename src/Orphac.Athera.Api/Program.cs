using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Cli;
using Orphac.Athera.Api.Endpoints;
using Orphac.Athera.Api.Security;
using Orphac.Athera.Api.Workers;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

var isMigrateMode = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var isSeedDemoMode = args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase);
var openApiIndex = Array.FindIndex(args, a => string.Equals(a, "--export-openapi", StringComparison.OrdinalIgnoreCase));
var isOpenApiMode = openApiIndex >= 0;

var builder = WebApplication.CreateBuilder(args);

var connectionKey = (isMigrateMode || isSeedDemoMode)
    ? "ConnectionStrings:Migrator"
    : "ConnectionStrings:Runtime";

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

builder.Services.AddDataProtection()
    .SetApplicationName("Orphac.Athera");

builder.Services.AddDbContext<AtheraDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config[connectionKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        if (!isOpenApiMode)
        {
            throw new InvalidOperationException(
                $"Missing required PostgreSQL connection string '{connectionKey}'. Configure via environment or local secret store.");
        }

        connectionString = "Host=127.0.0.1;Port=55433;Database=openapi_schema_only;Username=athera_app;Password=schema_only";
    }

    options.UseAtheraNpgsql(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AtheraDbContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

var useLoopbackHttpCookies = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "athera.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = useLoopbackHttpCookies
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/problem+json";
            HealthAndAuthEndpoints.SetNoStoreHeaders(ctx.HttpContext);
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication required",
                Detail = "Sign in to access this resource.",
                Extensions =
                {
                    ["errorCode"] = "unauthenticated",
                    ["traceId"] = ctx.HttpContext.TraceIdentifier
                }
            });
        },
        OnRedirectToAccessDenied = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/problem+json";
            HealthAndAuthEndpoints.SetNoStoreHeaders(ctx.HttpContext);
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You are not authorized for this action.",
                Extensions =
                {
                    ["errorCode"] = "forbidden",
                    ["traceId"] = ctx.HttpContext.TraceIdentifier
                }
            });
        }
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "athera.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = useLoopbackHttpCookies
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-Token";
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<TenantScopeExecutor>();
builder.Services.AddScoped<TenantOutboxProcessor>();
builder.Services.AddSingleton<ITenantOutboxSignal, TenantOutboxSignal>();
builder.Services.AddHostedService<OutboxBackgroundWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapHealthAndAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapAdminAndApprovalEndpoints();
app.MapMasterDataEndpoints();
app.MapFinanceEndpoints();
app.MapProcurementEndpoints();
app.MapReceivingEndpoints();

if (isMigrateMode)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AtheraDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("PostgreSQL migrations applied successfully.");
    if (!isSeedDemoMode)
    {
        return;
    }
}

if (isSeedDemoMode)
{
    var demoPassword = Environment.GetEnvironmentVariable("ATHERA_DEMO_PASSWORD") ?? string.Empty;
    await DemoDataSeeder.SeedAsync(app.Services, app.Environment, demoPassword, CancellationToken.None);
    Console.WriteLine("Disposable demo identities seeded successfully.");
    return;
}

if (isOpenApiMode)
{
    var outputPath = openApiIndex + 1 < args.Length
        ? args[openApiIndex + 1]
        : throw new ArgumentException("Usage: --export-openapi <outputPath>");

    app.Urls.Clear();
    app.Urls.Add("http://127.0.0.1:0");
    await app.StartAsync();

    try
    {
        var serverAddresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Unable to resolve server address for OpenAPI export.");
        var baseUrl = serverAddresses.Addresses.First();

        using var http = new HttpClient();
        var openApiJson = await http.GetStringAsync($"{baseUrl}/openapi/v1.json");
        var root = System.Text.Json.Nodes.JsonNode.Parse(openApiJson)!.AsObject();
        root["servers"] = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject { ["url"] = "/" }
        };
        var normalizedJson = root.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullOutputPath, normalizedJson + Environment.NewLine);
        Console.WriteLine($"OpenAPI contract exported to {fullOutputPath}");
    }
    finally
    {
        await app.StopAsync();
    }

    return;
}

await app.RunAsync();

public partial class Program;
