using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Performance;
using PayMaster.Domain.Common;
using PayMaster.Web.Infrastructure;
using PayMaster.Web.ViewModels.Performance;

namespace PayMaster.Web.Controllers;

[RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access)]
public class PerformanceController(PerformanceDashboardService dashboard) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await dashboard.GetAsync(ct));
}

[RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access)]
public class GoalsController(GoalService goals, KpiService kpis, ReviewService reviews, PerformanceScope scope, EmployeeService employees) : AppController
{
    public async Task<IActionResult> Index([FromQuery] GoalQuery query, string tab = "goals", CancellationToken ct = default)
    {
        var lookups = await employees.GetLookupsAsync(ct);
        return View(new GoalsIndexViewModel
        {
            Tab = tab,
            Query = query,
            Goals = tab == "goals" ? await goals.ListAsync(query, ct) : null,
            Kpis = tab == "kpis" ? await kpis.ListAsync(ct: ct) : [],
            RatingScale = tab == "scale" ? await kpis.RatingScaleAsync(ct) : [],
            Plans = tab == "development" ? await reviews.ListPlansAsync(null, ct) : [],
            Departments = lookups.Departments,
            CanCreate = scope.IsHr || scope.IsManager,
            CanConfigure = scope.CanConfigure
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await goals.GetAsync(id, ct));

    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public async Task<IActionResult> Create(int? employeeId, CancellationToken ct) =>
        View("Form", await FormAsync(null, new GoalInput
        {
            EmployeeId = employeeId ?? 0, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3)
        }, ct));

    [HttpPost]
    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public Task<IActionResult> Create(GoalInput input, CancellationToken ct) => SaveAsync(null, input, ct);

    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", await FormAsync(id, await goals.GetForEditAsync(id, ct), ct));

    [HttpPost]
    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public Task<IActionResult> Edit(int id, GoalInput input, CancellationToken ct) => SaveAsync(id, input, ct);

    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Progress(int id, GoalProgressInput input, IFormFile? evidence, CancellationToken ct)
    {
        try
        {
            if (evidence is { Length: > 0 })
            {
                await using var stream = evidence.OpenReadStream();
                await goals.UpdateProgressAsync(id, input, (stream, evidence.FileName, evidence.Length), ct);
            }
            else
            {
                await goals.UpdateProgressAsync(id, input, null, ct);
            }
            ToastSuccess("Progress updated.");
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Evidence(int id, int updateId, CancellationToken ct)
    {
        var (content, fileName) = await goals.EvidenceAsync(id, updateId, ct);
        return File(content, "application/octet-stream", fileName);
    }

    [HttpPost]
    public Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        ExecuteAsync(() => goals.CancelAsync(id, ct), "Goal cancelled.", () => RedirectToAction(nameof(Details), new { id }), () => RedirectToAction(nameof(Details), new { id }));

    // ---------------------------------------------------------------- KPIs & rating scale

    [Authorize(Policy = Permissions.Performance.Configure)]
    public async Task<IActionResult> Kpi(int? id, CancellationToken ct) =>
        View("KpiForm", new KpiFormViewModel
        {
            Id = id, Input = id is int k ? await kpis.GetAsync(k, ct) : new KpiInput(), Lookups = await employees.GetLookupsAsync(ct)
        });

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.Configure)]
    public async Task<IActionResult> Kpi(int? id, KpiInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await kpis.SaveAsync(id, input, ct);
                ToastSuccess("KPI saved.");
                return RedirectToAction(nameof(Index), new { tab = "kpis" });
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("KpiForm", new KpiFormViewModel { Id = id, Input = input, Lookups = await employees.GetLookupsAsync(ct) });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.Configure)]
    public Task<IActionResult> ToggleKpi(int id, bool active, CancellationToken ct) =>
        ExecuteAsync(() => kpis.SetActiveAsync(id, active, ct), active ? "KPI activated." : "KPI deactivated.",
            () => RedirectToAction(nameof(Index), new { tab = "kpis" }), () => RedirectToAction(nameof(Index), new { tab = "kpis" }));

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.Configure)]
    public Task<IActionResult> RatingScale(List<RatingLevelInput> levels, CancellationToken ct) =>
        ExecuteAsync(() => kpis.SaveRatingScaleAsync(levels.Select(l => (l.Value, l.Label, l.Description, l.MinScore, l.MaxScore)).ToList(), ct),
            "Rating scale saved. New and in-progress reviews use it immediately.",
            () => RedirectToAction(nameof(Index), new { tab = "scale" }), () => RedirectToAction(nameof(Index), new { tab = "scale" }));

    // ---------------------------------------------------------------- Development plans

    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public async Task<IActionResult> Plan(int? employeeId, int? reviewId, CancellationToken ct) =>
        View("PlanForm", new PlanFormViewModel
        {
            Input = new DevelopmentPlanInput { EmployeeId = employeeId ?? 0, ReviewId = reviewId },
            Employees = (await employees.GetLookupsAsync(ct)).Managers
        });

    [HttpPost]
    [RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam)]
    public async Task<IActionResult> Plan(DevelopmentPlanInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await reviews.SavePlanAsync(null, input, ct);
                ToastSuccess("Development plan added.");
                return input.ReviewId is int reviewId
                    ? RedirectToAction("Details", "Reviews", new { id = reviewId })
                    : RedirectToAction(nameof(Index), new { tab = "development" });
            }
            catch (Exception ex) when (ex is DomainException or ForbiddenAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("PlanForm", new PlanFormViewModel { Input = input, Employees = (await employees.GetLookupsAsync(ct)).Managers });
    }

    [HttpPost]
    public Task<IActionResult> PlanProgress(int id, int progress, CancellationToken ct) =>
        ExecuteAsync(() => reviews.UpdatePlanProgressAsync(id, progress, ct), "Development plan updated.",
            () => RedirectToAction(nameof(Index), new { tab = "development" }), () => RedirectToAction(nameof(Index), new { tab = "development" }));

    private async Task<IActionResult> SaveAsync(int? id, GoalInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var goalId = await goals.SaveAsync(id, input, ct);
                ToastSuccess(id is null ? "Goal created and the employee notified." : "Goal updated.");
                return RedirectToAction(nameof(Details), new { id = goalId });
            }
            catch (Exception ex) when (ex is DomainException or ForbiddenAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("Form", await FormAsync(id, input, ct));
    }

    private async Task<GoalFormViewModel> FormAsync(int? id, GoalInput input, CancellationToken ct)
    {
        var all = (await employees.GetLookupsAsync(ct)).Managers;
        var managed = scope.IsHr ? null : (await scope.ManagedEmployeeIds().ToListAsync(ct)).ToHashSet();
        var allowed = managed is null ? all : all.Where(e => managed.Contains(e.Id)).ToList();
        return new GoalFormViewModel { Id = id, Input = input, Employees = allowed };
    }
}

