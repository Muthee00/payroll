using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Statutory;

public sealed record TaxBand(decimal LowerBound, decimal? UpperBound, decimal Rate);

/// <summary>
/// Immutable, typed snapshot of one statutory configuration version. The calculation engine
/// reads only this object; every rate comes from the database. It is serialised onto each
/// payroll run so the exact rules used are preserved.
/// </summary>
public sealed record StatutoryRules
{
    public int ConfigurationId { get; init; }
    public int Version { get; init; }
    public string Name { get; init; } = "";
    public DateOnly EffectiveFrom { get; init; }
    public IReadOnlyList<TaxBand> TaxBands { get; init; } = [];

    public decimal PersonalRelief { get; init; }
    public decimal InsuranceReliefRate { get; init; }
    public decimal InsuranceReliefMonthlyCap { get; init; }
    public decimal PensionContributionMonthlyCap { get; init; }
    public decimal DisabilityExemptionMonthly { get; init; }

    public decimal ShaEmployeeRate { get; init; }
    public decimal ShaMinimumContribution { get; init; }
    /// <summary>Null when there is no maximum.</summary>
    public decimal? ShaMaximumContribution { get; init; }
    public bool ShaDeductibleForPaye { get; init; }
    public bool ShaQualifiesForInsuranceRelief { get; init; }

    public decimal HousingLevyEmployeeRate { get; init; }
    public decimal HousingLevyEmployerRate { get; init; }
    public bool HousingLevyDeductibleForPaye { get; init; }
    public decimal HousingLevyReliefRate { get; init; }
    public decimal HousingLevyReliefMonthlyCap { get; init; }

    public decimal NssfEmployeeRate { get; init; }
    public decimal NssfEmployerRate { get; init; }
    public decimal NssfLowerEarningsLimit { get; init; }
    public decimal NssfUpperEarningsLimit { get; init; }
    public bool NssfDeductibleForPaye { get; init; }

    public static readonly IReadOnlyList<StatutoryRateKey> RequiredKeys =
    [
        StatutoryRateKey.PersonalRelief, StatutoryRateKey.InsuranceReliefRate, StatutoryRateKey.InsuranceReliefMonthlyCap,
        StatutoryRateKey.PensionContributionMonthlyCap, StatutoryRateKey.ShaEmployeeRate, StatutoryRateKey.ShaMinimumContribution,
        StatutoryRateKey.ShaDeductibleForPaye, StatutoryRateKey.HousingLevyEmployeeRate, StatutoryRateKey.HousingLevyEmployerRate,
        StatutoryRateKey.HousingLevyDeductibleForPaye, StatutoryRateKey.NssfEmployeeRate, StatutoryRateKey.NssfEmployerRate,
        StatutoryRateKey.NssfLowerEarningsLimit, StatutoryRateKey.NssfUpperEarningsLimit, StatutoryRateKey.NssfDeductibleForPaye
    ];

    /// <summary>Builds and validates the rules. Throws <see cref="DomainException"/> listing every problem.</summary>
    public static StatutoryRules From(StatutoryConfiguration configuration)
    {
        var problems = Validate(configuration.TaxBrackets, configuration.Rates);
        if (problems.Count > 0)
            throw new DomainException($"Statutory configuration '{configuration.Name}' is incomplete: {string.Join(" ", problems)}");

        var rates = configuration.Rates.ToDictionary(r => r.Key, r => r.Value);
        decimal Get(StatutoryRateKey key) => rates.GetValueOrDefault(key);
        bool Flag(StatutoryRateKey key) => rates.GetValueOrDefault(key) == 1m;

        var shaMax = Get(StatutoryRateKey.ShaMaximumContribution);
        return new StatutoryRules
        {
            ConfigurationId = configuration.Id,
            Version = configuration.Version,
            Name = configuration.Name,
            EffectiveFrom = configuration.EffectiveFrom,
            TaxBands = configuration.TaxBrackets.OrderBy(b => b.Order)
                .Select(b => new TaxBand(b.LowerBound, b.UpperBound, b.Rate)).ToList(),
            PersonalRelief = Get(StatutoryRateKey.PersonalRelief),
            InsuranceReliefRate = Get(StatutoryRateKey.InsuranceReliefRate),
            InsuranceReliefMonthlyCap = Get(StatutoryRateKey.InsuranceReliefMonthlyCap),
            PensionContributionMonthlyCap = Get(StatutoryRateKey.PensionContributionMonthlyCap),
            DisabilityExemptionMonthly = Get(StatutoryRateKey.DisabilityExemptionMonthly),
            ShaEmployeeRate = Get(StatutoryRateKey.ShaEmployeeRate),
            ShaMinimumContribution = Get(StatutoryRateKey.ShaMinimumContribution),
            ShaMaximumContribution = shaMax > 0 ? shaMax : null,
            ShaDeductibleForPaye = Flag(StatutoryRateKey.ShaDeductibleForPaye),
            ShaQualifiesForInsuranceRelief = Flag(StatutoryRateKey.ShaQualifiesForInsuranceRelief),
            HousingLevyEmployeeRate = Get(StatutoryRateKey.HousingLevyEmployeeRate),
            HousingLevyEmployerRate = Get(StatutoryRateKey.HousingLevyEmployerRate),
            HousingLevyDeductibleForPaye = Flag(StatutoryRateKey.HousingLevyDeductibleForPaye),
            HousingLevyReliefRate = Get(StatutoryRateKey.HousingLevyReliefRate),
            HousingLevyReliefMonthlyCap = Get(StatutoryRateKey.HousingLevyReliefMonthlyCap),
            NssfEmployeeRate = Get(StatutoryRateKey.NssfEmployeeRate),
            NssfEmployerRate = Get(StatutoryRateKey.NssfEmployerRate),
            NssfLowerEarningsLimit = Get(StatutoryRateKey.NssfLowerEarningsLimit),
            NssfUpperEarningsLimit = Get(StatutoryRateKey.NssfUpperEarningsLimit),
            NssfDeductibleForPaye = Flag(StatutoryRateKey.NssfDeductibleForPaye)
        };
    }

