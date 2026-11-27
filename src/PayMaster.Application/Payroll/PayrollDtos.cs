using PayMaster.Application.Common.Models;
using PayMaster.Application.Payroll.Calculation;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Payroll;

public class CreatePayrollRunInput
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollScope Scope { get; set; } = PayrollScope.AllActive;
    public int? DepartmentId { get; set; }
    public List<int> EmployeeIds { get; set; } = [];
    public string? Notes { get; set; }
}

public record CreateRunResult(int RunId, int EmployeeCount, IReadOnlyList<string> Skipped);

public record PayrollRunQuery : PageQuery
{
    public int? Year { get; init; }
    public PayrollRunStatus? Status { get; init; }
}

public record PayrollRunListItem(
    int Id, string Reference, int Year, int Month, string PeriodName, PayrollRunType RunType, PayrollScope Scope,
    string? DepartmentName, PayrollRunStatus Status, int EmployeeCount, int ErrorCount,
    decimal TotalGross, decimal TotalPaye, decimal TotalSha, decimal TotalHousingLevy, decimal TotalNssf,
    decimal TotalDeductions, decimal TotalNetPay, decimal TotalEmployerContributions,
    DateTime CreatedAt, DateTime? CalculatedAt, DateTime? ApprovedAt, DateTime? FinalizedAt,
    int? ProcessedById, int? ApprovedById, string? ProcessedBy = null, string? ApprovedBy = null);

public record PayrollLineQuery : PageQuery
{
    public PayrollLineStatus? Status { get; init; }
    public bool? WithWarnings { get; init; }
}

public record PayrollLineListItem(
    int Id, int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName, string? JobTitle,
    decimal BasicSalary, decimal GrossEarnings, decimal Paye, decimal Sha, decimal HousingLevy, decimal Nssf,
    decimal OtherDeductions, decimal TotalDeductions, decimal NetPay, PayrollLineStatus Status,
    string? Errors, string? Warnings, int DaysPayable, int DaysInPeriod);

public record PayrollRunDetails
{
    public required PayrollRunListItem Summary { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public DateOnly PaymentDate { get; init; }
    public int? StatutoryConfigurationId { get; init; }
    public string? StatutoryVersionName { get; init; }
    public string? StatusReason { get; init; }
    public string? Notes { get; init; }
    public int? OriginalRunId { get; init; }
    public string? OriginalRunReference { get; init; }
    public int? SubmittedById { get; init; }
    public int? CalculatedById { get; init; }
    public string? SubmittedBy { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public string? FinalizedBy { get; init; }
    public DateTime? PaidAt { get; init; }
    public int WarningCount { get; init; }
    public int PayslipCount { get; init; }
    public bool RequireSeparateApprover { get; init; }
    public required PagedResult<PayrollLineListItem> Lines { get; init; }
}

public record LineAmount(string Code, string Name, decimal Amount, string? Detail = null);

public record PayrollLineDetails(
    int Id, int RunId, string RunReference, PayrollRunStatus RunStatus, string PeriodName,
    int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName, string? JobTitle,
    string? KraPin, string? NssfNumber, string? ShaNumber, PaymentMethod PaymentMethod, string? BankName, string? MaskedAccount,
    int DaysPayable, int DaysInPeriod, PayrollLineStatus Status, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings,
    IReadOnlyList<LineAmount> Earnings, IReadOnlyList<LineAmount> Deductions, IReadOnlyList<LineAmount> EmployerContributions,
    decimal GrossEarnings, decimal TaxableEarnings, decimal NonTaxableEarnings, decimal AllowableDeductions, decimal TaxableIncome,
    decimal TaxBeforeReliefs, decimal PersonalRelief, decimal InsuranceRelief, decimal Paye,
    decimal NssfTier1, decimal NssfTier2, decimal Sha, decimal HousingLevy, decimal TotalDeductions, decimal NetPay,
    decimal TotalEmployerContributions, IReadOnlyList<BreakdownStep> Steps);

public record ValidationIssue(int EmployeeId, string EmployeeNumber, string EmployeeName, string Severity, string Message);

public record PeriodSummary(
    int Year, int Month, string PeriodName, int? RunId, string? RunReference, PayrollRunStatus? Status,
    int EmployeeCount, decimal TotalGross, decimal TotalDeductions, decimal TotalNetPay, decimal TotalPaye, decimal TotalSha,
    decimal TotalHousingLevy, decimal TotalNssf, decimal OtherDeductions, decimal EmployerContributions,
    int ActiveEmployees, int NewThisMonth);

public record PayrollTrendPoint(int Year, int Month, string Label, decimal Gross, decimal Deductions, decimal Net, int Employees);
