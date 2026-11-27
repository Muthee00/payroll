using PayMaster.Application.Common.Models;
using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;

namespace PayMaster.Web.ViewModels.Payroll;

public class PayrollIndexViewModel
{
    public required IReadOnlyList<PeriodOption> Periods { get; init; }
    public required PeriodOption Selected { get; init; }
    public required PeriodSummary Summary { get; init; }
    public PagedResult<PayrollLineListItem>? Lines { get; init; }
    public PayrollLineQuery LineQuery { get; init; } = new();
    public IReadOnlyList<PayrollRunListItem> RecentRuns { get; init; } = [];
    public IReadOnlyList<LookupItem> Departments { get; init; } = [];
    public IReadOnlyList<(int Id, string Name)> Employees { get; init; } = [];
}

public class PayrollRunViewModel
{
    public required PayrollRunDetails Details { get; init; }
    public PayrollLineQuery LineQuery { get; init; } = new();
}

public class PayrollValidationViewModel
{
    public required PayrollRunListItem Run { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
}

public class PayrollHistoryViewModel
{
    public required PayrollRunQuery Query { get; init; }
    public required PagedResult<PayrollRunListItem> Runs { get; init; }
    public IReadOnlyList<PayrollTrendPoint> Trend { get; init; } = [];
}

public class PayrollCompareViewModel
{
    public IReadOnlyList<PayrollRunListItem> Runs { get; init; } = [];
    public int? A { get; init; }
    public int? B { get; init; }
    public RunComparison? Comparison { get; init; }
}

public class EmployeePayrollHistoryViewModel
{
    public required EmployeeDetailsDto Employee { get; init; }
    public IReadOnlyList<EmployeePayrollHistoryRow> Rows { get; init; } = [];
}
