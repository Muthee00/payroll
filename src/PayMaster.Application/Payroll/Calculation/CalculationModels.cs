using PayMaster.Domain.Enums;

namespace PayMaster.Application.Payroll.Calculation;

/// <summary>An earning or deduction fed into the engine.</summary>
/// <param name="Prorate">Recurring pay (allowances) is prorated for part-month employment; variable pay is not.</param>
public sealed record PayLine(
    string Code,
    string Name,
    decimal Amount,
    ComponentCategory Category,
    TaxTreatment TaxTreatment,
    bool IsPensionable = false,
    bool Prorate = false,
    int? SalaryComponentId = null,
    int? EmployeeDeductionId = null);

public sealed record EmployeePayInput
{
    /// <summary>Full monthly basic salary before proration.</summary>
    public decimal BasicSalary { get; init; }
    public int DaysInPeriod { get; init; } = 30;
    public int DaysPayable { get; init; } = 30;
    public IReadOnlyList<PayLine> Earnings { get; init; } = [];
    public IReadOnlyList<PayLine> Deductions { get; init; } = [];
    public bool HasDisabilityExemption { get; init; }

    /// <summary>Warn when net pay is below this fraction of gross (Employment Act s.19(3): one third).</summary>
    public decimal MinimumNetPayRatio { get; init; } = 0.3333m;
}

public sealed record CalculatedLine(
    string Code,
    string Name,
    decimal Amount,
    ComponentCategory? Category,
    TaxTreatment? TaxTreatment,
    bool IsPensionable,
    int? SalaryComponentId,
    int? EmployeeDeductionId,
    DeductionKind Kind = DeductionKind.Voluntary);

public sealed record PayeBandResult(decimal From, decimal? To, decimal Rate, decimal TaxableInBand, decimal Tax);

/// <summary>One human-readable line of the calculation explanation.</summary>
public sealed record BreakdownStep(string Section, string Label, string Detail, decimal Amount);

public sealed record NssfResult(decimal Tier1, decimal Tier2, decimal EmployerTier1, decimal EmployerTier2)
{
    public decimal Employee => Tier1 + Tier2;
    public decimal Employer => EmployerTier1 + EmployerTier2;
}

public sealed record PayeResult(
    IReadOnlyList<PayeBandResult> Bands,
    decimal TaxBeforeReliefs,
    decimal PersonalRelief,
    decimal InsuranceRelief,
    decimal HousingLevyRelief,
    decimal Paye);

public sealed record EmployeePayResult
{
    public decimal ProrationFactor { get; init; } = 1m;
    public decimal BasicSalary { get; init; }
    public decimal GrossEarnings { get; init; }
    public decimal TaxableEarnings { get; init; }
    public decimal NonTaxableEarnings { get; init; }
    public decimal PensionablePay { get; init; }
    public decimal StatutoryBasis { get; init; }

    public NssfResult Nssf { get; init; } = new(0, 0, 0, 0);
    public decimal Sha { get; init; }
    public decimal HousingLevy { get; init; }
    public decimal EmployerHousingLevy { get; init; }

    public decimal PensionDeduction { get; init; }
    public decimal AllowableDeductions { get; init; }
    public decimal DisabilityExemption { get; init; }
    public decimal TaxableIncome { get; init; }
    public PayeResult Paye { get; init; } = new([], 0, 0, 0, 0, 0);

    public IReadOnlyList<CalculatedLine> Earnings { get; init; } = [];
    public IReadOnlyList<CalculatedLine> Deductions { get; init; } = [];
    public decimal StatutoryDeductions => Paye.Paye + Sha + HousingLevy + Nssf.Employee;
    public decimal OtherDeductions { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetPay { get; init; }
    public decimal TotalEmployerContributions => Nssf.Employer + EmployerHousingLevy;

    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<BreakdownStep> Steps { get; init; } = [];
}
