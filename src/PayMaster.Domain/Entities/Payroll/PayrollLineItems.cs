using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Payroll;

public class PayrollEarning : BaseEntity
{
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee PayrollEmployee { get; set; } = null!;
    public int? SalaryComponentId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ComponentCategory Category { get; set; }
    public TaxTreatment TaxTreatment { get; set; }
    public bool IsPensionable { get; set; }
    public decimal Amount { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Employee deduction (statutory or voluntary). Employer contributions are never stored here.</summary>
public class PayrollDeduction : BaseEntity
{
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee PayrollEmployee { get; set; } = null!;
    public int? SalaryComponentId { get; set; }

    /// <summary>Source recurring deduction (loan/HELB) whose balance is reduced on finalization.</summary>
    public int? EmployeeDeductionId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeductionKind Kind { get; set; }
    public ComponentCategory? Category { get; set; }
    public TaxTreatment? TaxTreatment { get; set; }
    public decimal Amount { get; set; }
    public int DisplayOrder { get; set; }
}

public class PayrollEmployerContribution : BaseEntity
{
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee PayrollEmployee { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
