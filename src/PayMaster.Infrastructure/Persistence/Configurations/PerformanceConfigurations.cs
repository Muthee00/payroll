using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayMaster.Domain.Entities.Performance;

namespace PayMaster.Infrastructure.Persistence.Configurations;

public class KpiConfiguration : IEntityTypeConfiguration<Kpi>
{
    public void Configure(EntityTypeBuilder<Kpi> b)
    {
        b.Property(k => k.Name).HasMaxLength(150);
        b.Property(k => k.Description).HasMaxLength(1000);
        b.Property(k => k.MeasurementUnit).HasMaxLength(30);
        b.Property(k => k.TargetValue).HasPrecision(18, 4);
        b.Property(k => k.Weight).HasPrecision(9, 4);
        b.HasOne(k => k.Department).WithMany().HasForeignKey(k => k.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(k => k.Position).WithMany().HasForeignKey(k => k.PositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PerformanceGoalConfiguration : IEntityTypeConfiguration<PerformanceGoal>
{
    public void Configure(EntityTypeBuilder<PerformanceGoal> b)
    {
        b.Property(g => g.Title).HasMaxLength(200);
        b.Property(g => g.Description).HasMaxLength(2000);
        b.Property(g => g.SuccessCriteria).HasMaxLength(2000);
        b.HasIndex(g => new { g.EmployeeId, g.Status });
        b.HasIndex(g => g.TargetDate);
        b.HasOne(g => g.Employee).WithMany().HasForeignKey(g => g.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(g => g.Manager).WithMany().HasForeignKey(g => g.ManagerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(g => g.Department).WithMany().HasForeignKey(g => g.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(g => g.IsOpen);
    }
}

public class GoalProgressUpdateConfiguration : IEntityTypeConfiguration<GoalProgressUpdate>
{
    public void Configure(EntityTypeBuilder<GoalProgressUpdate> b)
    {
        b.Property(u => u.Comment).HasMaxLength(2000);
        b.Property(u => u.EvidenceFileName).HasMaxLength(255);
        b.Property(u => u.EvidenceStoragePath).HasMaxLength(500);
        b.HasOne(u => u.PerformanceGoal).WithMany(g => g.Updates)
            .HasForeignKey(u => u.PerformanceGoalId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewCycleConfiguration : IEntityTypeConfiguration<ReviewCycle>
{
    public void Configure(EntityTypeBuilder<ReviewCycle> b)
    {
        b.Property(c => c.Name).HasMaxLength(150);
        b.HasIndex(c => c.Status);
    }
}

public class PerformanceReviewConfiguration : IEntityTypeConfiguration<PerformanceReview>
{
    public void Configure(EntityTypeBuilder<PerformanceReview> b)
    {
        foreach (var text in new[]
                 {
                     nameof(PerformanceReview.Strengths), nameof(PerformanceReview.AreasForImprovement),
                     nameof(PerformanceReview.ManagerComments), nameof(PerformanceReview.SelfAssessment),
                     nameof(PerformanceReview.EmployeeComments), nameof(PerformanceReview.DevelopmentRecommendations),
                     nameof(PerformanceReview.HrComments)
                 })
        {
            b.Property(text).HasColumnType("text");
        }

        b.Property(r => r.OverallScore).HasPrecision(9, 4);
        b.Property(r => r.OverallRating).HasMaxLength(60);
        b.HasIndex(r => new { r.ReviewCycleId, r.EmployeeId }).IsUnique();
        b.HasIndex(r => r.Status);

        b.HasOne(r => r.ReviewCycle).WithMany(c => c.Reviews)
            .HasForeignKey(r => r.ReviewCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(r => r.IsFinalized);
    }
}

public class PerformanceReviewScoreConfiguration : IEntityTypeConfiguration<PerformanceReviewScore>
{
    public void Configure(EntityTypeBuilder<PerformanceReviewScore> b)
    {
        b.Property(s => s.Name).HasMaxLength(200);
        b.Property(s => s.TargetValue).HasPrecision(18, 4);
        b.Property(s => s.ActualValue).HasPrecision(18, 4);
        b.Property(s => s.Weight).HasPrecision(9, 4);
        b.Property(s => s.Score).HasPrecision(9, 4);
        b.Property(s => s.WeightedScore).HasPrecision(9, 4);
        b.Property(s => s.Comments).HasMaxLength(2000);
        b.HasOne(s => s.PerformanceReview).WithMany(r => r.Scores)
            .HasForeignKey(s => s.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Kpi).WithMany().HasForeignKey(s => s.KpiId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.PerformanceGoal).WithMany()
            .HasForeignKey(s => s.PerformanceGoalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PerformanceFeedbackConfiguration : IEntityTypeConfiguration<PerformanceFeedback>
{
    public void Configure(EntityTypeBuilder<PerformanceFeedback> b)
    {
        b.Property(f => f.Content).HasColumnType("text");
        b.HasOne(f => f.Employee).WithMany().HasForeignKey(f => f.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(f => f.PerformanceReview).WithMany(r => r.Feedback)
            .HasForeignKey(f => f.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RatingScaleLevelConfiguration : IEntityTypeConfiguration<RatingScaleLevel>
{
    public void Configure(EntityTypeBuilder<RatingScaleLevel> b)
    {
        b.Property(l => l.Label).HasMaxLength(60);
        b.Property(l => l.Description).HasMaxLength(500);
        b.Property(l => l.Color).HasMaxLength(9);
        b.Property(l => l.MinScore).HasPrecision(9, 4);
        b.Property(l => l.MaxScore).HasPrecision(9, 4);
        b.HasIndex(l => l.Value).IsUnique();
    }
}

public class DevelopmentPlanConfiguration : IEntityTypeConfiguration<DevelopmentPlan>
{
    public void Configure(EntityTypeBuilder<DevelopmentPlan> b)
    {
        b.Property(p => p.Title).HasMaxLength(200);
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.Skills).HasMaxLength(1000);
        b.HasIndex(p => new { p.EmployeeId, p.Status });
        b.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.PerformanceReview).WithMany()
            .HasForeignKey(p => p.PerformanceReviewId).OnDelete(DeleteBehavior.SetNull);
    }
}
