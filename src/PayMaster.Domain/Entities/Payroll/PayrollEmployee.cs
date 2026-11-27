using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Payroll;

/// <summary>
/// One employee's calculated payroll within a run. Employee details are snapshotted
/// so payslips and history do not change when the employee record changes later.
/// </summary>
public class PayrollEmployee : BaseEntity
{
    public int PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = null!;
    public int PayrollPeriodId { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// True while the owning run holds this employee/period; null once the run is cancelled or
    /// reversed. Unique index (PayrollPeriodId, EmployeeId, PeriodLock) blocks duplicate payroll
    /// at the database level (MySQL allows multiple NULLs in a unique index).
    /// </summary>
    public bool? PeriodLock { get; set; } = true;

    // Snapshot of employee data at calculation time
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? NationalId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? MobileMoneyNumber { get; set; }

    // Proration
    public int DaysInPeriod { get; set; }
    public int DaysPayable { get; set; }

    // Earnings
    public decimal BasicSalary { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TaxableEarnings { get; set; }
    public decimal NonTaxableEarnings { get; set; }
    public decimal PensionablePay { get; set; }

    // Statutory (employee)
    public decimal NssfTier1 { get; set; }
    public decimal NssfTier2 { get; set; }
    public decimal Nssf { get; set; }
    public decimal Sha { get; set; }
    public decimal HousingLevy { get; set; }

    // PAYE
    public decimal AllowableDeductions { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal TaxBeforeReliefs { get; set; }
    public decimal PersonalRelief { get; set; }
    public decimal InsuranceRelief { get; set; }
    public decimal HousingLevyRelief { get; set; }
    public decimal Paye { get; set; }

    // Totals
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    // Employer contributions (not deducted from the employee)
    public decimal EmployerNssf { get; set; }
    public decimal EmployerHousingLevy { get; set; }
    public decimal TotalEmployerContributions { get; set; }

    public PayrollLineStatus Status { get; set; } = PayrollLineStatus.Pending;

    /// <summary>Newline-separated blocking errors.</summary>
    public string? Errors { get; set; }

    /// <summary>Newline-separated non-blocking warnings (e.g. net pay below one third of basic).</summary>
    public string? Warnings { get; set; }

    /// <summary>JSON explanation of how each statutory figure was derived.</summary>
    public string? CalculationBreakdownJson { get; set; }

    public ICollection<PayrollEarning> Earnings { get; set; } = [];
    public ICollection<PayrollDeduction> Deductions { get; set; } = [];
    public ICollection<PayrollEmployerContribution> EmployerContributions { get; set; } = [];
    public Payslip? Payslip { get; set; }
}