[RequireAnyPermission(Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access)]
public class ReviewsController(ReviewService reviews, PerformanceScope scope, EmployeeService employees) : AppController
{
    public async Task<IActionResult> Index([FromQuery] ReviewQuery query, CancellationToken ct) =>
        View(new ReviewsIndexViewModel
        {
            Query = query,
            Reviews = await reviews.ListAsync(query, ct),
            Cycles = await reviews.ListCyclesAsync(ct),
            Departments = (await employees.GetLookupsAsync(ct)).Departments,
            CanManageCycles = scope.CanManageCycles
        });

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await reviews.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.ManageCycles)]
    public async Task<IActionResult> CreateCycle(ReviewCycleInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ToastError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Index));
        }
        return await ExecuteAsync(() => reviews.CreateCycleAsync(input, ct), "Review cycle created and reviewers notified.",
            () => RedirectToAction(nameof(Index)), () => RedirectToAction(nameof(Index)));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.ManageCycles)]
    public Task<IActionResult> CloseCycle(int id, CancellationToken ct) =>
        ExecuteAsync(() => reviews.CloseCycleAsync(id, ct), "Cycle closed.", () => RedirectToAction(nameof(Index)), () => RedirectToAction(nameof(Index)));

    [HttpPost]
    public Task<IActionResult> Evaluate(int id, ManagerEvaluationInput input, CancellationToken ct) =>
        Step(id, () => reviews.SaveEvaluationAsync(id, input, ct), input.Complete ? "Evaluation completed. The employee has been asked for a self-assessment." : "Evaluation saved.");

    [HttpPost]
    public Task<IActionResult> SelfAssess(int id, string selfAssessment, CancellationToken ct) =>
        Step(id, () => reviews.SubmitSelfAssessmentAsync(id, selfAssessment ?? string.Empty, ct), "Self-assessment submitted to your manager.");

    [HttpPost]
    public Task<IActionResult> Feedback(int id, ManagerFeedbackInput input, CancellationToken ct) =>
        Step(id, () => reviews.SubmitFeedbackAsync(id, input, ct), "Feedback shared with the employee.");

    [HttpPost]
    public Task<IActionResult> Acknowledge(int id, string? comments, CancellationToken ct) =>
        Step(id, () => reviews.AcknowledgeAsync(id, comments, ct), "Feedback acknowledged. HR will finalize the review.");

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.ManageCycles)]
    public Task<IActionResult> Finalize(int id, string? hrComments, CancellationToken ct) =>
        Step(id, () => reviews.FinalizeAsync(id, hrComments, ct), "Review finalized.");

    [HttpPost]
    [Authorize(Policy = Permissions.Performance.ManageCycles)]
    public Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        Step(id, () => reviews.CancelAsync(id, ct), "Review cancelled.");

    private Task<IActionResult> Step(int id, Func<Task> action, string success) =>
        ExecuteAsync(action, success, () => RedirectToAction(nameof(Details), new { id }), () => RedirectToAction(nameof(Details), new { id }));
}
