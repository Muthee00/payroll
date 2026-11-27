using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;
using PayMaster.Application.Reports;

namespace PayMaster.Web.ViewModels.Reports;

public class ReportViewModel
{
    public required ReportDefinition Definition { get; init; }
    public required ReportResult Result { get; init; }
    public required ReportFilter Filter { get; init; }
    public IReadOnlyList<PeriodOption> Periods { get; init; } = [];
    public IReadOnlyList<LookupItem> Departments { get; init; } = [];
    public IReadOnlyList<LookupItem> Employees { get; init; } = [];
}
