using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Domain;

public class PerformanceDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 2);

    [Fact]
    public void Goal_progress_drives_status()
    {
        var goal = new PerformanceGoal { TargetDate = new DateOnly(2026, 12, 31) };
        goal.UpdateProgress(40, Today, Now);
        goal.Status.ShouldBe(GoalStatus.InProgress);

        goal.UpdateProgress(100, Today, Now);
        goal.Status.ShouldBe(GoalStatus.Completed);
        goal.CompletedAt.ShouldBe(Now);
        Should.Throw<DomainException>(() => goal.UpdateProgress(50, Today, Now));
    }

    [Fact]
    public void Open_goal_past_target_date_is_overdue()
    {
        var goal = new PerformanceGoal { TargetDate = new DateOnly(2026, 9, 30) };
        goal.UpdateProgress(60, Today, Now);
        goal.Status.ShouldBe(GoalStatus.Overdue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Goal_progress_must_be_a_percentage(int progress) =>
        Should.Throw<DomainException>(() => new PerformanceGoal().UpdateProgress(progress, Today, Now));

    [Fact]
    public void Review_follows_the_six_stage_process()
    {
        var review = new PerformanceReview();
        review.Scores.Add(new PerformanceReviewScore { Name = "Sales target", Score = 4, Weight = 100 });

        review.CompleteManagerEvaluation(Now);
        review.Status.ShouldBe(ReviewStatus.PendingSelfAssessment);

        review.SubmitSelfAssessment("Delivered the Q3 integration project.", Now);
        review.Status.ShouldBe(ReviewStatus.PendingManagerFeedback);

        review.ManagerComments = "Strong delivery; improve documentation.";
        review.ProvideManagerFeedback(Now);
        review.Status.ShouldBe(ReviewStatus.PendingAcknowledgement);

        review.Acknowledge("Agreed.", Now);
        review.Status.ShouldBe(ReviewStatus.PendingHrFinalization);

        review.Finalize(99, Now);
        review.IsFinalized.ShouldBeTrue();
        Should.Throw<DomainException>(review.Cancel);
    }

    [Fact]
    public void Review_steps_cannot_be_skipped()
    {
        var review = new PerformanceReview();
        Should.Throw<DomainException>(() => review.SubmitSelfAssessment("x", Now));
        Should.Throw<DomainException>(() => review.Finalize(1, Now));
        Should.Throw<DomainException>(() => review.CompleteManagerEvaluation(Now)); // no scores yet
    }
}
