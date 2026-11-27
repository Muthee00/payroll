using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

public sealed class PerformanceDashboardService(IApplicationDbContext db, PerformanceScope scope, ICurrentUser currentUser,
    ReviewService reviews, GoalService goals, TimeProvider clock)
{
    public async Task<PerformanceDashboard> GetAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var visible = scope.VisibleEmployeeIds();
        var visibleReviews = db.PerformanceReviews.AsNoTracking().Where(r => visible.Contains(r.EmployeeId) && r.Status != ReviewStatus.Cancelled);
        var visibleGoals = db.PerformanceGoals.AsNoTracking().Where(g => visible.Contains(g.EmployeeId));

        var totalEmployees = await db.Employees.CountAsync(e => visible.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnLeave), ct);
        var withGoals = await visibleGoals.Where(g => g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.InProgress || g.Status == GoalStatus.Overdue)
            .Select(g => g.EmployeeId).Distinct().CountAsync(ct);
        var pending = await visibleReviews.CountAsync(r => r.Status != ReviewStatus.Finalized, ct);
        var completed = await visibleReviews.CountAsync(r => r.Status == ReviewStatus.Finalized, ct);
        var average = await visibleReviews.Where(r => r.Status == ReviewStatus.Finalized && r.OverallScore != null).AverageAsync(r => r.OverallScore, ct);
        var overdueReviews = await visibleReviews.CountAsync(r => r.Status != ReviewStatus.Finalized && r.ReviewCycle.DueDate < today, ct);
        var overdueGoals = await visibleGoals.CountAsync(g => g.Status == GoalStatus.Overdue
                                                             || ((g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.InProgress) && g.TargetDate < today), ct);

        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive)
            .Select(d => new DepartmentPerformance(d.Name,
                visibleReviews.Where(r => r.Employee.DepartmentId == d.Id && r.Status == ReviewStatus.Finalized).Average(r => r.OverallScore),
                visibleReviews.Count(r => r.Employee.DepartmentId == d.Id),
                visibleReviews.Count(r => r.Employee.DepartmentId == d.Id && r.Status == ReviewStatus.Finalized),
                visibleGoals.Count(g => g.DepartmentId == d.Id && (g.Status == GoalStatus.InProgress || g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.Overdue)),
                visibleGoals.Count(g => g.DepartmentId == d.Id && g.Status == GoalStatus.Completed)))
            .ToListAsync(ct);

        var scale = await db.RatingScaleLevels.AsNoTracking().OrderBy(l => l.Value).ToListAsync(ct);
        var ratings = await visibleReviews.Where(r => r.Status == ReviewStatus.Finalized && r.OverallRating != null)
            .GroupBy(r => r.OverallRating!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var distribution = scale.Select(l => (l.Label, ratings.FirstOrDefault(r => r.Key == l.Label)?.Count ?? 0, l.Color)).ToList();

        var trend = await visibleReviews.Where(r => r.Status == ReviewStatus.Finalized)
            .GroupBy(r => new { r.ReviewCycle.Id, r.ReviewCycle.Name, r.ReviewCycle.PeriodEnd })
            .Select(g => new { g.Key.Name, g.Key.PeriodEnd, Average = g.Average(r => r.OverallScore) })
            .OrderBy(g => g.PeriodEnd).ToListAsync(ct);

        var attention = (await reviews.ListAsync(new ReviewQuery { PageSize = 50 }, ct)).Items
            .Where(r => r.IsOverdue || (r.OverallScore is decimal s && s < 2.5m && r.Status == ReviewStatus.Finalized))
            .Take(8).ToList();
        var upcoming = (await reviews.ListAsync(new ReviewQuery { PageSize = 50 }, ct)).Items
            .Where(r => r.Status is not (ReviewStatus.Finalized or ReviewStatus.Cancelled) && !r.IsOverdue)
            .OrderBy(r => r.DueDate).Take(6).ToList();

        var self = currentUser.EmployeeId;
        var myGoals = self is int id ? (await goals.ListAsync(new GoalQuery { EmployeeId = id, PageSize = 10 }, ct)).Items : [];
        var myReviews = self is int id2 ? (await reviews.ListAsync(new ReviewQuery { PageSize = 10 }, ct)).Items.Where(r => r.EmployeeId == id2).ToList() : [];
        var myPlans = self is int id3 ? await reviews.ListPlansAsync(id3, ct) : [];

        return new PerformanceDashboard(totalEmployees, withGoals, pending, completed, average is null ? null : Math.Round(average.Value, 2),
            overdueReviews, overdueGoals, departments.Where(d => d.Reviews > 0 || d.ActiveGoals > 0 || d.CompletedGoals > 0).ToList(),
            distribution, trend.Select(t => (t.Name, t.Average is null ? (decimal?)null : Math.Round(t.Average.Value, 2))).ToList(),
            attention, upcoming, myGoals, myReviews, myPlans);
    }
}

/// <summary>Daily housekeeping: marks overdue goals and reminds people of approaching deadlines.</summary>
public sealed class PerformanceReminderJob(IApplicationDbContext db, INotificationService notifications, IUserDirectory users,
    TimeProvider clock, ILogger<PerformanceReminderJob> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var overdue = await db.PerformanceGoals
            .Where(g => (g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.InProgress) && g.TargetDate < today).ToListAsync(ct);
        foreach (var goal in overdue)
            goal.RefreshOverdue(today);

        var soon = today.AddDays(7);
        var dueSoon = await db.PerformanceGoals.AsNoTracking()
            .Where(g => (g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.InProgress) && g.TargetDate == soon)
            .Select(g => new { g.Id, g.Title, g.EmployeeId, g.TargetDate }).ToListAsync(ct);
        foreach (var goal in dueSoon)
            if (await users.GetUserIdForEmployeeAsync(goal.EmployeeId, ct) is int userId)
                notifications.Notify([userId], "Goal deadline approaching", $"'{goal.Title}' is due {goal.TargetDate:dd MMM yyyy}.",
                    NotificationType.Warning, $"/Goals/Details/{goal.Id}");

        var overdueReviews = await db.PerformanceReviews.AsNoTracking()
            .Where(r => r.Status != ReviewStatus.Finalized && r.Status != ReviewStatus.Cancelled && r.ReviewCycle.DueDate == today.AddDays(-1))
            .Select(r => new { r.Id, r.ReviewerId, r.EmployeeId, r.Status, Name = r.Employee.FirstName + " " + r.Employee.LastName, Cycle = r.ReviewCycle.Name })
            .ToListAsync(ct);
        foreach (var review in overdueReviews)
        {
            var target = review.Status is ReviewStatus.PendingSelfAssessment or ReviewStatus.PendingAcknowledgement ? review.EmployeeId : review.ReviewerId;
            if (target is int employeeId && await users.GetUserIdForEmployeeAsync(employeeId, ct) is int userId)
                notifications.Notify([userId], "Performance review overdue", $"{review.Name} — {review.Cycle} is past its due date.",
                    NotificationType.Error, $"/Reviews/Details/{review.Id}");
        }
        if (overdueReviews.Count > 0)
            await notifications.NotifyPermissionHoldersAsync(Permissions.Performance.ManageCycles, "Overdue performance reviews",
                $"{overdueReviews.Count} review(s) became overdue.", NotificationType.Warning, "/Reviews", ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Performance reminders: {Overdue} goals marked overdue, {Soon} deadline reminders, {Reviews} overdue reviews",
            overdue.Count, dueSoon.Count, overdueReviews.Count);
    }
}
