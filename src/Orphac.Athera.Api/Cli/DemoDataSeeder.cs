using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Api.Cli;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        string demoPassword,
        CancellationToken ct)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException("--seed-demo is strictly restricted to Development and Testing environments.");
        }

        if (string.IsNullOrWhiteSpace(demoPassword) || demoPassword.Length < 12)
        {
            throw new InvalidOperationException("ATHERA_DEMO_PASSWORD must be provided and at least 12 characters.");
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AtheraDbContext>();

        await EnsureUserAsync(
            userManager,
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111101"),
            "rina.utami@demo.athera.local",
            "Rina Utami (Demo Procurement)",
            SupportedLocales.Indonesian,
            demoPassword,
            ct);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        AtheraDbContext db,
        Guid userId,
        string email,
        string displayName,
        string preferredLocale,
        string password,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        var existingAccount = await db.UserAccounts.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (existingAccount is null)
        {
            db.UserAccounts.Add(UserAccount.Create(user.Id, displayName, preferredLocale, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }
    }
}
