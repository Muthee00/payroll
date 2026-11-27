using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Web.Infrastructure;

/// <summary>Sends users flagged with "must change password" to the change-password page before anything else.</summary>
public sealed class ForcePasswordChangeFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AllowedActions =
        new(StringComparer.OrdinalIgnoreCase) { "ChangePassword", "Logout" };

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var mustChange = user.HasClaim(PayMasterClaimTypes.MustChangePassword, "true");
        var controller = context.RouteData.Values["controller"] as string;
        var action = context.RouteData.Values["action"] as string;

        if (mustChange
            && !(string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) && AllowedActions.Contains(action ?? "")))
        {
            context.Result = new RedirectToActionResult("ChangePassword", "Account", new { required = true });
            return Task.CompletedTask;
        }

        return next();
    }
}
