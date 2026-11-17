using FamilyHub.Web.Services;
using System.Globalization;
using System.Security.Claims;
using Xunit;

namespace FamilyHub.Web.Tests;

public sealed class SharedPasswordValidatorTests
{
    private const string Password = "family-secret-password";
    private static readonly string PasswordHash = SharedPasswordValidator.CreateHash(Password);

    [Fact]
    public void IsValid_AcceptsTheConfiguredPassword()
    {
        var validator = new SharedPasswordValidator(PasswordHash);

        Assert.True(validator.IsValid(Password));
    }

    [Theory]
    [InlineData("wrong-family-password")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_RejectsIncorrectOrMissingPasswords(string? candidate)
    {
        var validator = new SharedPasswordValidator(PasswordHash);

        Assert.False(validator.IsValid(candidate));
    }

    [Fact]
    public void CreateHash_UsesDifferentRandomSalts()
    {
        var firstHash = SharedPasswordValidator.CreateHash(Password);
        var secondHash = SharedPasswordValidator.CreateHash(Password);

        Assert.NotEqual(firstHash, secondHash);
        Assert.True(new SharedPasswordValidator(firstHash).IsValid(Password));
        Assert.True(new SharedPasswordValidator(secondHash).IsValid(Password));
    }

    [Fact]
    public void Constructor_RejectsMalformedPasswordHash()
    {
        Assert.Throws<ArgumentException>(() => new SharedPasswordValidator("not-a-password-hash"));
        Assert.Throws<ArgumentException>(() => new SharedPasswordValidator(
            "pbkdf2-sha256$1000$YWJj$YWJj"));
    }

    [Fact]
    public void CreateHash_RejectsPasswordsUnderSixteenCharacters()
    {
        Assert.Throws<ArgumentException>(() => SharedPasswordValidator.CreateHash("short"));
    }

    [Fact]
    public void AuthenticationState_RemainsValidUntilSessionExpires()
    {
        var expiresAt = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
        var principal = CreateAuthenticatedPrincipal(expiresAt);

        Assert.True(FamilyHubAuthenticationStateProvider.IsSessionValid(
            principal,
            expiresAt.AddSeconds(-1)));
        Assert.False(FamilyHubAuthenticationStateProvider.IsSessionValid(
            principal,
            expiresAt));
    }

    [Fact]
    public void AuthenticationState_RejectsMissingOrInvalidExpirationClaim()
    {
        var missingExpiration = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Family")],
            "cookie"));
        var invalidExpiration = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Family"),
                new Claim(FamilyHubAuthenticationStateProvider.SessionExpiresAtClaim, "invalid")
            ],
            "cookie"));

        Assert.False(FamilyHubAuthenticationStateProvider.IsSessionValid(
            missingExpiration,
            DateTimeOffset.UtcNow));
        Assert.False(FamilyHubAuthenticationStateProvider.IsSessionValid(
            invalidExpiration,
            DateTimeOffset.UtcNow));
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(DateTimeOffset expiresAt) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Family"),
                new Claim(
                    FamilyHubAuthenticationStateProvider.SessionExpiresAtClaim,
                    expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            ],
            "cookie"));
}
