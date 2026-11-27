using PayMaster.Application.Performance;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Performance;

public class PerformanceScoringTests
{
    [Theory]
    [InlineData(100, 100, 3.00)]   // on target = meets expectations
    [InlineData(100, 120, 3.60)]
    [InlineData(100, 200, 5.00)]   // capped at 5
    [InlineData(100, 50, 1.50)]
    [InlineData(100, 10, 1.00)]    // floored at 1
    public void Higher_is_better_scales_achievement_to_three_at_target(decimal target, decimal actual, decimal expected) =>
        PerformanceScoring.ScoreKpi(KpiScoringMethod.HigherIsBetter, target, actual).ShouldBe(expected);

    [Theory]
    [InlineData(4, 4, 3.00)]       // e.g. 4-hour response target met exactly
    [InlineData(4, 2, 5.00)]       // twice as fast, capped
    [InlineData(4, 6, 2.00)]
    [InlineData(4, 0, 5.00)]
    public void Lower_is_better_inverts_the_ratio(decimal target, decimal actual, decimal expected) =>
        PerformanceScoring.ScoreKpi(KpiScoringMethod.LowerIsBetter, target, actual).ShouldBe(expected);

    [Fact]
    public void Binary_and_manual_scoring()
    {
        PerformanceScoring.ScoreKpi(KpiScoringMethod.Binary, 1, 1).ShouldBe(3m);
        PerformanceScoring.ScoreKpi(KpiScoringMethod.Binary, 1, 0).ShouldBe(1m);
        PerformanceScoring.ScoreKpi(KpiScoringMethod.ManualRating, null, null, 4.5m).ShouldBe(4.5m);
        PerformanceScoring.ScoreKpi(KpiScoringMethod.ManualRating, null, null, 9m).ShouldBe(5m);
        Should.Throw<ArgumentException>(() => PerformanceScoring.ScoreKpi(KpiScoringMethod.HigherIsBetter, 100, null));
    }

    [Fact]
    public void Weighted_score_uses_relative_weights()
    {
        // Sales 50% weight scored 4, quality 30% scored 3, attendance 20% scored 5 → 2.0 + 0.9 + 1.0 = 3.9
        PerformanceScoring.WeightedScore([new(4m, 50m), new(3m, 30m), new(5m, 20m)]).ShouldBe(3.9m);
        // Weights need not add to 100.
        PerformanceScoring.WeightedScore([new(4m, 1m), new(2m, 1m)]).ShouldBe(3m);
        PerformanceScoring.WeightedScore([]).ShouldBeNull();
    }

    [Theory]
    [InlineData(1.2, "Needs Improvement")]
    [InlineData(2.5, "Meets Expectations")]
    [InlineData(3.49, "Meets Expectations")]
    [InlineData(3.5, "Exceeds Expectations")]
    [InlineData(5.0, "Outstanding")]
    public void Weighted_score_maps_to_configured_rating(decimal score, string label)
    {
        RatingScaleLevel[] scale =
        [
            new() { Value = 1, Label = "Needs Improvement", MinScore = 0m },
            new() { Value = 2, Label = "Below Expectations", MinScore = 1.5m },
            new() { Value = 3, Label = "Meets Expectations", MinScore = 2.5m },
            new() { Value = 4, Label = "Exceeds Expectations", MinScore = 3.5m },
            new() { Value = 5, Label = "Outstanding", MinScore = 4.5m }
        ];
        PerformanceScoring.Rate(score, scale)!.Label.ShouldBe(label);
    }
}
