using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Web.ViewModels.Employees;

namespace PayMaster.Web.Controllers;

public class EmployeesController(
    EmployeeService employees,
    EmployeeDocumentService documents,
    EmployeeImportExportService importExport,
    EmployeeAccessScope scope,
    ICurrentUser currentUser) : AppController
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Authorize(Policy = Permissions.Employees.View)]
    public async Task<IActionResult> Index([FromQuery] EmployeeQuery query, CancellationToken ct)
    {
        var lookups = await employees.GetLookupsAsync(ct);
        return View(new EmployeeIndexViewModel
        {
            Query = query,
            Result = await employees.ListAsync(query, ct),
            Departments = lookups.Departments,
            ShowSalary = scope.CanViewSalary
        });
    }

    /// <summary>Access is enforced row-by-row in the service (managers: own team; employees: self).</summary>
    public async Task<IActionResult> Details(int id, CancellationToken ct) =>
        View(await employees.GetAsync(id, ct));

    [Authorize(Policy = Permissions.Self.Access)]
    public IActionResult Me() =>
        currentUser.EmployeeId is int id
            ? RedirectToAction(nameof(Details), new { id })
            : View("NoEmployeeRecord");

    [Authorize(Policy = Permissions.Employees.Manage)]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View("Form", new EmployeeFormViewModel
        {
            Lookups = await employees.GetLookupsAsync(ct),
            Input = new EmployeeInput { EmploymentDate = DateOnly.FromDateTime(DateTime.UtcNow) }
        });

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Manage)]
    public async Task<IActionResult> Create(EmployeeInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await employees.CreateAsync(input, ct);
                ToastSuccess(input.BasicSalary is null
                    ? "Employee created."
                    : "Employee created. The starting salary is awaiting approval.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        return View("Form", new EmployeeFormViewModel { Input = input, Lookups = await employees.GetLookupsAsync(ct) });
    }

    [Authorize(Policy = Permissions.Employees.Manage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var details = await employees.GetAsync(id, ct);
        return View("Form", new EmployeeFormViewModel
        {
            Id = id,
            EmployeeNumber = details.EmployeeNumber,
            Input = await employees.GetForEditAsync(id, ct),
            Lookups = await employees.GetLookupsAsync(ct)
        });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Manage)]
    public async Task<IActionResult> Edit(int id, EmployeeInput input, CancellationToken ct)
    {
        // Users without bank-detail access edit everything except the stored account number.
        if (!scope.CanViewBankDetails)
            input.KeepExistingBankAccount = true;

        if (ModelState.IsValid)
        {
            try
            {
                await employees.UpdateAsync(id, input, ct);
                ToastSuccess("Employee details saved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var details = await employees.GetAsync(id, ct);
        return View("Form", new EmployeeFormViewModel
        {
            Id = id, EmployeeNumber = details.EmployeeNumber, Input = input, Lookups = await employees.GetLookupsAsync(ct)
        });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Terminate)]
    public Task<IActionResult> Deactivate(int id, string? reason, CancellationToken ct) =>
        ExecuteAsync(() => employees.DeactivateAsync(id, string.IsNullOrWhiteSpace(reason) ? "Deactivated" : reason.Trim(), ct),
            "Employee deactivated. They will be excluded from payroll.", () => ToDetails(id), () => ToDetails(id));

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Terminate)]
    public Task<IActionResult> Reactivate(int id, CancellationToken ct) =>
        ExecuteAsync(() => employees.ReactivateAsync(id, ct), "Employee reactivated.", () => ToDetails(id), () => ToDetails(id));

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Manage)]
    public Task<IActionResult> SetLeave(int id, bool onLeave, CancellationToken ct) =>
        ExecuteAsync(() => employees.SetOnLeaveAsync(id, onLeave, ct),
            onLeave ? "Employee marked as on leave." : "Employee marked as returned from leave.", () => ToDetails(id), () => ToDetails(id));

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Terminate)]
    public Task<IActionResult> Terminate(int id, DateOnly terminationDate, string reason, CancellationToken ct) =>
        ExecuteAsync(() => employees.TerminateAsync(id, terminationDate, reason ?? string.Empty, ct),
            "Employee terminated and system access revoked.", () => ToDetails(id), () => ToDetails(id));

    // ------------------------------------------------------------ Import / export

    [Authorize(Policy = Permissions.Employees.Import)]
    public IActionResult Import() => View(new EmployeeImportViewModel());

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Import)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ToastError("Choose an .xlsx file to import.");
            return View(new EmployeeImportViewModel());
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ToastError("Only .xlsx files are supported. Download the template to get started.");
            return View(new EmployeeImportViewModel());
        }

        await using var stream = file.OpenReadStream();
        var result = await importExport.ImportAsync(stream, ct);
        if (result.Succeeded)
        {
            ToastSuccess($"{result.Imported} employees imported.");
            return RedirectToAction(nameof(Index));
        }
        return View(new EmployeeImportViewModel { Result = result });
    }

    [Authorize(Policy = Permissions.Employees.Import)]
    public IActionResult ImportTemplate() =>
        File(importExport.CreateTemplate(), XlsxContentType, "PayMaster-employee-import-template.xlsx");

    [Authorize(Policy = Permissions.Employees.Export)]
    public async Task<IActionResult> Export([FromQuery] EmployeeQuery query, CancellationToken ct) =>
        File(await importExport.ExportAsync(query, ct), XlsxContentType, $"Employees-{DateTime.UtcNow:yyyyMMdd}.xlsx");

    // ------------------------------------------------------------ Documents

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Manage)]
    [RequestSizeLimit(EmployeeDocumentService.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile? file, EmployeeDocumentType documentType,
        string? title, DateOnly? expiryDate, CancellationToken ct)
    {
        if (file is null)
        {
            ToastError("Choose a file to upload.");
            return ToDetails(id, "documents");
        }

        await using var stream = file.OpenReadStream();
        return await ExecuteAsync(
            () => documents.UploadAsync(id, new DocumentUpload(documentType, title ?? string.Empty, expiryDate,
                file.FileName, file.ContentType, file.Length, stream), ct),
            "Document uploaded.", () => ToDetails(id, "documents"), () => ToDetails(id, "documents"));
    }

    public async Task<IActionResult> Document(int id, int documentId, CancellationToken ct)
    {
        var download = await documents.DownloadAsync(id, documentId, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Employees.Manage)]
    public Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken ct) =>
        ExecuteAsync(() => documents.DeleteAsync(id, documentId, ct), "Document removed.",
            () => ToDetails(id, "documents"), () => ToDetails(id, "documents"));

    private RedirectResult ToDetails(int id, string? tab = null) =>
        Redirect(Url.Action(nameof(Details), new { id }) + (tab is null ? "" : $"#{tab}"));
}
