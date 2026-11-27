using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Notifications;

namespace PayMaster.Web.Controllers;

public class NotificationsController(INotificationService notifications, ICurrentUser currentUser) : AppController
{
    public async Task<IActionResult> Index(int page = 1, bool unread = false, CancellationToken ct = default)
    {
        var result = await notifications.ListAsync(currentUser.RequireUserId(), new PageQuery { Page = page, PageSize = 25 }, unread, ct);
        ViewBag.UnreadOnly = unread;
        return View(result);
    }

    [HttpPost]
    public async Task<IActionResult> Open(int id, CancellationToken ct)
    {
        var link = await notifications.MarkReadAsync(currentUser.RequireUserId(), id, ct);
        return !string.IsNullOrEmpty(link) && Url.IsLocalUrl(link) ? Redirect(link) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(currentUser.RequireUserId(), ct);
        ToastSuccess("All notifications marked as read.");
        return RedirectToLocal(Request.Headers.Referer.ToString() is { Length: > 0 } referer && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            ? uri.PathAndQuery
            : null);
    }
}
