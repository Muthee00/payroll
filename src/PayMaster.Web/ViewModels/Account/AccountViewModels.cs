using FluentValidation;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Web.ViewModels.Account;

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class LoginValidator : AbstractValidator<LoginViewModel>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class ForgotPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordViewModel>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
}

public class ResetPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordViewModel>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(128);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}

public class ChangePasswordViewModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordViewModel>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).MaximumLength(128)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must differ from the current one.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTime? LastLoginAt { get; set; }
    public IReadOnlyList<UserSession> Sessions { get; set; } = [];
    public Guid? CurrentSessionId { get; set; }
}

public class ProfileValidator : AbstractValidator<ProfileViewModel>
{
    public ProfileValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).Matches(@"^\+?[0-9 ]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber)).WithMessage("Enter a valid phone number.");
    }
}
