using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Domain;

public class SalaryDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1234.565, 1234.57)]
    [InlineData(1234.564, 1234.56)]
    [InlineData(-0.005, -0.01)]
    [InlineData(2.675, 2.68)] // decimal keeps 2.675 exact, unlike double
    public void Money_rounds_half_away_from_zero(decimal input, decimal expected) =>
        Money.Round(input).ShouldBe(expected);

    [Fact]
    public void Money_percent_uses_decimal_arithmetic()
    {
        Money.Percent(33_333.33m, 0.0275m).ShouldBe(916.67m);
        Money.Percent(0.1m + 0.2m, 1m).ShouldBe(0.3m);
    }

    [Fact]
    public void Salary_change_requires_a_different_approver()
    {
        var revision = new EmployeeSalary { BasicSalary = 100_000m, CreatedById = 5 };
        Should.Throw<DomainException>(() => revision.Approve(5, Now));

        revision.Approve(6, Now);
        revision.Status.ShouldBe(SalaryRevisionStatus.Approved);
        revision.ApprovedById.ShouldBe(6);
    }

    [Fact]
    public void Approved_revision_cannot_be_approved_again()
    {
        var revision = new EmployeeSalary { CreatedById = 5 };
        revision.Approve(6, Now);
        Should.Throw<DomainException>(() => revision.Approve(7, Now));
    }

    [Fact]
    public void Superseding_closes_the_previous_revision_the_day_before()
    {
        var current = new EmployeeSalary { EffectiveFrom = new DateOnly(2025, 1, 1), Status = SalaryRevisionStatus.Approved };
        current.Supersede(new DateOnly(2026, 1, 1));
        current.EffectiveTo.ShouldBe(new DateOnly(2025, 12, 31));
        current.Status.ShouldBe(SalaryRevisionStatus.Superseded);
    }

    [Fact]
    public void New_revision_must_start_after_the_current_one()
    {
        var current = new EmployeeSalary { EffectiveFrom = new DateOnly(2026, 1, 1), Status = SalaryRevisionStatus.Approved };
        Should.Throw<DomainException>(() => current.Supersede(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Gross_salary_is_basic_plus_recurring_components()
    {
        var revision = new EmployeeSalary
        {
            BasicSalary = 100_000m,
            Components =
            [
                new EmployeeSalaryComponent { Amount = 20_000m },
                new EmployeeSalaryComponent { Amount = 10_000m },
                new EmployeeSalaryComponent { Amount = 5_000m }
            ]
        };
        revision.GrossSalary.ShouldBe(135_000m);
    }

    [Fact]
    public void Structure_resolves_percentage_of_basic()
    {
        var structure = new SalaryStructure { BasicSalary = 100_000m };
        structure.ResolveAmount(new SalaryStructureComponent { AmountType = AmountType.PercentageOfBasic, Amount = 15m }).ShouldBe(15_000m);
        structure.ResolveAmount(new SalaryStructureComponent { AmountType = AmountType.Fixed, Amount = 10_000m }).ShouldBe(10_000m);
    }

    [Fact]
    public void Loan_instalment_never_exceeds_remaining_balance_and_stops_when_repaid()
    {
        var loan = new EmployeeDeduction
        {
            MonthlyAmount = 10_000m, TotalAmount = 25_000m, AmountRecovered = 20_000m,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var october = (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        loan.AmountDueFor(october.Item1, october.Item2).ShouldBe(5_000m);
        loan.RecordRecovery(5_000m);
        loan.IsActive.ShouldBeFalse();
        loan.AmountDueFor(october.Item1, october.Item2).ShouldBe(0m);

        loan.ReverseRecovery(5_000m);
        loan.IsActive.ShouldBeTrue();
        loan.RemainingBalance.ShouldBe(5_000m);
    }

    [Fact]
    public void Deduction_outside_its_date_range_is_not_due()
    {
        var helb = new EmployeeDeduction { MonthlyAmount = 3_500m, StartDate = new DateOnly(2026, 11, 1) };
        helb.AmountDueFor(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).ShouldBe(0m);
        helb.AmountDueFor(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)).ShouldBe(3_500m);
    }
}
