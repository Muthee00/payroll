using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

public record ScoredItem(decimal Score, decimal Weight);

/// <summary>
/// Pure scoring rules. A KPI's achievement ratio maps linearly onto the rating scale so that hitting
/// the target exactly scores 3 ("Meets Expectations"); scores are clamped to [1, 5]. Performance scores
/// never change pay - salary changes always go through the approval workflow.
/// </summary>
public static class PerformanceScoring
{
    public const decimal MinScore = 1m;
    public const decimal MaxScore = 5m;
    public const decimal TargetScore = 3m;

    public static decimal ScoreKpi(KpiScoringMethod method, decimal? target, decimal? actual, decimal? manualScore = null)
    {
        decimal raw = method switch
        {
            KpiScoringMethod.ManualRating => manualScore ?? throw new ArgumentException("A manual rating is required."),
            KpiScoringMethod.Binary => actual is decimal a && target is decimal t && a >= t ? TargetScore : MinScore,
            KpiScoringMethod.HigherIsBetter when target is > 0 && actual is decimal a => a / target.Value * TargetScore,
            KpiScoringMethod.LowerIsBetter when actual is decimal a && target is decimal t =>
                a <= 0 ? MaxScore : t / a * TargetScore,
            _ => throw new ArgumentException("Target and actual values are required for this KPI.")
        };
        return Math.Round(Math.Clamp(raw, MinScore, MaxScore), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Weighted average of item scores (weights need not sum to 100).</summary>
    public static decimal? WeightedScore(IReadOnlyCollection<ScoredItem> items)
    {
        var totalWeight = items.Sum(i => i.Weight);
        if (items.Count == 0 || totalWeight <= 0)
            return null;
        return Math.Round(items.Sum(i => i.Score * i.Weight) / totalWeight, 2, MidpointRounding.AwayFromZero);
    }

    public static RatingScaleLevel? Rate(decimal? score, IEnumerable<RatingScaleLevel> scale) =>
        score is decimal s ? scale.OrderBy(l => l.MinScore).LastOrDefault(l => s >= l.MinScore) : null;
}
