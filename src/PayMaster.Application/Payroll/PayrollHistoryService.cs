using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Employees;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Payroll;

public record RunComparisonRow(string Label, decimal A, decimal B)
{
    public decimal Change => B - A;
    public decimal? ChangePercent => A == 0 ? null : Math.Round((B - A) / A * 100m, 1);
}

public record EmployeeVarianceRow(int EmployeeId, string EmployeeNumber, string EmployeeName, decimal? NetA, decimal? NetB)
{
    public decimal Change => (NetB ?? 0) - (NetA ?? 0);
}

public record RunComparison(PayrollRunListItem A, PayrollRunListItem B, IReadOnlyList<RunComparisonRow> Totals,
    IReadOnlyList<EmployeeVarianceRow> Employees);

public record EmployeePayrollHistoryRow(
    int RunId, string Reference, string PeriodName, int Year, int Month, PayrollRunStatus RunStatus,
    decimal GrossEarnings, decimal Paye, decimal Sha, decimal HousingLevy, decimal Nssf, decimal OtherDeductions,
    decimal TotalDeductions, decimal NetPay, int? PayslipId);

public sealed class PayrollHistoryService(IApplicationDbContext db, PayrollService payroll, EmployeeAccessScope scope,
    ISpreadsheetService spreadsheets, IAuditService audit)
{
    public async Task<byte[]> ExportAsync(PayrollRunQuery query, CancellationToken ct = default)
    {
        var rows = (await payroll.ListRunsAsync(query with { Page = 1, PageSize = Common.Models.PageQuery.MaxPageSize }, ct)).Items;
        audit.Record("Payroll.HistoryExported", "PayrollRun", null, $"{rows.Count} runs");
        await db.SaveChangesAsync(ct);
        return spreadsheets.Export("Payroll history", rows,
        [
            new("Period", r => r.PeriodName), new("Reference", r => r.Reference), new("Type", r => r.RunType),
            new("Employees", r => r.EmployeeCount), new("Gross", r => r.TotalGross, "#,##0.00"), new("PAYE", r => r.TotalPaye, "#,##0.00"),
            new("SHIF", r => r.TotalSha, "#,##0.00"), new("Housing Levy", r => r.TotalHousingLevy, "#,##0.00"),
            new("NSSF", r => r.TotalNssf, "#,##0.00"), new("Total Deductions", r => r.TotalDeductions, "#,##0.00"),
            new("Net Pay", r => r.TotalNetPay, "#,##0.00"), new("Employer Contributions", r => r.TotalEmployerContributions, "#,##0.00"),
            new("Status", r => r.Status), new("Processed By", r => r.ProcessedBy), new("Approved By", r => r.ApprovedBy),
            new("Calculated", r => r.CalculatedAt), new("Finalized", r => r.FinalizedAt)
        ], "Payroll history");
    }

    public async Task<RunComparison> CompareAsync(int runA, int runB, CancellationToken ct = default)
    {
        var a = (await payroll.GetRunAsync(runA, new PayrollLineQuery { PageSize = 1 }, ct)).Summary;
        var b = (await payroll.GetRunAsync(runB, new PayrollLineQuery { PageSize = 1 }, ct)).Summary;

        var totals = new List<RunComparisonRow>
        {
            new("Employees", a.EmployeeCount, b.EmployeeCount),
            new("Gross pay", a.TotalGross, b.TotalGross),
            new("PAYE", a.TotalPaye, b.TotalPaye),
            new("SHIF", a.TotalSha, b.TotalSha),
            new("Housing Levy", a.TotalHousingLevy, b.TotalHousingLevy),
            new("NSSF", a.TotalNssf, b.TotalNssf),
            new("Total deductions", a.TotalDeductions, b.TotalDeductions),
            new("Net pay", a.TotalNetPay, b.TotalNetPay),
            new("Employer contributions", a.TotalEmployerContributions, b.TotalEmployerContributions)
        };

        var lines = await db.PayrollEmployees.AsNoTracking()
            .Where(l => (l.PayrollRunId == runA || l.PayrollRunId == runB) && l.Status == PayrollLineStatus.Calculated)
            .Select(l => new { l.PayrollRunId, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.NetPay })
            .ToListAsync(ct);
        var employees = lines.GroupBy(l => l.EmployeeId)
            .Select(g => new EmployeeVarianceRow(g.Key, g.First().EmployeeNumber, g.First().EmployeeName,
                g.FirstOrDefault(x => x.PayrollRunId == runA)?.NetPay, g.FirstOrDefault(x => x.PayrollRunId == runB)?.NetPay))
            .Where(r => r.Change != 0)
            .OrderByDescending(r => Math.Abs(r.Change))
            .ToList();

        return new RunComparison(a, b, totals, employees);
    }

    public async Task<IReadOnlyList<EmployeePayrollHistoryRow>> EmployeeHistoryAsync(int employeeId, CancellationToken ct = default)
    {
        await scope.EnsureCanViewAsync(employeeId, ct);
        if (!scope.CanViewSalary)
            throw new ForbiddenAccessException("You do not have access to payroll information.");

        return await db.PayrollEmployees.AsNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.PayrollRun.Status != PayrollRunStatus.Cancelled)
            .OrderByDescending(l => l.PayrollRun.PayrollPeriod.Year).ThenByDescending(l => l.PayrollRun.PayrollPeriod.Month).ThenByDescending(l => l.PayrollRunId)
            .Select(l => new EmployeePayrollHistoryRow(l.PayrollRunId, l.PayrollRun.Reference, l.PayrollRun.PayrollPeriod.Name,
                l.PayrollRun.PayrollPeriod.Year, l.PayrollRun.PayrollPeriod.Month, l.PayrollRun.Status, l.GrossEarnings, l.Paye, l.Sha,
                l.HousingLevy, l.Nssf, l.OtherDeductions, l.TotalDeductions, l.NetPay, l.Payslip != null ? l.Payslip.Id : null))
            .ToListAsync(ct);
    }
}
