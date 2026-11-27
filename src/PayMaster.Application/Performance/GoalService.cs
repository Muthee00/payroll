using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

public sealed class KpiService(IApplicationDbContext db, PerformanceScope scope)
{
    public async Task<IReadOnlyList<KpiDto>> ListAsync(bool includeInactive = true, CancellationToken ct = default) =>
        await db.Kpis.AsNoTracking()
            .Where(k => includeInactive || k.IsActive)
            .OrderBy(k => k.Department == null ? "" : k.Department.Name).ThenBy(k => k.Name)
            .Select(k => new KpiDto(k.Id, k.Name, k.Description, k.MeasurementUnit, k.TargetValue, k.Weight, k.ScoringMethod, k.IsActive,
                k.DepartmentId, k.Department != null ? k.Department.Name : null, k.PositionId, k.Position != null ? k.Position.Title : null))
            .ToListAsync(ct);

    public async Task<KpiInput> GetAsync(int id, CancellationToken ct = default)
    {
        var k = await db.Kpis.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("KPI", id);
        return new KpiInput
        {
            Name = k.Name, Description = k.Description, MeasurementUnit = k.MeasurementUnit, TargetValue = k.TargetValue,
            Weight = k.Weight, ScoringMethod = k.ScoringMethod, DepartmentId = k.DepartmentId, PositionId = k.PositionId
        };
    }

    public async Task<int> SaveAsync(int? id, KpiInput input, CancellationToken ct = default)
    {
        if (!scope.CanConfigure)
            throw new ForbiddenAccessException();
        if (input.ScoringMethod != KpiScoringMethod.ManualRating && input.TargetValue <= 0)
            throw new DomainException("A positive target is required for measured KPIs.");

        var kpi = id is null ? db.Kpis.Add(new Kpi()).Entity
            : await db.Kpis.SingleOrDefaultAsync(k => k.Id == id, ct) ?? throw new NotFoundException("KPI", id);
        kpi.Name = input.Name.Trim();
        kpi.Description = input.Description?.Trim();
        kpi.MeasurementUnit = input.MeasurementUnit.Trim();
        kpi.TargetValue = input.TargetValue;
        kpi.Weight = input.Weight;
        kpi.ScoringMethod = input.ScoringMethod;
        kpi.DepartmentId = input.DepartmentId;
        kpi.PositionId = input.PositionId;
        await db.SaveChangesAsync(ct);
        return kpi.Id;
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        if (!scope.CanConfigure)
            throw new ForbiddenAccessException();
        var kpi = await db.Kpis.SingleOrDefaultAsync(k => k.Id == id, ct) ?? throw new NotFoundException("KPI", id);
        kpi.IsActive = active;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RatingScaleLevel>> RatingScaleAsync(CancellationToken ct = default) =>
        await db.RatingScaleLevels.AsNoTracking().OrderBy(l => l.Value).ToListAsync(ct);

    /// <summary>Replaces labels and score ranges of the rating scale (ranges must cover 1–5 without gaps).</summary>
    public async Task SaveRatingScaleAsync(IReadOnlyList<(int Value, string Label, string? Description, decimal Min, decimal Max)> levels, CancellationToken ct = default)
    {
        if (!scope.CanConfigure)
            throw new ForbiddenAccessException();
        var ordered = levels.OrderBy(l => l.Min).ToList();
        if (ordered.Count < 2 || ordered[0].Min > PerformanceScoring.MinScore || ordered[^1].Max < PerformanceScoring.MaxScore)
            throw new DomainException("The rating scale must cover scores from 1 to 5.");
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].Min <= ordered[i - 1].Min || ordered[i].Min > ordered[i - 1].Max + 0.01m)
                throw new DomainException("Rating ranges must be in order without gaps.");

