namespace PayMaster.Domain.Interfaces;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    int? CreatedById { get; set; }
    DateTime? UpdatedAt { get; set; }
    int? UpdatedById { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    int? DeletedById { get; set; }
}

/// <summary>
/// Marker for entities whose every change is written to the audit log with
/// previous and new values (salary, statutory configuration, payroll runs, etc.).
/// </summary>
public interface IAuditTracked;
