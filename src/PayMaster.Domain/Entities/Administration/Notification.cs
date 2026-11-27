using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Administration;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;

    /// <summary>Relative URL to the subject (e.g. /Payroll/Details/12).</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
