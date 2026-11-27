using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;
using PayMaster.Application.Reports;
using PayMaster.Domain.Common;
using PayMaster.Web.Infrastructure;
using PayMaster.Web.ViewModels.Reports;

namespace PayMaster.Web.Controllers;

[RequireAnyPermission(Permissions.Reports.Payroll, Permissions.Reports.Performance)]
public class ReportsController(
    PayrollReportService payrollReports,
    PerformanceReportService performanceReports,
    ReportExporter exporter,
    EmployeeService employees,
    PayrollPeriodService periods) : AppController
{
    public IActionResult Index() =>
        View(ReportCatalog.All.Where(r => User.HasClaim(Permissions.ClaimType, r.Permission)).ToList());

    public async Task<IActionResult> Run(string id, [FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var (definition, result) = await RunAsync(id, filter, ct);
        if (result is null)
            return RedirectToAction(nameof(Index));
        var lookups = await employees.GetLookupsAsync(ct);
        return View("Report", new ReportViewModel
        {
            Definition = definition!,
            Result = result,
            Filter = filter,
            Periods = periods.Recent(36, 1),
            Departments = lookups.Departments,
            Employees = lookups.Managers
        });
    }

    public async Task<IActionResult> Excel(string id, [FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var (_, result) = await RunAsync(id, filter, ct);
        return result is null
            ? RedirectToAction(nameof(Index))
            : File(await exporter.ToExcelAsync(result, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{result.Key}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> Pdf(string id, [FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var (_, result) = await RunAsync(id, filter, ct);
        return result is null
            ? RedirectToAction(nameof(Index))
            : File(await exporter.ToPdfAsync(result, ct), "application/pdf", $"{result.Key}-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    private async Task<(ReportDefinition? Definition, ReportResult? Result)> RunAsync(string id, ReportFilter filter, CancellationToken ct)
    {
        var definition = ReportCatalog.Find(id) ?? throw new NotFoundException("Report", id);
        if (!User.HasClaim(Permissions.ClaimType, definition.Permission))
            throw new ForbiddenAccessException();
        try
        {
            var result = definition.Category == "Performance"
                ? await performanceReports.RunAsync(id, filter, ct)
                : await payrollReports.RunAsync(id, filter, ct);
            return (definition, result);
        }
        catch (DomainException ex)
        {
            ToastError(ex.Message);
            return (definition, null);
        }
    }
}