    /// <summary>Structural checks shared by activation and the editor.</summary>
    public static List<string> Validate(IEnumerable<TaxBracket> brackets, IEnumerable<StatutoryRate> rates)
    {
        var problems = new List<string>();
        var bands = brackets.OrderBy(b => b.Order).ToList();
        var rateMap = rates.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.First().Value);

        if (bands.Count == 0)
            problems.Add("At least one PAYE band is required.");
        else
        {
            if (bands[0].LowerBound != 0)
                problems.Add("The first PAYE band must start at 0.");
            for (var i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                if (band.Rate is < 0 or > 1)
                    problems.Add($"Band {i + 1} rate must be between 0 and 1 (e.g. 0.30 for 30%).");
                if (band.UpperBound is decimal upper && upper <= band.LowerBound)
                    problems.Add($"Band {i + 1} upper limit must be above its lower limit.");
                if (i < bands.Count - 1)
                {
                    if (band.UpperBound is null)
                        problems.Add($"Only the last PAYE band can be open-ended (band {i + 1} is not last).");
                    else if (bands[i + 1].LowerBound != band.UpperBound)
                        problems.Add($"Band {i + 2} must start where band {i + 1} ends ({band.UpperBound:N2}).");
                }
            }
            if (bands[^1].UpperBound is not null)
                problems.Add("The last PAYE band must be open-ended (no upper limit).");
        }

        foreach (var key in RequiredKeys.Where(k => !rateMap.ContainsKey(k)))
            problems.Add($"Missing value for {key}.");

        foreach (var key in new[]
                 {
                     StatutoryRateKey.InsuranceReliefRate, StatutoryRateKey.ShaEmployeeRate, StatutoryRateKey.HousingLevyEmployeeRate,
                     StatutoryRateKey.HousingLevyEmployerRate, StatutoryRateKey.HousingLevyReliefRate,
                     StatutoryRateKey.NssfEmployeeRate, StatutoryRateKey.NssfEmployerRate
                 })
        {
            if (rateMap.TryGetValue(key, out var v) && v is < 0 or > 1)
                problems.Add($"{key} must be a fraction between 0 and 1.");
        }

        foreach (var key in new[]
                 {
                     StatutoryRateKey.ShaDeductibleForPaye, StatutoryRateKey.ShaQualifiesForInsuranceRelief,
                     StatutoryRateKey.HousingLevyDeductibleForPaye, StatutoryRateKey.NssfDeductibleForPaye
                 })
        {
            if (rateMap.TryGetValue(key, out var v) && v is not (0m or 1m))
                problems.Add($"{key} must be 1 (yes) or 0 (no).");
        }

        if (rateMap.TryGetValue(StatutoryRateKey.NssfLowerEarningsLimit, out var lel)
            && rateMap.TryGetValue(StatutoryRateKey.NssfUpperEarningsLimit, out var uel) && uel < lel)
            problems.Add("NSSF upper earnings limit must not be below the lower earnings limit.");

        if (rateMap.Any(r => r.Value < 0))
            problems.Add("Statutory values cannot be negative.");

        return problems;
    }
}
