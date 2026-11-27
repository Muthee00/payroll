using System.Security.Claims;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Web.Infrastructure;

/// <summary>Current user from the HTTP request, or the job owner when running a background job.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor, JobUserContext jobUser) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId => jobUser.UserId
                          ?? (int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);

    public string? UserName => jobUser.UserName ?? Principal?.Identity?.Name;

    public int? EmployeeId =>
        int.TryParse(Principal?.FindFirstValue(PayMasterClaimTypes.EmployeeId), out var id) ? id : null;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? (jobUser.UserId is null ? null : "background-job");

    public bool IsAuthenticated => jobUser.UserId is not null || Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(Permissions.ClaimType, permission) == true;
}
