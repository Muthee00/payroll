using PayMaster.Domain.Common;

namespace PayMaster.Domain.Entities.Performance;

/// <summary>
/// Configurable rating scale entry (e.g. 3 = "Meets Expectations"). A weighted score maps
/// to the level whose [MinScore, MaxScore] range contains it.
/// </summary>
public class RatingScaleLevel : AuditableEntity
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public string Color { get; set; } = "#64748b";
}
