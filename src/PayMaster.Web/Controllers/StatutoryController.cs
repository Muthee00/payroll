using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Statutory;
using PayMaster.Domain.Common;
using PayMaster.Web.ViewModels.Statutory;

namespace PayMaster.Web.Controllers;

[Authorize(Policy = Permissions.Statutory.View)]
public class StatutoryController(StatutoryConfigService statutory) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var versions = await statutory.ListAsync(ct);
        var current = versions.FirstOrDefault(v => v.Status == Domain.Enums.StatutoryConfigurationStatus.Active);
        return View(new StatutoryIndexViewModel
        {
            Versions = versions,
            Current = current is null ? null : await statutory.GetAsync(current.Id, ct)
        });
    }

    public async Task<IActionResult> Details(int id, decimal? previewGross, CancellationToken ct)
    {
        var details = await statutory.GetAsync(id, ct);
        var model = new StatutoryDetailsViewModel { Details = details, PreviewGross = previewGross ?? 100_000m };
        if (details.ValidationProblems.Count == 0)
            model.Preview = await statutory.PreviewAsync(id, model.PreviewGross, ct);
        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Statutory.Manage)]
    public async Task<IActionResult> CreateDraft(int? sourceId, CancellationToken ct)
    {
        try
        {
            var id = await statutory.CreateDraftAsync(sourceId, ct);
            ToastSuccess("Draft version created. Update the values, then activate it.");
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex) when (ex is DomainException or ConflictException)
        {
            ToastError(ex.Message);
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = Permissions.Statutory.Manage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) =>
        View(new StatutoryEditViewModel { Id = id, Input = await statutory.GetForEditAsync(id, ct) });

    [HttpPost]
    [Authorize(Policy = Permissions.Statutory.Manage)]
    public async Task<IActionResult> Edit(int id, StatutoryConfigInput input, CancellationToken ct)
    {
        try
        {
            await statutory.UpdateDraftAsync(id, input, ct);
            ToastSuccess("Draft saved. Check the sample calculation before activating.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new StatutoryEditViewModel { Id = id, Input = input });
        }
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Statutory.Manage)]
    public Task<IActionResult> Activate(int id, CancellationToken ct) =>
        ExecuteAsync(() => statutory.ActivateAsync(id, ct), "Version activated. Payroll for periods from its effective date will use these rules.",
            () => RedirectToAction(nameof(Details), new { id }), () => RedirectToAction(nameof(Details), new { id }));

    [HttpPost]
    [Authorize(Policy = Permissions.Statutory.Manage)]
    public Task<IActionResult> Delete(int id, CancellationToken ct) =>
        ExecuteAsync(() => statutory.DeleteDraftAsync(id, ct), "Draft deleted.",
            () => RedirectToAction(nameof(Index)), () => RedirectToAction(nameof(Details), new { id }));
}
