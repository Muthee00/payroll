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

/// <summary>Reusable salary templates and grades. Structures never modify existing employee salaries directly.</summary>
public sealed class SalaryStructureService(
    IApplicationDbContext db,
    SalaryChangeService salaryChanges,
    INotificationService notifications,
    IAuditService audit,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<IReadOnlyList<SalaryStructureListItem>> ListAsync(SalaryStructureStatus? status = null, CancellationToken ct = default)
    {
        var structures = await db.SalaryStructures.AsNoTracking()
            .Include(s => s.SalaryGrade).Include(s => s.Department).Include(s => s.Position)
            .Include(s => s.Components)
            .Where(s => status == null || s.Status == status)
            .OrderBy(s => s.Status).ThenBy(s => s.Name)
            .ToListAsync(ct);

        var counts = await CurrentEmployeeCountsAsync(ct);
        return structures.Select(s => new SalaryStructureListItem(
                s.Id, s.Code, s.Name, s.SalaryGrade?.Name, s.Department?.Name, s.Position?.Title, s.Status,
                s.BasicSalary, Gross(s), s.Components.Count, counts.GetValueOrDefault(s.Id), s.EffectiveFrom, s.EffectiveTo))
            .ToList();
    }

    public async Task<SalaryStructureDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var s = await db.SalaryStructures.AsNoTracking()
            .Include(x => x.SalaryGrade).Include(x => x.Department).Include(x => x.Position)
            .Include(x => x.Components).ThenInclude(c => c.SalaryComponent)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Salary structure", id);

        var today = Today;
        var employees = await db.EmployeeSalaries.AsNoTracking()
            .Where(r => r.SalaryStructureId == id
                        && (r.Status == SalaryRevisionStatus.Approved || r.Status == SalaryRevisionStatus.Superseded)
                        && r.EffectiveFrom <= today && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .Select(r => new
            {
                r.EmployeeId, r.Employee.EmployeeNumber, Name = r.Employee.FirstName + " " + r.Employee.LastName,
                Department = r.Employee.Department.Name, r.BasicSalary,
                Allowances = r.Components.Sum(c => (decimal?)c.Amount) ?? 0m, r.EffectiveFrom
            })
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return new SalaryStructureDetails(
            s.Id, s.Code, s.Name, s.Description, s.Status, s.SalaryGradeId, s.SalaryGrade?.Name,
            s.SalaryGrade?.MinimumSalary, s.SalaryGrade?.MaximumSalary, s.DepartmentId, s.Department?.Name,
            s.PositionId, s.Position?.Title, s.BasicSalary, Gross(s), s.EffectiveFrom, s.EffectiveTo,
            s.Components.OrderBy(c => c.SalaryComponent.DisplayOrder)
                .Select(c => new StructureLineDto(c.SalaryComponentId, c.SalaryComponent.Name, c.AmountType, c.Amount, s.ResolveAmount(c)))
                .ToList(),
            employees.Select(e => new StructureEmployeeDto(e.EmployeeId, e.EmployeeNumber, e.Name, e.Department,
                e.BasicSalary, Money.Round(e.BasicSalary + e.Allowances), e.EffectiveFrom)).ToList(),
            s.CreatedAt, s.UpdatedAt);
    }

    public async Task<SalaryStructureInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var s = await db.SalaryStructures.AsNoTracking().Include(x => x.Components)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Salary structure", id);
        return new SalaryStructureInput
        {
            Code = s.Code, Name = s.Name, Description = s.Description, SalaryGradeId = s.SalaryGradeId,
            DepartmentId = s.DepartmentId, PositionId = s.PositionId, BasicSalary = s.BasicSalary,
            EffectiveFrom = s.EffectiveFrom, EffectiveTo = s.EffectiveTo,
            Lines = s.Components.Select(c => new StructureLineInput
            {
                SalaryComponentId = c.SalaryComponentId, AmountType = c.AmountType, Amount = c.Amount
            }).ToList()
        };
    }

    public async Task<int> CreateAsync(SalaryStructureInput input, CancellationToken ct = default)
    {
        var structure = new SalaryStructure { Status = SalaryStructureStatus.Draft };
        await ApplyAsync(structure, input, null, ct);
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync(ct);
        return structure.Id;
    }

    /// <summary>
    /// Updates the template only. Employees already on it keep their approved amounts until a
    /// revision is proposed and approved, so payroll history is never changed retroactively.
    /// </summary>
    public async Task UpdateAsync(int id, SalaryStructureInput input, CancellationToken ct = default)
    {
        var structure = await db.SalaryStructures.Include(s => s.Components)
            .SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Salary structure", id);
        if (structure.Status == SalaryStructureStatus.Archived)
            throw new DomainException("Archived structures cannot be edited.");

        await ApplyAsync(structure, input, id, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(int id, SalaryStructureStatus status, CancellationToken ct = default)
    {
        var structure = await db.SalaryStructures.Include(s => s.Components)
            .SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Salary structure", id);
        if (status == SalaryStructureStatus.Active && structure.BasicSalary <= 0)
            throw new DomainException("Set a basic salary before activating the structure.");
        if (structure.Status == SalaryStructureStatus.Archived && status != SalaryStructureStatus.Archived)
            throw new DomainException("Archived structures cannot be reactivated. Create a new structure instead.");
        structure.Status = status;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Proposes a % change for every employee currently on the structure (pending approval).
    /// Employees who already have a change awaiting approval are skipped and reported.
    /// </summary>
    public async Task<StructureRevisionResult> ProposeRevisionAsync(int id, StructureRevisionInput input, CancellationToken ct = default)
    {
        var structure = await db.SalaryStructures.Include(s => s.Components).ThenInclude(c => c.SalaryComponent)
            .SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Salary structure", id);
        if (structure.Status != SalaryStructureStatus.Active)
            throw new DomainException("Only active structures can be revised.");

        var details = await GetAsync(id, ct);
        if (details.Employees.Count == 0)
            throw new DomainException("No employees are currently on this structure.");

        var factor = 1 + input.IncreasePercent / 100m;
        var effective = input.EffectiveFrom!.Value;
        var created = 0;
        var employeeIds = details.Employees.Select(e => e.EmployeeId).ToList();
        var withPending = (await db.EmployeeSalaries
            .Where(r => employeeIds.Contains(r.EmployeeId) && r.Status == SalaryRevisionStatus.PendingApproval)
            .Select(r => r.EmployeeId).ToListAsync(ct)).ToHashSet();
        var skipped = details.Employees.Where(e => withPending.Contains(e.EmployeeId)).Select(e => e.Name).ToList();
        if (skipped.Count == details.Employees.Count)
            throw new DomainException("Every employee on this structure already has a salary change awaiting approval.");

        foreach (var employee in details.Employees.Where(e => !withPending.Contains(e.EmployeeId)))
        {
            var newBasic = Money.Round(employee.BasicSalary * factor);
            var sized = new SalaryStructure { BasicSalary = newBasic };
            var change = new SalaryChangeInput
            {
                EmployeeId = employee.EmployeeId,
                SalaryStructureId = structure.Id,
                BasicSalary = newBasic,
                EffectiveFrom = effective,
                Reason = input.Reason,
                Components = structure.Components.Where(c => c.SalaryComponent.IsEarning)
                    .Select(c => new ComponentAmountInput { SalaryComponentId = c.SalaryComponentId, Amount = sized.ResolveAmount(c) })
                    .ToList()
            };
            await salaryChanges.ProposeCoreAsync(change, notify: false, ct);
            created++;
        }

        // The structure itself moves to the revised basic for future assignments.
        structure.BasicSalary = Money.Round(structure.BasicSalary * factor);
        audit.Record("Salary.StructureRevisionProposed", nameof(SalaryStructure), id,
            $"{input.IncreasePercent:+0.##;-0.##}% for {created} employees from {effective:yyyy-MM-dd}: {input.Reason}");
        await notifications.NotifyPermissionHoldersAsync(Permissions.Salary.Approve, "Salary revisions awaiting approval",
            $"{created} salary revisions on '{structure.Name}' need approval.", NotificationType.Warning, "/SalaryStructures/Approvals", ct);
        await db.SaveChangesAsync(ct);
        return new StructureRevisionResult(created, skipped);
    }

    // ------------------------------------------------------------- Grades

    public async Task<IReadOnlyList<SalaryGradeDto>> ListGradesAsync(CancellationToken ct = default) =>
        await db.SalaryGrades.AsNoTracking()
            .OrderBy(g => g.MinimumSalary)
            .Select(g => new SalaryGradeDto(g.Id, g.Code, g.Name, g.Description, g.MinimumSalary, g.MaximumSalary,
                db.SalaryStructures.Count(s => s.SalaryGradeId == g.Id)))
            .ToListAsync(ct);

    public async Task<int> SaveGradeAsync(int? id, SalaryGradeInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.SalaryGrades.IgnoreQueryFilters().AnyAsync(g => g.Code == code && g.Id != id, ct))
            throw new ConflictException($"Grade code {code} already exists.");

        var grade = id is null
            ? db.SalaryGrades.Add(new SalaryGrade()).Entity
            : await db.SalaryGrades.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Salary grade", id);

        grade.Code = code;
        grade.Name = input.Name.Trim();
        grade.Description = input.Description?.Trim();
        grade.MinimumSalary = Money.Round(input.MinimumSalary);
        grade.MaximumSalary = Money.Round(input.MaximumSalary);
        await db.SaveChangesAsync(ct);
        return grade.Id;
    }

    // ------------------------------------------------------------- Helpers

    private async Task ApplyAsync(SalaryStructure structure, SalaryStructureInput input, int? id, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.SalaryStructures.IgnoreQueryFilters().AnyAsync(s => s.Code == code && s.Id != id, ct))
            throw new ConflictException($"Structure code {code} already exists.");

        if (input.SalaryGradeId is int gradeId)
        {
            var grade = await db.SalaryGrades.SingleOrDefaultAsync(g => g.Id == gradeId, ct)
                        ?? throw new DomainException("The selected grade does not exist.");
            if (!grade.IsWithinBand(input.BasicSalary))
                throw new DomainException($"Basic salary must be within the {grade.Name} band ({Money.Format(grade.MinimumSalary)} – {Money.Format(grade.MaximumSalary)}).");
        }

        if (input.PositionId is int positionId && input.DepartmentId is int departmentId
            && await db.Positions.AnyAsync(p => p.Id == positionId && p.DepartmentId != null && p.DepartmentId != departmentId, ct))
            throw new DomainException("The selected job title belongs to a different department.");

        var componentIds = input.Lines.Select(l => l.SalaryComponentId).ToList();
        var validComponents = await db.SalaryComponents
            .Where(c => componentIds.Contains(c.Id) && c.Type == ComponentType.Earning && c.IsRecurring && c.IsActive)
            .Select(c => c.Id).ToListAsync(ct);
        if (validComponents.Count != componentIds.Distinct().Count())
            throw new DomainException("Structures can only contain active recurring earnings (allowances).");

        structure.Code = code;
        structure.Name = input.Name.Trim();
        structure.Description = input.Description?.Trim();
        structure.SalaryGradeId = input.SalaryGradeId;
        structure.DepartmentId = input.DepartmentId;
        structure.PositionId = input.PositionId;
        structure.BasicSalary = Money.Round(input.BasicSalary);
        structure.EffectiveFrom = input.EffectiveFrom!.Value;
        structure.EffectiveTo = input.EffectiveTo;

        structure.Components.Clear();
        foreach (var line in input.Lines)
            structure.Components.Add(new SalaryStructureComponent
            {
                SalaryComponentId = line.SalaryComponentId,
                AmountType = line.AmountType,
                Amount = line.AmountType == AmountType.Fixed ? Money.Round(line.Amount) : line.Amount
            });
    }

    private static decimal Gross(SalaryStructure s) =>
        Money.Round(s.BasicSalary + s.Components.Sum(s.ResolveAmount));

    private async Task<Dictionary<int, int>> CurrentEmployeeCountsAsync(CancellationToken ct)
    {
        var today = Today;
        return await db.EmployeeSalaries.AsNoTracking()
            .Where(r => r.SalaryStructureId != null
                        && (r.Status == SalaryRevisionStatus.Approved || r.Status == SalaryRevisionStatus.Superseded)
                        && r.EffectiveFrom <= today && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .GroupBy(r => r.SalaryStructureId!.Value)
            .Select(g => new { g.Key, Count = g.Select(r => r.EmployeeId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}
