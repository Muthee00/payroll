using FluentValidation;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Salary;

public sealed class SalaryStructureInputValidator : AbstractValidator<SalaryStructureInput>
{
    public SalaryStructureInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code may contain letters, numbers, dashes and underscores only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.BasicSalary).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.EffectiveFrom).NotNull().WithMessage("Effective date is required.");
        RuleFor(x => x.EffectiveTo).GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo is not null && x.EffectiveFrom is not null)
            .WithMessage("End date must be after the effective date.");
        RuleFor(x => x.Lines).Must(lines => lines.Select(l => l.SalaryComponentId).Distinct().Count() == lines.Count)
            .WithMessage("Each allowance can appear only once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SalaryComponentId).GreaterThan(0).WithMessage("Choose an allowance.");
            line.RuleFor(l => l.Amount).GreaterThanOrEqualTo(0).WithMessage("Amounts cannot be negative.");
            line.RuleFor(l => l.Amount).LessThanOrEqualTo(100).When(l => l.AmountType == AmountType.PercentageOfBasic)
                .WithMessage("A percentage of basic cannot exceed 100%.");
        });
    }
}

public sealed class SalaryGradeInputValidator : AbstractValidator<SalaryGradeInput>
{
    public SalaryGradeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MinimumSalary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumSalary).GreaterThan(x => x.MinimumSalary).WithMessage("Maximum must be greater than minimum.");
    }
}

public sealed class StructureRevisionInputValidator : AbstractValidator<StructureRevisionInput>
{
    public StructureRevisionInputValidator()
    {
        RuleFor(x => x.IncreasePercent).GreaterThan(-50).LessThanOrEqualTo(100).NotEqual(0)
            .WithMessage("Enter a percentage change between -50% and 100%.");
        RuleFor(x => x.EffectiveFrom).NotNull().WithMessage("Effective date is required.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SalaryChangeInputValidator : AbstractValidator<SalaryChangeInput>
{
    public SalaryChangeInputValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.BasicSalary).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.EffectiveFrom).NotNull().WithMessage("Effective date is required.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Give a reason for the salary change.");
        RuleForEach(x => x.Components).ChildRules(c => c.RuleFor(i => i.Amount).GreaterThanOrEqualTo(0)
            .WithMessage("Allowance amounts cannot be negative."));
    }
}

public sealed class SalaryComponentInputValidator : AbstractValidator<SalaryComponentInput>
{
    private static readonly TaxTreatment[] EarningTreatments = [TaxTreatment.Taxable, TaxTreatment.NonTaxable];

    public SalaryComponentInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Z0-9_]+$")
            .WithMessage("Code must be upper-case letters, digits or underscores (e.g. NIGHT_ALLOWANCE).");
        RuleFor(x => x.Code).Must(c => c is not ("BASIC" or "PAYE" or "SHA" or "SHIF" or "AHL" or "NSSF"))
            .WithMessage("That code is reserved for statutory or basic pay lines.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TaxTreatment).Must((input, treatment) =>
                input.Type == ComponentType.Earning ? EarningTreatments.Contains(treatment) : !EarningTreatments.Contains(treatment))
            .WithMessage("Earnings are Taxable or Non-taxable; deductions use a deduction tax treatment.");
        RuleFor(x => x.Category).Must((input, category) =>
                input.Type == ComponentType.Earning ? (int)category < 20 : (int)category >= 20)
            .WithMessage("Choose a category that matches the component type.");
    }
}

public sealed class RecurringDeductionInputValidator : AbstractValidator<RecurringDeductionInput>
{
    public RecurringDeductionInputValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.SalaryComponentId).GreaterThan(0).WithMessage("Choose a deduction type.");
        RuleFor(x => x.MonthlyAmount).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(x => x.MonthlyAmount).When(x => x.TotalAmount is not null)
            .WithMessage("The total amount must be at least one monthly instalment.");
        RuleFor(x => x.AmountRecovered).GreaterThanOrEqualTo(0)
            .LessThan(x => x.TotalAmount ?? decimal.MaxValue).WithMessage("Amount already recovered must be less than the total.");
        RuleFor(x => x.StartDate).NotNull().WithMessage("Start date is required.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null && x.StartDate is not null);
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class PayrollInputInputValidator : AbstractValidator<PayrollInputInput>
{
    public PayrollInputInputValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.SalaryComponentId).GreaterThan(0).WithMessage("Choose an item type.");
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x).Must(x => x.Amount is > 0 || (x.Quantity is > 0 && x.Rate is > 0))
            .OverridePropertyName("Amount").WithMessage("Enter an amount, or a quantity and rate.");
        RuleFor(x => x.Amount).LessThan(100_000_000m).When(x => x.Amount is not null);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
