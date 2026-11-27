using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Application.Performance;
using PayMaster.Application.Reports;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Enums;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Services;
using PayMaster.Tests.IntegrationTests.Employees;
using PayMaster.Tests.TestSupport;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests.Performance;

[Trait("Category", "MySql")]
[Collection(MySqlCollection.Name)]
public class PerformanceTests : IClassFixture<EmployeeFixture>
{
    private readonly EmployeeFixture f;
    private static int _seq;

    public PerformanceTests(EmployeeFixture fixture)
    {
        f = fixture;
        using var ctx = f.Db.CreateContext();
        if (!ctx.RatingScaleLevels.Any())
        {
            ctx.RatingScaleLevels.AddRange(
                new RatingScaleLevel { Value = 1, Label = "Needs Improvement", MinScore = 0m, MaxScore = 1.4999m },
                new RatingScaleLevel { Value = 2, Label = "Below Expectations", MinScore = 1.5m, MaxScore = 2.4999m },
                new RatingScaleLevel { Value = 3, Label = "Meets Expectations", MinScore = 2.5m, MaxScore = 3.4999m },
                new RatingScaleLevel { Value = 4, Label = "Exceeds Expectations", MinScore = 3.5m, MaxScore = 4.4999m },
                new RatingScaleLevel { Value = 5, Label = "Outstanding", MinScore = 4.5m, MaxScore = 5m });
            ctx.SaveChanges();
        }
    }