        var existing = await db.RatingScaleLevels.ToListAsync(ct);
        foreach (var level in levels)
        {
            var row = existing.FirstOrDefault(e => e.Value == level.Value) ?? db.RatingScaleLevels.Add(new RatingScaleLevel { Value = level.Value }).Entity;
            row.Label = level.Label.Trim();
            row.Description = level.Description?.Trim();
            row.MinScore = level.Min;
            row.MaxScore = level.Max;
        }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class GoalService(
    IApplicationDbContext db,
    PerformanceScope scope,
    ICurrentUser currentUser,
    IFileStorage storage,
    INotificationService notifications,
    IUserDirectory users,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public Task<PagedResult<GoalListItem>> ListAsync(GoalQuery query, CancellationToken ct = default)
    {
        var visible = scope.VisibleEmployeeIds();
        var goals = db.PerformanceGoals.AsNoTracking().Where(g => visible.Contains(g.EmployeeId));
        if (query.EmployeeId is int employeeId)
            goals = goals.Where(g => g.EmployeeId == employeeId);
        if (query.DepartmentId is int departmentId)
            goals = goals.Where(g => g.DepartmentId == departmentId);
        if (query.Status is GoalStatus status)
            goals = goals.Where(g => g.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            goals = goals.Where(g => g.Title.Contains(term) || g.Employee.FirstName.Contains(term) || g.Employee.LastName.Contains(term));
        }

        return goals.OrderBy(g => g.Status == GoalStatus.Completed || g.Status == GoalStatus.Cancelled).ThenBy(g => g.TargetDate)
            .Select(Projection())
            .ToPagedResultAsync(query, ct);
    }

    public async Task<GoalDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var goal = await db.PerformanceGoals.AsNoTracking().Include(g => g.Updates)
            .SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Goal", id);
        await scope.EnsureCanViewAsync(goal.EmployeeId, ct);

        var item = await db.PerformanceGoals.AsNoTracking().Where(g => g.Id == id).Select(Projection()).SingleAsync(ct);
        var names = await users.GetDisplayNamesAsync(goal.Updates.Where(u => u.CreatedById != null).Select(u => u.CreatedById!.Value), ct);
        var canManage = await scope.CanManageAsync(goal.EmployeeId, ct);
        return new GoalDetails(item, goal.Description, goal.SuccessCriteria, goal.CompletedAt,
            goal.Updates.OrderByDescending(u => u.CreatedAt).Select(u => new GoalUpdateDto(u.Id, u.Progress, u.Comment, u.EvidenceFileName,
                u.EvidenceStoragePath != null, u.CreatedAt, u.CreatedById is int uid ? names.GetValueOrDefault(uid) : null)).ToList(),
            canManage, goal.IsOpen && (canManage || goal.EmployeeId == currentUser.EmployeeId));
    }

