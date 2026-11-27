using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;

namespace PayMaster.Application.Performance;

/// <summary>
/// Who may see or act on performance records: HR sees everyone; managers see their department
/// (as head) and direct reports; employees see only themselves. Payroll staff get no access by default.
/// </summary>
public sealed class PerformanceScope(IApplicationDbContext db, ICurrentUser user)
{
    public bool IsHr => user.HasPermission(Permissions.Performance.ViewAll);
    public bool IsManager => user.HasPermission(Permissions.Performance.ManageTeam);
    public bool CanConfigure => user.HasPermission(Permissions.Performance.Configure);
    public bool CanManageCycles => user.HasPermission(Permissions.Performance.ManageCycles);
    public int? Self => user.EmployeeId;

    /// <summary>Employee ids visible to the current user (as a composable query).</summary>
    public IQueryable<int> VisibleEmployeeIds()
    {
        if (IsHr)
            return db.Employees.Select(e => e.Id);
        var self = user.EmployeeId ?? -1;
        if (IsManager)
            return db.Employees.Where(e => e.Id == self || e.ManagerId == self || e.Department.HeadEmployeeId == self).Select(e => e.Id);
        return db.Employees.Where(e => e.Id == self).Select(e => e.Id);
    }

    /// <summary>Team members the current user manages (excluding themselves), or everyone for HR.</summary>
    public IQueryable<int> ManagedEmployeeIds()
    {
        if (IsHr)
            return db.Employees.Select(e => e.Id);
        var self = user.EmployeeId ?? -1;
        return IsManager
            ? db.Employees.Where(e => e.Id != self && (e.ManagerId == self || e.Department.HeadEmployeeId == self)).Select(e => e.Id)
            : db.Employees.Where(_ => false).Select(e => e.Id);
    }

    public async Task<bool> CanManageAsync(int employeeId, CancellationToken ct) =>
        await ManagedEmployeeIds().AnyAsync(id => id == employeeId, ct);

    public async Task EnsureCanViewAsync(int employeeId, CancellationToken ct)
    {
        if (!await VisibleEmployeeIds().AnyAsync(id => id == employeeId, ct))
            throw new ForbiddenAccessException("You do not have access to this employee's performance records.");
    }

    public async Task EnsureCanManageAsync(int employeeId, CancellationToken ct)
    {
        if (!await CanManageAsync(employeeId, ct))
            throw new ForbiddenAccessException("Only the employee's manager or HR can do this.");
    }
}
