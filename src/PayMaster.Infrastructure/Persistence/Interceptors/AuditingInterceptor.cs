using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Applies cross-cutting persistence rules on every save:
/// creation/modification stamps, soft delete, field-level audit trail for
/// <see cref="IAuditTracked"/> entities, and immutability of <see cref="AuditLog"/>.
/// </summary>
public sealed class AuditingInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    private static readonly HashSet<string> MaskedProperties =
        new(StringComparer.OrdinalIgnoreCase) { "BankAccountNumber", "MobileMoneyNumber", "PasswordHash", "SecurityStamp" };

    private static readonly HashSet<string> IgnoredProperties =
        new(StringComparer.OrdinalIgnoreCase) { "CreatedAt", "CreatedById", "UpdatedAt", "UpdatedById", "ConcurrencyStamp" };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly List<(EntityEntry Entry, AuditLog Log)> _pendingInserts = [];
    private bool _savingPendingInserts;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (FlushPendingInserts(eventData.Context))
        {
            try { eventData.Context!.SaveChanges(); }
            finally { _savingPendingInserts = false; }
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (FlushPendingInserts(eventData.Context))
        {
            try { await eventData.Context!.SaveChangesAsync(cancellationToken); }
            finally { _savingPendingInserts = false; }
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingInserts.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingInserts.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null || _savingPendingInserts)
            return;

        var now = clock.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit log entries are immutable.");
        }

        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is ISoftDelete softDelete && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = now;
                softDelete.DeletedById = userId;
            }

            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedById ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedById = userId;
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedById)).IsModified = false;
                }
            }

            if (entry.Entity is IAuditTracked && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var log = BuildAuditLog(entry, now);
                if (log is null)
                    continue;

                if (entry.State == EntityState.Added && entry.Properties.Any(p => p.Metadata.IsPrimaryKey() && p.IsTemporary))
                    _pendingInserts.Add((entry, log));
                else
                    auditLogs.Add(log);
            }
        }

        if (auditLogs.Count > 0)
            context.Set<AuditLog>().AddRange(auditLogs);
    }

    private AuditLog? BuildAuditLog(EntityEntry entry, DateTime now)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var action = entry.State switch
        {
            EntityState.Added => "Created",
            EntityState.Deleted => "Deleted",
            _ => entry.Entity is ISoftDelete { IsDeleted: true } && !(bool)(entry.Property(nameof(ISoftDelete.IsDeleted)).OriginalValue ?? false)
                ? "Deleted"
                : "Updated"
        };

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name) || property.Metadata.IsPrimaryKey())
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = Mask(name, property.CurrentValue);
                    break;
                case EntityState.Deleted:
                    oldValues[name] = Mask(name, property.OriginalValue);
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    oldValues[name] = Mask(name, property.OriginalValue);
                    newValues[name] = Mask(name, property.CurrentValue);
                    break;
            }
        }

        if (entry.State == EntityState.Modified && newValues.Count == 0)
            return null;

        return new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            IpAddress = currentUser.IpAddress,
            Action = action,
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = PrimaryKey(entry),
            OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            Timestamp = now
        };
    }

    private bool FlushPendingInserts(DbContext? context)
    {
        if (context is null || _pendingInserts.Count == 0 || _savingPendingInserts)
            return false;

        foreach (var (entry, log) in _pendingInserts)
            log.EntityId = PrimaryKey(entry);

        context.Set<AuditLog>().AddRange(_pendingInserts.Select(p => p.Log));
        _pendingInserts.Clear();
        _savingPendingInserts = true;
        return true;
    }

    private static string? PrimaryKey(EntityEntry entry) =>
        string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));

    private static object? Mask(string propertyName, object? value)
    {
        if (value is null || !MaskedProperties.Contains(propertyName))
            return value;
        var text = value.ToString() ?? string.Empty;
        return text.Length <= 4 ? "****" : $"****{text[^4..]}";
    }
}
