using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PayMaster.Infrastructure.Identity;
using PayMaster.Web.ViewModels.Account;

namespace PayMaster.Web.Controllers;

public class AccountController(AccountService accounts, UserManager<ApplicationUser> userManager) : AppController
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var outcome = await accounts.SignInAsync(model.Email, model.Password, model.RememberMe,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        switch (outcome)
        {
            case SignInOutcome.Succeeded:
                return RedirectToLocal(model.ReturnUrl);
            case SignInOutcome.LockedOut:
                ModelState.AddModelError(string.Empty, "Too many failed attempts. Your account is locked for 15 minutes.");
                break;
            case SignInOutcome.Inactive:
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Contact your administrator.");
                break;
            default:
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                break;
        }

        model.Password = string.Empty;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await accounts.SignOutAsync(User);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        await accounts.RequestPasswordResetAsync(model.Email, (email, token) =>
            Url.Action(nameof(ResetPassword), "Account", new { email, token }, Request.Scheme)!);

        return View("ForgotPasswordConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await accounts.ResetPasswordAsync(model.Email, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        ToastSuccess("Your password has been reset. Sign in with your new password.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword(bool required = false) =>
        View(new ChangePasswordViewModel { Required = required });

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await accounts.ChangePasswordAsync(User, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        ToastSuccess("Password changed. Your other sessions have been signed out.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var model = await BuildProfileAsync();
        return model is null ? RedirectToAction(nameof(Login)) : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await accounts.UpdateProfileAsync(User, model.FirstName, model.LastName, model.PhoneNumber);
            if (result.Succeeded)
            {
                ToastSuccess("Profile updated.");
                return RedirectToAction(nameof(Profile));
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
        }

        var current = await BuildProfileAsync();
        if (current is null)
            return RedirectToAction(nameof(Login));
        current.FirstName = model.FirstName;
        current.LastName = model.LastName;
        current.PhoneNumber = model.PhoneNumber;
        return View(current);
    }

    [HttpPost]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        if (AccountService.TryGetSession(User, out var userId, out var currentSession))
        {
            await accounts.RevokeSessionAsync(userId, id);
            if (id == currentSession)
                return RedirectToAction(nameof(Login));
            ToastSuccess("Session signed out.");
        }
        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task<ProfileViewModel?> BuildProfileAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return null;

        AccountService.TryGetSession(User, out _, out var sessionId);
        return new ProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            LastLoginAt = user.LastLoginAt,
            Roles = (await userManager.GetRolesAsync(user)).ToList(),
            Sessions = await accounts.GetSessionsAsync(user.Id),
            CurrentSessionId = sessionId
        };
    }
}
