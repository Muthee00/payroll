using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Payroll;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Reports;

/// <summary>
/// Payroll and statutory reports. Only finalized or paid payroll is reported, so figures match
/// payslips and statutory returns exactly.
/// </summary>
public sealed class PayrollReportService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{

    public async Task<ReportResult> RunAsync(string key, ReportFilter filter, CancellationToken ct = default)
    {
        var (from, to) = Range(filter);
        var lines = await LoadLinesAsync(from, to, filter, ct);
        var subtitle = $"{Label(from)}{(from == to ? "" : $" – {Label(to)}")} · finalized and paid payroll";

        return key switch
        {
            "payroll-summary" => Summary(lines, subtitle),
            "department" => Department(lines, subtitle),
            "employee" => EmployeeTotals(lines, subtitle),
            "gross" => Gross(lines, subtitle),
            "net" => Net(lines, subtitle),
            "paye" => Paye(lines, subtitle),
            "sha" => Sha(lines, subtitle),
            "housing-levy" => HousingLevy(lines, subtitle),
            "nssf" => Nssf(lines, subtitle),
            "employer-contributions" => Employer(lines, subtitle),
            "deductions" => await DeductionsAsync(lines, subtitle, ct),
            "variance" => await VarianceAsync(to, filter, ct),
            "reconciliation" => Reconciliation(lines, subtitle),
            _ => throw new DomainException($"Unknown report '{key}'.")
        };
    }

    // ------------------------------------------------------------------ data

    private sealed record Line(
        int Id, int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName, string? KraPin, string? NssfNumber,
        string? ShaNumber, string? NationalId, PaymentMethod PaymentMethod, string? BankName, string? BankAccount, string? Mobile,
        int Year, int Month, string PeriodName, string RunReference, decimal Basic, decimal Gross, decimal Taxable, decimal NonTaxable,
        decimal Pensionable, decimal Tier1, decimal Tier2, decimal Nssf, decimal Sha, decimal Levy, decimal Allowable, decimal TaxableIncome,
        decimal TaxBefore, decimal PersonalRelief, decimal InsuranceRelief, decimal Paye, decimal Other, decimal TotalDeductions, decimal Net,
        decimal EmployerNssf, decimal EmployerLevy);

