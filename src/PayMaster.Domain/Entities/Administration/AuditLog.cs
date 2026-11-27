using PayMaster.Domain.Common;

namespace PayMaster.Domain.Entities.Administration;

/// <summary>
/// Append-only audit record. The application exposes no update or delete path for
/// these rows; the persistence layer rejects modifications.
/// </summary>
public class AuditLog : BaseEntity
{
    public int? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>e.g. "Created", "Updated", "Payroll.Approved", "Statutory.Activated".</summary>
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
}
