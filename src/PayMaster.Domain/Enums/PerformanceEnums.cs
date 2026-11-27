namespace PayMaster.Domain.Enums;

public enum GoalStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Overdue = 4,
    Cancelled = 5
}

public enum GoalPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum GoalCategory
{
    Individual = 1,
    Team = 2,
    Departmental = 3,
    Organizational = 4,
    Development = 5
}

public enum KpiScoringMethod
{
    /// <summary>Score scales with actual/target (sales, revenue).</summary>
    HigherIsBetter = 1,

    /// <summary>Score scales with target/actual (defects, turnaround time).</summary>
    LowerIsBetter = 2,

    /// <summary>Target met or not.</summary>
    Binary = 3,

    /// <summary>Reviewer assigns the rating directly.</summary>
    ManualRating = 4
}

public enum ReviewType
{
    Monthly = 1,
    Quarterly = 2,
    SemiAnnual = 3,
    Annual = 4,
    Probation = 5
}

public enum ReviewCycleStatus
{
    Draft = 1,
    Active = 2,
    Closed = 3
}

/// <summary>
/// HR creates cycle → manager evaluates → employee self-assesses → manager feedback
/// → employee acknowledges → HR finalizes.
/// </summary>
public enum ReviewStatus
{
    PendingManagerEvaluation = 1,
    PendingSelfAssessment = 2,
    PendingManagerFeedback = 3,
    PendingAcknowledgement = 4,
    PendingHrFinalization = 5,
    Finalized = 6,
    Cancelled = 7
}

public enum ReviewScoreSource
{
    Kpi = 1,
    Goal = 2
}

public enum FeedbackType
{
    Manager = 1,
    Self = 2,
    Hr = 3
}

public enum DevelopmentPlanType
{
    Training = 1,
    Development = 2,
    ImprovementPlan = 3,
    CareerObjective = 4,
    SkillsAssessment = 5
}

public enum DevelopmentPlanStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}
