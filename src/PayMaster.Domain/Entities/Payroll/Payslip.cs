using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Payroll;

/// <summary>
/// Payslip issued for a finalized payroll line. The PDF is rendered from the immutable
/// <see cref="PayrollEmployee"/> snapshot, so it is identical every time it is produced.
/// </summary>
public class Payslip : AuditableEntity
{
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee PayrollEmployee { get; set; } = null!;
    public int EmployeeId { get; set; }

    public string PayslipNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }

    public PayslipEmailStatus EmailStatus { get; set; } = PayslipEmailStatus.NotSent;
    public DateTime? EmailedAt { get; set; }
    public string? EmailError { get; set; }
}
