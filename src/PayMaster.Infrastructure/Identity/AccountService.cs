using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Email;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Infrastructure.Persistence;

namespace PayMaster.Infrastructure.Identity;

public enum SignInOutcome
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    Inactive
}

public record AccountResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static AccountResult Ok() => new(true, []);
    public static AccountResult Fail(IEnumerable<string> errors) => new(false, errors.ToList());
    public static AccountResult From(IdentityResult result) =>
        result.Succeeded ? Ok() : Fail(result.Errors.Select(e => e.Description));
}

/// <summary>Authentication use cases: sign-in/out, password reset/change, profile and sessions.</summary>
public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    SessionService sessions,
    ApplicationDbContext db,
    IAuditService audit,
    IEmailSender emailSender,
    TimeProvider clock,
    ILogger<AccountService> logger)
{
    public async Task<SignInOutcome> SignInAsync(string email, string password, bool rememberMe,
        string? ipAddress, string? userAgent)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            await RecordAsync("Auth.LoginFailed", null, $"Unknown account {email}");
            return SignInOutcome.InvalidCredentials;
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            await RecordAsync("Auth.LockedOut", user.Id, "Account locked after repeated failures");
            return SignInOutcome.LockedOut;
        }
        if (!check.Succeeded)
        {
            await RecordAsync("Auth.LoginFailed", user.Id, "Invalid password");
            return SignInOutcome.InvalidCredentials;
        }
        if (!user.IsActive)
        {
            await RecordAsync("Auth.LoginRejected", user.Id, "Account is deactivated");
            return SignInOutcome.Inactive;
        }

        var session = await sessions.CreateAsync(user.Id, ipAddress, userAgent, rememberMe);
        await signInManager.SignInWithClaimsAsync(user, rememberMe,
            [new Claim(PayMasterClaimTypes.SessionId, session.Id.ToString())]);

        user.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);
        await RecordAsync("Auth.Login", user.Id, $"Session {session.Id}");
        return SignInOutcome.Succeeded;
    }

    public async Task SignOutAsync(ClaimsPrincipal principal)
    {
        if (TryGetSession(principal, out var userId, out var sessionId))
        {
            await sessions.RevokeAsync(sessionId, userId);
            await RecordAsync("Auth.Logout", userId, $"Session {sessionId}");
        }
        await signInManager.SignOutAsync();
    }

    /// <summary>Sends a reset link if the account exists. Always succeeds to prevent account enumeration.</summary>
    public async Task RequestPasswordResetAsync(string email, Func<string, string, string> buildResetUrl)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive)
        {
            logger.LogInformation("Password reset requested for unknown or inactive account");
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var url = buildResetUrl(user.Email!, token);
        await emailSender.SendAsync(new EmailMessage(
            user.Email!,
            "Reset your PayMaster password",
            $"""
             <p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>
             <p>We received a request to reset your PayMaster password. This link is valid for 2 hours:</p>
             <p><a href="{WebUtility.HtmlEncode(url)}">Reset my password</a></p>
             <p>If you did not request this, you can ignore this email; your password will not change.</p>
             """,
            user.FullName));
        await RecordAsync("Auth.PasswordResetRequested", user.Id, null);
    }

    public async Task<AccountResult> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
            return AccountResult.Fail(["The reset link is invalid or has expired."]);

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            return AccountResult.From(result);

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await userManager.ResetAccessFailedCountAsync(user);
        await sessions.RevokeAllAsync(user.Id);
        await RecordAsync("Auth.PasswordReset", user.Id, "All sessions revoked");
        return AccountResult.Ok();
    }

    public async Task<AccountResult> ChangePasswordAsync(ClaimsPrincipal principal, string currentPassword, string newPassword)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
            return AccountResult.Fail(["User not found."]);

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return AccountResult.From(result);

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);

        TryGetSession(principal, out _, out var currentSession);
        await sessions.RevokeAllAsync(user.Id, currentSession);
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false,
            [new Claim(PayMasterClaimTypes.SessionId, currentSession.ToString())]);
        await RecordAsync("Auth.PasswordChanged", user.Id, "Other sessions revoked");
        return AccountResult.Ok();
    }

    public async Task<AccountResult> UpdateProfileAsync(ClaimsPrincipal principal, string firstName, string lastName, string? phone)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
            return AccountResult.Fail(["User not found."]);

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await signInManager.RefreshSignInAsync(user);
            await RecordAsync("Auth.ProfileUpdated", user.Id, null);
        }
        return AccountResult.From(result);
    }

    public Task<List<UserSession>> GetSessionsAsync(int userId) => sessions.ListActiveAsync(userId);

    public async Task RevokeSessionAsync(int userId, Guid sessionId)
    {
        await sessions.RevokeAsync(sessionId, userId);
        await RecordAsync("Auth.SessionRevoked", userId, $"Session {sessionId}");
    }

    public static bool TryGetSession(ClaimsPrincipal principal, out int userId, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
               & Guid.TryParse(principal.FindFirstValue(PayMasterClaimTypes.SessionId), out sessionId);
    }

    private async Task RecordAsync(string action, int? userId, string? details)
    {
        audit.Record(action, "User", userId, details);
        await db.SaveChangesAsync();
    }
}
