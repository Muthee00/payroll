using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Performance;

/// <summary>
/// One employee's review within a cycle. Stages:
/// manager evaluation → self-assessment → manager feedback → acknowledgement → HR finalization.
/// </summary>
public class PerformanceReview : AuditableEntity, IAuditTracked
{
    public int ReviewCycleId { get; set; }
    public ReviewCycle ReviewCycle { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? ReviewerId { get; set; }
    public Employee? Reviewer { get; set; }

    public ReviewStatus Status { get; private set; } = ReviewStatus.PendingManagerEvaluation;

    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? ManagerComments { get; set; }
    public string? SelfAssessment { get; set; }
    public string? EmployeeComments { get; set; }
    public string? DevelopmentRecommendations { get; set; }
    public string? HrComments { get; set; }

    /// <summary>Weighted score on the rating scale (e.g. 1.00–5.00).</summary>
    public decimal? OverallScore { get; set; }
    public string? OverallRating { get; set; }

    public DateTime? ManagerEvaluatedAt { get; private set; }
    public DateTime? SelfAssessedAt { get; private set; }
    public DateTime? FeedbackGivenAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }
    public int? FinalizedById { get; private set; }

    public ICollection<PerformanceReviewScore> Scores { get; set; } = [];
    public ICollection<PerformanceFeedback> Feedback { get; set; } = [];

    public bool IsFinalized => Status == ReviewStatus.Finalized;

    public void CompleteManagerEvaluation(DateTime nowUtc)
    {
        EnsureStatus(ReviewStatus.PendingManagerEvaluation);
        if (Scores.Count == 0)
            throw new DomainException("Score at least one KPI or goal before completing the evaluation.");
        ManagerEvaluatedAt = nowUtc;
        Status = ReviewStatus.PendingSelfAssessment;
    }

    public void SubmitSelfAssessment(string selfAssessment, DateTime nowUtc)
    {
        EnsureStatus(ReviewStatus.PendingSelfAssessment);
        if (string.IsNullOrWhiteSpace(selfAssessment))
            throw new DomainException("The self-assessment cannot be empty.");
        SelfAssessment = selfAssessment.Trim();
        SelfAssessedAt = nowUtc;
        Status = ReviewStatus.PendingManagerFeedback;
    }

    public void ProvideManagerFeedback(DateTime nowUtc)
    {
        EnsureStatus(ReviewStatus.PendingManagerFeedback);
        if (string.IsNullOrWhiteSpace(ManagerComments))
            throw new DomainException("Manager comments are required.");
        FeedbackGivenAt = nowUtc;
        Status = ReviewStatus.PendingAcknowledgement;
    }

    public void Acknowledge(string? employeeComments, DateTime nowUtc)
    {
        EnsureStatus(ReviewStatus.PendingAcknowledgement);
        EmployeeComments = string.IsNullOrWhiteSpace(employeeComments) ? EmployeeComments : employeeComments.Trim();
        AcknowledgedAt = nowUtc;
        Status = ReviewStatus.PendingHrFinalization;
    }

    public void Finalize(int userId, DateTime nowUtc)
    {
        EnsureStatus(ReviewStatus.PendingHrFinalization);
        FinalizedById = userId;
        FinalizedAt = nowUtc;
        Status = ReviewStatus.Finalized;
    }

    public void Cancel()
    {
        if (IsFinalized)
            throw new DomainException("A finalized review cannot be cancelled.");
        Status = ReviewStatus.Cancelled;
    }

    public void EnsureScoresEditable()
    {
        if (Status is not (ReviewStatus.PendingManagerEvaluation or ReviewStatus.PendingManagerFeedback))
            throw new DomainException($"Scores cannot be changed while the review is {Status}.");
    }

    private void EnsureStatus(ReviewStatus expected)
    {
        if (Status != expected)
            throw new DomainException($"This step requires the review to be {expected}, but it is {Status}.");
    }
}

/// <summary>KPI or goal evaluation within a review.</summary>
public class PerformanceReviewScore : BaseEntity
{
    public int PerformanceReviewId { get; set; }
    public PerformanceReview PerformanceReview { get; set; } = null!;

    public ReviewScoreSource Source { get; set; }
    public int? KpiId { get; set; }
    public Kpi? Kpi { get; set; }
    public int? PerformanceGoalId { get; set; }
    public PerformanceGoal? PerformanceGoal { get; set; }

    /// <summary>Snapshot of the KPI/goal name at review time.</summary>
    public string Name { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal Weight { get; set; }

    /// <summary>Rating on the configured scale (e.g. 1–5).</summary>
    public decimal Score { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Comments { get; set; }
}

public class PerformanceFeedback : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? PerformanceReviewId { get; set; }
    public PerformanceReview? PerformanceReview { get; set; }
    public FeedbackType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}
