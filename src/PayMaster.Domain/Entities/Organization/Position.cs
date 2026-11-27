using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Salary;

namespace PayMaster.Domain.Entities.Organization;

/// <summary>Job title / role within a department.</summary>
public class Position : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? SalaryGradeId { get; set; }
    public SalaryGrade? SalaryGrade { get; set; }
}
