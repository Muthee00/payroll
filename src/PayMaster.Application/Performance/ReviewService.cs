using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

/// <summary>
/// Review cycles and the review workflow:
/// HR creates the cycle → manager evaluates → employee self-assesses → manager gives feedback →
/// employee acknowledges → HR finalizes. Each step is enforced by <see cref="PerformanceReview"/>.
/// </summary>
public sealed class ReviewService(
    IApplicationDbContext db,
    PerformanceScope scope,
    ICurrentUser currentUser,
    INotificationService notifications,
    IUserDirectory users,
    IAuditService audit,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ================================================================== Cycles

    public async Task<IReadOnlyList<ReviewCycleDto>> ListCyclesAsync(CancellationToken ct = default) =>
        await db.ReviewCycles.AsNoTracking().OrderByDescending(c => c.PeriodEnd)
            .Select(c => new ReviewCycleDto(c.Id, c.Name, c.ReviewType, c.PeriodStart, c.PeriodEnd, c.DueDate, c.Status,
                c.Reviews.Count(r => r.Status != ReviewStatus.Cancelled), c.Reviews.Count(r => r.Status == ReviewStatus.Finalized)))
            .ToListAsync(ct);

    public async Task<int> CreateCycleAsync(ReviewCycleInput input, CancellationToken ct = default)
    {
        if (!scope.CanManageCycles)
            throw new ForbiddenAccessException();
        if (input.PeriodEnd < input.PeriodStart)
            throw new DomainException("The review period must end after it starts.");
        if (input.DueDate < input.PeriodStart)
            throw new DomainException("The due date cannot be before the review period starts.");

        var employees = await db.Employees
            .Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnLeave)
                        && e.EmploymentDate <= input.PeriodEnd
                        && (input.DepartmentId == null || e.DepartmentId == input.DepartmentId))
            .Select(e => new { e.Id, ReviewerId = e.ManagerId ?? e.Department.HeadEmployeeId })
            .ToListAsync(ct);
        if (employees.Count == 0)
            throw new DomainException("No eligible employees for this review cycle.");

        var cycle = new ReviewCycle
        {
            Name = input.Name.Trim(), ReviewType = input.ReviewType, PeriodStart = input.PeriodStart!.Value,
            PeriodEnd = input.PeriodEnd!.Value, DueDate = input.DueDate!.Value, Status = ReviewCycleStatus.Active
        };
        foreach (var e in employees)
            cycle.Reviews.Add(new PerformanceReview { EmployeeId = e.Id, ReviewerId = e.ReviewerId == e.Id ? null : e.ReviewerId });
        db.ReviewCycles.Add(cycle);
        await db.SaveChangesAsync(ct);

        // Notify each reviewer once and each employee that a review has started.
        var reviewerIds = employees.Where(e => e.ReviewerId is not null).Select(e => e.ReviewerId!.Value).Distinct();
        foreach (var reviewerId in reviewerIds)
            if (await users.GetUserIdForEmployeeAsync(reviewerId, ct) is int userId)
                notifications.Notify([userId], "Performance reviews assigned",
                    $"{employees.Count(e => e.ReviewerId == reviewerId)} review(s) in '{cycle.Name}' are due {cycle.DueDate:dd MMM yyyy}.",
                    NotificationType.Warning, "/Reviews");
        audit.Record("Performance.CycleCreated", nameof(ReviewCycle), cycle.Id, $"{cycle.Name}: {employees.Count} reviews");
        await db.SaveChangesAsync(ct);
        return cycle.Id;
    }

    public async Task CloseCycleAsync(int id, CancellationToken ct = default)
    {
        if (!scope.CanManageCycles)
            throw new ForbiddenAccessException();
        var cycle = await db.ReviewCycles.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Review cycle", id);
        cycle.Status = ReviewCycleStatus.Closed;
        await db.SaveChangesAsync(ct);
    }

    // ================================================================== Reviews

    public Task<PagedResult<ReviewListItem>> ListAsync(ReviewQuery query, CancellationToken ct = default)
    {
        var visible = scope.VisibleEmployeeIds();
        var self = currentUser.EmployeeId ?? -1;
        var reviews = db.PerformanceReviews.AsNoTracking()
            .Where(r => visible.Contains(r.EmployeeId) || r.ReviewerId == self);
        if (query.CycleId is int cycleId)
            reviews = reviews.Where(r => r.ReviewCycleId == cycleId);
        if (query.Status is ReviewStatus status)
            reviews = reviews.Where(r => r.Status == status);
        if (query.DepartmentId is int departmentId)
            reviews = reviews.Where(r => r.Employee.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            reviews = reviews.Where(r => r.Employee.FirstName.Contains(term) || r.Employee.LastName.Contains(term) || r.Employee.EmployeeNumber.Contains(term));
        }

        return reviews.OrderBy(r => r.Status == ReviewStatus.Finalized || r.Status == ReviewStatus.Cancelled)
            .ThenBy(r => r.ReviewCycle.DueDate).ThenBy(r => r.Employee.FirstName)
            .Select(Projection(Today))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<ReviewDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var r = await db.PerformanceReviews.AsNoTracking()
            .Include(x => x.ReviewCycle).Include(x => x.Employee).ThenInclude(e => e.Position)
            .Include(x => x.Scores).ThenInclude(s => s.Kpi)
            .Include(x => x.Feedback)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review", id);
        var access = await AccessAsync(r, ct);
        if (!access.CanView)
            throw new ForbiddenAccessException("You do not have access to this review.");

        var item = await db.PerformanceReviews.AsNoTracking().Where(x => x.Id == id).Select(Projection(Today)).SingleAsync(ct);
        var names = await users.GetDisplayNamesAsync(r.Feedback.Where(f => f.CreatedById != null).Select(f => f.CreatedById!.Value), ct);

        return new ReviewDetails(item, r.ReviewCycle.PeriodStart, r.ReviewCycle.PeriodEnd, r.Employee.Position?.Title,
            r.Strengths, r.AreasForImprovement, r.ManagerComments, r.SelfAssessment, r.EmployeeComments, r.DevelopmentRecommendations,
            r.HrComments, r.ManagerEvaluatedAt, r.SelfAssessedAt, r.FeedbackGivenAt, r.AcknowledgedAt, r.FinalizedAt,
            r.Scores.OrderBy(s => s.Source).ThenBy(s => s.Name).Select(s => new ReviewScoreDto(s.Id, s.Source, s.KpiId, s.PerformanceGoalId,
                s.Name, s.Kpi?.MeasurementUnit, s.Kpi?.ScoringMethod, s.TargetValue, s.ActualValue, s.Weight, s.Score, s.WeightedScore, s.Comments)).ToList(),
            access.IsReviewer ? await CandidatesAsync(r, ct) : [],
            r.Feedback.OrderBy(f => f.CreatedAt).Select(f => new FeedbackDto(f.Type, f.Content,
                f.CreatedById is int uid ? names.GetValueOrDefault(uid) : null, f.CreatedAt)).ToList(),
            await PlansQuery(p => p.PerformanceReviewId == id || (p.EmployeeId == r.EmployeeId && p.Status != DevelopmentPlanStatus.Completed)).ToListAsync(ct),
            access.IsEmployee, access.IsReviewer, scope.CanManageCycles);
    }

    public async Task SaveEvaluationAsync(int id, ManagerEvaluationInput input, CancellationToken ct = default)
    {
        var review = await LoadAsync(id, ct);
        await EnsureReviewerAsync(review, ct);
        review.EnsureScoresEditable();

        var candidates = (await CandidatesAsync(review, ct)).ToDictionary(c => (c.Source, c.Id));
        var kpiIds = candidates.Keys.Where(x => x.Source == ReviewScoreSource.Kpi).Select(x => x.Id).ToList();
        var kpis = await db.Kpis.AsNoTracking().Where(k => kpiIds.Contains(k.Id)).ToDictionaryAsync(k => k.Id, ct);

        db.PerformanceReviewScores.RemoveRange(review.Scores);
        review.Scores.Clear();
        foreach (var score in input.Scores.Where(s => s.Include))
        {
            if (!candidates.TryGetValue((score.Source, score.ItemId), out var candidate))
                throw new DomainException("A scored item does not apply to this employee.");
            if (score.Weight <= 0)
                throw new DomainException($"Give '{candidate.Name}' a weight greater than zero.");

            decimal value;
            try
            {
                value = candidate.Source == ReviewScoreSource.Kpi
                    ? PerformanceScoring.ScoreKpi(candidate.Method, candidate.Target, score.ActualValue, score.ManualScore)
                    : PerformanceScoring.ScoreKpi(KpiScoringMethod.ManualRating, null, null,
                        score.ManualScore ?? throw new DomainException($"Rate the goal '{candidate.Name}' from 1 to 5."));
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"{candidate.Name}: {ex.Message}");
            }

            review.Scores.Add(new PerformanceReviewScore
            {
                Source = candidate.Source,
                KpiId = candidate.Source == ReviewScoreSource.Kpi ? candidate.Id : null,
                PerformanceGoalId = candidate.Source == ReviewScoreSource.Goal ? candidate.Id : null,
                Name = candidate.Name,
                TargetValue = candidate.Target,
                ActualValue = candidate.Source == ReviewScoreSource.Kpi && kpis[candidate.Id].ScoringMethod != KpiScoringMethod.ManualRating
                    ? score.ActualValue : candidate.GoalProgress,
                Weight = score.Weight,
                Score = value,
                Comments = score.Comments?.Trim()
            });
        }

        ApplyOverall(review, await db.RatingScaleLevels.AsNoTracking().ToListAsync(ct));
        review.Strengths = input.Strengths?.Trim();
        review.AreasForImprovement = input.AreasForImprovement?.Trim();

        if (input.Complete && review.Status == ReviewStatus.PendingManagerEvaluation)
        {
            review.CompleteManagerEvaluation(Now);
            await NotifyEmployeeAsync(review, "Self-assessment requested",
                $"Your manager has completed their evaluation for '{review.ReviewCycle.Name}'. Please submit your self-assessment.", ct);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitSelfAssessmentAsync(int id, string text, CancellationToken ct = default)
    {
        var review = await LoadAsync(id, ct);
        EnsureEmployee(review);
        review.SubmitSelfAssessment(text, Now);
        review.Feedback.Add(new PerformanceFeedback { EmployeeId = review.EmployeeId, Type = FeedbackType.Self, Content = review.SelfAssessment! });
        await NotifyReviewerAsync(review, "Self-assessment submitted", $"{review.Employee.FullName} submitted their self-assessment.", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitFeedbackAsync(int id, ManagerFeedbackInput input, CancellationToken ct = default)
    {
        var review = await LoadAsync(id, ct);
        await EnsureReviewerAsync(review, ct);
        review.ManagerComments = input.ManagerComments.Trim();
        review.DevelopmentRecommendations = input.DevelopmentRecommendations?.Trim();
        review.ProvideManagerFeedback(Now);
        review.Feedback.Add(new PerformanceFeedback { EmployeeId = review.EmployeeId, Type = FeedbackType.Manager, Content = review.ManagerComments });
        await NotifyEmployeeAsync(review, "Performance feedback ready", $"Feedback for '{review.ReviewCycle.Name}' is ready. Please review and acknowledge it.", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AcknowledgeAsync(int id, string? comments, CancellationToken ct = default)
    {
        var review = await LoadAsync(id, ct);
        EnsureEmployee(review);
        review.Acknowledge(comments, Now);
        await notifications.NotifyPermissionHoldersAsync(Permissions.Performance.ManageCycles, "Review ready to finalize",
            $"{review.Employee.FullName} acknowledged their '{review.ReviewCycle.Name}' review.", NotificationType.Info, $"/Reviews/Details/{id}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task FinalizeAsync(int id, string? hrComments, CancellationToken ct = default)
    {
        if (!scope.CanManageCycles)
            throw new ForbiddenAccessException();
        var review = await LoadAsync(id, ct);
        review.HrComments = hrComments?.Trim();
        review.Finalize(currentUser.RequireUserId(), Now);
        if (!string.IsNullOrWhiteSpace(review.HrComments))
            review.Feedback.Add(new PerformanceFeedback { EmployeeId = review.EmployeeId, Type = FeedbackType.Hr, Content = review.HrComments });
        audit.Record("Performance.ReviewFinalized", nameof(PerformanceReview), id,
            $"{review.Employee.FullName}: {review.OverallScore:0.00} ({review.OverallRating})");
        await NotifyEmployeeAsync(review, "Review finalized", $"Your '{review.ReviewCycle.Name}' review is complete: {review.OverallRating}.", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        if (!scope.CanManageCycles)
            throw new ForbiddenAccessException();
        var review = await LoadAsync(id, ct);
        review.Cancel();
        await db.SaveChangesAsync(ct);
    }

    // ================================================================== Development plans

    public async Task<IReadOnlyList<DevelopmentPlanDto>> ListPlansAsync(int? employeeId, CancellationToken ct = default)
    {
        var visible = scope.VisibleEmployeeIds();
        return await PlansQuery(p => visible.Contains(p.EmployeeId) && (employeeId == null || p.EmployeeId == employeeId)).ToListAsync(ct);
    }

    public async Task<int> SavePlanAsync(int? id, DevelopmentPlanInput input, CancellationToken ct = default)
    {
        await scope.EnsureCanManageAsync(input.EmployeeId, ct);
        var plan = id is null ? db.DevelopmentPlans.Add(new DevelopmentPlan()).Entity
            : await db.DevelopmentPlans.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Development plan", id);
        plan.EmployeeId = input.EmployeeId;
        plan.PerformanceReviewId = input.ReviewId;
        plan.Type = input.Type;
        plan.Title = input.Title.Trim();
        plan.Description = input.Description?.Trim();
        plan.Skills = input.Skills?.Trim();
        plan.StartDate = input.StartDate;
        plan.TargetDate = input.TargetDate;
        await db.SaveChangesAsync(ct);

        if (id is null && await users.GetUserIdForEmployeeAsync(input.EmployeeId, ct) is int userId && userId != currentUser.UserId)
        {
            notifications.Notify([userId], "Development plan added", plan.Title, NotificationType.Info, "/Goals?tab=development");
            await db.SaveChangesAsync(ct);
        }
        return plan.Id;
    }

    public async Task UpdatePlanProgressAsync(int id, int progress, CancellationToken ct = default)
    {
        var plan = await db.DevelopmentPlans.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Development plan", id);
        if (plan.EmployeeId != currentUser.EmployeeId)
            await scope.EnsureCanManageAsync(plan.EmployeeId, ct);
        if (progress is < 0 or > 100)
            throw new DomainException("Progress must be between 0 and 100.");
        plan.Progress = progress;
        plan.Status = progress == 100 ? DevelopmentPlanStatus.Completed : progress > 0 ? DevelopmentPlanStatus.InProgress : DevelopmentPlanStatus.Planned;
        await db.SaveChangesAsync(ct);
    }

    // ================================================================== Helpers

    public static void ApplyOverall(PerformanceReview review, IReadOnlyList<RatingScaleLevel> scale)
    {
        var total = review.Scores.Sum(s => s.Weight);
        foreach (var s in review.Scores)
            s.WeightedScore = total == 0 ? 0 : Math.Round(s.Score * s.Weight / total, 4);
        review.OverallScore = PerformanceScoring.WeightedScore(review.Scores.Select(s => new ScoredItem(s.Score, s.Weight)).ToList());
        review.OverallRating = PerformanceScoring.Rate(review.OverallScore, scale)?.Label;
    }

    /// <summary>KPIs that apply to the employee's department/position plus their goals in the review period.</summary>
    private async Task<IReadOnlyList<ScoreCandidate>> CandidatesAsync(PerformanceReview r, CancellationToken ct)
    {
        var employee = r.Employee;
        var cycle = r.ReviewCycle;
        var kpis = await db.Kpis.AsNoTracking()
            .Where(k => k.IsActive && (k.DepartmentId == null || k.DepartmentId == employee.DepartmentId)
                        && (k.PositionId == null || k.PositionId == employee.PositionId))
            .Select(k => new ScoreCandidate(ReviewScoreSource.Kpi, k.Id, k.Name, k.MeasurementUnit, k.ScoringMethod, k.TargetValue, k.Weight, null))
            .ToListAsync(ct);
        var goals = await db.PerformanceGoals.AsNoTracking()
            .Where(g => g.EmployeeId == r.EmployeeId && g.Status != GoalStatus.Cancelled
                        && g.StartDate <= cycle.PeriodEnd && g.TargetDate >= cycle.PeriodStart)
            .Select(g => new ScoreCandidate(ReviewScoreSource.Goal, g.Id, g.Title, "%", KpiScoringMethod.ManualRating, 100m,
                g.Priority == GoalPriority.Critical ? 20 : g.Priority == GoalPriority.High ? 15 : 10, g.Progress))
            .ToListAsync(ct);
        return kpis.Concat(goals).ToList();
    }

    private async Task<PerformanceReview> LoadAsync(int id, CancellationToken ct) =>
        await db.PerformanceReviews.Include(r => r.ReviewCycle).Include(r => r.Employee).Include(r => r.Scores)
            .SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Review", id);

    private async Task<(bool CanView, bool IsEmployee, bool IsReviewer)> AccessAsync(PerformanceReview r, CancellationToken ct)
    {
        var isEmployee = r.EmployeeId == currentUser.EmployeeId;
        var isReviewer = (r.ReviewerId is not null && r.ReviewerId == currentUser.EmployeeId) || await scope.CanManageAsync(r.EmployeeId, ct);
        return (isEmployee || isReviewer || scope.IsHr, isEmployee, isReviewer && !isEmployee);
    }

    private async Task EnsureReviewerAsync(PerformanceReview review, CancellationToken ct)
    {
        var (_, isEmployee, isReviewer) = await AccessAsync(review, ct);
        if (!isReviewer || isEmployee)
            throw new ForbiddenAccessException("Only the reviewing manager or HR can do this step.");
    }

    private void EnsureEmployee(PerformanceReview review)
    {
        if (review.EmployeeId != currentUser.EmployeeId)
            throw new ForbiddenAccessException("Only the employee being reviewed can do this step.");
    }

    private async Task NotifyEmployeeAsync(PerformanceReview review, string title, string message, CancellationToken ct)
    {
        if (await users.GetUserIdForEmployeeAsync(review.EmployeeId, ct) is int userId)
            notifications.Notify([userId], title, message, NotificationType.Info, $"/Reviews/Details/{review.Id}");
    }

    private async Task NotifyReviewerAsync(PerformanceReview review, string title, string message, CancellationToken ct)
    {
        if (review.ReviewerId is int reviewerId && await users.GetUserIdForEmployeeAsync(reviewerId, ct) is int userId)
            notifications.Notify([userId], title, message, NotificationType.Info, $"/Reviews/Details/{review.Id}");
    }

    private IQueryable<DevelopmentPlanDto> PlansQuery(System.Linq.Expressions.Expression<Func<DevelopmentPlan, bool>> filter) =>
        db.DevelopmentPlans.AsNoTracking().Where(filter).OrderBy(p => p.Status).ThenBy(p => p.TargetDate).Select(p => new DevelopmentPlanDto(p.Id, p.EmployeeId, p.Employee.FirstName + " " + p.Employee.LastName,
            p.Type, p.Title, p.Description, p.Skills, p.StartDate, p.TargetDate, p.Progress, p.Status, p.PerformanceReviewId));

    private static System.Linq.Expressions.Expression<Func<PerformanceReview, ReviewListItem>> Projection(DateOnly today) => r =>
        new ReviewListItem(r.Id, r.ReviewCycleId, r.ReviewCycle.Name, r.ReviewCycle.ReviewType, r.EmployeeId,
            r.Employee.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeNumber, r.Employee.Department.Name,
            r.Reviewer != null ? r.Reviewer.FirstName + " " + r.Reviewer.LastName : null, r.Status, r.OverallScore, r.OverallRating,
            r.ReviewCycle.DueDate,
            r.ReviewCycle.DueDate < today && r.Status != ReviewStatus.Finalized && r.Status != ReviewStatus.Cancelled);
}
