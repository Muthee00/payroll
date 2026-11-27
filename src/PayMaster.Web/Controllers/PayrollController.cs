using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;
using PayMaster.Application.Payslips;
using PayMaster.Application.Salary;
using PayMaster.Domain.Common;
using PayMaster.Web.ViewModels.Payroll;

namespace PayMaster.Web.Controllers;

[Authorize(Policy = Permissions.Payroll.View)]
public class PayrollController(
    PayrollService payroll,
    PayrollPeriodService periods,
    PayslipService payslips,
    EmployeeService employees,
    PayItemService payItems,
    ISpreadsheetService spreadsheets) : AppController
{
    public async Task<IActionResult> Index(string? period, [FromQuery] PayrollLineQuery lineQuery, CancellationToken ct)
    {
        var selected = PayrollPeriodService.TryParse(period, out var y, out var m) ? PayrollPeriodService.Option(y, m) : periods.Current();
        var summary = await payroll.GetPeriodSummaryAsync(selected.Year, selected.Month, ct);
        var lines = summary.RunId is int runId
            ? (await payroll.GetRunAsync(runId, lineQuery with { PageSize = 10 }, ct)).Lines
            : null;

        return View(new PayrollIndexViewModel
        {
            Periods = periods.Recent(),
            Selected = selected,
            Summary = summary,
            Lines = lines,
            LineQuery = lineQuery,
            RecentRuns = (await payroll.ListRunsAsync(new PayrollRunQuery { PageSize = 5 }, ct)).Items,
            Departments = (await employees.GetLookupsAsync(ct)).Departments,
            Employees = await payItems.ActiveEmployeesAsync(ct)
        });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Process)]
    public async Task<IActionResult> Create(CreatePayrollRunInput input, CancellationToken ct)
    {
        try
        {
            var result = await payroll.CreateRunAsync(input, ct);
            ToastSuccess($"Payroll created for {result.EmployeeCount} employees. Run the calculation next.");
            if (result.Skipped.Count > 0)
                ToastWarning($"{result.Skipped.Count} employee(s) skipped: {string.Join("; ", result.Skipped.Take(3))}{(result.Skipped.Count > 3 ? "…" : "")}");
            return RedirectToAction(nameof(Run), new { id = result.RunId });
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
            return RedirectToAction(nameof(Index), new { period = $"{input.Year:D4}-{input.Month:D2}" });
        }
    }

    public async Task<IActionResult> Run(int id, [FromQuery] PayrollLineQuery lineQuery, CancellationToken ct) =>
        View(new PayrollRunViewModel { Details = await payroll.GetRunAsync(id, lineQuery with { PageSize = Math.Max(lineQuery.PageSize, 25) }, ct), LineQuery = lineQuery });

    /// <summary>Breakdown drawer content (HTML fragment).</summary>
    public async Task<IActionResult> Line(int runId, int lineId, CancellationToken ct) =>
        PartialView("_LineBreakdown", await payroll.GetLineAsync(runId, lineId, ct));

    [Authorize(Policy = Permissions.Payroll.Process)]
    public async Task<IActionResult> Validate(int id, CancellationToken ct)
    {
        var details = await payroll.GetRunAsync(id, new PayrollLineQuery { PageSize = 1 }, ct);
        return View(new PayrollValidationViewModel { Run = details.Summary, Issues = await payroll.ValidateAsync(id, ct) });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Process)]
    public async Task<IActionResult> Calculate(int id, CancellationToken ct)
    {
        try
        {
            var background = await payroll.CalculateAsync(id, ct);
            if (background)
                ToastSuccess("Calculation started in the background. This page refreshes automatically.");
            else
                ToastSuccess("Payroll calculated. Review the results before submitting for approval.");
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
        }
        return RedirectToAction(nameof(Run), new { id });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Process)]
    public Task<IActionResult> RemoveEmployee(int id, int lineId, CancellationToken ct) =>
        Workflow(id, () => payroll.RemoveEmployeeAsync(id, lineId, ct), "Employee removed from this payroll. Recalculate before submitting.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Process)]
    public Task<IActionResult> Submit(int id, CancellationToken ct) =>
        Workflow(id, () => payroll.SubmitAsync(id, ct), "Payroll submitted for approval.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Approve)]
    public Task<IActionResult> Approve(int id, CancellationToken ct) =>
        Workflow(id, () => payroll.ApproveAsync(id, ct), "Payroll approved.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Approve)]
    public Task<IActionResult> Reject(int id, string? reason, CancellationToken ct) =>
        Workflow(id, () => payroll.RejectAsync(id, reason ?? string.Empty, ct), "Payroll returned to draft.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Finalize)]
    public Task<IActionResult> Finalize(int id, CancellationToken ct) =>
        Workflow(id, () => payroll.FinalizeAsync(id, ct), "Payroll finalized. Payslips are now available to employees.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.MarkPaid)]
    public Task<IActionResult> MarkPaid(int id, CancellationToken ct) =>
        Workflow(id, () => payroll.MarkPaidAsync(id, ct), "Payroll marked as paid.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Process)]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct) =>
        Workflow(id, () => payroll.CancelAsync(id, reason ?? string.Empty, ct), "Payroll cancelled. Employees can be included in a new run.");

    [HttpPost]
    [Authorize(Policy = Permissions.Payroll.Reverse)]
    public async Task<IActionResult> Reverse(int id, string? reason, bool createCorrection, CancellationToken ct)
    {
        try
        {
            var correction = await payroll.ReverseAsync(id, reason ?? string.Empty, createCorrection, ct);
            ToastSuccess(correction is null ? "Payroll reversed." : "Payroll reversed and a correction run created.");
            return RedirectToAction(nameof(Run), new { id = correction ?? id });
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
            return RedirectToAction(nameof(Run), new { id });
        }
    }

    [Authorize(Policy = Permissions.Payroll.Export)]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var (content, fileName) = await payroll.ExportRunAsync(id, spreadsheets, ct);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [Authorize(Policy = Permissions.Payslips.ViewAll)]
    public async Task<IActionResult> Payslips(int id, CancellationToken ct)
    {
        try
        {
            var (pdf, fileName) = await payslips.RenderRunAsync(id, ct);
            return File(pdf, "application/pdf", fileName);
        }
        catch (DomainException ex)
        {
            ToastError(ex.Message);
            return RedirectToAction(nameof(Run), new { id });
        }
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Payslips.Generate)]
    public async Task<IActionResult> EmailPayslips(int id, CancellationToken ct)
    {
        try
        {
            var queued = await payslips.EmailRunAsync(id, ct);
            ToastSuccess(queued ? "Payslips are being emailed in the background. You'll be notified when done." : "Payslips emailed.");
        }
        catch (DomainException ex)
        {
            ToastError(ex.Message);
        }
        return RedirectToAction(nameof(Run), new { id });
    }

    private async Task<IActionResult> Workflow(int id, Func<Task> action, string success)
    {
        try
        {
            await action();
            ToastSuccess(success);
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
        }
        return RedirectToAction(nameof(Run), new { id });
    }
}
