using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Performance;

public class PerformanceGoal : SoftDeletableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public GoalCategory Category { get; set; } = GoalCategory.Individual;
    public GoalPriority Priority { get; set; } = GoalPriority.Medium;
    public DateOnly StartDate { get; set; }
    public DateOnly TargetDate { get; set; }
    public int Progress { get; private set; }
    public GoalStatus Status { get; private set; } = GoalStatus.NotStarted;
    public DateTime? CompletedAt { get; private set; }

    public ICollection<GoalProgressUpdate> Updates { get; set; } = [];

    public bool IsOpen => Status is GoalStatus.NotStarted or GoalStatus.InProgress or GoalStatus.Overdue;

    public void UpdateProgress(int progress, DateOnly today, DateTime nowUtc)
    {
        if (!IsOpen)
            throw new DomainException($"A {Status} goal cannot be updated.");
        if (progress is < 0 or > 100)
            throw new DomainException("Progress must be between 0 and 100.");

        Progress = progress;
        if (progress == 100)
        {
            Status = GoalStatus.Completed;
            CompletedAt = nowUtc;
        }
        else
        {
            Status = progress == 0 ? GoalStatus.NotStarted : GoalStatus.InProgress;
            RefreshOverdue(today);
        }
    }

    /// <summary>Flags an open goal as overdue once its target date has passed.</summary>
    public void RefreshOverdue(DateOnly today)
    {
        if (IsOpen && today > TargetDate)
            Status = GoalStatus.Overdue;
    }

    public void Cancel()
    {
        if (Status == GoalStatus.Completed)
            throw new DomainException("A completed goal cannot be cancelled.");
        Status = GoalStatus.Cancelled;
    }
}

/// <summary>Progress update with optional supporting evidence.</summary>
public class GoalProgressUpdate : AuditableEntity
{
    public int PerformanceGoalId { get; set; }
    public PerformanceGoal PerformanceGoal { get; set; } = null!;
    public int Progress { get; set; }
    public string? Comment { get; set; }
    public string? EvidenceFileName { get; set; }
    public string? EvidenceStoragePath { get; set; }
}
