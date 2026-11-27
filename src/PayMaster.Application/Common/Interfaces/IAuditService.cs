namespace PayMaster.Application.Common.Interfaces;

/// <summary>
/// Records business actions (approvals, finalizations, exports) in the audit log.
/// Field-level changes to <c>IAuditTracked</c> entities are captured automatically.
/// </summary>
public interface IAuditService
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the next SaveChanges).</summary>
    void Record(string action, string entityName, object? entityId, string? details = null,
        object? oldValues = null, object? newValues = null);
}
