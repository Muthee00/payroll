using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Employees;

public class EmployeeDocument : SoftDeletableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public EmployeeDocumentType DocumentType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Path relative to the configured private document store (never under wwwroot).</summary>
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
