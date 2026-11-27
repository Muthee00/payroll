using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Payroll;
using PayMaster.Application.Payslips;
using PayMaster.Application.Performance;
using PayMaster.Application.Salary;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Dashboard;

public record PendingApproval(string Kind, string Title, string Detail, string Link, DateTime Since);

public record RecentHire(int Id, string Name, string EmployeeNumber, string? JobTitle, string Department, DateOnly EmploymentDate);

public record DashboardData
{
    public bool ShowPayroll { get; init; }
    public bool ShowPeople { get; init; }
    public bool ShowPerformance { get; init; }
    public bool IsSelfService { get; init; }

    public PeriodSummary? CurrentPeriod { get; init; }
    public int TotalEmployees { get; init; }
    public int NewThisMonth { get; init; }
    public int PendingPayrollApprovals { get; init; }
    public IReadOnlyList<PayrollTrendPoint> Trend { get; init; } = [];
    public IReadOnlyList<(string Department, int Count)> Headcount { get; init; } = [];
    public IReadOnlyList<PayrollRunListItem> RecentRuns { get; init; } = [];
    public IReadOnlyList<RecentHire> RecentHires { get; init; } = [];
    public IReadOnlyList<PendingApproval> Approvals { get; init; } = [];
    public PerformanceDashboard? Performance { get; init; }
    public PayslipListItem? LatestPayslip { get; init; }
}

public sealed class DashboardService(
    IApplicationDbContext db,
    ICurrentUser user,
    PayrollService payroll,
    PayrollPeriodService periods,
    SalaryChangeService salaryChanges,
    PerformanceDashboardService performance,
    PayslipService payslips)
{
    public async Task<DashboardData> GetAsync(CancellationToken ct = default)
    {
        var showPayroll = user.HasPermission(Permissions.Payroll.View);
        var showPeople = user.HasPermission(Permissions.Employees.Manage) || showPayroll;
        var showPerformance = user.HasPermission(Permissions.Performance.ViewAll) || user.HasPermission(Permissions.Performance.ManageTeam);
        var current = periods.Current();
        var monthStart = new DateOnly(current.Year, current.Month, 1);

        var data = new DashboardData
        {
            ShowPayroll = showPayroll,
            ShowPeople = showPeople,
            ShowPerformance = showPerformance,
            IsSelfService = !showPayroll && !showPeople && !showPerformance
        };

        if (showPeople)
        {
            var active = db.Employees.AsNoTracking().Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnLeave);
            data = data with
            {
                TotalEmployees = await active.CountAsync(ct),
                NewThisMonth = await db.Employees.CountAsync(e => e.EmploymentDate >= monthStart, ct),
                Headcount = (await active.GroupBy(e => e.Department.Name).Select(g => new { g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count).ToListAsync(ct)).Select(g => (g.Key, g.Count)).ToList(),
                RecentHires = await db.Employees.AsNoTracking().OrderByDescending(e => e.EmploymentDate).Take(5)
                    .Select(e => new RecentHire(e.Id, e.FirstName + " " + e.LastName, e.EmployeeNumber, e.Position != null ? e.Position.Title : null,
                        e.Department.Name, e.EmploymentDate)).ToListAsync(ct)
            };
        }

        if (showPayroll)
        {
            var summary = await payroll.GetPeriodSummaryAsync(current.Year, current.Month, ct);
            if (summary.RunId is null)
            {
                // Before this month's payroll runs, show the latest month that has one.
                var latest = await db.PayrollRuns.AsNoTracking()
                    .Where(r => r.Status != PayrollRunStatus.Cancelled && r.Status != PayrollRunStatus.Reversed)
                    .OrderByDescending(r => r.PayrollPeriod.Year).ThenByDescending(r => r.PayrollPeriod.Month)
                    .Select(r => new { r.PayrollPeriod.Year, r.PayrollPeriod.Month }).FirstOrDefaultAsync(ct);
                if (latest is not null)
                    summary = await payroll.GetPeriodSummaryAsync(latest.Year, latest.Month, ct);
            }

            var pendingRuns = await db.PayrollRuns.AsNoTracking()
                .Where(r => r.Status == PayrollRunStatus.PendingApproval || r.Status == PayrollRunStatus.Approved)
                .Select(r => new { r.Id, r.Reference, r.Status, r.PayrollPeriod.Name, r.TotalNetPay, r.EmployeeCount, Since = r.SubmittedAt ?? r.CreatedAt })
                .ToListAsync(ct);
            var approvals = pendingRuns.Select(r => new PendingApproval(
                    r.Status == PayrollRunStatus.PendingApproval ? "Payroll approval" : "Ready to finalize",
                    $"{r.Name} payroll ({r.Reference})", $"{r.EmployeeCount} employees · net KSh {r.TotalNetPay:N0}", $"/Payroll/Run/{r.Id}", r.Since))
                .ToList();
            if (user.HasPermission(Permissions.Salary.Approve) || user.HasPermission(Permissions.Salary.ManageEmployeeSalary))
                approvals.AddRange((await salaryChanges.ListPendingAsync(ct)).Select(p => new PendingApproval("Salary change", p.EmployeeName,
                    $"KSh {p.NewBasic:N0} basic from {p.EffectiveFrom:dd MMM yyyy}", "/SalaryStructures?tab=approvals", p.RequestedAt)));

            data = data with
            {
                CurrentPeriod = summary,
                PendingPayrollApprovals = pendingRuns.Count(r => r.Status == PayrollRunStatus.PendingApproval),
                Trend = await payroll.GetTrendAsync(12, ct),
                RecentRuns = (await payroll.ListRunsAsync(new PayrollRunQuery { PageSize = 5 }, ct)).Items,
                Approvals = approvals.OrderBy(a => a.Since).Take(8).ToList()
            };
        }

        if (showPerformance || user.HasPermission(Permissions.Self.Access))
            data = data with { Performance = await performance.GetAsync(ct) };

        if (user.EmployeeId is not null)
            data = data with { LatestPayslip = (await payslips.ListAsync(new PayslipQuery { PageSize = 1 }, ownOnly: true, ct)).Items.FirstOrDefault() };

        return data;
    }
}
