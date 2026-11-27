using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Domain.Common;

namespace PayMaster.Web.Controllers;

/// <summary>Base controller: toast helpers and translation of business-rule failures into user feedback.</summary>
public abstract class AppController : Controller
{
    public const string ToastSuccessKey = "Toast.Success";
    public const string ToastErrorKey = "Toast.Error";
    public const string ToastWarningKey = "Toast.Warning";

    protected void ToastSuccess(string message) => TempData[ToastSuccessKey] = message;
    protected void ToastError(string message) => TempData[ToastErrorKey] = message;
    protected void ToastWarning(string message) => TempData[ToastWarningKey] = message;

    /// <summary>
    /// Runs a state-changing operation; business-rule violations become an error toast
    /// and the user is sent to <paramref name="onFailure"/> instead of an error page.
    /// </summary>
    protected async Task<IActionResult> ExecuteAsync(Func<Task> operation, string successMessage,
        Func<IActionResult> onSuccess, Func<IActionResult> onFailure)
    {
        try
        {
            await operation();
            ToastSuccess(successMessage);
            return onSuccess();
        }
        catch (Exception ex) when (ex is DomainException or ConflictException or FluentValidation.ValidationException)
        {
            ToastError(ex is FluentValidation.ValidationException ve
                ? string.Join(" ", ve.Errors.Select(e => e.ErrorMessage))
                : ex.Message);
            return onFailure();
        }
    }

    protected IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
}
