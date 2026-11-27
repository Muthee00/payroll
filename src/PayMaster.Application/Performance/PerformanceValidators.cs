using FluentValidation;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Performance;

public sealed class GoalInputValidator : AbstractValidator<GoalInput>
{
    public GoalInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SuccessCriteria).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotNull();
        RuleFor(x => x.TargetDate).NotNull().GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Target date must be on or after the start date.");
    }
}

public sealed class KpiInputValidator : AbstractValidator<KpiInput>
{
    public KpiInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MeasurementUnit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Weight).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.TargetValue).GreaterThan(0).When(x => x.ScoringMethod != KpiScoringMethod.ManualRating);
    }
}

public sealed class ReviewCycleInputValidator : AbstractValidator<ReviewCycleInput>
{
    public ReviewCycleInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PeriodStart).NotNull();
        RuleFor(x => x.PeriodEnd).NotNull().GreaterThan(x => x.PeriodStart).WithMessage("Period end must be after the start.");
        RuleFor(x => x.DueDate).NotNull();
    }
}

public sealed class DevelopmentPlanInputValidator : AbstractValidator<DevelopmentPlanInput>
{
    public DevelopmentPlanInputValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Skills).MaximumLength(1000);
        RuleFor(x => x.TargetDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.StartDate is not null && x.TargetDate is not null);
    }
}
