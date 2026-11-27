using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Performance;

/// <summary>
/// KPI definition. Department/position scoped when set; organisation-wide otherwise.
/// Actual values are captured per review in <see cref="PerformanceReviewScore"/>.
/// </summary>
public class Kpi : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MeasurementUnit { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }

    /// <summary>Relative weight in percent (weights in a review are normalised).</summary>
    public decimal Weight { get; set; }
    public KpiScoringMethod ScoringMethod { get; set; } = KpiScoringMethod.HigherIsBetter;
    public bool IsActive { get; set; } = true;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? PositionId { get; set; }
    public Position? Position { get; set; }
}
