using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }
}

/// <summary>
/// Entity with creation/modification stamps. Stamps are set by the persistence
/// interceptor, never by application code.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public int? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>Auditable entity that is soft-deleted instead of removed.</summary>
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
}
