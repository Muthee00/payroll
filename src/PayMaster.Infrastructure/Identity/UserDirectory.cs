using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Infrastructure.Persistence;

namespace PayMaster.Infrastructure.Identity;

public sealed class UserDirectory(ApplicationDbContext db, UserManager<ApplicationUser> userManager, SessionService sessions) : IUserDirectory
{
    public async Task<IReadOnlyList<int>> GetUserIdsWithPermissionAsync(string permission, CancellationToken ct = default)
    {
        var roleIds = db.RoleClaims
            .Where(c => c.ClaimType == Permissions.ClaimType && c.ClaimValue == permission)
            .Select(c => c.RoleId);

        return await (from userRole in db.UserRoles
                      join user in db.Users on userRole.UserId equals user.Id
                      where roleIds.Contains(userRole.RoleId) && user.IsActive
                      select user.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<int?> GetUserIdForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        db.Users.Where(u => u.EmployeeId == employeeId && u.IsActive).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IEnumerable<int> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, string>();
        return await db.Users.Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
    }

    public async Task DisableLoginAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return;
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        await sessions.RevokeAllAsync(userId);
    }
}
