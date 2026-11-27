using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Salary;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Web.Infrastructure;
using PayMaster.Web.ViewModels.Salary;

namespace PayMaster.Web.Controllers;

public class SalaryStructuresController(
    SalaryStructureService structures,
    SalaryChangeService salaryChanges,
    PayItemService payItems,
    EmployeeService employees) : AppController
{
    [Authorize(Policy = Permissions.Salary.View)]
    public async Task<IActionResult> Index(string tab = "structures", CancellationToken ct = default)
    {
        var canSeeApprovals = User.HasClaim(Permissions.ClaimType, Permissions.Salary.Approve)
                              || User.HasClaim(Permissions.ClaimType, Permissions.Salary.ManageEmployeeSalary);
        return View(new SalaryStructuresIndexViewModel
        {
            Tab = tab,
            Structures = await structures.ListAsync(ct: ct),
            Grades = await structures.ListGradesAsync(ct),
            Pending = canSeeApprovals ? await salaryChanges.ListPendingAsync(ct) : [],
            CanSeeApprovals = canSeeApprovals
        });
    }

    [RequireAnyPermission(Permissions.Salary.Approve, Permissions.Salary.ManageEmployeeSalary)]
    public IActionResult Approvals() => RedirectToAction(nameof(Index), new { tab = "approvals" });

    [Authorize(Policy = Permissions.Salary.View)]
    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await structures.GetAsync(id, ct));

    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View("Form", await BuildFormAsync(null, new SalaryStructureInput { EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow) }, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> Create(SalaryStructureInput input, CancellationToken ct)
    {
        input.Lines = input.Lines.Where(l => l.Amount > 0).ToList();
        if (ModelState.IsValid)
        {
            try
            {
                var id = await structures.CreateAsync(input, ct);
                ToastSuccess("Salary structure created as a draft. Activate it to assign it to employees.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("Form", await BuildFormAsync(null, input, ct));
    }

    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) =>
        View("Form", await BuildFormAsync(id, await structures.GetForEditAsync(id, ct), ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> Edit(int id, SalaryStructureInput input, CancellationToken ct)
    {
        input.Lines = input.Lines.Where(l => l.Amount > 0).ToList();
        if (ModelState.IsValid)
        {
            try
            {
                await structures.UpdateAsync(id, input, ct);
                ToastSuccess("Structure saved. Existing employee salaries are unchanged until a revision is approved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("Form", await BuildFormAsync(id, input, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public Task<IActionResult> SetStatus(int id, SalaryStructureStatus status, CancellationToken ct) =>
        ExecuteAsync(() => structures.SetStatusAsync(id, status, ct), $"Structure {Ui(status)}.",
            () => RedirectToAction(nameof(Details), new { id }), () => RedirectToAction(nameof(Details), new { id }));

    [Authorize(Policy = Permissions.Salary.ManageEmployeeSalary)]
    public async Task<IActionResult> Revise(int id, CancellationToken ct) =>
        View(new StructureRevisionViewModel
        {
            Structure = await structures.GetAsync(id, ct),
            Input = new StructureRevisionInput { EffectiveFrom = FirstOfNextMonth() }
        });

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageEmployeeSalary)]
    public async Task<IActionResult> Revise(int id, StructureRevisionInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await structures.ProposeRevisionAsync(id, input, ct);
                ToastSuccess($"{result.Proposed} salary revisions proposed and sent for approval.");
                if (result.SkippedEmployees.Count > 0)
                    ToastWarning($"Skipped {result.SkippedEmployees.Count} employee(s) with a change already awaiting approval: {string.Join(", ", result.SkippedEmployees)}.");
                return RedirectToAction(nameof(Index), new { tab = "approvals" });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(new StructureRevisionViewModel { Structure = await structures.GetAsync(id, ct), Input = input });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> SaveGrade(int? id, SalaryGradeInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ToastError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Index), new { tab = "grades" });
        }
        return await ExecuteAsync(() => structures.SaveGradeAsync(id, input, ct), id is null ? "Grade added." : "Grade updated.",
            () => RedirectToAction(nameof(Index), new { tab = "grades" }), () => RedirectToAction(nameof(Index), new { tab = "grades" }));
    }

    // ------------------------------------------------------------- Employee salary changes

    [Authorize(Policy = Permissions.Salary.ManageEmployeeSalary)]
    public async Task<IActionResult> ChangeSalary(int employeeId, int? structureId, decimal? basic, CancellationToken ct) =>
        View(await BuildChangeAsync(await salaryChanges.BuildProposalAsync(employeeId, structureId, basic, ct), ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageEmployeeSalary)]
    public async Task<IActionResult> ChangeSalary(SalaryChangeInput input, string? command, CancellationToken ct)
    {
        if (command == "load")
        {
            // Recalculate allowances from the chosen structure for the entered basic, then redisplay.
            ModelState.Clear();
            var loaded = await salaryChanges.BuildProposalAsync(input.EmployeeId, input.SalaryStructureId,
                input.BasicSalary > 0 ? input.BasicSalary : null, ct);
            loaded.EffectiveFrom = input.EffectiveFrom ?? loaded.EffectiveFrom;
            loaded.Reason = input.Reason;
            return View(await BuildChangeAsync(loaded, ct));
        }

        if (ModelState.IsValid)
        {
            try
            {
                await salaryChanges.ProposeAsync(input, ct);
                ToastSuccess("Salary change submitted for approval.");
                return Redirect(Url.Action("Details", "Employees", new { id = input.EmployeeId }) + "#salary");
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(await BuildChangeAsync(input, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.Approve)]
    public Task<IActionResult> Approve(int id, CancellationToken ct) =>
        ExecuteAsync(() => salaryChanges.ApproveAsync(id, ct), "Salary change approved.",
            () => RedirectToAction(nameof(Index), new { tab = "approvals" }), () => RedirectToAction(nameof(Index), new { tab = "approvals" }));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.Approve)]
    public Task<IActionResult> Reject(int id, string? reason, CancellationToken ct) =>
        ExecuteAsync(() => salaryChanges.RejectAsync(id, reason ?? string.Empty, ct), "Salary change rejected.",
            () => RedirectToAction(nameof(Index), new { tab = "approvals" }), () => RedirectToAction(nameof(Index), new { tab = "approvals" }));

    // ------------------------------------------------------------- Helpers

    private async Task<SalaryStructureFormViewModel> BuildFormAsync(int? id, SalaryStructureInput input, CancellationToken ct)
    {
        var allowances = (await payItems.ListComponentsAsync(includeInactive: false, ct))
            .Where(c => c.Type == ComponentType.Earning && c.IsRecurring).ToList();

        // One row per allowance so the form needs no client-side row management; zero amounts are ignored.
        var lines = allowances.Select(a => input.Lines.FirstOrDefault(l => l.SalaryComponentId == a.Id)
                                           ?? new StructureLineInput { SalaryComponentId = a.Id }).ToList();
        input.Lines = lines;

        return new SalaryStructureFormViewModel
        {
            Id = id, Input = input, Allowances = allowances,
            Grades = await structures.ListGradesAsync(ct), Lookups = await employees.GetLookupsAsync(ct)
        };
    }

    private async Task<SalaryChangeViewModel> BuildChangeAsync(SalaryChangeInput input, CancellationToken ct)
    {
        var names = (await payItems.ListComponentsAsync(includeInactive: true, ct)).ToDictionary(c => c.Id, c => c.Name);
        foreach (var c in input.Components)
            c.Name ??= names.GetValueOrDefault(c.SalaryComponentId);
        return new SalaryChangeViewModel
        {
            Employee = await employees.GetAsync(input.EmployeeId, ct),
            Input = input,
            Structures = (await employees.GetLookupsAsync(ct)).SalaryStructures
        };
    }

    private static string Ui(SalaryStructureStatus status) => status switch
    {
        SalaryStructureStatus.Active => "activated",
        SalaryStructureStatus.Archived => "archived",
        _ => "moved to draft"
    };

    private static DateOnly FirstOfNextMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new DateOnly(today.Year, today.Month, 1).AddMonths(1);
    }
}
