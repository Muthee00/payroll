using PayMaster.Application.Statutory;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Payroll.Calculation;

/// <summary>
/// Computes one employee's pay for a period from configured statutory rules. Pure and
/// deterministic: no database, clock or hard-coded rates. All arithmetic is decimal.
///
/// Order of operations:
///  1. Prorate basic and recurring allowances for part-month employment.
///  2. Gross = earnings − gross reductions (unpaid leave / absenteeism).
///  3. NSSF on pensionable pay (tiers), SHIF and Housing Levy on gross salary.
///  4. Taxable income = taxable earnings − allowable deductions (NSSF + pension capped, SHIF, AHL where
///     configured as deductible) − disability exemption.
///  5. PAYE by bands, less personal / insurance / housing reliefs (never below zero).
///  6. Net = gross − statutory − voluntary deductions. Employer contributions are separate.
/// </summary>
public static class PayrollCalculationEngine
{
    public static EmployeePayResult Calculate(EmployeePayInput input, StatutoryRules rules)
    {
        var steps = new List<BreakdownStep>();
        var errors = new List<string>();
        var warnings = new List<string>();

        if (input.DaysInPeriod <= 0 || input.DaysPayable < 0 || input.DaysPayable > input.DaysInPeriod)
            throw new ArgumentException("Payable days must be between 0 and the days in the period.", nameof(input));

        // 1. Proration ------------------------------------------------------------------
        var factor = input.DaysPayable == input.DaysInPeriod ? 1m : (decimal)input.DaysPayable / input.DaysInPeriod;
        var basic = Money.Round(input.BasicSalary * factor);
        var earnings = new List<CalculatedLine>
        {
            new("BASIC", "Basic Salary", basic, ComponentCategory.Basic, TaxTreatment.Taxable, true, null, null)
        };
        if (factor != 1m)
            steps.Add(new("Earnings", "Proration", $"{input.DaysPayable} of {input.DaysInPeriod} days payable ({factor:P2})", factor));
        steps.Add(new("Earnings", "Basic salary", factor == 1m ? "Monthly basic" : $"{Money.Format(input.BasicSalary)} × {input.DaysPayable}/{input.DaysInPeriod}", basic));

        foreach (var line in input.Earnings.Where(e => e.Amount != 0))
        {
            var amount = line.Prorate ? Money.Round(line.Amount * factor) : Money.Round(line.Amount);
            earnings.Add(new(line.Code, line.Name, amount, line.Category, line.TaxTreatment, line.IsPensionable, line.SalaryComponentId, null));
            steps.Add(new("Earnings", line.Name, $"{Describe(line.TaxTreatment)}{(line.Prorate && factor != 1m ? ", prorated" : "")}", amount));
        }

        // 2. Gross reductions (unpaid leave / absenteeism) reduce pay before tax --------
        foreach (var line in input.Deductions.Where(d => d.TaxTreatment == TaxTreatment.ReducesGross && d.Amount > 0))
        {
            var amount = -Money.Round(line.Amount);
            earnings.Add(new(line.Code, line.Name, amount, line.Category, line.TaxTreatment, true, line.SalaryComponentId, null));
            steps.Add(new("Earnings", line.Name, "Reduces gross pay", amount));
        }

        var gross = Money.Round(earnings.Sum(e => e.Amount));
        if (gross < 0)
        {
            errors.Add($"Absenteeism deductions ({Money.Format(-earnings.Where(e => e.Amount < 0).Sum(e => e.Amount))}) exceed earnings.");
            gross = 0;
        }
        var nonTaxable = Money.Round(earnings.Where(e => e.TaxTreatment == TaxTreatment.NonTaxable).Sum(e => e.Amount));
        var taxableEarnings = Money.NonNegative(gross - nonTaxable);
        var pensionable = Money.NonNegative(Money.Round(earnings.Where(e => e.IsPensionable).Sum(e => e.Amount)));
        var statutoryBasis = taxableEarnings; // gross salary excluding non-taxable reimbursements

        steps.Add(new("Earnings", "Gross earnings", "Total earnings after gross reductions", gross));
        if (nonTaxable > 0)
            steps.Add(new("Earnings", "Non-taxable earnings", "Excluded from taxable pay, SHIF and Housing Levy", nonTaxable));

        // 3. Statutory contributions -----------------------------------------------------
        var nssf = NssfCalculator.Calculate(pensionable, rules);
        steps.Add(new("NSSF", "Pensionable pay", "Pensionable earnings", pensionable));
        steps.Add(new("NSSF", "Tier I", $"{rules.NssfEmployeeRate:P2} of {Money.Format(Math.Min(pensionable, rules.NssfLowerEarningsLimit))} (up to LEL {Money.Format(rules.NssfLowerEarningsLimit)})", nssf.Tier1));
        steps.Add(new("NSSF", "Tier II", $"{rules.NssfEmployeeRate:P2} of earnings between LEL and UEL {Money.Format(rules.NssfUpperEarningsLimit)}", nssf.Tier2));
        steps.Add(new("NSSF", "Employer contribution", $"{rules.NssfEmployerRate:P2}, matched by employer (not deducted from employee)", nssf.Employer));

        var sha = ShaCalculator.Calculate(statutoryBasis, rules);
        var shaDetail = $"{rules.ShaEmployeeRate:P2} of {Money.Format(statutoryBasis)}";
        if (statutoryBasis > 0 && sha == rules.ShaMinimumContribution && Money.Percent(statutoryBasis, rules.ShaEmployeeRate) < rules.ShaMinimumContribution)
            shaDetail += $", raised to the minimum {Money.Format(rules.ShaMinimumContribution)}";
        steps.Add(new("SHIF", "SHIF contribution", shaDetail, sha));

        var (levy, employerLevy) = HousingLevyCalculator.Calculate(statutoryBasis, rules);
        steps.Add(new("Housing Levy", "Employee levy", $"{rules.HousingLevyEmployeeRate:P2} of {Money.Format(statutoryBasis)}", levy));
        steps.Add(new("Housing Levy", "Employer levy", $"{rules.HousingLevyEmployerRate:P2} of {Money.Format(statutoryBasis)} (employer cost)", employerLevy));

        // 4. Taxable income ---------------------------------------------------------------
        var pensionContributions = Money.Round(input.Deductions.Where(d => d.TaxTreatment == TaxTreatment.AllowableDeduction).Sum(d => d.Amount));
        var pensionClaim = pensionContributions + (rules.NssfDeductibleForPaye ? nssf.Employee : 0m);
        var pensionDeduction = Math.Min(pensionClaim, rules.PensionContributionMonthlyCap);
        var allowable = pensionDeduction
                        + (rules.ShaDeductibleForPaye ? sha : 0m)
                        + (rules.HousingLevyDeductibleForPaye ? levy : 0m);
        allowable = Money.Round(allowable);

        steps.Add(new("PAYE", "Taxable earnings", "Gross earnings less non-taxable items", taxableEarnings));
        if (pensionClaim > 0)
        {
            var parts = new List<string>();
            if (rules.NssfDeductibleForPaye && nssf.Employee > 0) parts.Add("NSSF employee contribution");
            if (pensionContributions > 0) parts.Add("registered pension");
            var detail = string.Join(" + ", parts);
            if (pensionClaim > rules.PensionContributionMonthlyCap)
                detail += $" ({Money.Format(pensionClaim)}) capped at {Money.Format(rules.PensionContributionMonthlyCap)}";
            steps.Add(new("PAYE", "Pension & NSSF deduction", detail, -pensionDeduction));
        }
        if (rules.ShaDeductibleForPaye && sha > 0)
            steps.Add(new("PAYE", "SHIF deduction", "Allowable deduction", -sha));
        if (rules.HousingLevyDeductibleForPaye && levy > 0)
            steps.Add(new("PAYE", "Housing Levy deduction", "Allowable deduction", -levy));

        var disability = 0m;
        if (input.HasDisabilityExemption && rules.DisabilityExemptionMonthly > 0)
        {
            disability = Math.Min(rules.DisabilityExemptionMonthly, Money.NonNegative(taxableEarnings - allowable));
            steps.Add(new("PAYE", "Disability exemption", $"Exempt up to {Money.Format(rules.DisabilityExemptionMonthly)} a month", -disability));
        }

        var taxableIncome = Money.NonNegative(Money.Round(taxableEarnings - allowable - disability));
        steps.Add(new("PAYE", "Taxable income", "Chargeable pay", taxableIncome));

        // 5. PAYE ----------------------------------------------------------------------------
        var insurancePremiums = Money.Round(input.Deductions.Where(d => d.TaxTreatment == TaxTreatment.InsuranceRelief).Sum(d => d.Amount));
        var paye = PayeCalculator.Calculate(taxableIncome, insurancePremiums, sha, levy, rules);
        foreach (var band in paye.Bands)
            steps.Add(new("PAYE", $"Band {Money.Format(band.From)} – {(band.To is decimal to ? Money.Format(to) : "above")}",
                $"{band.Rate:P1} × {Money.Format(band.TaxableInBand)}", band.Tax));
        steps.Add(new("PAYE", "Tax before reliefs", "Sum of bands", paye.TaxBeforeReliefs));
        if (paye.PersonalRelief > 0)
            steps.Add(new("PAYE", "Personal relief", $"Monthly relief {Money.Format(rules.PersonalRelief)}", -paye.PersonalRelief));
        if (paye.InsuranceRelief > 0)
            steps.Add(new("PAYE", "Insurance relief", $"{rules.InsuranceReliefRate:P0} of qualifying premiums, max {Money.Format(rules.InsuranceReliefMonthlyCap)}", -paye.InsuranceRelief));
        if (paye.HousingLevyRelief > 0)
            steps.Add(new("PAYE", "Affordable housing relief", $"{rules.HousingLevyReliefRate:P0} of levy", -paye.HousingLevyRelief));
        steps.Add(new("PAYE", "PAYE", "Tax payable", paye.Paye));

        // 6. Deductions & net ---------------------------------------------------------------
        var deductions = new List<CalculatedLine>
        {
            new("PAYE", "PAYE", paye.Paye, null, null, false, null, null, DeductionKind.Statutory),
            new("SHIF", "SHIF", sha, null, null, false, null, null, DeductionKind.Statutory),
            new("AHL", "Housing Levy", levy, null, null, false, null, null, DeductionKind.Statutory),
            new("NSSF", "NSSF", nssf.Employee, null, null, false, null, null, DeductionKind.Statutory)
        };
        foreach (var line in input.Deductions.Where(d => d.TaxTreatment != TaxTreatment.ReducesGross && d.Amount > 0))
        {
            deductions.Add(new(line.Code, line.Name, Money.Round(line.Amount), line.Category, line.TaxTreatment, false,
                line.SalaryComponentId, line.EmployeeDeductionId));
            steps.Add(new("Deductions", line.Name, Describe(line.TaxTreatment), Money.Round(line.Amount)));
        }

        var other = Money.Round(deductions.Where(d => d.Kind == DeductionKind.Voluntary).Sum(d => d.Amount));
        var totalDeductions = Money.Round(deductions.Sum(d => d.Amount));
        var net = Money.Round(gross - totalDeductions);
        steps.Add(new("Summary", "Gross earnings", "", gross));
        steps.Add(new("Summary", "Total deductions", "Statutory + other deductions", totalDeductions));
        steps.Add(new("Summary", "Net pay", "Gross − deductions", net));
        steps.Add(new("Summary", "Employer contributions", "NSSF + Housing Levy (employer)", nssf.Employer + employerLevy));

        if (net < 0)
            errors.Add($"Deductions exceed gross pay by {Money.Format(-net)}. Reduce voluntary deductions for this period.");
        else if (gross > 0 && net < Money.Round(gross * input.MinimumNetPayRatio))
            warnings.Add($"Net pay is below {input.MinimumNetPayRatio:P0} of gross pay (Employment Act s.19(3) limits deductions to two-thirds of wages).");
        if (gross == 0 && errors.Count == 0)
            warnings.Add("No pay for this period.");

        return new EmployeePayResult
        {
            ProrationFactor = factor,
            BasicSalary = basic,
            GrossEarnings = gross,
            TaxableEarnings = taxableEarnings,
            NonTaxableEarnings = nonTaxable,
            PensionablePay = pensionable,
            StatutoryBasis = statutoryBasis,
            Nssf = nssf,
            Sha = sha,
            HousingLevy = levy,
            EmployerHousingLevy = employerLevy,
            PensionDeduction = pensionDeduction,
            AllowableDeductions = allowable,
            DisabilityExemption = disability,
            TaxableIncome = taxableIncome,
            Paye = paye,
            Earnings = earnings,
            Deductions = deductions,
            OtherDeductions = other,
            TotalDeductions = totalDeductions,
            NetPay = net,
            Errors = errors,
            Warnings = warnings,
            Steps = steps
        };
    }

    private static string Describe(TaxTreatment treatment) => treatment switch
    {
        TaxTreatment.Taxable => "Taxable",
        TaxTreatment.NonTaxable => "Non-taxable",
        TaxTreatment.PostTax => "Deducted after tax",
        TaxTreatment.AllowableDeduction => "Reduces taxable income (capped)",
        TaxTreatment.InsuranceRelief => "Qualifies for insurance relief",
        TaxTreatment.ReducesGross => "Reduces gross pay",
        _ => treatment.ToString()
    };
}
