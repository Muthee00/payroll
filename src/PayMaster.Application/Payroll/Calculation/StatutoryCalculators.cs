using PayMaster.Application.Statutory;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Payroll.Calculation;

/// <summary>NSSF Act 2013 tiered contributions: Tier I up to the LEL, Tier II from LEL to UEL.</summary>
public static class NssfCalculator
{
    public static NssfResult Calculate(decimal pensionablePay, StatutoryRules rules)
    {
        var pay = Money.NonNegative(pensionablePay);
        var tier1Base = Math.Min(pay, rules.NssfLowerEarningsLimit);
        var tier2Base = Money.NonNegative(Math.Min(pay, rules.NssfUpperEarningsLimit) - rules.NssfLowerEarningsLimit);

        return new NssfResult(
            Tier1: Money.Percent(tier1Base, rules.NssfEmployeeRate),
            Tier2: Money.Percent(tier2Base, rules.NssfEmployeeRate),
            EmployerTier1: Money.Percent(tier1Base, rules.NssfEmployerRate),
            EmployerTier2: Money.Percent(tier2Base, rules.NssfEmployerRate));
    }
}

/// <summary>Social Health Insurance Fund: rate × gross salary, subject to a minimum (and optional maximum).</summary>
public static class ShaCalculator
{
    public static decimal Calculate(decimal grossSalary, StatutoryRules rules)
    {
        if (grossSalary <= 0)
            return 0m;
        var amount = Math.Max(Money.Percent(grossSalary, rules.ShaEmployeeRate), rules.ShaMinimumContribution);
        if (rules.ShaMaximumContribution is decimal max)
            amount = Math.Min(amount, max);
        return Money.Round(amount);
    }
}

/// <summary>Affordable Housing Levy: employee and employer each pay a rate of gross salary.</summary>
public static class HousingLevyCalculator
{
    public static (decimal Employee, decimal Employer) Calculate(decimal grossSalary, StatutoryRules rules)
    {
        var basis = Money.NonNegative(grossSalary);
        return (Money.Percent(basis, rules.HousingLevyEmployeeRate), Money.Percent(basis, rules.HousingLevyEmployerRate));
    }
}

/// <summary>Progressive PAYE with personal, insurance and (legacy) housing reliefs. Tax never goes below zero.</summary>
public static class PayeCalculator
{
    public static PayeResult Calculate(decimal taxableIncome, decimal insurancePremiums, decimal sha, decimal housingLevy, StatutoryRules rules)
    {
        var income = Money.NonNegative(taxableIncome);
        var bands = new List<PayeBandResult>();
        var tax = 0m;

        foreach (var band in rules.TaxBands)
        {
            if (income <= band.LowerBound)
                break;
            var top = band.UpperBound is decimal upper ? Math.Min(income, upper) : income;
            var inBand = top - band.LowerBound;
            var bandTax = Money.Percent(inBand, band.Rate);
            bands.Add(new PayeBandResult(band.LowerBound, band.UpperBound, band.Rate, inBand, bandTax));
            tax += bandTax;
        }

        tax = Money.Round(tax);

        var insuranceBase = insurancePremiums + (rules.ShaQualifiesForInsuranceRelief ? sha : 0m);
        var insuranceRelief = Math.Min(Money.Percent(insuranceBase, rules.InsuranceReliefRate), rules.InsuranceReliefMonthlyCap);
        var housingRelief = rules.HousingLevyReliefRate > 0
            ? Math.Min(Money.Percent(housingLevy, rules.HousingLevyReliefRate), rules.HousingLevyReliefMonthlyCap)
            : 0m;

        // Reliefs reduce tax only as far as zero; unused relief is not paid out.
        var personal = Math.Min(rules.PersonalRelief, tax);
        var remaining = tax - personal;
        var insurance = Math.Min(insuranceRelief, remaining);
        remaining -= insurance;
        var housing = Math.Min(housingRelief, remaining);
        remaining -= housing;

        return new PayeResult(bands, tax, personal, insurance, housing, Money.Round(remaining));
    }
}
