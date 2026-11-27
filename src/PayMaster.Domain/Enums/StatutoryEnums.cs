namespace PayMaster.Domain.Enums;

public enum StatutoryConfigurationStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

/// <summary>
/// Keys of configurable statutory parameters. Values live in the database
/// (<c>StatutoryRate</c>) - never in code. Rates are fractions (0.015 = 1.5%);
/// amounts are monthly KES; flags are 1 (true) or 0 (false).
/// </summary>
public enum StatutoryRateKey
{
    // PAYE
    PersonalRelief = 1,
    InsuranceReliefRate = 2,
    InsuranceReliefMonthlyCap = 3,
    PensionContributionMonthlyCap = 4,
    DisabilityExemptionMonthly = 5,

    // SHA (Social Health Authority / SHIF)
    ShaEmployeeRate = 20,
    ShaMinimumContribution = 21,
    ShaMaximumContribution = 22,
    ShaDeductibleForPaye = 23,
    ShaQualifiesForInsuranceRelief = 24,

    // Affordable Housing Levy
    HousingLevyEmployeeRate = 40,
    HousingLevyEmployerRate = 41,
    HousingLevyDeductibleForPaye = 42,
    HousingLevyReliefRate = 43,
    HousingLevyReliefMonthlyCap = 44,

    // NSSF (NSSF Act 2013 tiers)
    NssfEmployeeRate = 60,
    NssfEmployerRate = 61,
    NssfLowerEarningsLimit = 62,
    NssfUpperEarningsLimit = 63,
    NssfDeductibleForPaye = 64
}
