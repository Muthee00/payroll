using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Payroll;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Salary;

/// <summary>Pay component catalogue, recurring employee deductions and per-period payroll inputs.</summary>
public sealed class PayItemService(IApplicationDbContext db, PayrollPeriodService periods, TimeProvider clock)
{
    // ------------------------------------------------------------- Component catalogue

    public async Task<IReadOnlyList<SalaryComponentDto>> ListComponentsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        await db.SalaryComponents.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Type).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new SalaryComponentDto(c.Id, c.Code, c.Name, c.Description, c.Type, c.Category, c.TaxTreatment,
                c.IsPensionable, c.IsRecurring, c.IsActive, c.DisplayOrder))
            .ToListAsync(ct);

    public async Task<SalaryComponentInput> GetComponentAsync(int id, CancellationToken ct = default)
    {
        var c = await db.SalaryComponents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Pay component", id);
        return new SalaryComponentInput
        {
            Code = c.Code, Name = c.Name, Description = c.Description, Type = c.Type, Category = c.Category,
            TaxTreatment = c.TaxTreatment, IsPensionable = c.IsPensionable, IsRecurring = c.IsRecurring, DisplayOrder = c.DisplayOrder
        };
    }

    public async Task<int> SaveComponentAsync(int? id, SalaryComponentInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.SalaryComponents.IgnoreQueryFilters().AnyAsync(c => c.Code == code && c.Id != id, ct))
            throw new ConflictException($"A pay component with code {code} already exists.");

        SalaryComponent component;
        if (id is null)
        {
            component = db.SalaryComponents.Add(new SalaryComponent()).Entity;
        }
        else
        {
            component = await db.SalaryComponents.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Pay component", id);
            if (await IsComponentUsedAsync(component.Id, ct) && (component.Type != input.Type || component.TaxTreatment != input.TaxTreatment))
                throw new DomainException(
                    "This component is already used in salaries or payroll. Its type and tax treatment cannot change; create a new component instead.");
        }

        component.Code = code;
        component.Name = input.Name.Trim();
        component.Description = input.Description?.Trim();
        component.Type = input.Type;
        component.Category = input.Category;
        component.TaxTreatment = input.TaxTreatment;
        component.IsPensionable = input.Type == ComponentType.Earning && input.IsPensionable;
        component.IsRecurring = input.IsRecurring;
        component.DisplayOrder = input.DisplayOrder;
        await db.SaveChangesAsync(ct);
        return component.Id;
    }

    public async Task SetComponentActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var component = await db.SalaryComponents.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Pay component", id);
        component.IsActive = active;
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> IsComponentUsedAsync(int id, CancellationToken ct) =>
        await db.EmployeeSalaryComponents.AnyAsync(c => c.SalaryComponentId == id, ct)
        || await db.EmployeeDeductions.IgnoreQueryFilters().AnyAsync(d => d.SalaryComponentId == id, ct)
        || await db.PayrollInputs.IgnoreQueryFilters().AnyAsync(i => i.SalaryComponentId == id, ct)
        || await db.PayrollEarnings.AnyAsync(e => e.SalaryComponentId == id, ct)
        || await db.PayrollDeductions.AnyAsync(d => d.SalaryComponentId == id, ct);

    // ------------------------------------------------------------- Recurring deductions

    public Task<PagedResult<RecurringDeductionListItem>> ListDeductionsAsync(RecurringDeductionQuery query, CancellationToken ct = default)
    {
        var q = db.EmployeeDeductions.AsNoTracking().AsQueryable();
        if (query.Active is bool active)
            q = q.Where(d => d.IsActive == active);
        if (query.SalaryComponentId is int componentId)
            q = q.Where(d => d.SalaryComponentId == componentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(d => d.Employee.FirstName.Contains(term) || d.Employee.LastName.Contains(term)
                             || d.Employee.EmployeeNumber.Contains(term) || (d.Reference != null && d.Reference.Contains(term)));
        }

        return q.OrderBy(d => d.Employee.FirstName).ThenBy(d => d.Employee.LastName).ThenBy(d => d.SalaryComponent.Name)
            .Select(d => new RecurringDeductionListItem(d.Id, d.EmployeeId, d.Employee.EmployeeNumber,
                d.Employee.FirstName + " " + d.Employee.LastName, d.SalaryComponent.Name, d.SalaryComponent.Category,
                d.MonthlyAmount, d.TotalAmount, d.AmountRecovered, d.StartDate, d.EndDate, d.Reference, d.IsActive))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<RecurringDeductionInput> GetDeductionAsync(int id, CancellationToken ct = default)
    {
        var d = await db.EmployeeDeductions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Deduction", id);
        return new RecurringDeductionInput
        {
            EmployeeId = d.EmployeeId, SalaryComponentId = d.SalaryComponentId, MonthlyAmount = d.MonthlyAmount,
            TotalAmount = d.TotalAmount, AmountRecovered = d.AmountRecovered, StartDate = d.StartDate, EndDate = d.EndDate,
            Reference = d.Reference
        };
    }

    public async Task<int> SaveDeductionAsync(int? id, RecurringDeductionInput input, CancellationToken ct = default)
    {
        var component = await db.SalaryComponents.SingleOrDefaultAsync(c => c.Id == input.SalaryComponentId, ct)
                        ?? throw new DomainException("Choose a valid deduction type.");
        if (component.Type != ComponentType.Deduction || component.TaxTreatment == TaxTreatment.ReducesGross)
            throw new DomainException($"{component.Name} cannot be used as a recurring deduction.");
        if (!await db.Employees.AnyAsync(e => e.Id == input.EmployeeId && e.Status != EmploymentStatus.Terminated, ct))
            throw new DomainException("Choose an active employee.");

        EmployeeDeduction deduction;
        if (id is null)
        {
            deduction = db.EmployeeDeductions.Add(new EmployeeDeduction { AmountRecovered = Money.Round(input.AmountRecovered) }).Entity;
        }
        else
        {
            deduction = await db.EmployeeDeductions.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Deduction", id);
            if (deduction.EmployeeId != input.EmployeeId)
                throw new DomainException("A deduction cannot be moved to another employee. Stop it and create a new one.");
            // AmountRecovered is maintained by payroll finalization and is not editable after creation.
        }

        deduction.EmployeeId = input.EmployeeId;
        deduction.SalaryComponentId = component.Id;
        deduction.MonthlyAmount = Money.Round(input.MonthlyAmount);
        deduction.TotalAmount = input.TotalAmount is decimal total ? Money.Round(total) : null;
        deduction.StartDate = input.StartDate!.Value;
        deduction.EndDate = input.EndDate;
        deduction.Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        deduction.IsActive = deduction.TotalAmount is null || deduction.AmountRecovered < deduction.TotalAmount;
        await db.SaveChangesAsync(ct);
        return deduction.Id;
    }

    public async Task StopDeductionAsync(int id, CancellationToken ct = default)
    {
        var deduction = await db.EmployeeDeductions.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Deduction", id);
        if (!deduction.IsActive)
            throw new DomainException("This deduction is already stopped.");
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        deduction.IsActive = false;
        deduction.EndDate = today < deduction.StartDate ? deduction.StartDate : today;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------- Period inputs

    public async Task<PeriodInputsSummary> ListInputsAsync(int year, int month, string? search, CancellationToken ct = default)
    {
        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        var option = PayrollPeriodService.Option(year, month);
        if (period is null)
            return new PeriodInputsSummary(year, month, option.Name, [], 0, 0);

        var locked = await LockedEmployeeIdsAsync(period.Id, ct);
        var q = db.PayrollInputs.AsNoTracking().Where(i => i.PayrollPeriodId == period.Id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(i => i.Employee.FirstName.Contains(term) || i.Employee.LastName.Contains(term) || i.Employee.EmployeeNumber.Contains(term));
        }

        var items = await q.OrderBy(i => i.Employee.FirstName).ThenBy(i => i.Employee.LastName)
            .Select(i => new PayrollInputListItem(i.Id, i.EmployeeId, i.Employee.EmployeeNumber,
                i.Employee.FirstName + " " + i.Employee.LastName, i.Employee.Department.Name, i.SalaryComponentId,
                i.SalaryComponent.Name, i.SalaryComponent.Type, i.Quantity, i.Rate, i.Amount, i.Description, false))
            .ToListAsync(ct);
        items = items.Select(i => i with { IsLocked = locked.Contains(i.EmployeeId) }).ToList();

        return new PeriodInputsSummary(year, month, period.Name, items,
            items.Where(i => i.Type == ComponentType.Earning).Sum(i => i.Amount),
            items.Where(i => i.Type == ComponentType.Deduction).Sum(i => i.Amount));
    }

    public async Task<int> SaveInputAsync(int? id, PayrollInputInput input, CancellationToken ct = default)
    {
        var component = await db.SalaryComponents.SingleOrDefaultAsync(c => c.Id == input.SalaryComponentId && c.IsActive, ct)
                        ?? throw new DomainException("Choose a valid item type.");
        if (component.IsRecurring && component.Type == ComponentType.Earning)
            throw new DomainException($"{component.Name} is a recurring allowance; change it through the employee's salary instead.");

        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
                       ?? throw new DomainException("Choose a valid employee.");
        var period = await periods.EnsureAsync(input.Year, input.Month, ct);
        if (employee.EmploymentDate > period.EndDate)
            throw new DomainException($"{employee.FullName} starts after {period.Name}.");
        if (employee.TerminationDate is DateOnly left && left < period.StartDate)
            throw new DomainException($"{employee.FullName} left before {period.Name}.");
        await EnsureEmployeeEditableAsync(period.Id, employee.Id, ct);

        PayrollInput item;
        if (id is null)
        {
            item = db.PayrollInputs.Add(new PayrollInput()).Entity;
        }
        else
        {
            item = await db.PayrollInputs.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Payroll input", id);
            if (item.PayrollPeriodId != period.Id || item.EmployeeId != employee.Id)
                await EnsureEmployeeEditableAsync(item.PayrollPeriodId, item.EmployeeId, ct);
        }

        item.EmployeeId = employee.Id;
        item.PayrollPeriodId = period.Id;
        item.SalaryComponentId = component.Id;
        item.Quantity = input.Quantity;
        item.Rate = input.Rate;
        item.Amount = input.Quantity is > 0 && input.Rate is > 0 && input.Amount is null or 0
            ? Money.Round(input.Quantity.Value * input.Rate.Value)
            : Money.Round(input.Amount ?? 0m);
        item.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (item.Amount <= 0)
            throw new DomainException("The amount must be greater than zero.");

        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task<PayrollInputInput> GetInputAsync(int id, CancellationToken ct = default)
    {
        var i = await db.PayrollInputs.AsNoTracking().Include(x => x.PayrollPeriod).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Payroll input", id);
        return new PayrollInputInput
        {
            EmployeeId = i.EmployeeId, Year = i.PayrollPeriod.Year, Month = i.PayrollPeriod.Month,
            SalaryComponentId = i.SalaryComponentId, Quantity = i.Quantity, Rate = i.Rate, Amount = i.Amount, Description = i.Description
        };
    }

    public async Task DeleteInputAsync(int id, CancellationToken ct = default)
    {
        var item = await db.PayrollInputs.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Payroll input", id);
        await EnsureEmployeeEditableAsync(item.PayrollPeriodId, item.EmployeeId, ct);
        db.PayrollInputs.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Inputs are frozen once the employee's payroll for the period has left Draft.</summary>
    private async Task EnsureEmployeeEditableAsync(int periodId, int employeeId, CancellationToken ct)
    {
        if (await db.PayrollEmployees.AnyAsync(p => p.PayrollPeriodId == periodId && p.EmployeeId == employeeId && p.PeriodLock == true
                                                    && p.PayrollRun.Status != PayrollRunStatus.Draft, ct))
            throw new DomainException("This employee's payroll for the period is already submitted or finalized; inputs can no longer change.");
    }

    private async Task<HashSet<int>> LockedEmployeeIdsAsync(int periodId, CancellationToken ct) =>
        (await db.PayrollEmployees
            .Where(p => p.PayrollPeriodId == periodId && p.PeriodLock == true && p.PayrollRun.Status != PayrollRunStatus.Draft)
            .Select(p => p.EmployeeId).ToListAsync(ct)).ToHashSet();

    // ------------------------------------------------------------- Lookups

    public async Task<IReadOnlyList<(int Id, string Name)>> ActiveEmployeesAsync(CancellationToken ct = default) =>
        (await db.Employees.AsNoTracking()
            .Where(e => e.Status != EmploymentStatus.Terminated)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName + " (" + e.EmployeeNumber + ")" })
            .ToListAsync(ct))
        .Select(e => (e.Id, e.Name)).ToList();
}
