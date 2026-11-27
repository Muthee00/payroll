using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Payroll;

/// <summary>
/// Period-specific variable item for one employee: overtime, bonus, commission,
/// absenteeism deduction or a one-off deduction.
/// </summary>
public class PayrollInput : SoftDeletableEntity, IAuditTracked
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;
    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    /// <summary>Optional quantity × rate (e.g. 10 overtime hours × KSh 750).</summary>
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
