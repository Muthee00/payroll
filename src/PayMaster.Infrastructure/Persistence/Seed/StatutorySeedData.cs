using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Enums;

namespace PayMaster.Infrastructure.Persistence.Seed;

/// <summary>
/// Initial statutory configuration versions. This is seed data only: the calculation
/// engine reads whatever is in the database, and administrators maintain new versions
/// through Statutory Settings. Re-verify against KRA/SHA/NSSF/Housing Fund notices
/// before production use.
/// </summary>
internal static class StatutorySeedData
{
    private const string Sources =
        "Income Tax Act (Cap 470) Third Schedule as amended by Finance Act 2023; " +
        "Tax Laws (Amendment) Act 2024 (SHIF, AHL deductible; pension cap KES 30,000; effective 27 Dec 2024); " +
        "Finance Act 2026 (assented 26 Jun 2026) left PAYE bands and reliefs unchanged; " +
        "Social Health Insurance (General) Regulations 2024 (2.75% of gross, min KES 300); " +
        "Affordable Housing Act 2024 (1.5% employee + 1.5% employer); " +
        "NSSF Act 2013 phased rates ({0}).";

    public static IEnumerable<StatutoryConfiguration> Build()
    {
        yield return Create(
            version: 1,
            name: "Kenya statutory rules - NSSF Year 3",
            effectiveFrom: new DateOnly(2025, 2, 1),
            effectiveTo: new DateOnly(2026, 1, 31),
            status: StatutoryConfigurationStatus.Archived,
            nssfLel: 8_000m,
            nssfUel: 72_000m,
            nssfNote: "Year 3 from Feb 2025: LEL 8,000, UEL 72,000");

        yield return Create(
            version: 2,
            name: "Kenya statutory rules - NSSF Year 4",
            effectiveFrom: new DateOnly(2026, 2, 1),
            effectiveTo: null,
            status: StatutoryConfigurationStatus.Active,
            nssfLel: 9_000m,
            nssfUel: 108_000m,
            nssfNote: "Year 4 from Feb 2026: LEL 9,000, UEL 108,000");
    }

    private static StatutoryConfiguration Create(int version, string name, DateOnly effectiveFrom, DateOnly? effectiveTo,
        StatutoryConfigurationStatus status, decimal nssfLel, decimal nssfUel, string nssfNote)
    {
        var config = new StatutoryConfiguration
        {
            Version = version,
            Name = name,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Status = status,
            ActivatedAt = DateTime.UtcNow,
            Description = "Monthly PAYE bands, reliefs, SHIF, Affordable Housing Levy and NSSF tiers.",
            LegalReference = string.Format(Sources, nssfNote),
            TaxBrackets =
            [
                new TaxBracket { Order = 1, LowerBound = 0m, UpperBound = 24_000m, Rate = 0.10m },
                new TaxBracket { Order = 2, LowerBound = 24_000m, UpperBound = 32_333m, Rate = 0.25m },
                new TaxBracket { Order = 3, LowerBound = 32_333m, UpperBound = 500_000m, Rate = 0.30m },
                new TaxBracket { Order = 4, LowerBound = 500_000m, UpperBound = 800_000m, Rate = 0.325m },
                new TaxBracket { Order = 5, LowerBound = 800_000m, UpperBound = null, Rate = 0.35m }
            ]
        };

        void Rate(StatutoryRateKey key, decimal value, string notes) =>
            config.Rates.Add(new StatutoryRate { Key = key, Value = value, Notes = notes });

        Rate(StatutoryRateKey.PersonalRelief, 2_400m, "Monthly personal relief (resident individuals)");
        Rate(StatutoryRateKey.InsuranceReliefRate, 0.15m, "15% of qualifying life/health/education insurance premiums");
        Rate(StatutoryRateKey.InsuranceReliefMonthlyCap, 5_000m, "Insurance relief capped at KES 60,000 p.a.");
        Rate(StatutoryRateKey.PensionContributionMonthlyCap, 30_000m, "Registered pension + NSSF deduction limit");
        Rate(StatutoryRateKey.DisabilityExemptionMonthly, 150_000m, "Exempt income for holders of a disability exemption certificate");

        Rate(StatutoryRateKey.ShaEmployeeRate, 0.0275m, "SHIF: 2.75% of gross salary");
        Rate(StatutoryRateKey.ShaMinimumContribution, 300m, "SHIF minimum monthly contribution");
        Rate(StatutoryRateKey.ShaMaximumContribution, 0m, "0 = no maximum");
        Rate(StatutoryRateKey.ShaDeductibleForPaye, 1m, "Allowable deduction from 27 Dec 2024");
        Rate(StatutoryRateKey.ShaQualifiesForInsuranceRelief, 0m, "Replaced by allowable deduction from 27 Dec 2024");

        Rate(StatutoryRateKey.HousingLevyEmployeeRate, 0.015m, "AHL employee: 1.5% of gross salary");
        Rate(StatutoryRateKey.HousingLevyEmployerRate, 0.015m, "AHL employer: 1.5% of gross salary");
        Rate(StatutoryRateKey.HousingLevyDeductibleForPaye, 1m, "Allowable deduction from 27 Dec 2024");
        Rate(StatutoryRateKey.HousingLevyReliefRate, 0m, "Affordable housing relief repealed 27 Dec 2024");
        Rate(StatutoryRateKey.HousingLevyReliefMonthlyCap, 0m, "Not applicable");

        Rate(StatutoryRateKey.NssfEmployeeRate, 0.06m, "6% of pensionable pay up to the UEL");
        Rate(StatutoryRateKey.NssfEmployerRate, 0.06m, "Employer matches employee contribution");
        Rate(StatutoryRateKey.NssfLowerEarningsLimit, nssfLel, $"Tier I ceiling ({nssfNote})");
        Rate(StatutoryRateKey.NssfUpperEarningsLimit, nssfUel, $"Tier II ceiling ({nssfNote})");
        Rate(StatutoryRateKey.NssfDeductibleForPaye, 1m, "Registered pension scheme contribution");

        return config;
    }
}
