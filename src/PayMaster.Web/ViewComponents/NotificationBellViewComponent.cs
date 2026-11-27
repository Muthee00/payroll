using Microsoft.AspNetCore.Mvc;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Notifications;

namespace PayMaster.Web.ViewComponents;

public class NotificationBellViewComponent(INotificationService notifications, ICurrentUser currentUser) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (currentUser.UserId is not int userId)
            return Content(string.Empty);

        var (unread, latest) = await notifications.GetSummaryAsync(userId);
        return View((unread, latest));
    }
}
