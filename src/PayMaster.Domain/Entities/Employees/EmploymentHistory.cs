using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Employees;

/// <summary>Append-only record of employment changes (hire, transfer, promotion, termination).</summary>
public class EmploymentHistory : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public EmploymentChangeType ChangeType { get; set; }
    public DateOnly EffectiveDate { get; set; }

    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromPosition { get; set; }
    public string? ToPosition { get; set; }
    public EmploymentStatus? FromStatus { get; set; }
    public EmploymentStatus? ToStatus { get; set; }
    public string? Notes { get; set; }
}
