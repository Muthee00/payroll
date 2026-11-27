using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Performance;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Reports;

/// <summary>Performance reports, limited to the employees the user may see (HR: all; managers: their team).</summary>
public sealed class PerformanceReportService(IApplicationDbContext db, PerformanceScope scope, PayrollReportService payrollReports)
{
    public async Task<ReportResult> RunAsync(string key, ReportFilter filter, CancellationToken ct = default)
    {
        var (from, to) = payrollReports.Range(filter);
        var end = to.AddMonths(1).AddDays(-1);
        var subtitle = $"{from:MMMM yyyy}{(from == to ? "" : $" – {to:MMMM yyyy}")} · review cycles ending in range";
        var visible = scope.VisibleEmployeeIds();

        var reviews = db.PerformanceReviews.AsNoTracking()
            .Where(r => visible.Contains(r.EmployeeId) && r.Status != ReviewStatus.Cancelled
                        && r.ReviewCycle.PeriodEnd >= from && r.ReviewCycle.PeriodEnd <= end);
        var goals = db.PerformanceGoals.AsNoTracking()
            .Where(g => visible.Contains(g.EmployeeId) && g.TargetDate >= from && g.StartDate <= end);
        if (filter.DepartmentId is int departmentId)
        {
            reviews = reviews.Where(r => r.Employee.DepartmentId == departmentId);
            goals = goals.Where(g => g.DepartmentId == departmentId);
        }
        if (filter.EmployeeId is int employeeId)
        {
            reviews = reviews.Where(r => r.EmployeeId == employeeId);
            goals = goals.Where(g => g.EmployeeId == employeeId);
        }

        switch (key)
        {
            case "performance-employee":
            {
                var rows = await reviews.OrderBy(r => r.Employee.FirstName).ThenBy(r => r.ReviewCycle.PeriodEnd)
                    .Select(r => new object?[]
                    {
                        r.Employee.EmployeeNumber, r.Employee.FirstName + " " + r.Employee.LastName, r.Employee.Department.Name,
                        r.ReviewCycle.Name, r.Status.ToString(), r.OverallScore, r.OverallRating,
                        r.Reviewer != null ? r.Reviewer.FirstName + " " + r.Reviewer.LastName : null
                    }).ToListAsync(ct);
                return new(key, "Employee performance", subtitle,
                    [new("Employee No."), new("Employee"), new("Department"), new("Cycle"), new("Status"), new("Score", ReportColumnKind.Number),
                     new("Rating"), new("Reviewer")], Fix(rows), []);
            }
            case "performance-department":
            {
                var rows = await reviews.GroupBy(r => r.Employee.Department.Name)
                    .Select(g => new object?[]
                    {
                        g.Key, g.Count(), g.Count(r => r.Status == ReviewStatus.Finalized),
                        g.Count() == 0 ? 0m : (decimal)g.Count(r => r.Status == ReviewStatus.Finalized) / g.Count(),
                        g.Where(r => r.Status == ReviewStatus.Finalized).Average(r => r.OverallScore),
                        g.Where(r => r.Status == ReviewStatus.Finalized).Min(r => r.OverallScore),
                        g.Where(r => r.Status == ReviewStatus.Finalized).Max(r => r.OverallScore)
                    }).ToListAsync(ct);
                return new(key, "Department performance", subtitle,
                    [new("Department"), new("Reviews", ReportColumnKind.Number, true), new("Finalized", ReportColumnKind.Number, true),
                     new("Completion", ReportColumnKind.Percent), new("Average score", ReportColumnKind.Number),
                     new("Lowest", ReportColumnKind.Number), new("Highest", ReportColumnKind.Number)], Fix(rows), []);
            }
            case "kpi-achievement":
            {
                var rows = await db.PerformanceReviewScores.AsNoTracking()
                    .Where(s => s.Source == ReviewScoreSource.Kpi && reviews.Select(r => r.Id).Contains(s.PerformanceReviewId))
                    .GroupBy(s => new { s.Name, Unit = s.Kpi!.MeasurementUnit })
                    .Select(g => new object?[]
                    {
                        g.Key.Name, g.Key.Unit, g.Count(), g.Average(s => s.TargetValue), g.Average(s => s.ActualValue),
                        g.Count(s => s.ActualValue >= s.TargetValue) * 1m / g.Count(), g.Average(s => s.Score)
                    }).ToListAsync(ct);
                return new(key, "KPI achievement", subtitle,
                    [new("KPI"), new("Unit"), new("Times scored", ReportColumnKind.Number), new("Average target", ReportColumnKind.Number),
                     new("Average actual", ReportColumnKind.Number), new("Target met", ReportColumnKind.Percent), new("Average score", ReportColumnKind.Number)],
                    Fix(rows), ["'Target met' counts actual ≥ target (for lower-is-better KPIs, read the average score instead)."]);
            }
            case "goal-completion":
            {
                var rows = await goals.GroupBy(g => g.Department.Name)
                    .Select(g => new object?[]
                    {
                        g.Key, g.Count(), g.Count(x => x.Status == GoalStatus.Completed), g.Count(x => x.Status == GoalStatus.InProgress || x.Status == GoalStatus.NotStarted),
                        g.Count(x => x.Status == GoalStatus.Overdue), g.Count(x => x.Status == GoalStatus.Cancelled),
                        g.Count(x => x.Status != GoalStatus.Cancelled) == 0 ? 0m : g.Count(x => x.Status == GoalStatus.Completed) * 1m / g.Count(x => x.Status != GoalStatus.Cancelled),
                        g.Average(x => (decimal)x.Progress) / 100m
                    }).ToListAsync(ct);
                return new(key, "Goal completion", subtitle,
                    [new("Department"), new("Goals", ReportColumnKind.Number, true), new("Completed", ReportColumnKind.Number, true),
                     new("Open", ReportColumnKind.Number, true), new("Overdue", ReportColumnKind.Number, true), new("Cancelled", ReportColumnKind.Number, true),
                     new("Completion rate", ReportColumnKind.Percent), new("Average progress", ReportColumnKind.Percent)], Fix(rows), []);
            }
            case "review-status":
            {
                var rows = await reviews.GroupBy(r => new { r.ReviewCycle.Name, r.ReviewCycle.DueDate })
                    .Select(g => new object?[]
                    {
                        g.Key.Name, g.Key.DueDate, g.Count(),
                        g.Count(r => r.Status == ReviewStatus.PendingManagerEvaluation), g.Count(r => r.Status == ReviewStatus.PendingSelfAssessment),
                        g.Count(r => r.Status == ReviewStatus.PendingManagerFeedback), g.Count(r => r.Status == ReviewStatus.PendingAcknowledgement),
                        g.Count(r => r.Status == ReviewStatus.PendingHrFinalization), g.Count(r => r.Status == ReviewStatus.Finalized)
                    }).ToListAsync(ct);
                return new(key, "Performance review status", subtitle,
                    [new("Cycle"), new("Due", ReportColumnKind.Date), new("Reviews", ReportColumnKind.Number, true),
                     new("Manager evaluation", ReportColumnKind.Number, true), new("Self-assessment", ReportColumnKind.Number, true),
                     new("Manager feedback", ReportColumnKind.Number, true), new("Acknowledgement", ReportColumnKind.Number, true),
                     new("HR finalization", ReportColumnKind.Number, true), new("Finalized", ReportColumnKind.Number, true)], Fix(rows), []);
            }
            case "development":
            {
                var rows = await db.DevelopmentPlans.AsNoTracking().Where(p => visible.Contains(p.EmployeeId)
                        && (filter.EmployeeId == null || p.EmployeeId == filter.EmployeeId)
                        && (filter.DepartmentId == null || p.Employee.DepartmentId == filter.DepartmentId))
                    .OrderBy(p => p.Employee.FirstName)
                    .Select(p => new object?[]
                    {
                        p.Employee.EmployeeNumber, p.Employee.FirstName + " " + p.Employee.LastName, p.Employee.Department.Name,
                        p.Type.ToString(), p.Title, p.Skills, p.TargetDate, p.Status.ToString(), p.Progress / 100m
                    }).ToListAsync(ct);
                return new(key, "Training & development", "All current and past development plans",
                    [new("Employee No."), new("Employee"), new("Department"), new("Type"), new("Plan"), new("Skills"),
                     new("Target date", ReportColumnKind.Date), new("Status"), new("Progress", ReportColumnKind.Percent)], Fix(rows), []);
            }
            default:
                throw new DomainException($"Unknown report '{key}'.");
        }
    }

    /// <summary>Normalises numeric cells to decimal so totals and formatting behave uniformly.</summary>
    private static List<object?[]> Fix(List<object?[]> rows)
    {
        foreach (var row in rows)
            for (var i = 0; i < row.Length; i++)
                row[i] = row[i] switch { int n => (decimal)n, double d => (decimal)d, _ => row[i] };
        return rows;
    }
}
