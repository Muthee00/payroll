using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Domain.Entities.Employees;

namespace PayMaster.Application.Employees;

/// <summary>
/// Server-side row-level visibility for employee data:
/// organisation-wide roles see everyone; managers see their departments and reports; everyone else sees only themselves.
/// </summary>
public sealed class EmployeeAccessScope(IApplicationDbContext db, ICurrentUser user)
{
    public bool SeesEveryone =>
        user.HasPermission(Permissions.Employees.Manage)
        || user.HasPermission(Permissions.Payroll.View)
        || user.HasPermission(Permissions.Performance.ViewAll);

    public bool CanViewSalary =>
        user.HasPermission(Permissions.Salary.View) || user.HasPermission(Permissions.Payroll.View);

    public bool CanViewBankDetails => user.HasPermission(Permissions.Employees.ViewBankDetails);

    /// <summary>Restricts an employee query to the rows the current user may see.</summary>
    public async Task<IQueryable<Employee>> ApplyAsync(IQueryable<Employee> query, CancellationToken ct = default)
    {
        if (SeesEveryone)
            return query;

        if (user.EmployeeId is not int self)
            return query.Where(_ => false);

        if (user.HasPermission(Permissions.Performance.ManageTeam))
        {
            var headedDepartments = await db.Departments
                .Where(d => d.HeadEmployeeId == self)
                .Select(d => d.Id)
                .ToListAsync(ct);
            return query.Where(e => e.Id == self || e.ManagerId == self || headedDepartments.Contains(e.DepartmentId));
        }

        return query.Where(e => e.Id == self);
    }

    public async Task EnsureCanViewAsync(int employeeId, CancellationToken ct = default)
    {
        var scoped = await ApplyAsync(db.Employees.AsNoTracking(), ct);
        if (!await scoped.AnyAsync(e => e.Id == employeeId, ct))
        {
            if (await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
                throw new ForbiddenAccessException("You do not have access to this employee.");
            throw new NotFoundException("Employee", employeeId);
        }
    }
}
