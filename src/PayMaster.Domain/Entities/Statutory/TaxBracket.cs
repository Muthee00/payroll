using PayMaster.Domain.Common;

namespace PayMaster.Domain.Entities.Statutory;

/// <summary>Monthly PAYE band. <see cref="UpperBound"/> null = no upper limit.</summary>
public class TaxBracket : BaseEntity
{
    public int StatutoryConfigurationId { get; set; }
    public StatutoryConfiguration StatutoryConfiguration { get; set; } = null!;

    public int Order { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }

    /// <summary>Fractional rate, e.g. 0.10 for 10%.</summary>
    public decimal Rate { get; set; }
}
