using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace FamilyHub.Web.Services;

public sealed class FamilyHubAuthenticationStateProvider(ILoggerFactory loggerFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    public const string SessionExpiresAtClaim = "FamilyHub:SessionExpiresAt";

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        var isValid = IsSessionValid(authenticationState.User, DateTimeOffset.UtcNow);
        return Task.FromResult(isValid);
    }

    public static bool IsSessionValid(ClaimsPrincipal principal, DateTimeOffset now)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        var expiresAtClaim = principal.FindFirstValue(SessionExpiresAtClaim);
        return long.TryParse(
                expiresAtClaim,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expiresAtSeconds)
            && expiresAtSeconds >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
            && expiresAtSeconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            && DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds) > now;
    }
}