    private PerformanceScope Scope(ApplicationDbContext ctx) => new(ctx, f.Db.User);
    private NotificationService Notes(ApplicationDbContext ctx) => new(ctx, f.Users, f.Db.Clock);
    private ReviewService Reviews(ApplicationDbContext ctx) =>
        new(ctx, Scope(ctx), f.Db.User, Notes(ctx), f.Users, new AuditService(ctx, f.Db.User, f.Db.Clock), f.Db.Clock);
    private GoalService Goals(ApplicationDbContext ctx) =>
        new(ctx, Scope(ctx), f.Db.User, new LocalFileStorage(Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { RootPath = Path.Combine(Path.GetTempPath(), "paymaster-tests") })),
            Notes(ctx), f.Users, f.Db.Clock);
    private KpiService Kpis(ApplicationDbContext ctx) => new(ctx, Scope(ctx));

    /// <summary>A department with a head (manager), one report and one outsider in another department.</summary>
    private async Task<(int DeptId, int Manager, int Report, int Outsider)> TeamAsync()
    {
        f.AsHr();
        var n = Interlocked.Increment(ref _seq);
        await using var ctx = f.Db.CreateContext();
        var dept = new Domain.Entities.Organization.Department { Code = $"P{n}", Name = $"Perf Dept {n}" };
        ctx.Departments.Add(dept);
        await ctx.SaveChangesAsync();
        var service = f.Employees(ctx);
        var manager = await service.CreateAsync(f.NewInput(departmentId: dept.Id, first: "Mgr", last: $"P{n}"));
        var reportInput = f.NewInput(departmentId: dept.Id, first: "Rep", last: $"P{n}");
        reportInput.ManagerId = manager;
        var report = await service.CreateAsync(reportInput);
        var outsider = await service.CreateAsync(f.NewInput(departmentId: f.Finance, first: "Out", last: $"P{n}"));
        dept.HeadEmployeeId = manager;
        await ctx.SaveChangesAsync();
        return (dept.Id, manager, report, outsider);
    }

    [Fact]
    public async Task Full_review_workflow_with_weighted_kpi_and_goal_scores()
    {
        var team = await TeamAsync();
        await using var ctx = f.Db.CreateContext();

        f.AsHr();
        var salesKpi = await Kpis(ctx).SaveAsync(null, new KpiInput
        {
            Name = "Sales target", MeasurementUnit = "KES", TargetValue = 1_000_000m, Weight = 60, DepartmentId = team.DeptId
        });
        f.As(Roles.Manager, team.Manager, userId: 300);
        var goalId = await Goals(ctx).SaveAsync(null, new GoalInput
        {
            Title = "Launch partner programme", EmployeeId = team.Report, StartDate = new DateOnly(2026, 7, 1), TargetDate = new DateOnly(2026, 9, 30)
        });

        f.AsHr();
        var cycleId = await Reviews(ctx).CreateCycleAsync(new ReviewCycleInput
        {
            Name = $"Q3 2026 {team.DeptId}", ReviewType = ReviewType.Quarterly, PeriodStart = new DateOnly(2026, 7, 1),
            PeriodEnd = new DateOnly(2026, 9, 30), DueDate = new DateOnly(2026, 10, 31), DepartmentId = team.DeptId
        });
        var review = await ctx.PerformanceReviews.SingleAsync(r => r.ReviewCycleId == cycleId && r.EmployeeId == team.Report);
        review.ReviewerId.ShouldBe(team.Manager);

        // The employee cannot evaluate themselves.
        f.As(Roles.Employee, team.Report, userId: 301);
        await Should.ThrowAsync<ForbiddenAccessException>(() => Reviews(ctx).SaveEvaluationAsync(review.Id, new ManagerEvaluationInput()));

        f.As(Roles.Manager, team.Manager, userId: 300);
        var details = await Reviews(ctx).GetAsync(review.Id);
        details.Candidates.ShouldContain(c => c.Source == ReviewScoreSource.Kpi && c.Id == salesKpi);
        details.Candidates.ShouldContain(c => c.Source == ReviewScoreSource.Goal && c.Id == goalId);

        await Reviews(ctx).SaveEvaluationAsync(review.Id, new ManagerEvaluationInput
        {
            Complete = true,
            Strengths = "Strong client relationships",
            Scores =
            [
                new ScoreInput { Source = ReviewScoreSource.Kpi, ItemId = salesKpi, Include = true, ActualValue = 1_200_000m, Weight = 60 },
                new ScoreInput { Source = ReviewScoreSource.Goal, ItemId = goalId, Include = true, ManualScore = 4m, Weight = 40 }
            ]
        });

        await using (var check = f.Db.CreateContext())
        {
            var saved = await check.PerformanceReviews.Include(r => r.Scores).SingleAsync(r => r.Id == review.Id);
            saved.Status.ShouldBe(ReviewStatus.PendingSelfAssessment);
            saved.Scores.Single(s => s.Source == ReviewScoreSource.Kpi).Score.ShouldBe(3.6m);   // 120% of target
            saved.OverallScore.ShouldBe(3.76m);                                                 // (3.6×60 + 4×40) / 100
            saved.OverallRating.ShouldBe("Exceeds Expectations");
        }

        f.As(Roles.Employee, team.Report, userId: 301);
        await Reviews(ctx).SubmitSelfAssessmentAsync(review.Id, "Exceeded sales target and launched the programme.");
        f.As(Roles.Manager, team.Manager, userId: 300);
        await Reviews(ctx).SubmitFeedbackAsync(review.Id, new ManagerFeedbackInput { ManagerComments = "Excellent quarter.", DevelopmentRecommendations = "Negotiation course" });
        f.As(Roles.Employee, team.Report, userId: 301);
        await Reviews(ctx).AcknowledgeAsync(review.Id, "Thank you");

        // Managers cannot finalize; HR can.
        f.As(Roles.Manager, team.Manager, userId: 300);
        await Should.ThrowAsync<ForbiddenAccessException>(() => Reviews(ctx).FinalizeAsync(review.Id, null));
        f.AsHr();
        await Reviews(ctx).FinalizeAsync(review.Id, "Agreed");

        await using var verify = f.Db.CreateContext();
        var final = await verify.PerformanceReviews.Include(r => r.Feedback).SingleAsync(r => r.Id == review.Id);
        final.Status.ShouldBe(ReviewStatus.Finalized);
        final.Feedback.Select(x => x.Type).ShouldBe([FeedbackType.Self, FeedbackType.Manager, FeedbackType.Hr], ignoreOrder: true);
        (await verify.AuditLogs.AnyAsync(a => a.Action == "Performance.ReviewFinalized" && a.EntityId == review.Id.ToString())).ShouldBeTrue();
    }

    [Fact]
    public async Task Goals_are_visible_only_to_the_team_and_employee_can_update_progress()
    {
        var team = await TeamAsync();
        await using var ctx = f.Db.CreateContext();

        f.As(Roles.Manager, team.Manager, userId: 310);
        var goalId = await Goals(ctx).SaveAsync(null, new GoalInput
        {
            Title = "Reduce ticket backlog", EmployeeId = team.Report, StartDate = new DateOnly(2026, 9, 1), TargetDate = new DateOnly(2026, 12, 31)
        });
        await Should.ThrowAsync<ForbiddenAccessException>(() => Goals(ctx).SaveAsync(null, new GoalInput
        {
            Title = "Not my team", EmployeeId = team.Outsider, StartDate = new DateOnly(2026, 9, 1), TargetDate = new DateOnly(2026, 12, 31)
        }));

        f.As(Roles.Employee, team.Report, userId: 311);
        await Goals(ctx).UpdateProgressAsync(goalId, new GoalProgressInput { Progress = 60, Comment = "Halfway" },
            (new MemoryStream("%PDF-1.4 evidence"u8.ToArray()), "evidence.pdf", 17));
        var details = await Goals(ctx).GetAsync(goalId);
        details.Goal.Progress.ShouldBe(60);
        details.Goal.Status.ShouldBe(GoalStatus.InProgress);
        details.Updates.ShouldHaveSingleItem().HasEvidence.ShouldBeTrue();

        f.As(Roles.Employee, team.Outsider, userId: 312);
        await Should.ThrowAsync<ForbiddenAccessException>(() => Goals(ctx).GetAsync(goalId));
        (await Goals(ctx).ListAsync(new GoalQuery { Search = "ticket backlog" })).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_report_in_the_catalogue_runs()
    {
        f.AsHr();
        f.Db.User.Permissions.Add(Permissions.Employees.ViewBankDetails);
        await using var ctx = f.Db.CreateContext();
        var payroll = new PayrollReportService(ctx, f.Db.User, f.Db.Clock);
        var performance = new PerformanceReportService(ctx, Scope(ctx), payroll);
        var filter = new ReportFilter { From = "2026-01", To = "2026-12" };

        foreach (var definition in ReportCatalog.All)
        {
            var result = definition.Category == "Performance"
                ? await performance.RunAsync(definition.Key, filter)
                : await payroll.RunAsync(definition.Key, filter);
            result.Columns.ShouldNotBeEmpty(definition.Key);
            result.Rows.ShouldAllBe(r => r.Length == result.Columns.Count, definition.Key);
        }
        await Should.ThrowAsync<DomainException>(() => payroll.RunAsync("nope", filter));
    }
}
