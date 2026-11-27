using System.Globalization;
using PayMaster.Application.Common.Security;

namespace PayMaster.Application.Reports;

public enum ReportColumnKind
{
    Text,
    Money,
    Number,
    Percent,
    Date
}

public record ReportColumn(string Header, ReportColumnKind Kind = ReportColumnKind.Text, bool Total = false);

public record ReportFilter
{
    /// <summary>First month included (yyyy-MM).</summary>
    public string? From { get; init; }
    /// <summary>Last month included (yyyy-MM).</summary>
    public string? To { get; init; }
    public int? DepartmentId { get; init; }
    public int? EmployeeId { get; init; }
}

public record ReportResult(
    string Key,
    string Title,
    string Subtitle,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<object?[]> Rows,
    IReadOnlyList<string> Notes)
{
    /// <summary>Column totals for columns flagged <see cref="ReportColumn.Total"/> (null elsewhere).</summary>
    public object?[] Totals => Columns.Select((c, i) => c.Total
        ? (object?)Rows.Sum(r => r[i] switch { decimal d => d, int n => n, _ => 0m })
        : null).ToArray();

    public bool HasTotals => Columns.Any(c => c.Total) && Rows.Count > 0;
}

public record ReportDefinition(string Key, string Title, string Description, string Category, string Icon, string Permission);

public static class ReportCatalog
{
    public static readonly IReadOnlyList<ReportDefinition> All =
    [
        new("payroll-summary", "Monthly payroll summary", "Totals per payroll month: gross, statutory deductions, net and employer cost.", "Payroll", "bi-calendar3", Permissions.Reports.Payroll),
        new("department", "Department payroll", "Gross, deductions and net pay by department.", "Payroll", "bi-diagram-3", Permissions.Reports.Payroll),
        new("employee", "Employee payroll", "Totals per employee for the selected months.", "Payroll", "bi-person-lines-fill", Permissions.Reports.Payroll),
        new("gross", "Gross salary", "Basic, allowances, variable pay and gross per employee.", "Payroll", "bi-cash-stack", Permissions.Reports.Payroll),
        new("net", "Net salary & payments", "Net pay with payment method and bank details (masked without permission).", "Payroll", "bi-bank2", Permissions.Reports.Payroll),
        new("paye", "PAYE report", "Taxable income, reliefs and PAYE per employee with KRA PINs.", "Statutory", "bi-receipt-cutoff", Permissions.Reports.Payroll),
        new("sha", "SHIF report", "SHIF contributions per employee with SHA numbers.", "Statutory", "bi-heart-pulse", Permissions.Reports.Payroll),
        new("housing-levy", "Housing Levy report", "Employee and employer Affordable Housing Levy per employee.", "Statutory", "bi-house", Permissions.Reports.Payroll),
        new("nssf", "NSSF report", "Tier I and Tier II contributions, employee and employer.", "Statutory", "bi-piggy-bank", Permissions.Reports.Payroll),
        new("employer-contributions", "Employer contributions", "Employer NSSF and Housing Levy cost per employee.", "Statutory", "bi-building", Permissions.Reports.Payroll),
        new("deductions", "Deductions report", "Loans, HELB, SACCO, insurance, pension and other deductions.", "Payroll", "bi-dash-circle", Permissions.Reports.Payroll),
        new("variance", "Payroll variance", "Change in gross and net pay per employee versus the previous month.", "Payroll", "bi-arrow-left-right", Permissions.Reports.Payroll),
        new("reconciliation", "Payroll reconciliation", "Gross-to-net reconciliation and amounts payable to each authority.", "Payroll", "bi-check2-square", Permissions.Reports.Payroll),

        new("performance-employee", "Employee performance", "Review scores and ratings per employee.", "Performance", "bi-person-check", Permissions.Reports.Performance),
        new("performance-department", "Department performance", "Average scores and review completion by department.", "Performance", "bi-diagram-3", Permissions.Reports.Performance),
        new("kpi-achievement", "KPI achievement", "Actual vs target for each KPI scored in reviews.", "Performance", "bi-speedometer2", Permissions.Reports.Performance),
        new("goal-completion", "Goal completion", "Goals by status and completion rate per department.", "Performance", "bi-bullseye", Permissions.Reports.Performance),
        new("review-status", "Performance reviews", "Review progress by stage for each cycle.", "Performance", "bi-clipboard-check", Permissions.Reports.Performance),
        new("development", "Training & development", "Development plans, training and improvement plans.", "Performance", "bi-mortarboard", Permissions.Reports.Performance)
    ];

    public static ReportDefinition? Find(string key) => All.FirstOrDefault(r => r.Key == key);
}

public interface IReportPdfRenderer
{
    byte[] Render(ReportResult report, string companyName);
}

public static class ReportFormat
{
    private static readonly CultureInfo Kenya = CultureInfo.GetCultureInfo("en-KE");

    public static string Cell(ReportColumn column, object? value) => value switch
    {
        null => "",
        decimal d when column.Kind == ReportColumnKind.Percent => (d * 100).ToString("0.0", Kenya) + "%",
        decimal d when column.Kind == ReportColumnKind.Number => d % 1 == 0 ? d.ToString("N0", Kenya) : d.ToString("N2", Kenya),
        decimal d => d.ToString("N2", Kenya),
        int n => n.ToString("N0", Kenya),
        DateOnly date => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}
