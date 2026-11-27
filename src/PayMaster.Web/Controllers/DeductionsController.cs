using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Payroll;
using PayMaster.Application.Salary;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Web.Infrastructure;
using PayMaster.Web.ViewModels.Salary;

namespace PayMaster.Web.Controllers;

/// <summary>Employee earnings and deductions: recurring deductions, per-period inputs and the pay component catalogue.</summary>
[RequireAnyPermission(Permissions.Salary.View, Permissions.Salary.ManagePayItems)]
public class DeductionsController(PayItemService payItems, PayrollPeriodService periods) : AppController
{
    private bool CanManage => User.HasClaim(Permissions.ClaimType, Permissions.Salary.ManagePayItems);

    public async Task<IActionResult> Index([FromQuery] RecurringDeductionQuery query, string tab = "recurring",
        string? period = null, string? inputSearch = null, CancellationToken ct = default)
    {
        var (year, month) = PayrollPeriodService.TryParse(period, out var y, out var m) ? (y, m) : (periods.Current().Year, periods.Current().Month);
        return View(new DeductionsIndexViewModel
        {
            Tab = tab,
            DeductionQuery = query,
            Deductions = tab == "recurring" ? await payItems.ListDeductionsAsync(query, ct) : null,
            Inputs = tab == "inputs" ? await payItems.ListInputsAsync(year, month, inputSearch, ct) : null,
            InputSearch = inputSearch,
            Periods = periods.Recent(),
            Components = await payItems.ListComponentsAsync(includeInactive: true, ct),
            CanManage = CanManage
        });
    }

    // ------------------------------------------------------------- Recurring deductions

    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public async Task<IActionResult> CreateDeduction(int? employeeId, CancellationToken ct) =>
        View("DeductionForm", await DeductionFormAsync(null, new RecurringDeductionInput
        {
            EmployeeId = employeeId ?? 0, StartDate = new DateOnly(periods.Current().Year, periods.Current().Month, 1)
        }, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> CreateDeduction(RecurringDeductionInput input, CancellationToken ct) => SaveDeductionAsync(null, input, ct);

    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public async Task<IActionResult> EditDeduction(int id, CancellationToken ct) =>
        View("DeductionForm", await DeductionFormAsync(id, await payItems.GetDeductionAsync(id, ct), ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> EditDeduction(int id, RecurringDeductionInput input, CancellationToken ct) => SaveDeductionAsync(id, input, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> StopDeduction(int id, CancellationToken ct) =>
        ExecuteAsync(() => payItems.StopDeductionAsync(id, ct), "Deduction stopped. It will not be applied to future payrolls.",
            () => RedirectToAction(nameof(Index)), () => RedirectToAction(nameof(Index)));

    private async Task<IActionResult> SaveDeductionAsync(int? id, RecurringDeductionInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await payItems.SaveDeductionAsync(id, input, ct);
                ToastSuccess(id is null ? "Recurring deduction added." : "Deduction updated.");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("DeductionForm", await DeductionFormAsync(id, input, ct));
    }

    private async Task<RecurringDeductionFormViewModel> DeductionFormAsync(int? id, RecurringDeductionInput input, CancellationToken ct) =>
        new()
        {
            Id = id, Input = input, Employees = await payItems.ActiveEmployeesAsync(ct),
            DeductionTypes = (await payItems.ListComponentsAsync(includeInactive: false, ct))
                .Where(c => c.Type == ComponentType.Deduction && c.TaxTreatment != TaxTreatment.ReducesGross).ToList()
        };

    // ------------------------------------------------------------- Period inputs

    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public async Task<IActionResult> CreateInput(string? period, CancellationToken ct)
    {
        var (year, month) = PayrollPeriodService.TryParse(period, out var y, out var m) ? (y, m) : (periods.Current().Year, periods.Current().Month);
        return View("InputForm", await InputFormAsync(null, new PayrollInputInput { Year = year, Month = month }, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> CreateInput(PayrollInputInput input, CancellationToken ct) => SaveInputAsync(null, input, ct);

    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public async Task<IActionResult> EditInput(int id, CancellationToken ct) =>
        View("InputForm", await InputFormAsync(id, await payItems.GetInputAsync(id, ct), ct));

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> EditInput(int id, PayrollInputInput input, CancellationToken ct) => SaveInputAsync(id, input, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManagePayItems)]
    public Task<IActionResult> DeleteInput(int id, string? period, CancellationToken ct) =>
        ExecuteAsync(() => payItems.DeleteInputAsync(id, ct), "Item removed.",
            () => RedirectToAction(nameof(Index), new { tab = "inputs", period }), () => RedirectToAction(nameof(Index), new { tab = "inputs", period }));

    private async Task<IActionResult> SaveInputAsync(int? id, PayrollInputInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await payItems.SaveInputAsync(id, input, ct);
                ToastSuccess(id is null ? "Payroll item added." : "Payroll item updated.");
                return RedirectToAction(nameof(Index), new { tab = "inputs", period = $"{input.Year:D4}-{input.Month:D2}" });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("InputForm", await InputFormAsync(id, input, ct));
    }

    private async Task<PayrollInputFormViewModel> InputFormAsync(int? id, PayrollInputInput input, CancellationToken ct) =>
        new()
        {
            Id = id, Input = input, Employees = await payItems.ActiveEmployeesAsync(ct), Periods = periods.Recent(),
            ItemTypes = (await payItems.ListComponentsAsync(includeInactive: false, ct))
                .Where(c => !(c.Type == ComponentType.Earning && c.IsRecurring)).ToList()
        };

    // ------------------------------------------------------------- Pay components

    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public IActionResult CreateComponent() => View("ComponentForm", new SalaryComponentFormViewModel());

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public Task<IActionResult> CreateComponent(SalaryComponentInput input, CancellationToken ct) => SaveComponentAsync(null, input, ct);

    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public async Task<IActionResult> EditComponent(int id, CancellationToken ct) =>
        View("ComponentForm", new SalaryComponentFormViewModel { Id = id, Input = await payItems.GetComponentAsync(id, ct) });

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public Task<IActionResult> EditComponent(int id, SalaryComponentInput input, CancellationToken ct) => SaveComponentAsync(id, input, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.Salary.ManageStructures)]
    public Task<IActionResult> ToggleComponent(int id, bool active, CancellationToken ct) =>
        ExecuteAsync(() => payItems.SetComponentActiveAsync(id, active, ct), active ? "Component activated." : "Component deactivated.",
            () => RedirectToAction(nameof(Index), new { tab = "components" }), () => RedirectToAction(nameof(Index), new { tab = "components" }));

    private async Task<IActionResult> SaveComponentAsync(int? id, SalaryComponentInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await payItems.SaveComponentAsync(id, input, ct);
                ToastSuccess("Pay component saved.");
                return RedirectToAction(nameof(Index), new { tab = "components" });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View("ComponentForm", new SalaryComponentFormViewModel { Id = id, Input = input });
    }
}
