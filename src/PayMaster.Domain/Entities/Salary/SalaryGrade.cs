using PayMaster.Domain.Common;

namespace PayMaster.Domain.Entities.Salary;

/// <summary>Salary grade with its permitted basic-salary band.</summary>
public class SalaryGrade : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinimumSalary { get; set; }
    public decimal MaximumSalary { get; set; }

    public bool IsWithinBand(decimal basicSalary) =>
        basicSalary >= MinimumSalary && basicSalary <= MaximumSalary;
}
