using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;
using PayMaster.Web.ViewModels.Payroll;

namespace PayMaster.Web.Controllers;

[Authorize(Policy = Permissions.Payroll.View)]
public class PayrollHistoryController(PayrollService payroll, PayrollHistoryService history, EmployeeService employees) : AppController
{
    public async Task<IActionResult> Index([FromQuery] PayrollRunQuery query, CancellationToken ct) =>
        View(new PayrollHistoryViewModel
        {
            Query = query,
            Runs = await payroll.ListRunsAsync(query, ct),
            Trend = await payroll.GetTrendAsync(12, ct)
        });

    [Authorize(Policy = Permissions.Payroll.Export)]
    public async Task<IActionResult> Export([FromQuery] PayrollRunQuery query, CancellationToken ct) =>
        File(await history.ExportAsync(query, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Payroll-history-{DateTime.UtcNow:yyyyMMdd}.xlsx");

    public async Task<IActionResult> Compare(int? a, int? b, CancellationToken ct)
    {
        var runs = (await payroll.ListRunsAsync(new PayrollRunQuery { PageSize = 60 }, ct)).Items;
        return View(new PayrollCompareViewModel
        {
            Runs = runs,
            A = a,
            B = b,
            Comparison = a is int x && b is int y && x != y ? await history.CompareAsync(x, y, ct) : null
        });
    }

    public async Task<IActionResult> Employee(int id, CancellationToken ct) =>
        View(new EmployeePayrollHistoryViewModel
        {
            Employee = await employees.GetAsync(id, ct),
            Rows = await history.EmployeeHistoryAsync(id, ct)
        });
}
