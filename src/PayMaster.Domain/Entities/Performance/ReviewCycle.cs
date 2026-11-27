using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Performance;

/// <summary>Review cycle created by HR (e.g. "Q3 2026 Quarterly Review").</summary>
public class ReviewCycle : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public ReviewType ReviewType { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public ReviewCycleStatus Status { get; set; } = ReviewCycleStatus.Draft;

    public ICollection<PerformanceReview> Reviews { get; set; } = [];
}
