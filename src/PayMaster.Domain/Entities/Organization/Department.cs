using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;

namespace PayMaster.Domain.Entities.Organization;

public class Department : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Head of department; used to scope the Manager role.</summary>
    public int? HeadEmployeeId { get; set; }
    public Employee? HeadEmployee { get; set; }

    public ICollection<Position> Positions { get; set; } = [];
    public ICollection<Employee> Employees { get; set; } = [];
}
