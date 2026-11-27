using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Domain.Entities.Salary;

/// <summary>
/// Reusable salary template (e.g. "Software Engineer"). Assigning it to an employee
/// copies resolved amounts into an <see cref="EmployeeSalary"/> revision, so later
/// changes to the template never alter existing salaries or payroll history.
/// </summary>
public class SalaryStructure : SoftDeletableEntity, IAuditTracked
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SalaryStructureStatus Status { get; set; } = SalaryStructureStatus.Draft;
    public decimal BasicSalary { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public int? SalaryGradeId { get; set; }
    public SalaryGrade? SalaryGrade { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public ICollection<SalaryStructureComponent> Components { get; set; } = [];

    public decimal ResolveAmount(SalaryStructureComponent line) => line.AmountType switch
    {
        AmountType.Fixed => Money.Round(line.Amount),
        AmountType.PercentageOfBasic => Money.Percent(BasicSalary, line.Amount / 100m),
        _ => throw new DomainException($"Unsupported amount type {line.AmountType}.")
    };
}

public class SalaryStructureComponent : BaseEntity
{
    public int SalaryStructureId { get; set; }
    public SalaryStructure SalaryStructure { get; set; } = null!;
    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public AmountType AmountType { get; set; } = AmountType.Fixed;

    /// <summary>KES amount when Fixed; percentage (e.g. 15 = 15%) when PercentageOfBasic.</summary>
    public decimal Amount { get; set; }
}
