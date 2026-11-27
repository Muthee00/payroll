using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Interfaces;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Domain.Entities.Salary;

/// <summary>
/// Recurring voluntary deduction (HELB, loan, salary advance, insurance, pension, SACCO).
/// When <see cref="TotalAmount"/> is set, the deduction stops once fully recovered.
/// </summary>
public class EmployeeDeduction : SoftDeletableEntity, IAuditTracked
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public decimal MonthlyAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal AmountRecovered { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reference { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal? RemainingBalance =>
        TotalAmount is null ? null : Money.NonNegative(TotalAmount.Value - AmountRecovered);

    /// <summary>Instalment due for a period ending on <paramref name="periodEnd"/>, never more than the balance.</summary>
    public decimal AmountDueFor(DateOnly periodStart, DateOnly periodEnd)
    {
        if (!IsActive || StartDate > periodEnd || (EndDate is not null && EndDate < periodStart))
            return 0m;

        var remaining = RemainingBalance;
        if (remaining is null)
            return MonthlyAmount;

        return Math.Min(MonthlyAmount, remaining.Value);
    }

    public void RecordRecovery(decimal amount)
    {
        AmountRecovered = Money.Round(AmountRecovered + amount);
        if (TotalAmount is not null && AmountRecovered >= TotalAmount)
            IsActive = false;
    }

    public void ReverseRecovery(decimal amount)
    {
        AmountRecovered = Money.NonNegative(Money.Round(AmountRecovered - amount));
        if (TotalAmount is null || AmountRecovered < TotalAmount)
            IsActive = true;
    }
}