    public async Task<GoalInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var g = await db.PerformanceGoals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Goal", id);
        await scope.EnsureCanManageAsync(g.EmployeeId, ct);
        return new GoalInput
        {
            Title = g.Title, Description = g.Description, SuccessCriteria = g.SuccessCriteria, EmployeeId = g.EmployeeId,
            Category = g.Category, Priority = g.Priority, StartDate = g.StartDate, TargetDate = g.TargetDate
        };
    }

    public async Task<int> SaveAsync(int? id, GoalInput input, CancellationToken ct = default)
    {
        await scope.EnsureCanManageAsync(input.EmployeeId, ct);
        if (input.TargetDate < input.StartDate)
            throw new DomainException("The target date must be on or after the start date.");
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
                       ?? throw new DomainException("Choose a valid employee.");

        PerformanceGoal goal;
        if (id is null)
        {
            goal = db.PerformanceGoals.Add(new PerformanceGoal()).Entity;
        }
        else
        {
            goal = await db.PerformanceGoals.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Goal", id);
            if (!goal.IsOpen)
                throw new DomainException($"A {goal.Status} goal cannot be edited.");
        }

        goal.Title = input.Title.Trim();
        goal.Description = input.Description?.Trim();
        goal.SuccessCriteria = input.SuccessCriteria?.Trim();
        goal.EmployeeId = employee.Id;
        goal.DepartmentId = employee.DepartmentId;
        goal.ManagerId = employee.ManagerId ?? currentUser.EmployeeId;
        goal.Category = input.Category;
        goal.Priority = input.Priority;
        goal.StartDate = input.StartDate!.Value;
        goal.TargetDate = input.TargetDate!.Value;
        goal.RefreshOverdue(Today);
        await db.SaveChangesAsync(ct);

        if (id is null && await users.GetUserIdForEmployeeAsync(employee.Id, ct) is int userId && userId != currentUser.UserId)
        {
            notifications.Notify([userId], "New goal assigned", $"{goal.Title} — due {goal.TargetDate:dd MMM yyyy}.", NotificationType.Info, $"/Goals/Details/{goal.Id}");
            await db.SaveChangesAsync(ct);
        }
        return goal.Id;
    }

    public async Task UpdateProgressAsync(int id, GoalProgressInput input, (Stream Content, string FileName, long Length)? evidence, CancellationToken ct = default)
    {
        var goal = await db.PerformanceGoals.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Goal", id);
        if (goal.EmployeeId != currentUser.EmployeeId)
            await scope.EnsureCanManageAsync(goal.EmployeeId, ct);

        string? path = null;
        string? fileName = null;
        if (evidence is { } file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".docx" or ".xlsx" or ".pptx"))
                throw new DomainException("Evidence must be a PDF, image or Office document.");
            if (file.Length > 10 * 1024 * 1024)
                throw new DomainException("Evidence files must be 10 MB or smaller.");
            path = await storage.SaveAsync("goal-evidence", file.Content, extension, ct);
            fileName = Path.GetFileName(file.FileName);
        }

        goal.UpdateProgress(input.Progress, Today, clock.GetUtcNow().UtcDateTime);
        goal.Updates.Add(new GoalProgressUpdate
        {
            Progress = input.Progress, Comment = input.Comment?.Trim(), EvidenceFileName = fileName, EvidenceStoragePath = path
        });

        if (goal.EmployeeId == currentUser.EmployeeId && goal.ManagerId is int managerId
            && await users.GetUserIdForEmployeeAsync(managerId, ct) is int managerUser)
            notifications.Notify([managerUser], goal.Status == GoalStatus.Completed ? "Goal completed" : "Goal progress updated",
                $"{goal.Title}: {goal.Progress}%", NotificationType.Info, $"/Goals/Details/{goal.Id}");
        await db.SaveChangesAsync(ct);
    }

    public async Task<(Stream Content, string FileName)> EvidenceAsync(int goalId, int updateId, CancellationToken ct = default)
    {
        var update = await db.GoalProgressUpdates.AsNoTracking().Include(u => u.PerformanceGoal)
            .SingleOrDefaultAsync(u => u.Id == updateId && u.PerformanceGoalId == goalId, ct) ?? throw new NotFoundException("Evidence", updateId);
        await scope.EnsureCanViewAsync(update.PerformanceGoal.EmployeeId, ct);
        if (update.EvidenceStoragePath is null)
            throw new NotFoundException("Evidence", updateId);
        return (await storage.OpenReadAsync(update.EvidenceStoragePath, ct), update.EvidenceFileName ?? "evidence");
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        var goal = await db.PerformanceGoals.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Goal", id);
        await scope.EnsureCanManageAsync(goal.EmployeeId, ct);
        goal.Cancel();
        await db.SaveChangesAsync(ct);
    }

    private static System.Linq.Expressions.Expression<Func<PerformanceGoal, GoalListItem>> Projection() => g =>
        new GoalListItem(g.Id, g.Title, g.EmployeeId, g.Employee.FirstName + " " + g.Employee.LastName, g.Employee.EmployeeNumber,
            g.Department.Name, g.Manager != null ? g.Manager.FirstName + " " + g.Manager.LastName : null, g.Category, g.Priority,
            g.StartDate, g.TargetDate, g.Progress, g.Status);
}
