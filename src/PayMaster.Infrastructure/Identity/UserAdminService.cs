using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Email;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Domain.Common;
using PayMaster.Infrastructure.Persistence;

namespace PayMaster.Infrastructure.Identity;

public record UserListItem(int Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive, bool IsLockedOut,
    DateTime? LastLoginAt, int? EmployeeId, string? EmployeeNumber, int ActiveSessions);

public class UserInput
{
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int? EmployeeId { get; set; }
    public List<string> Roles { get; set; } = [];
}

public record RoleDto(int Id, string Name, string? Description, IReadOnlyList<string> Permissions, int UserCount);

/// <summary>User and role administration (Super Administrator only). Every change is audited.</summary>
public sealed class UserAdminService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ApplicationDbContext db,
    SessionService sessions,
    IEmailSender email,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<PagedResult<UserListItem>> ListAsync(PageQuery query, CancellationToken ct = default)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u => u.Email!.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }
        var page = await users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToPagedResultAsync(query, ct);

        var ids = page.Items.Select(u => u.Id).ToList();
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var sessionCounts = await db.UserSessions.Where(s => ids.Contains(s.UserId) && s.RevokedAt == null && s.ExpiresAt > now)
            .GroupBy(s => s.UserId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var employeeIds = page.Items.Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
        var numbers = await db.Employees.IgnoreQueryFilters().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.EmployeeNumber, ct);

        return new PagedResult<UserListItem>(page.Items.Select(u => new UserListItem(u.Id, u.Email ?? "", u.FullName,
                roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).OrderBy(Order).ToList(), u.IsActive,
                u.LockoutEnd is { } end && end > clock.GetUtcNow(), u.LastLoginAt, u.EmployeeId,
                u.EmployeeId is int e ? numbers.GetValueOrDefault(e) : null, sessionCounts.GetValueOrDefault(u.Id)))
            .ToList(), page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<UserInput> GetAsync(int id)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);
        return new UserInput
        {
            Email = user.Email ?? "", FirstName = user.FirstName, LastName = user.LastName, EmployeeId = user.EmployeeId,
            Roles = (await userManager.GetRolesAsync(user)).ToList()
        };
    }

    /// <summary>Creates the account with an unusable random password and emails a link to set one.</summary>
    public async Task<int> InviteAsync(UserInput input, Func<string, string, string> resetUrl, CancellationToken ct = default)
    {
        ValidateRoles(input.Roles);
        await EnsureEmployeeLinkAvailableAsync(input.EmployeeId, null, ct);

        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(), Email = input.Email.Trim(), EmailConfirmed = true,
            FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(), EmployeeId = input.EmployeeId,
            CreatedAt = clock.GetUtcNow().UtcDateTime, MustChangePassword = true
        };
        var result = await userManager.CreateAsync(user, RandomPassword());
        if (!result.Succeeded)
            throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRolesAsync(user, input.Roles.Append(Roles.Employee).Distinct());

        if (input.EmployeeId is int employeeId)
            await db.Employees.Where(e => e.Id == employeeId).ExecuteUpdateAsync(u => u.SetProperty(e => e.UserId, user.Id), ct);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await email.SendAsync(new EmailMessage(user.Email!, "Your PayMaster account",
            $"""
             <p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>
             <p>An account has been created for you in PayMaster. Choose your password to sign in (link valid for 2 hours):</p>
             <p><a href="{WebUtility.HtmlEncode(resetUrl(user.Email!, token))}">Set my password</a></p>
             """, user.FullName), ct);

        audit.Record("User.Created", "User", user.Id, $"{user.Email} with roles {string.Join(", ", input.Roles)}");
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task UpdateAsync(int id, UserInput input, CancellationToken ct = default)
    {
        ValidateRoles(input.Roles);
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);
        await EnsureEmployeeLinkAvailableAsync(input.EmployeeId, id, ct);

        var before = (await userManager.GetRolesAsync(user)).ToList();
        var after = input.Roles.Append(Roles.Employee).Distinct().ToList();
        if (before.Contains(Roles.SuperAdmin) && !after.Contains(Roles.SuperAdmin))
            await EnsureAnotherSuperAdminAsync(user.Id);

        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        if (user.EmployeeId != input.EmployeeId)
        {
            if (user.EmployeeId is int oldEmployee)
                await db.Employees.Where(e => e.Id == oldEmployee).ExecuteUpdateAsync(u => u.SetProperty(e => e.UserId, (int?)null), ct);
            if (input.EmployeeId is int newEmployee)
                await db.Employees.Where(e => e.Id == newEmployee).ExecuteUpdateAsync(u => u.SetProperty(e => e.UserId, user.Id), ct);
            user.EmployeeId = input.EmployeeId;
        }
        await userManager.UpdateAsync(user);
        await userManager.RemoveFromRolesAsync(user, before.Except(after));
        await userManager.AddToRolesAsync(user, after.Except(before));
        // Force a refresh of the user's cookie so new permissions apply immediately.
        await userManager.UpdateSecurityStampAsync(user);

        if (!before.Order().SequenceEqual(after.Order()))
            audit.Record("User.RolesChanged", "User", id, user.Email, new { Roles = before }, new { Roles = after });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);
        if (!active)
        {
            if (user.Id == currentUser.UserId)
                throw new DomainException("You cannot deactivate your own account.");
            if (await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                await EnsureAnotherSuperAdminAsync(user.Id);
        }
        user.IsActive = active;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        if (!active)
            await sessions.RevokeAllAsync(user.Id);
        audit.Record(active ? "User.Activated" : "User.Deactivated", "User", id, user.Email);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlockAsync(int id, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        audit.Record("User.Unlocked", "User", id, user.Email);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeSessionsAsync(int id, CancellationToken ct = default)
    {
        await sessions.RevokeAllAsync(id);
        audit.Record("User.SessionsRevoked", "User", id, null);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Roles

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await db.Roles.AsNoTracking().ToListAsync(ct);
        var claims = await db.RoleClaims.AsNoTracking().Where(c => c.ClaimType == Permissions.ClaimType).ToListAsync(ct);
        var counts = await db.UserRoles.GroupBy(r => r.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return roles.OrderBy(r => Order(r.Name!))
            .Select(r => new RoleDto(r.Id, r.Name!, r.Description, claims.Where(c => c.RoleId == r.Id).Select(c => c.ClaimValue!).ToList(),
                counts.GetValueOrDefault(r.Id)))
            .ToList();
    }

    public async Task SetRolePermissionsAsync(int roleId, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString()) ?? throw new NotFoundException("Role", roleId);
        if (role.Name == Roles.SuperAdmin)
            throw new DomainException("The Super Administrator role always has every permission.");
        var unknown = permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Unknown permission(s): {string.Join(", ", unknown)}");

        var current = (await roleManager.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).ToList();
        var before = current.Select(c => c.Value).ToList();
        foreach (var claim in current.Where(c => !permissions.Contains(c.Value)))
            await roleManager.RemoveClaimAsync(role, claim);
        foreach (var permission in permissions.Except(before))
            await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));

        // Members pick up the change at their next security-stamp check (within 5 minutes).
        var members = await userManager.GetUsersInRoleAsync(role.Name!);
        foreach (var member in members)
            await userManager.UpdateSecurityStampAsync(member);

        audit.Record("Role.PermissionsChanged", "Role", roleId, role.Name, new { Permissions = before }, new { Permissions = permissions });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Helpers

    private static void ValidateRoles(IEnumerable<string> roles)
    {
        var unknown = roles.Except(Roles.All).ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Unknown role(s): {string.Join(", ", unknown)}");
    }

    private async Task EnsureAnotherSuperAdminAsync(int exceptUserId)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        if (!admins.Any(a => a.Id != exceptUserId && a.IsActive))
            throw new DomainException("At least one active Super Administrator must remain.");
    }

    private async Task EnsureEmployeeLinkAvailableAsync(int? employeeId, int? userId, CancellationToken ct)
    {
        if (employeeId is int id && await db.Users.AnyAsync(u => u.EmployeeId == id && u.Id != userId, ct))
            throw new ConflictException("That employee is already linked to another login.");
    }

    private static int Order(string role) => Roles.All.ToList().IndexOf(role) is var i and >= 0 ? i : 99;

    private static string RandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1!";
}
