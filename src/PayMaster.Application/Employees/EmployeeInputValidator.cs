using FluentValidation;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Employees;

public sealed class EmployeeInputValidator : AbstractValidator<EmployeeInput>
{
    public const string KraPinPattern = "^[AP][0-9]{9}[A-Z]$";
    public const string KenyanPhonePattern = @"^(\+?254|0)[17][0-9]{8}$";

    public EmployeeInputValidator(TimeProvider clock)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).IsInEnum();

        RuleFor(x => x.DateOfBirth).NotNull().WithMessage("Date of birth is required.")
            .Must(d => d is null || d.Value <= today.AddYears(-16)).WithMessage("Employee must be at least 16 years old.")
            .Must(d => d is null || d.Value >= today.AddYears(-100)).WithMessage("Enter a valid date of birth.");

        RuleFor(x => x.NationalId).NotEmpty().Matches("^[0-9]{6,10}$")
            .WithMessage("National ID must be 6–10 digits.");
        RuleFor(x => x.KraPin).Must(p => p is null || System.Text.RegularExpressions.Regex.IsMatch(p.Trim().ToUpperInvariant(), KraPinPattern))
            .WithMessage("KRA PIN must look like A123456789Z.");
        RuleFor(x => x.NssfNumber).MaximumLength(20).Matches("^[0-9]+$").When(x => !string.IsNullOrWhiteSpace(x.NssfNumber))
            .WithMessage("NSSF number must contain digits only.");
        RuleFor(x => x.ShaNumber).MaximumLength(30);

        RuleFor(x => x.PersonalEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));
        RuleFor(x => x.WorkEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.WorkEmail));
        RuleFor(x => x.PhoneNumber).Must(BeKenyanPhone).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Enter a Kenyan phone number, e.g. +254712345678.");
        RuleFor(x => x.EmergencyContactPhone).Must(BeKenyanPhone).When(x => !string.IsNullOrWhiteSpace(x.EmergencyContactPhone))
            .WithMessage("Enter a Kenyan phone number, e.g. +254712345678.");
        RuleFor(x => x.PhysicalAddress).MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactRelationship).MaximumLength(50);

        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Select a department.");
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.EmploymentDate).NotNull().WithMessage("Employment date is required.")
            .Must(d => d is null || d.Value <= today.AddMonths(6)).WithMessage("Employment date is too far in the future.");
        RuleFor(x => x.ContractEndDate).NotNull().When(x => x.EmploymentType == EmploymentType.Contract)
            .WithMessage("Contract end date is required for contract employees.");
        RuleFor(x => x.ContractEndDate).GreaterThan(x => x.EmploymentDate)
            .When(x => x.ContractEndDate is not null && x.EmploymentDate is not null)
            .WithMessage("Contract end date must be after the employment date.");
        RuleFor(x => x.WorkLocation).MaximumLength(100);

        RuleFor(x => x.PaymentMethod).IsInEnum();
        When(x => x.PaymentMethod == PaymentMethod.BankTransfer, () =>
        {
            RuleFor(x => x.BankName).NotEmpty().WithMessage("Bank name is required for bank transfers.").MaximumLength(100);
            RuleFor(x => x.BankAccountNumber).NotEmpty().When(x => !x.KeepExistingBankAccount)
                .WithMessage("Account number is required for bank transfers.");
            RuleFor(x => x.BankAccountNumber).Matches("^[0-9A-Za-z-]{6,34}$")
                .When(x => !x.KeepExistingBankAccount && !string.IsNullOrWhiteSpace(x.BankAccountNumber))
                .WithMessage("Enter a valid account number.");
        });
        RuleFor(x => x.MobileMoneyNumber)
            .NotEmpty().WithMessage("A mobile money number is required for mobile money payments.")
            .Must(BeKenyanPhone).WithMessage("Enter a valid Kenyan mobile money number.")
            .When(x => x.PaymentMethod == PaymentMethod.MobileMoney);
        RuleFor(x => x.BankBranch).MaximumLength(100);

        RuleFor(x => x.DisabilityExemptionCertificateNumber)
            .NotEmpty().WithMessage("Certificate number is required for the disability exemption.")
            .MaximumLength(50)
            .When(x => x.HasDisabilityExemption);

        RuleFor(x => x.BasicSalary).GreaterThan(0).When(x => x.BasicSalary is not null)
            .LessThan(100_000_000m);
    }

    private static bool BeKenyanPhone(string? phone) =>
        phone is not null && System.Text.RegularExpressions.Regex.IsMatch(phone.Replace(" ", ""), KenyanPhonePattern);
}
