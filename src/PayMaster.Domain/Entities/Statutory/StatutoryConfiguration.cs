using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Statutory;

/// <summary>
/// A versioned set of Kenyan statutory rules (PAYE bands, reliefs, SHA, Housing Levy,
/// NSSF) valid from <see cref="EffectiveFrom"/>. Only Draft versions are editable;
/// a new rule change is a new version. Once a finalized payroll references a version
/// it is locked permanently.
/// </summary>
public class StatutoryConfiguration : AuditableEntity, IAuditTracked
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public StatutoryConfigurationStatus Status { get; set; } = StatutoryConfigurationStatus.Draft;
    public string? Description { get; set; }

    /// <summary>Legal sources the values were verified against (Acts, KRA/SHA/NSSF notices).</summary>
    public string? LegalReference { get; set; }

    public bool IsLocked { get; set; }
    public int? ActivatedById { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public ICollection<TaxBracket> TaxBrackets { get; set; } = [];
    public ICollection<StatutoryRate> Rates { get; set; } = [];

    public bool IsEditable => Status == StatutoryConfigurationStatus.Draft && !IsLocked;

    public void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException(
                "Only draft statutory configurations can be edited. Create a new version to change active rules.");
    }

    public void Activate(int userId, DateTime nowUtc)
    {
        if (Status != StatutoryConfigurationStatus.Draft)
            throw new DomainException("Only a draft configuration can be activated.");
        if (TaxBrackets.Count == 0)
            throw new DomainException("A configuration needs at least one PAYE tax bracket.");

        Status = StatutoryConfigurationStatus.Active;
        ActivatedById = userId;
        ActivatedAt = nowUtc;
    }

    /// <summary>Ends this version the day before its successor takes effect.</summary>
    public void CloseBefore(DateOnly successorEffectiveFrom)
    {
        if (successorEffectiveFrom <= EffectiveFrom)
            throw new DomainException("The new configuration must take effect after the current one.");
        EffectiveTo = successorEffectiveFrom.AddDays(-1);
        Status = StatutoryConfigurationStatus.Archived;
    }

    public void Lock() => IsLocked = true;

    public bool IsEffectiveOn(DateOnly date) =>
        Status != StatutoryConfigurationStatus.Draft
        && EffectiveFrom <= date
        && (EffectiveTo is null || EffectiveTo >= date);
}
