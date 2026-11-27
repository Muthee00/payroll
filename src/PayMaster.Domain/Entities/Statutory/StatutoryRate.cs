using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Statutory;

/// <summary>One configurable statutory parameter within a configuration version.</summary>
public class StatutoryRate : BaseEntity
{
    public int StatutoryConfigurationId { get; set; }
    public StatutoryConfiguration StatutoryConfiguration { get; set; } = null!;

    public StatutoryRateKey Key { get; set; }
    public decimal Value { get; set; }
    public string? Notes { get; set; }
}
