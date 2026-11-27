using PayMaster.Application.Common.Models;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

// ------------------------------------------------------------------ KPIs

public record KpiDto(int Id, string Name, string? Description, string MeasurementUnit, decimal TargetValue, decimal Weight,
    KpiScoringMethod ScoringMethod, bool IsActive, int? DepartmentId, string? DepartmentName, int? PositionId, string? PositionTitle);

public class KpiInput
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string MeasurementUnit { get; set; } = "%";
    public decimal TargetValue { get; set; }
    public decimal Weight { get; set; } = 10;
    public KpiScoringMethod ScoringMethod { get; set; } = KpiScoringMethod.HigherIsBetter;
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
}

// ------------------------------------------------------------------ Goals

public record GoalQuery : PageQuery
{
    public int? EmployeeId { get; init; }
    public int? DepartmentId { get; init; }
    public GoalStatus? Status { get; init; }
}

public record GoalListItem(int Id, string Title, int EmployeeId, string EmployeeName, string EmployeeNumber, string DepartmentName,
    string? ManagerName, GoalCategory Category, GoalPriority Priority, DateOnly StartDate, DateOnly TargetDate, int Progress, GoalStatus Status);

public record GoalUpdateDto(int Id, int Progress, string? Comment, string? EvidenceFileName, bool HasEvidence, DateTime CreatedAt, string? By);

public record GoalDetails(GoalListItem Goal, string? Description, string? SuccessCriteria, DateTime? CompletedAt,
    IReadOnlyList<GoalUpdateDto> Updates, bool CanEdit, bool CanUpdateProgress);

public class GoalInput
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public int EmployeeId { get; set; }
    public GoalCategory Category { get; set; } = GoalCategory.Individual;
    public GoalPriority Priority { get; set; } = GoalPriority.Medium;
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetDate { get; set; }
}

public class GoalProgressInput
{
    public int Progress { get; set; }
    public string? Comment { get; set; }
}

// ------------------------------------------------------------------ Reviews

public class ReviewCycleInput
{
    public string Name { get; set; } = "";
    public ReviewType ReviewType { get; set; } = ReviewType.Quarterly;
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? DepartmentId { get; set; }
}

public record ReviewCycleDto(int Id, string Name, ReviewType ReviewType, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate,
    ReviewCycleStatus Status, int ReviewCount, int FinalizedCount);

public record ReviewQuery : PageQuery
{
    public int? CycleId { get; init; }
    public ReviewStatus? Status { get; init; }
    public int? DepartmentId { get; init; }
}

public record ReviewListItem(int Id, int CycleId, string CycleName, ReviewType ReviewType, int EmployeeId, string EmployeeName,
    string EmployeeNumber, string DepartmentName, string? ReviewerName, ReviewStatus Status, decimal? OverallScore, string? OverallRating,
    DateOnly DueDate, bool IsOverdue);

public record ReviewScoreDto(int Id, ReviewScoreSource Source, int? KpiId, int? GoalId, string Name, string? Unit, KpiScoringMethod? Method,
    decimal? TargetValue, decimal? ActualValue, decimal Weight, decimal Score, decimal WeightedScore, string? Comments);

public record ScoreCandidate(ReviewScoreSource Source, int Id, string Name, string? Unit, KpiScoringMethod Method, decimal? Target,
    decimal Weight, int? GoalProgress);

public record FeedbackDto(FeedbackType Type, string Content, string? By, DateTime CreatedAt);

public record DevelopmentPlanDto(int Id, int EmployeeId, string EmployeeName, DevelopmentPlanType Type, string Title, string? Description,
    string? Skills, DateOnly? StartDate, DateOnly? TargetDate, int Progress, DevelopmentPlanStatus Status, int? ReviewId);

public record ReviewDetails(
    ReviewListItem Review, DateOnly PeriodStart, DateOnly PeriodEnd, string? JobTitle,
    string? Strengths, string? AreasForImprovement, string? ManagerComments, string? SelfAssessment, string? EmployeeComments,
    string? DevelopmentRecommendations, string? HrComments,
    DateTime? ManagerEvaluatedAt, DateTime? SelfAssessedAt, DateTime? FeedbackGivenAt, DateTime? AcknowledgedAt, DateTime? FinalizedAt,
    IReadOnlyList<ReviewScoreDto> Scores, IReadOnlyList<ScoreCandidate> Candidates, IReadOnlyList<FeedbackDto> Feedback,
    IReadOnlyList<DevelopmentPlanDto> DevelopmentPlans,
    bool IsEmployee, bool IsReviewer, bool IsHr);

public class ScoreInput
{
    public ReviewScoreSource Source { get; set; }
    public int ItemId { get; set; }
    public bool Include { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? ManualScore { get; set; }
    public decimal Weight { get; set; }
    public string? Comments { get; set; }
}

public class ManagerEvaluationInput
{
    public List<ScoreInput> Scores { get; set; } = [];
    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public bool Complete { get; set; }
}

public class ManagerFeedbackInput
{
    public string ManagerComments { get; set; } = "";
    public string? DevelopmentRecommendations { get; set; }
}

public class DevelopmentPlanInput
{
    public int EmployeeId { get; set; }
    public int? ReviewId { get; set; }
    public DevelopmentPlanType Type { get; set; } = DevelopmentPlanType.Training;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Skills { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetDate { get; set; }
}

// ------------------------------------------------------------------ Dashboard

public record DepartmentPerformance(string Department, decimal? AverageScore, int Reviews, int Finalized, int ActiveGoals, int CompletedGoals);

public record PerformanceDashboard(
    int TotalEmployees, int EmployeesWithActiveGoals, int PendingReviews, int CompletedReviews, decimal? AverageScore,
    int OverdueReviews, int OverdueGoals, IReadOnlyList<DepartmentPerformance> Departments,
    IReadOnlyList<(string Label, int Count, string Color)> Distribution, IReadOnlyList<(string Label, decimal? Score)> Trend,
    IReadOnlyList<ReviewListItem> NeedsAttention, IReadOnlyList<ReviewListItem> UpcomingReviews,
    IReadOnlyList<GoalListItem> MyGoals, IReadOnlyList<ReviewListItem> MyReviews, IReadOnlyList<DevelopmentPlanDto> MyPlans);
