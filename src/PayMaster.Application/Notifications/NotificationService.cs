using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Notifications;

public record NotificationDto(int Id, string Title, string Message, NotificationType Type, string? Link, bool IsRead, DateTime CreatedAt);

public interface INotificationService
{
    /// <summary>Queues in-app notifications (saved with the caller's unit of work).</summary>
    void Notify(IEnumerable<int> userIds, string title, string message, NotificationType type = NotificationType.Info, string? link = null);

    /// <summary>Notifies every active user holding the given permission.</summary>
    Task NotifyPermissionHoldersAsync(string permission, string title, string message,
        NotificationType type = NotificationType.Info, string? link = null, CancellationToken ct = default);

    Task<(int Unread, IReadOnlyList<NotificationDto> Latest)> GetSummaryAsync(int userId, int take = 6, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> ListAsync(int userId, PageQuery page, bool unreadOnly, CancellationToken ct = default);
    Task<string?> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(int userId, CancellationToken ct = default);
}

/// <summary>Resolves users holding a permission (implemented over Identity in Infrastructure).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<int>> GetUserIdsWithPermissionAsync(string permission, CancellationToken ct = default);
    Task<int?> GetUserIdForEmployeeAsync(int employeeId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IEnumerable<int> userIds, CancellationToken ct = default);

    /// <summary>Blocks sign-in and ends all sessions (used when an employee is terminated).</summary>
    Task DisableLoginAsync(int userId, CancellationToken ct = default);
}

public sealed class NotificationService(IApplicationDbContext db, IUserDirectory users, TimeProvider clock) : INotificationService
{
    public void Notify(IEnumerable<int> userIds, string title, string message,
        NotificationType type = NotificationType.Info, string? link = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var userId in userIds.Distinct())
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, Title = title, Message = message, Type = type, Link = link, CreatedAt = now
            });
        }
    }

    public async Task NotifyPermissionHoldersAsync(string permission, string title, string message,
        NotificationType type = NotificationType.Info, string? link = null, CancellationToken ct = default)
    {
        var recipients = await users.GetUserIdsWithPermissionAsync(permission, ct);
        Notify(recipients, title, message, type, link);
    }

    public async Task<(int Unread, IReadOnlyList<NotificationDto> Latest)> GetSummaryAsync(int userId, int take = 6,
        CancellationToken ct = default)
    {
        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        var latest = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        return (unread, latest);
    }

    public Task<PagedResult<NotificationDto>> ListAsync(int userId, PageQuery page, bool unreadOnly, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.Link, n.IsRead, n.CreatedAt))
            .ToPagedResultAsync(page, ct);

    public async Task<string?> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null)
            return null;
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return notification.Link;
    }

    public Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
    }
}
