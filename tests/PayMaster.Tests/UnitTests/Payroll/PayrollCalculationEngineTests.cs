using PayMaster.Application.Payroll.Calculation;
using PayMaster.Application.Statutory;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Tests.TestSupport;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Payroll;

public class PayrollCalculationEngineTests
{
    private static readonly StatutoryRules Rules = VerifiedKenyaRules.Year4();

    private static EmployeePayResult Pay(decimal basic, params PayLine[] lines) =>
        PayrollCalculationEngine.Calculate(new EmployeePayInput
        {
            BasicSalary = basic,
            Earnings = lines.Where(l => (int)l.Category < 20).ToList(),
            Deductions = lines.Where(l => (int)l.Category >= 20).ToList()
        }, Rules);

    private static PayLine Allowance(string name, decimal amount) =>
        new(name.ToUpperInvariant(), name, amount, ComponentCategory.Allowance, TaxTreatment.Taxable, IsPensionable: true, Prorate: true);

    /// <summary>
    /// Published 2026 worked examples (statum.co.ke "How to calculate net salary in Kenya", consistent with
    /// nashthuo.com PAYE calculator): gross → NSSF, SHIF, AHL, PAYE, net (published to the nearest shilling).
    /// Exact cent values follow from the verified rules.
    /// </summary>
    [Theory]
    [InlineData(30_000, 1_800.00, 825.00, 450.00, 731.25, 26_193.75, 731, 26_194)]
    [InlineData(50_000, 3_000.00, 1_375.00, 750.00, 5_845.85, 39_029.15, 5_846, 39_029)]
    [InlineData(100_000, 6_000.00, 2_750.00, 1_500.00, 19_308.35, 70_441.65, 19_308, 70_442)]
    [InlineData(150_000, 6_480.00, 4_125.00, 2_250.00, 33_526.85, 103_618.15, 33_527, 103_618)]
    [InlineData(200_000, 6_480.00, 5_500.00, 3_000.00, 47_889.35, 137_130.65, 47_889, 137_131)]
    public void Matches_published_2026_examples(decimal gross, decimal nssf, decimal sha, decimal levy, decimal paye, decimal net,
        decimal publishedPaye, decimal publishedNet)
    {
        var result = Pay(gross);

        result.Nssf.Employee.ShouldBe(nssf);
        result.Sha.ShouldBe(sha);
        result.HousingLevy.ShouldBe(levy);
        result.Paye.Paye.ShouldBe(paye);
        result.NetPay.ShouldBe(net);
        Math.Round(result.Paye.Paye, MidpointRounding.AwayFromZero).ShouldBe(publishedPaye);
        Math.Round(result.NetPay, MidpointRounding.AwayFromZero).ShouldBe(publishedNet);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Kes_100000_breakdown_is_explained_step_by_step()
    {
        var r = Pay(100_000m);

        r.Nssf.Tier1.ShouldBe(540m);                 // 6% × 9,000
        r.Nssf.Tier2.ShouldBe(5_460m);               // 6% × (100,000 − 9,000)
        r.Nssf.Employer.ShouldBe(6_000m);
        r.EmployerHousingLevy.ShouldBe(1_500m);
        r.TotalEmployerContributions.ShouldBe(7_500m);
        r.AllowableDeductions.ShouldBe(10_250m);     // NSSF 6,000 + SHIF 2,750 + AHL 1,500
        r.TaxableIncome.ShouldBe(89_750m);
        r.Paye.Bands.Select(b => b.Tax).ShouldBe([2_400m, 2_083.25m, 17_225.10m]);
        r.Paye.TaxBeforeReliefs.ShouldBe(21_708.35m);
        r.Paye.PersonalRelief.ShouldBe(2_400m);
        r.TotalDeductions.ShouldBe(29_558.35m);
        r.Steps.ShouldContain(s => s.Section == "NSSF" && s.Label == "Tier II" && s.Amount == 5_460m);
        r.Steps.ShouldContain(s => s.Section == "PAYE" && s.Label == "Taxable income" && s.Amount == 89_750m);
    }

    [Fact]
    public void Employer_contributions_are_not_employee_deductions()
    {
        var r = Pay(100_000m);
        r.Deductions.Sum(d => d.Amount).ShouldBe(r.TotalDeductions);
        r.TotalDeductions.ShouldNotBe(r.TotalDeductions + r.TotalEmployerContributions);
        r.NetPay.ShouldBe(r.GrossEarnings - r.TotalDeductions);
        r.Deductions.ShouldNotContain(d => d.Name.Contains("Employer"));
    }

    [Fact]
    public void Low_earner_pays_the_shif_minimum_and_no_paye()
    {
        var r = Pay(10_000m);
        r.Sha.ShouldBe(300m);                        // 2.75% = 275, raised to the 300 minimum
        r.Nssf.Employee.ShouldBe(600m);              // 540 + 6% × 1,000
        r.HousingLevy.ShouldBe(150m);
        r.TaxableIncome.ShouldBe(8_950m);
        r.Paye.TaxBeforeReliefs.ShouldBe(895m);
        r.Paye.PersonalRelief.ShouldBe(895m);        // relief limited to the tax due
        r.Paye.Paye.ShouldBe(0m);
        r.NetPay.ShouldBe(8_950m);
    }

    [Fact]
    public void Software_engineer_structure_with_allowances()
    {
        // Spec example: basic 100,000 + housing 20,000 + transport 10,000 + medical 5,000.
        var r = Pay(100_000m, Allowance("Housing", 20_000m), Allowance("Transport", 10_000m), Allowance("Medical", 5_000m));

        r.GrossEarnings.ShouldBe(135_000m);
        r.Nssf.Employee.ShouldBe(6_480m);            // capped at UEL 108,000
        r.Sha.ShouldBe(3_712.50m);
        r.HousingLevy.ShouldBe(2_025m);
        r.TaxableIncome.ShouldBe(122_782.50m);
        r.Paye.Paye.ShouldBe(29_218.10m);            // 2,400 + 2,083.25 + 27,134.85 − 2,400
        r.NetPay.ShouldBe(93_564.40m);
    }

    [Fact]
    public void Top_band_applies_above_800000()
    {
        var r = Pay(1_000_000m);
        r.TaxableIncome.ShouldBe(951_020m);          // − NSSF 6,480 − SHIF 27,500 − AHL 15,000
        r.Paye.Bands.Count.ShouldBe(5);
        r.Paye.Bands[3].Tax.ShouldBe(97_500m);       // 32.5% × 300,000
        r.Paye.Bands[4].Tax.ShouldBe(52_857m);       // 35% × 151,020
        r.Paye.Paye.ShouldBe(292_740.35m);
    }

    [Fact]
    public void Non_taxable_reimbursement_is_paid_but_not_taxed_or_levied()
    {
        var withReimbursement = Pay(100_000m,
            new PayLine("REIMB", "Expense Reimbursement", 8_000m, ComponentCategory.OtherEarning, TaxTreatment.NonTaxable));
        var plain = Pay(100_000m);

        withReimbursement.GrossEarnings.ShouldBe(108_000m);
        withReimbursement.NonTaxableEarnings.ShouldBe(8_000m);
        withReimbursement.Paye.Paye.ShouldBe(plain.Paye.Paye);
        withReimbursement.Sha.ShouldBe(plain.Sha);
        withReimbursement.HousingLevy.ShouldBe(plain.HousingLevy);
        withReimbursement.NetPay.ShouldBe(plain.NetPay + 8_000m);
    }

    [Fact]
    public void Pension_and_nssf_deduction_is_capped_at_30000()
    {
        var pension = new PayLine("PENSION", "Pension", 25_000m, ComponentCategory.Pension, TaxTreatment.AllowableDeduction);
        var r = Pay(300_000m, pension);

        r.Nssf.Employee.ShouldBe(6_480m);
        r.PensionDeduction.ShouldBe(30_000m);        // 25,000 + 6,480 = 31,480 → capped
        r.AllowableDeductions.ShouldBe(30_000m + 8_250m + 4_500m);
        r.TotalDeductions.ShouldBe(r.Paye.Paye + 6_480m + 8_250m + 4_500m + 25_000m);
    }

    [Fact]
    public void Insurance_premiums_earn_15_percent_relief_up_to_the_cap()
    {
        var premium = new PayLine("INS", "Life insurance", 4_000m, ComponentCategory.Insurance, TaxTreatment.InsuranceRelief);
        var r = Pay(100_000m, premium);
        r.Paye.InsuranceRelief.ShouldBe(600m);
        r.Paye.Paye.ShouldBe(19_308.35m - 600m);
        r.NetPay.ShouldBe(70_441.65m + 600m - 4_000m);

        var big = Pay(100_000m, premium with { Amount = 50_000m });
        big.Paye.InsuranceRelief.ShouldBe(5_000m);   // 7,500 capped at 5,000
    }

    [Fact]
    public void Disability_exemption_removes_up_to_150000_from_taxable_income()
    {
        var result = PayrollCalculationEngine.Calculate(new EmployeePayInput { BasicSalary = 200_000m, HasDisabilityExemption = true }, Rules);
        result.DisabilityExemption.ShouldBe(150_000m);
        result.TaxableIncome.ShouldBe(185_020m - 150_000m);
        result.Paye.Paye.ShouldBe(2_889.35m);         // 2,400 + 2,083.25 + 30% × 2,687 (= 806.10) − 2,400
    }

    [Fact]
    public void Joiner_mid_month_has_basic_and_allowances_prorated_but_not_variable_pay()
    {
        var input = new EmployeePayInput
        {
            BasicSalary = 90_000m, DaysInPeriod = 30, DaysPayable = 10,
            Earnings =
            [
                Allowance("Housing", 15_000m),
                new PayLine("OVERTIME", "Overtime", 3_000m, ComponentCategory.Overtime, TaxTreatment.Taxable, IsPensionable: true)
            ]
        };
        var r = PayrollCalculationEngine.Calculate(input, Rules);

        r.ProrationFactor.ShouldBe(10m / 30m);
        r.BasicSalary.ShouldBe(30_000m);
        r.Earnings.Single(e => e.Code == "HOUSING").Amount.ShouldBe(5_000m);
        r.Earnings.Single(e => e.Code == "OVERTIME").Amount.ShouldBe(3_000m);
        r.GrossEarnings.ShouldBe(38_000m);
        r.Steps.ShouldContain(s => s.Label == "Proration");
    }

    [Fact]
    public void Leaver_with_zero_payable_days_has_no_statutory_deductions()
    {
        var r = PayrollCalculationEngine.Calculate(new EmployeePayInput { BasicSalary = 80_000m, DaysInPeriod = 31, DaysPayable = 0 }, Rules);
        r.GrossEarnings.ShouldBe(0m);
        r.TotalDeductions.ShouldBe(0m);
        r.NetPay.ShouldBe(0m);
        r.Warnings.ShouldContain(w => w.Contains("No pay"));
    }

    [Fact]
    public void Absenteeism_reduces_gross_before_tax()
    {
        var absent = new PayLine("ABSENT", "Unpaid leave (2 days)", 10_000m, ComponentCategory.Absenteeism, TaxTreatment.ReducesGross);
        var r = Pay(110_000m, absent);
        r.GrossEarnings.ShouldBe(100_000m);
        r.Paye.Paye.ShouldBe(19_308.35m);
        r.NetPay.ShouldBe(70_441.65m);
        r.Earnings.ShouldContain(e => e.Code == "ABSENT" && e.Amount == -10_000m);
        r.Deductions.ShouldNotContain(d => d.Code == "ABSENT");
    }

    [Fact]
    public void Multiple_post_tax_deductions_reduce_net_only()
    {
        var helb = new PayLine("HELB", "HELB", 3_500m, ComponentCategory.Helb, TaxTreatment.PostTax);
        var loan = new PayLine("LOAN", "Staff loan", 10_000m, ComponentCategory.Loan, TaxTreatment.PostTax, EmployeeDeductionId: 7);
        var r = Pay(100_000m, helb, loan);

        r.Paye.Paye.ShouldBe(19_308.35m);
        r.OtherDeductions.ShouldBe(13_500m);
        r.NetPay.ShouldBe(70_441.65m - 13_500m);
        r.Deductions.Single(d => d.Code == "LOAN").EmployeeDeductionId.ShouldBe(7);
    }

    [Fact]
    public void Deductions_exceeding_pay_are_an_error_and_low_net_pay_is_a_warning()
    {
        var bigLoan = new PayLine("LOAN", "Loan", 80_000m, ComponentCategory.Loan, TaxTreatment.PostTax);
        Pay(100_000m, bigLoan).Errors.ShouldContain(e => e.Contains("exceed gross pay"));

        var largeLoan = new PayLine("LOAN", "Loan", 50_000m, ComponentCategory.Loan, TaxTreatment.PostTax);
        var r = Pay(100_000m, largeLoan);
        r.Errors.ShouldBeEmpty();
        r.Warnings.ShouldContain(w => w.Contains("s.19(3)"));
    }

    [Theory]
    [InlineData(33_333.33, 916.67, 500.00)]       // 916.666575 rounds up; 499.99995 rounds to 500.00
    [InlineData(12_345.67, 339.51, 185.19)]       // 339.505925 → 339.51; 185.18505 → 185.19
    public void Statutory_amounts_round_half_away_from_zero_to_cents(decimal gross, decimal sha, decimal levy)
    {
        var r = Pay(gross);
        r.Sha.ShouldBe(sha);
        r.HousingLevy.ShouldBe(levy);
        (r.GrossEarnings - r.TotalDeductions).ShouldBe(r.NetPay);
        decimal.Round(r.NetPay, 2).ShouldBe(r.NetPay);
    }

    [Fact]
    public void Nssf_limits_come_from_configuration_not_code()
    {
        var year3 = PayrollCalculationEngine.Calculate(new EmployeePayInput { BasicSalary = 100_000m }, VerifiedKenyaRules.Year3());
        year3.Nssf.Tier1.ShouldBe(480m);              // 6% × 8,000
        year3.Nssf.Tier2.ShouldBe(3_840m);            // 6% × (72,000 − 8,000)
        Pay(100_000m).Nssf.Employee.ShouldBe(6_000m);
    }

    [Fact]
    public void Invalid_configuration_is_rejected_with_every_problem_listed()
    {
        var config = VerifiedKenyaRules.Configuration(9_000m, 108_000m);
        config.TaxBrackets.Remove(config.TaxBrackets.Single(b => b.Order == 2)); // gap between bands
        config.Rates.Remove(config.Rates.Single(r => r.Key == StatutoryRateKey.ShaEmployeeRate));

        var ex = Should.Throw<DomainException>(() => StatutoryRules.From(config));
        ex.Message.ShouldContain("Band 2 must start where band 1 ends");
        ex.Message.ShouldContain("ShaEmployeeRate");
    }
}
