using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Web.Infrastructure;

public static class AuthCookieEvents
{
    /// <summary>
    /// Runs Identity's security-stamp check, then rejects the cookie if its server-side
    /// session has been revoked or expired (logout elsewhere, password reset, admin revoke).
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        if (!AccountService.TryGetSession(context.Principal, out var userId, out var sessionId))
        {
            await RejectAsync(context);
            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
        if (!await sessions.ValidateAsync(sessionId, userId))
            await RejectAsync(context);
    }

    /// <summary>Keeps the session id claim when the security-stamp validator rebuilds the principal.</summary>
    public static Task PreserveSessionClaim(SecurityStampRefreshingPrincipalContext context)
    {
        var sessionClaim = context.CurrentPrincipal?.FindFirst(PayMasterClaimTypes.SessionId);
        if (sessionClaim is not null
            && context.NewPrincipal?.Identity is ClaimsIdentity identity
            && !identity.HasClaim(c => c.Type == PayMasterClaimTypes.SessionId))
        {
            identity.AddClaim(new Claim(PayMasterClaimTypes.SessionId, sessionClaim.Value));
        }
        return Task.CompletedTask;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
