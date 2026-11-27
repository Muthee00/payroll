using Microsoft.Extensions.Time.Testing;
using PayMaster.Application.Employees;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Employees;

public class EmployeeInputValidatorTests
{
    private readonly EmployeeInputValidator _validator = new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero)));

    private static EmployeeInput Valid() => new()
    {
        FirstName = "Jane", LastName = "Doe", Gender = Gender.Female, DateOfBirth = new DateOnly(1990, 1, 1),
        NationalId = "31234567", KraPin = "A012345678K", DepartmentId = 1, EmploymentType = EmploymentType.Permanent,
        EmploymentDate = new DateOnly(2026, 1, 1), PaymentMethod = PaymentMethod.BankTransfer,
        BankName = "Equity Bank", BankAccountNumber = "0123456789"
    };

    private string[] Errors(EmployeeInput input) => _validator.Validate(input).Errors.Select(e => e.PropertyName).ToArray();

    [Fact]
    public void Valid_input_passes() => _validator.Validate(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("A012345678K", true)]
    [InlineData("P051234567Q", true)]
    [InlineData("a012345678k", true)] // normalised to upper case
    [InlineData("A01234567K", false)]
    [InlineData("B012345678K", false)]
    [InlineData("A0123456789", false)]
    public void Kra_pin_format(string pin, bool valid)
    {
        var input = Valid();
        input.KraPin = pin;
        Errors(input).Contains(nameof(EmployeeInput.KraPin)).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("+254712345678", true)]
    [InlineData("0712345678", true)]
    [InlineData("0112345678", true)]
    [InlineData("254712345678", true)]
    [InlineData("12345", false)]
    [InlineData("+255712345678", false)]
    public void Kenyan_phone_numbers(string phone, bool valid)
    {
        var input = Valid();
        input.PhoneNumber = phone;
        Errors(input).Contains(nameof(EmployeeInput.PhoneNumber)).ShouldBe(!valid);
    }

    [Fact]
    public void Contract_employees_need_an_end_date_after_start()
    {
        var input = Valid();
        input.EmploymentType = EmploymentType.Contract;
        Errors(input).ShouldContain(nameof(EmployeeInput.ContractEndDate));

        input.ContractEndDate = new DateOnly(2025, 12, 31);
        Errors(input).ShouldContain(nameof(EmployeeInput.ContractEndDate));

        input.ContractEndDate = new DateOnly(2027, 12, 31);
        Errors(input).ShouldNotContain(nameof(EmployeeInput.ContractEndDate));
    }

    [Fact]
    public void Payment_details_depend_on_payment_method()
    {
        var bank = Valid();
        bank.BankAccountNumber = null;
        Errors(bank).ShouldContain(nameof(EmployeeInput.BankAccountNumber));

        bank.KeepExistingBankAccount = true; // editing without bank permission
        Errors(bank).ShouldNotContain(nameof(EmployeeInput.BankAccountNumber));

        var mobile = Valid();
        mobile.PaymentMethod = PaymentMethod.MobileMoney;
        mobile.BankName = null;
        mobile.BankAccountNumber = null;
        Errors(mobile).ShouldBe([nameof(EmployeeInput.MobileMoneyNumber)]);
    }

    [Fact]
    public void Employees_must_be_at_least_sixteen()
    {
        var input = Valid();
        input.DateOfBirth = new DateOnly(2012, 1, 1);
        Errors(input).ShouldContain(nameof(EmployeeInput.DateOfBirth));
    }

    [Fact]
    public void Disability_exemption_requires_certificate()
    {
        var input = Valid();
        input.HasDisabilityExemption = true;
        Errors(input).ShouldContain(nameof(EmployeeInput.DisabilityExemptionCertificateNumber));
    }
}
