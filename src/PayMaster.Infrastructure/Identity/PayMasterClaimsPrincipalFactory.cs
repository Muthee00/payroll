using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PayMaster.Infrastructure.Identity;

public static class PayMasterClaimTypes
{
    public const string EmployeeId = "employee_id";
    public const string FullName = "full_name";
    public const string SessionId = "session_id";
    public const string MustChangePassword = "must_change_password";
}

/// <summary>Adds the linked employee id and display name to the principal (roles and permission claims come from the base factory).</summary>
public class PayMasterClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(PayMasterClaimTypes.FullName, user.FullName));
        if (user.EmployeeId is int employeeId)
            identity.AddClaim(new Claim(PayMasterClaimTypes.EmployeeId, employeeId.ToString()));
        if (user.MustChangePassword)
            identity.AddClaim(new Claim(PayMasterClaimTypes.MustChangePassword, "true"));
        return identity;
    }
}
