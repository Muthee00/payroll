using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayMaster.Application.Common.Security;

namespace PayMaster.Web.Infrastructure;

/// <summary>Allows the request when the user holds at least one of the listed permissions.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireAnyPermissionAttribute(params string[] permissions) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }
        if (!permissions.Any(p => user.HasClaim(Permissions.ClaimType, p)))
            context.Result = new ForbidResult();
    }
}
