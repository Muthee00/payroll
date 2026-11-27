using PayMaster.Domain.Enums;

namespace PayMaster.Application.Statutory;

public enum StatutoryValueUnit
{
    Percent,
    Amount,
    Flag
}

public sealed record StatutoryKeyInfo(StatutoryRateKey Key, string Group, string Label, StatutoryValueUnit Unit, string Help, bool Required);

/// <summary>Display metadata for statutory parameters (labels, units, grouping). Contains no rate values.</summary>
public static class StatutoryKeyMetadata
{
    public static readonly IReadOnlyList<StatutoryKeyInfo> All =
    [
        new(StatutoryRateKey.PersonalRelief, "PAYE", "Personal relief", StatutoryValueUnit.Amount, "Monthly relief deducted from tax for resident individuals.", true),
        new(StatutoryRateKey.InsuranceReliefRate, "PAYE", "Insurance relief rate", StatutoryValueUnit.Percent, "Share of qualifying premiums allowed as relief.", true),
        new(StatutoryRateKey.InsuranceReliefMonthlyCap, "PAYE", "Insurance relief cap", StatutoryValueUnit.Amount, "Maximum insurance relief per month.", true),
        new(StatutoryRateKey.PensionContributionMonthlyCap, "PAYE", "Pension deduction cap", StatutoryValueUnit.Amount, "Maximum monthly deduction for registered pension + NSSF.", true),
        new(StatutoryRateKey.DisabilityExemptionMonthly, "PAYE", "Disability exemption", StatutoryValueUnit.Amount, "Monthly income exempt for exemption-certificate holders.", false),

        new(StatutoryRateKey.ShaEmployeeRate, "SHIF", "Contribution rate", StatutoryValueUnit.Percent, "Share of gross salary.", true),
        new(StatutoryRateKey.ShaMinimumContribution, "SHIF", "Minimum contribution", StatutoryValueUnit.Amount, "Minimum monthly contribution when there is pay.", true),
        new(StatutoryRateKey.ShaMaximumContribution, "SHIF", "Maximum contribution", StatutoryValueUnit.Amount, "0 means no maximum.", false),
        new(StatutoryRateKey.ShaDeductibleForPaye, "SHIF", "Deductible before PAYE", StatutoryValueUnit.Flag, "Contribution reduces taxable income.", true),
        new(StatutoryRateKey.ShaQualifiesForInsuranceRelief, "SHIF", "Qualifies for insurance relief", StatutoryValueUnit.Flag, "Legacy treatment (pre-Dec 2024).", false),

        new(StatutoryRateKey.HousingLevyEmployeeRate, "Housing Levy", "Employee rate", StatutoryValueUnit.Percent, "Share of gross salary deducted from the employee.", true),
        new(StatutoryRateKey.HousingLevyEmployerRate, "Housing Levy", "Employer rate", StatutoryValueUnit.Percent, "Employer's matching contribution (not deducted).", true),
        new(StatutoryRateKey.HousingLevyDeductibleForPaye, "Housing Levy", "Deductible before PAYE", StatutoryValueUnit.Flag, "Levy reduces taxable income.", true),
        new(StatutoryRateKey.HousingLevyReliefRate, "Housing Levy", "Housing relief rate", StatutoryValueUnit.Percent, "Legacy affordable housing relief (0 if repealed).", false),
        new(StatutoryRateKey.HousingLevyReliefMonthlyCap, "Housing Levy", "Housing relief cap", StatutoryValueUnit.Amount, "Legacy relief cap per month.", false),

        new(StatutoryRateKey.NssfEmployeeRate, "NSSF", "Employee rate", StatutoryValueUnit.Percent, "Applied to Tier I and Tier II earnings.", true),
        new(StatutoryRateKey.NssfEmployerRate, "NSSF", "Employer rate", StatutoryValueUnit.Percent, "Employer's matching contribution.", true),
        new(StatutoryRateKey.NssfLowerEarningsLimit, "NSSF", "Lower earnings limit (Tier I)", StatutoryValueUnit.Amount, "Tier I covers pensionable pay up to this amount.", true),
        new(StatutoryRateKey.NssfUpperEarningsLimit, "NSSF", "Upper earnings limit (Tier II)", StatutoryValueUnit.Amount, "Tier II covers pay from the LEL up to this amount.", true),
        new(StatutoryRateKey.NssfDeductibleForPaye, "NSSF", "Deductible before PAYE", StatutoryValueUnit.Flag, "Counts toward the pension deduction cap.", true)
    ];

    public static StatutoryKeyInfo For(StatutoryRateKey key) => All.Single(k => k.Key == key);

    /// <summary>Stored value (fraction / amount / 0-1) → editor value (percent / amount / 0-1).</summary>
    public static decimal ToDisplay(StatutoryRateKey key, decimal stored) =>
        For(key).Unit == StatutoryValueUnit.Percent ? stored * 100m : stored;

    public static decimal FromDisplay(StatutoryRateKey key, decimal display) =>
        For(key).Unit == StatutoryValueUnit.Percent ? display / 100m : display;
}
