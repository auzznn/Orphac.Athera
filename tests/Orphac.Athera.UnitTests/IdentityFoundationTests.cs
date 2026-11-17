using Orphac.Athera.Api.Security;
using Orphac.Athera.Modules.Identity;

namespace Orphac.Athera.UnitTests;

public sealed class IdentityFoundationTests
{
    [Theory]
    [InlineData(null, "id-ID")]
    [InlineData("", "id-ID")]
    [InlineData("id-ID", "id-ID")]
    [InlineData("ID", "id-ID")]
    [InlineData("en-US", "en-US")]
    [InlineData("en", "en-US")]
    public void NormalizeLocale_ReturnsSupportedLocale(string? raw, string expected)
    {
        var actual = SupportedLocales.NormalizeOrDefault(raw);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeLocale_ThrowsOnUnsupportedLocale()
    {
        Assert.Throws<ArgumentException>(() => SupportedLocales.NormalizeOrDefault("fr-FR"));
    }

    [Fact]
    public void UserAccount_CreateAndDeactivate_TracksStateAndVersion()
    {
        var userId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

        var account = UserAccount.Create(userId, "  Rina Utami  ", "id-ID", createdAt);

        Assert.Equal(userId, account.UserId);
        Assert.Equal("Rina Utami", account.DisplayName);
        Assert.Equal("id-ID", account.PreferredLocale);
        Assert.True(account.IsActive);
        Assert.Equal(1L, account.Version);

        var updatedAt = createdAt.AddMinutes(10);
        account.Deactivate(updatedAt);

        Assert.False(account.IsActive);
        Assert.Equal(updatedAt, account.UpdatedAtUtc);
        Assert.Equal(2L, account.Version);
    }

    [Theory]
    [InlineData("http://127.0.0.1:3200", "127.0.0.1:3200", true)]
    [InlineData("http://localhost:3200", "localhost:3200", true)]
    [InlineData("https://evil.example.net", "127.0.0.1:3200", false)]
    [InlineData("not-a-uri", "127.0.0.1:3200", false)]
    public void OriginValidator_ChecksBrowserOriginAgainstAllowedOrigins(
        string origin,
        string host,
        bool expected)
    {
        string[] allowed = ["http://127.0.0.1:3200", "http://localhost:3200"];
        var allowedResult = OriginValidator.IsAllowed(origin, host, allowed);
        Assert.Equal(expected, allowedResult);
    }
}