    private async Task<List<Line>> LoadLinesAsync(DateOnly from, DateOnly to, ReportFilter filter, CancellationToken ct)
    {
        var query = db.PayrollEmployees.AsNoTracking()
            .Where(l => (l.PayrollRun.Status == PayrollRunStatus.Finalized || l.PayrollRun.Status == PayrollRunStatus.Paid)
                        && l.Status == PayrollLineStatus.Calculated
                        && l.PayrollRun.PayrollPeriod.StartDate >= from && l.PayrollRun.PayrollPeriod.StartDate <= to);
        if (filter.DepartmentId is int departmentId)
            query = query.Where(l => l.Employee.DepartmentId == departmentId);
        if (filter.EmployeeId is int employeeId)
            query = query.Where(l => l.EmployeeId == employeeId);

        return await query
            .OrderBy(l => l.PayrollRun.PayrollPeriod.StartDate).ThenBy(l => l.EmployeeName)
            .Select(l => new Line(l.Id, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.DepartmentName, l.KraPin, l.NssfNumber, l.ShaNumber,
                l.NationalId, l.PaymentMethod, l.BankName, l.BankAccountNumber, l.MobileMoneyNumber,
                l.PayrollRun.PayrollPeriod.Year, l.PayrollRun.PayrollPeriod.Month, l.PayrollRun.PayrollPeriod.Name, l.PayrollRun.Reference,
                l.BasicSalary, l.GrossEarnings, l.TaxableEarnings, l.NonTaxableEarnings, l.PensionablePay, l.NssfTier1, l.NssfTier2, l.Nssf,
                l.Sha, l.HousingLevy, l.AllowableDeductions, l.TaxableIncome, l.TaxBeforeReliefs, l.PersonalRelief, l.InsuranceRelief, l.Paye,
                l.OtherDeductions, l.TotalDeductions, l.NetPay, l.EmployerNssf, l.EmployerHousingLevy))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ reports

    private static ReportResult Summary(List<Line> lines, string subtitle) => new("payroll-summary", "Monthly payroll summary", subtitle,
        [
            new("Period"), new("Employees", ReportColumnKind.Number), new("Gross pay", ReportColumnKind.Money, true),
            new("PAYE", ReportColumnKind.Money, true), new("SHIF", ReportColumnKind.Money, true), new("Housing Levy", ReportColumnKind.Money, true),
            new("NSSF", ReportColumnKind.Money, true), new("Other deductions", ReportColumnKind.Money, true),
            new("Net pay", ReportColumnKind.Money, true), new("Employer contributions", ReportColumnKind.Money, true),
            new("Total employer cost", ReportColumnKind.Money, true)
        ],
        lines.GroupBy(l => (l.Year, l.Month, l.PeriodName)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => Row(g.Key.PeriodName, g.Select(l => l.EmployeeId).Distinct().Count(), g.Sum(l => l.Gross), g.Sum(l => l.Paye),
                g.Sum(l => l.Sha), g.Sum(l => l.Levy), g.Sum(l => l.Nssf), g.Sum(l => l.Other), g.Sum(l => l.Net),
                g.Sum(l => l.EmployerNssf + l.EmployerLevy), g.Sum(l => l.Gross + l.EmployerNssf + l.EmployerLevy)))
            .ToList(), []);

    private static ReportResult Department(List<Line> lines, string subtitle) => new("department", "Department payroll", subtitle,
        [
            new("Department"), new("Employees", ReportColumnKind.Number), new("Gross pay", ReportColumnKind.Money, true),
            new("Statutory deductions", ReportColumnKind.Money, true), new("Other deductions", ReportColumnKind.Money, true),
            new("Net pay", ReportColumnKind.Money, true), new("Employer contributions", ReportColumnKind.Money, true),
            new("Share of gross", ReportColumnKind.Percent)
        ],
        lines.GroupBy(l => l.DepartmentName).OrderByDescending(g => g.Sum(l => l.Gross))
            .Select(g => Row(g.Key, g.Select(l => l.EmployeeId).Distinct().Count(), g.Sum(l => l.Gross),
                g.Sum(l => l.Paye + l.Sha + l.Levy + l.Nssf), g.Sum(l => l.Other), g.Sum(l => l.Net), g.Sum(l => l.EmployerNssf + l.EmployerLevy),
                Share(g.Sum(l => l.Gross), lines.Sum(l => l.Gross))))
            .ToList(), []);

    private static ReportResult EmployeeTotals(List<Line> lines, string subtitle) => new("employee", "Employee payroll", subtitle,
        [
            new("Employee No."), new("Employee"), new("Department"), new("Months", ReportColumnKind.Number),
            new("Gross pay", ReportColumnKind.Money, true), new("PAYE", ReportColumnKind.Money, true), new("SHIF", ReportColumnKind.Money, true),
            new("Housing Levy", ReportColumnKind.Money, true), new("NSSF", ReportColumnKind.Money, true),
            new("Other deductions", ReportColumnKind.Money, true), new("Net pay", ReportColumnKind.Money, true)
        ],
        ByEmployee(lines).Select(g => Row(g.First().EmployeeNumber, g.First().EmployeeName, g.Last().DepartmentName, g.Count(),
            g.Sum(l => l.Gross), g.Sum(l => l.Paye), g.Sum(l => l.Sha), g.Sum(l => l.Levy), g.Sum(l => l.Nssf), g.Sum(l => l.Other),
            g.Sum(l => l.Net))).ToList(), []);

    private static ReportResult Gross(List<Line> lines, string subtitle) => new("gross", "Gross salary report", subtitle,
        [
            new("Period"), new("Employee No."), new("Employee"), new("Department"), new("Basic", ReportColumnKind.Money, true),
            new("Allowances & other pay", ReportColumnKind.Money, true), new("Non-taxable", ReportColumnKind.Money, true),
            new("Gross pay", ReportColumnKind.Money, true)
        ],
        lines.Select(l => Row(l.PeriodName, l.EmployeeNumber, l.EmployeeName, l.DepartmentName, l.Basic, l.Gross - l.Basic - l.NonTaxable,
            l.NonTaxable, l.Gross)).ToList(), []);

    private ReportResult Net(List<Line> lines, string subtitle)
    {
        var showBank = currentUser.HasPermission(Permissions.Employees.ViewBankDetails);
        return new("net", "Net salary & payments", subtitle,
            [
                new("Period"), new("Employee No."), new("Employee"), new("Department"), new("Payment method"), new("Bank"),
                new("Account / mobile"), new("Gross pay", ReportColumnKind.Money, true), new("Deductions", ReportColumnKind.Money, true),
                new("Net pay", ReportColumnKind.Money, true)
            ],
            lines.Select(l =>
            {
                var account = l.PaymentMethod == PaymentMethod.MobileMoney ? l.Mobile : l.BankAccount;
                return Row(l.PeriodName, l.EmployeeNumber, l.EmployeeName, l.DepartmentName, l.PaymentMethod.ToString(), l.BankName,
                    showBank ? account : Mask(account), l.Gross, l.TotalDeductions, l.Net);
            }).ToList(),
            showBank ? [] : ["Account numbers are masked. Users with bank-detail permission see full numbers."]);
    }

    private static ReportResult Paye(List<Line> lines, string subtitle) => new("paye", "PAYE report", subtitle,
        [
            new("Period"), new("KRA PIN"), new("Employee"), new("Taxable earnings", ReportColumnKind.Money, true),
            new("Allowable deductions", ReportColumnKind.Money, true), new("Taxable income", ReportColumnKind.Money, true),
            new("Tax charged", ReportColumnKind.Money, true), new("Personal relief", ReportColumnKind.Money, true),
            new("Insurance relief", ReportColumnKind.Money, true), new("PAYE", ReportColumnKind.Money, true)
        ],
        lines.Select(l => Row(l.PeriodName, l.KraPin ?? "MISSING", l.EmployeeName, l.Taxable, l.Allowable, l.TaxableIncome, l.TaxBefore,
            l.PersonalRelief, l.InsuranceRelief, l.Paye)).ToList(),
        lines.Any(l => l.KraPin is null) ? ["Employees without a KRA PIN are marked MISSING and must be corrected before filing."] : []);

    private static ReportResult Sha(List<Line> lines, string subtitle) => new("sha", "SHIF report", subtitle,
        [
            new("Period"), new("SHA No."), new("National ID"), new("Employee"), new("Gross salary", ReportColumnKind.Money, true),
            new("SHIF contribution", ReportColumnKind.Money, true)
        ],
        lines.Select(l => Row(l.PeriodName, l.ShaNumber ?? "MISSING", l.NationalId, l.EmployeeName, l.Taxable, l.Sha)).ToList(), []);

    private static ReportResult HousingLevy(List<Line> lines, string subtitle) => new("housing-levy", "Affordable Housing Levy report", subtitle,
        [
            new("Period"), new("KRA PIN"), new("National ID"), new("Employee"), new("Gross salary", ReportColumnKind.Money, true),
            new("Employee levy", ReportColumnKind.Money, true), new("Employer levy", ReportColumnKind.Money, true),
            new("Total remittance", ReportColumnKind.Money, true)
        ],
        lines.Select(l => Row(l.PeriodName, l.KraPin ?? "MISSING", l.NationalId, l.EmployeeName, l.Taxable, l.Levy, l.EmployerLevy,
            l.Levy + l.EmployerLevy)).ToList(), []);

    private static ReportResult Nssf(List<Line> lines, string subtitle) => new("nssf", "NSSF report", subtitle,
        [
            new("Period"), new("NSSF No."), new("National ID"), new("Employee"), new("Pensionable pay", ReportColumnKind.Money, true),
            new("Tier I (employee)", ReportColumnKind.Money, true), new("Tier II (employee)", ReportColumnKind.Money, true),
            new("Employee total", ReportColumnKind.Money, true), new("Employer total", ReportColumnKind.Money, true),
            new("Total remittance", ReportColumnKind.Money, true)
        ],
        lines.Select(l => Row(l.PeriodName, l.NssfNumber ?? "MISSING", l.NationalId, l.EmployeeName, l.Pensionable, l.Tier1, l.Tier2,
            l.Nssf, l.EmployerNssf, l.Nssf + l.EmployerNssf)).ToList(), []);

    private static ReportResult Employer(List<Line> lines, string subtitle) => new("employer-contributions", "Employer contributions", subtitle,
        [
            new("Employee No."), new("Employee"), new("Department"), new("Gross pay", ReportColumnKind.Money, true),
            new("Employer NSSF", ReportColumnKind.Money, true), new("Employer Housing Levy", ReportColumnKind.Money, true),
            new("Total employer contributions", ReportColumnKind.Money, true), new("Total cost", ReportColumnKind.Money, true)
        ],
        ByEmployee(lines).Select(g => Row(g.First().EmployeeNumber, g.First().EmployeeName, g.Last().DepartmentName, g.Sum(l => l.Gross),
            g.Sum(l => l.EmployerNssf), g.Sum(l => l.EmployerLevy), g.Sum(l => l.EmployerNssf + l.EmployerLevy),
            g.Sum(l => l.Gross + l.EmployerNssf + l.EmployerLevy))).ToList(), []);

    private async Task<ReportResult> DeductionsAsync(List<Line> lines, string subtitle, CancellationToken ct)
    {
        var ids = lines.Select(l => l.Id).ToList();
        var byLine = lines.ToDictionary(l => l.Id);
        var deductions = await db.PayrollDeductions.AsNoTracking()
            .Where(d => ids.Contains(d.PayrollEmployeeId) && d.Kind == DeductionKind.Voluntary)
            .Select(d => new { d.PayrollEmployeeId, d.Name, d.Category, d.Amount })
            .ToListAsync(ct);
        return new("deductions", "Deductions report", subtitle,
            [new("Period"), new("Employee No."), new("Employee"), new("Deduction"), new("Type"), new("Amount", ReportColumnKind.Money, true)],
            deductions.Select(d => (Line: byLine[d.PayrollEmployeeId], d))
                .OrderBy(x => x.Line.Year).ThenBy(x => x.Line.Month).ThenBy(x => x.d.Name).ThenBy(x => x.Line.EmployeeName)
                .Select(x => Row(x.Line.PeriodName, x.Line.EmployeeNumber, x.Line.EmployeeName, x.d.Name,
                    x.d.Category?.ToString() ?? "", x.d.Amount))
                .ToList(), []);
    }

    private async Task<ReportResult> VarianceAsync(DateOnly month, ReportFilter filter, CancellationToken ct)
    {
        var previous = month.AddMonths(-1);
        var current = await LoadLinesAsync(month, month, filter, ct);
        var prior = await LoadLinesAsync(previous, previous, filter, ct);
        var employees = current.Select(l => l.EmployeeId).Union(prior.Select(l => l.EmployeeId)).Distinct();

        var rows = employees.Select(id =>
            {
                var a = prior.Where(l => l.EmployeeId == id).ToList();
                var b = current.Where(l => l.EmployeeId == id).ToList();
                var any = b.FirstOrDefault() ?? a.First();
                decimal ga = a.Sum(l => l.Gross), gb = b.Sum(l => l.Gross), na = a.Sum(l => l.Net), nb = b.Sum(l => l.Net);
                var note = a.Count == 0 ? "New this month" : b.Count == 0 ? "Not paid this month" : "";
                return (Change: Math.Abs(nb - na), Row: Row(any.EmployeeNumber, any.EmployeeName, any.DepartmentName, ga, gb, gb - ga, na, nb, nb - na,
                    na == 0 ? null : (object)Math.Round((nb - na) / na, 4), note));
            })
            .Where(x => x.Change != 0)
            .OrderByDescending(x => x.Change)
            .Select(x => x.Row)
            .ToList();

        return new("variance", "Payroll variance", $"{Label(month)} compared with {Label(previous)}",
            [
                new("Employee No."), new("Employee"), new("Department"), new($"Gross {previous:MMM yy}", ReportColumnKind.Money, true),
                new($"Gross {month:MMM yy}", ReportColumnKind.Money, true), new("Gross change", ReportColumnKind.Money, true),
                new($"Net {previous:MMM yy}", ReportColumnKind.Money, true), new($"Net {month:MMM yy}", ReportColumnKind.Money, true),
                new("Net change", ReportColumnKind.Money, true), new("Net change %", ReportColumnKind.Percent), new("Note")
            ], rows, ["Only employees whose net pay changed are listed. The comparison uses the 'To' month and the month before it."]);
    }

    private static ReportResult Reconciliation(List<Line> lines, string subtitle)
    {
        var rows = new List<object?[]>();
        foreach (var g in lines.GroupBy(l => (l.Year, l.Month, l.PeriodName)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
        {
            decimal gross = g.Sum(l => l.Gross), deductions = g.Sum(l => l.TotalDeductions), net = g.Sum(l => l.Net);
            var difference = gross - deductions - net;
            rows.Add(Row(g.Key.PeriodName, gross, deductions, net, difference,
                g.Sum(l => l.Paye), g.Sum(l => l.Sha), g.Sum(l => l.Nssf + l.EmployerNssf), g.Sum(l => l.Levy + l.EmployerLevy),
                g.Sum(l => l.Other), difference == 0 ? "Balanced" : "CHECK"));
        }
        return new("reconciliation", "Payroll reconciliation", subtitle,
            [
                new("Period"), new("Gross pay", ReportColumnKind.Money, true), new("Total deductions", ReportColumnKind.Money, true),
                new("Net pay", ReportColumnKind.Money, true), new("Difference", ReportColumnKind.Money, true),
                new("Payable to KRA (PAYE)", ReportColumnKind.Money, true), new("Payable to SHA", ReportColumnKind.Money, true),
                new("Payable to NSSF (both shares)", ReportColumnKind.Money, true), new("Payable to Housing Fund (both shares)", ReportColumnKind.Money, true),
                new("Other deductions (to third parties)", ReportColumnKind.Money, true), new("Check")
            ], rows,
            ["Difference must be zero: gross pay − deductions = net pay. Remittances are due by the 9th of the following month."]);
    }

    // ------------------------------------------------------------------ helpers

    public (DateOnly From, DateOnly To) Range(ReportFilter filter)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var defaultMonth = new DateOnly(today.Year, today.Month, 1);
        var to = PayrollPeriodService.TryParse(filter.To, out var ty, out var tm) ? new DateOnly(ty, tm, 1) : defaultMonth;
        var from = PayrollPeriodService.TryParse(filter.From, out var fy, out var fm) ? new DateOnly(fy, fm, 1) : to;
        if (from > to)
            (from, to) = (to, from);
        if (from < to.AddMonths(-35))
            throw new DomainException("Reports can cover at most 36 months.");
        return (from, to);
    }

    private static IEnumerable<IGrouping<int, Line>> ByEmployee(List<Line> lines) =>
        lines.GroupBy(l => l.EmployeeId).OrderBy(g => g.First().EmployeeName);

    private static object?[] Row(params object?[] values) => values;

    private static decimal? Share(decimal part, decimal total) => total == 0 ? null : Math.Round(part / total, 4);

    private static string Label(DateOnly month) => month.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static string? Mask(string? value) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= 4 ? "****" : $"••••{value[^4..]}";
}
