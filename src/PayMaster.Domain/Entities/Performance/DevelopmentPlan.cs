using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Performance;

/// <summary>Training recommendation, development/improvement plan, career objective or skills assessment.</summary>
public class DevelopmentPlan : SoftDeletableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? PerformanceReviewId { get; set; }
    public PerformanceReview? PerformanceReview { get; set; }

    public DevelopmentPlanType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Skills { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetDate { get; set; }
    public int Progress { get; set; }
    public DevelopmentPlanStatus Status { get; set; } = DevelopmentPlanStatus.Planned;
}
