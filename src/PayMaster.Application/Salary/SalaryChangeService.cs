using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Salary;

/// <summary>
/// Employee salary revisions: propose → approve/reject. Approval closes the revision in force
/// (never overwriting it) and is blocked inside periods whose payroll is already finalized.
/// </summary>
public sealed class SalaryChangeService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    INotificationService notifications,
    IUserDirectory users,
    IAuditService audit,
    TimeProvider clock)
{
    public async Task<SalaryChangeInput> BuildProposalAsync(int employeeId, int? structureId = null, decimal? basic = null,
        CancellationToken ct = default)
    {
        var current = await CurrentRevisionAsync(employeeId, ct);
        var input = new SalaryChangeInput
        {
            EmployeeId = employeeId,
            SalaryStructureId = structureId ?? current?.SalaryStructureId,
            BasicSalary = basic ?? current?.BasicSalary ?? 0m,
            EffectiveFrom = FirstOfNextMonth()
        };

        var allowances = await db.SalaryComponents.AsNoTracking()
            .Where(c => c.Type == ComponentType.Earning && c.IsRecurring && c.IsActive)
            .OrderBy(c => c.DisplayOrder).ToListAsync(ct);

        Dictionary<int, decimal> amounts;
        if (structureId is int sid)
        {
            var structure = await db.SalaryStructures.AsNoTracking().Include(s => s.Components)
                .SingleOrDefaultAsync(s => s.Id == sid, ct) ?? throw new NotFoundException("Salary structure", sid);
            if (basic is null)
                input.BasicSalary = structure.BasicSalary;
            var sized = new SalaryStructure { BasicSalary = input.BasicSalary };
            amounts = structure.Components.ToDictionary(c => c.SalaryComponentId, c => sized.ResolveAmount(c));
        }
        else
        {
            amounts = current?.Components.ToDictionary(c => c.SalaryComponentId, c => c.Amount) ?? [];
        }

        input.Components = allowances
            .Select(c => new ComponentAmountInput { SalaryComponentId = c.Id, Name = c.Name, Amount = amounts.GetValueOrDefault(c.Id) })
            .ToList();
        return input;
    }

    public async Task<int> ProposeAsync(SalaryChangeInput input, CancellationToken ct = default)
    {
        var revision = await ProposeCoreAsync(input, notify: true, ct);
        await db.SaveChangesAsync(ct);
        return revision.Id;
    }

    /// <summary>Adds a pending revision to the unit of work (caller saves).</summary>
    internal async Task<EmployeeSalary> ProposeCoreAsync(SalaryChangeInput input, bool notify, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
                       ?? throw new NotFoundException("Employee", input.EmployeeId);
        if (employee.Status == EmploymentStatus.Terminated)
            throw new DomainException("Salary changes cannot be made for terminated employees.");

        var effective = input.EffectiveFrom!.Value;
        if (await db.EmployeeSalaries.AnyAsync(s => s.EmployeeId == employee.Id && s.Status == SalaryRevisionStatus.PendingApproval, ct))
            throw new ConflictException($"{employee.FullName} already has a salary change awaiting approval. Approve or reject it first.");
        await EnsureNotInFinalizedPeriodAsync(employee.Id, effective, ct);

        var current = await CurrentRevisionAsync(employee.Id, ct);
        if (current is not null && effective <= current.EffectiveFrom)
            throw new DomainException($"The new salary must take effect after {current.EffectiveFrom:dd MMM yyyy}, when the current salary started.");

        if (input.SalaryStructureId is int structureId
            && !await db.SalaryStructures.AnyAsync(s => s.Id == structureId && s.Status == SalaryStructureStatus.Active, ct))
            throw new DomainException("The selected salary structure is not active.");

        var componentIds = input.Components.Where(c => c.Amount > 0).Select(c => c.SalaryComponentId).ToList();
        var validCount = await db.SalaryComponents.CountAsync(c => componentIds.Contains(c.Id) && c.Type == ComponentType.Earning && c.IsRecurring, ct);
        if (validCount != componentIds.Distinct().Count())
            throw new DomainException("Salary revisions can only include recurring earnings.");

        var revision = new EmployeeSalary
        {
            EmployeeId = employee.Id,
            SalaryStructureId = input.SalaryStructureId,
            BasicSalary = Money.Round(input.BasicSalary),
            EffectiveFrom = effective,
            Status = SalaryRevisionStatus.PendingApproval,
            ChangeReason = input.Reason.Trim(),
            CreatedById = currentUser.UserId,
            Components = input.Components.Where(c => c.Amount > 0)
                .Select(c => new EmployeeSalaryComponent { SalaryComponentId = c.SalaryComponentId, Amount = Money.Round(c.Amount) })
                .ToList()
        };
        db.EmployeeSalaries.Add(revision);

        if (notify)
            await notifications.NotifyPermissionHoldersAsync(Permissions.Salary.Approve, "Salary change awaiting approval",
                $"{employee.FullName}: {Money.Format(revision.BasicSalary)} basic from {effective:dd MMM yyyy}.",
                NotificationType.Warning, "/SalaryStructures/Approvals", ct);
        return revision;
    }

    public async Task<IReadOnlyList<PendingSalaryChangeDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var pending = await db.EmployeeSalaries.AsNoTracking()
            .Where(s => s.Status == SalaryRevisionStatus.PendingApproval)
            .Select(s => new
            {
                s.Id, s.EmployeeId, s.Employee.EmployeeNumber, Name = s.Employee.FirstName + " " + s.Employee.LastName,
                Department = s.Employee.Department.Name, s.BasicSalary,
                Allowances = s.Components.Sum(c => (decimal?)c.Amount) ?? 0m, s.EffectiveFrom, s.ChangeReason,
                Structure = s.SalaryStructure != null ? s.SalaryStructure.Name : null, s.CreatedById, s.CreatedAt
            })
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        var employeeIds = pending.Select(p => p.EmployeeId).ToList();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var currents = await db.EmployeeSalaries.AsNoTracking()
            .Where(s => employeeIds.Contains(s.EmployeeId) && s.Status == SalaryRevisionStatus.Approved)
            .Select(s => new { s.EmployeeId, s.EffectiveFrom, s.BasicSalary, Allowances = s.Components.Sum(c => (decimal?)c.Amount) ?? 0m })
            .ToListAsync(ct);
        var names = await users.GetDisplayNamesAsync(pending.Where(p => p.CreatedById != null).Select(p => p.CreatedById!.Value), ct);

        return pending.Select(p =>
        {
            var current = currents.Where(c => c.EmployeeId == p.EmployeeId).MaxBy(c => c.EffectiveFrom);
            return new PendingSalaryChangeDto(p.Id, p.EmployeeId, p.EmployeeNumber, p.Name, p.Department,
                current?.BasicSalary, current is null ? null : Money.Round(current.BasicSalary + current.Allowances),
                p.BasicSalary, Money.Round(p.BasicSalary + p.Allowances), p.EffectiveFrom, p.ChangeReason, p.Structure,
                p.CreatedById, p.CreatedById is int uid ? names.GetValueOrDefault(uid) : null, p.CreatedAt);
        }).ToList();
    }

    public async Task ApproveAsync(int revisionId, CancellationToken ct = default)
    {
        var revision = await db.EmployeeSalaries.Include(s => s.Employee)
            .SingleOrDefaultAsync(s => s.Id == revisionId, ct) ?? throw new NotFoundException("Salary revision", revisionId);
        await EnsureNotInFinalizedPeriodAsync(revision.EmployeeId, revision.EffectiveFrom, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        revision.Approve(currentUser.RequireUserId(), now);

        // Close the revision in force (and any later-dated approved one would be a conflict).
        var approved = await db.EmployeeSalaries
            .Where(s => s.EmployeeId == revision.EmployeeId && s.Id != revision.Id && s.Status == SalaryRevisionStatus.Approved)
            .ToListAsync(ct);
        if (approved.Any(s => s.EffectiveFrom >= revision.EffectiveFrom))
            throw new DomainException("Another approved salary starts on or after this date. Reject this change and propose a later date.");
        foreach (var previous in approved)
            previous.Supersede(revision.EffectiveFrom);

        audit.Record("Salary.ChangeApproved", "Employee", revision.EmployeeId,
            $"Revision {revision.Id}: basic {Money.Format(revision.BasicSalary)} from {revision.EffectiveFrom:yyyy-MM-dd}");
        NotifyRequester(revision, "Salary change approved", NotificationType.Success);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int revisionId, string reason, CancellationToken ct = default)
    {
        var revision = await db.EmployeeSalaries.Include(s => s.Employee)
            .SingleOrDefaultAsync(s => s.Id == revisionId, ct) ?? throw new NotFoundException("Salary revision", revisionId);
        revision.Reject(currentUser.RequireUserId(), clock.GetUtcNow().UtcDateTime, reason);
        audit.Record("Salary.ChangeRejected", "Employee", revision.EmployeeId, $"Revision {revision.Id}: {reason}");
        NotifyRequester(revision, "Salary change rejected", NotificationType.Error);
        await db.SaveChangesAsync(ct);
    }

    private void NotifyRequester(EmployeeSalary revision, string title, NotificationType type)
    {
        if (revision.CreatedById is int requester && requester != currentUser.UserId)
            notifications.Notify([requester], title,
                $"{revision.Employee.FullName}: {Money.Format(revision.BasicSalary)} basic from {revision.EffectiveFrom:dd MMM yyyy}.",
                type, $"/Employees/Details/{revision.EmployeeId}#salary");
    }

    private Task<EmployeeSalary?> CurrentRevisionAsync(int employeeId, CancellationToken ct) =>
        db.EmployeeSalaries.AsNoTracking().Include(s => s.Components)
            .Where(s => s.EmployeeId == employeeId && s.Status == SalaryRevisionStatus.Approved)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    /// <summary>A change dated inside a finalized payroll period would silently disagree with paid history.</summary>
    private async Task EnsureNotInFinalizedPeriodAsync(int employeeId, DateOnly effective, CancellationToken ct)
    {
        var lastFinalizedEnd = await db.PayrollEmployees
            .Where(p => p.EmployeeId == employeeId && p.PeriodLock == true
                        && (p.PayrollRun.Status == PayrollRunStatus.Finalized || p.PayrollRun.Status == PayrollRunStatus.Paid))
            .Select(p => (DateOnly?)p.PayrollRun.PayrollPeriod.EndDate)
            .MaxAsync(ct);
        if (lastFinalizedEnd is not null && effective <= lastFinalizedEnd)
            throw new DomainException(
                $"Payroll is already finalized up to {lastFinalizedEnd:dd MMM yyyy}. Use an effective date after that, or process a payroll correction.");
    }

    private DateOnly FirstOfNextMonth()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return new DateOnly(today.Year, today.Month, 1).AddMonths(1);
    }
}
