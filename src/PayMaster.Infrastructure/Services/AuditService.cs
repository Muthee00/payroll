using System.Text.Json;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Domain.Entities.Administration;

namespace PayMaster.Infrastructure.Services;

public sealed class AuditService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditService
{
    public void Record(string action, string entityName, object? entityId, string? details = null,
        object? oldValues = null, object? newValues = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            IpAddress = currentUser.IpAddress,
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString(),
            Details = details,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Timestamp = clock.GetUtcNow().UtcDateTime
        });
    }
}
