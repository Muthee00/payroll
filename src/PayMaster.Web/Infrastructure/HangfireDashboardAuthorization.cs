using Hangfire.Dashboard;
using PayMaster.Application.Common.Security;

namespace PayMaster.Web.Infrastructure;

/// <summary>The Hangfire dashboard is limited to signed-in Super Administrators.</summary>
public sealed class HangfireDashboardAuthorization : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.SuperAdmin);
    }
}
