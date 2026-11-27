using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Payslips;
using PayMaster.Domain.Common;
using PayMaster.Web.Infrastructure;
using PayMaster.Web.ViewModels.Payslips;

namespace PayMaster.Web.Controllers;

/// <summary>Payslips: employees see only their own (enforced in <see cref="PayslipService"/>); payroll staff see all.</summary>
[RequireAnyPermission(Permissions.Payslips.ViewAll, Permissions.Self.Access)]
public class PayslipsController(PayslipService payslips, EmployeeService employees) : AppController
{
    public async Task<IActionResult> Index([FromQuery] PayslipQuery query, bool mine = false, CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim(Permissions.ClaimType, Permissions.Payslips.ViewAll);
        var ownOnly = mine || !canViewAll;
        return View(new PayslipIndexViewModel
        {
            Query = query,
            OwnOnly = ownOnly,
            CanViewAll = canViewAll,
            Result = await payslips.ListAsync(query, ownOnly, ct),
            Departments = canViewAll ? (await employees.GetLookupsAsync(ct)).Departments : []
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await payslips.GetDocumentAsync(id, ct));

    public async Task<IActionResult> Pdf(int id, bool inline = false, CancellationToken ct = default)
    {
        var (pdf, fileName) = await payslips.RenderPdfAsync(id, ct);
        if (inline)
        {
            Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
            return File(pdf, "application/pdf");
        }
        return File(pdf, "application/pdf", fileName);
    }

    [HttpPost]
    public async Task<IActionResult> Email(int id, CancellationToken ct)
    {
        try
        {
            await payslips.EmailAsync(id, ct);
            ToastSuccess("Payslip emailed.");
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
