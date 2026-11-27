using PayMaster.Application.Statutory;
using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Enums;

namespace PayMaster.Tests.TestSupport;

/// <summary>
/// Kenyan statutory parameters verified on 2 Oct 2026 against official/legal sources:
/// - PAYE monthly bands 10% / 25% / 30% / 32.5% / 35% at 24,000 / 32,333 / 500,000 / 800,000; personal relief 2,400
///   (Income Tax Act Third Schedule; unchanged by the Finance Act 2026, assented 26 June 2026).
/// - SHIF 2.75% of gross, minimum 300, no maximum (Social Health Insurance (General) Regulations 2024).
/// - Affordable Housing Levy 1.5% employee + 1.5% employer (Affordable Housing Act 2024).
/// - SHIF and AHL are allowable deductions from 27 Dec 2024 (Tax Laws (Amendment) Act 2024); pension + NSSF cap 30,000.
/// - NSSF 6% + 6%; Year 4 (from Feb 2026) LEL 9,000, UEL 108,000; Year 3 (Feb 2025) LEL 8,000, UEL 72,000.
/// Expected results in tests are cross-checked against published 2026 worked examples.
/// </summary>
public static class VerifiedKenyaRules
{
    public static StatutoryRules Year4() => StatutoryRules.From(Configuration(9_000m, 108_000m));
    public static StatutoryRules Year3() => StatutoryRules.From(Configuration(8_000m, 72_000m));

    public static StatutoryConfiguration Configuration(decimal lel, decimal uel, int version = 1)
    {
        var config = new StatutoryConfiguration
        {
            Id = version, Version = version, Name = $"Verified Kenya rules (LEL {lel}, UEL {uel})",
            EffectiveFrom = new DateOnly(2026, 2, 1), Status = StatutoryConfigurationStatus.Active,
            TaxBrackets =
            [
                new TaxBracket { Order = 1, LowerBound = 0m, UpperBound = 24_000m, Rate = 0.10m },
                new TaxBracket { Order = 2, LowerBound = 24_000m, UpperBound = 32_333m, Rate = 0.25m },
                new TaxBracket { Order = 3, LowerBound = 32_333m, UpperBound = 500_000m, Rate = 0.30m },
                new TaxBracket { Order = 4, LowerBound = 500_000m, UpperBound = 800_000m, Rate = 0.325m },
                new TaxBracket { Order = 5, LowerBound = 800_000m, UpperBound = null, Rate = 0.35m }
            ]
        };

        void Rate(StatutoryRateKey key, decimal value) => config.Rates.Add(new StatutoryRate { Key = key, Value = value });
        Rate(StatutoryRateKey.PersonalRelief, 2_400m);
        Rate(StatutoryRateKey.InsuranceReliefRate, 0.15m);
        Rate(StatutoryRateKey.InsuranceReliefMonthlyCap, 5_000m);
        Rate(StatutoryRateKey.PensionContributionMonthlyCap, 30_000m);
        Rate(StatutoryRateKey.DisabilityExemptionMonthly, 150_000m);
        Rate(StatutoryRateKey.ShaEmployeeRate, 0.0275m);
        Rate(StatutoryRateKey.ShaMinimumContribution, 300m);
        Rate(StatutoryRateKey.ShaMaximumContribution, 0m);
        Rate(StatutoryRateKey.ShaDeductibleForPaye, 1m);
        Rate(StatutoryRateKey.ShaQualifiesForInsuranceRelief, 0m);
        Rate(StatutoryRateKey.HousingLevyEmployeeRate, 0.015m);
        Rate(StatutoryRateKey.HousingLevyEmployerRate, 0.015m);
        Rate(StatutoryRateKey.HousingLevyDeductibleForPaye, 1m);
        Rate(StatutoryRateKey.HousingLevyReliefRate, 0m);
        Rate(StatutoryRateKey.HousingLevyReliefMonthlyCap, 0m);
        Rate(StatutoryRateKey.NssfEmployeeRate, 0.06m);
        Rate(StatutoryRateKey.NssfEmployerRate, 0.06m);
        Rate(StatutoryRateKey.NssfLowerEarningsLimit, lel);
        Rate(StatutoryRateKey.NssfUpperEarningsLimit, uel);
        Rate(StatutoryRateKey.NssfDeductibleForPaye, 1m);
        return config;
    }
}
