using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Domain.Entities.Payroll;

/// <summary>A monthly pay period. One period per (Year, Month).</summary>
public class PayrollPeriod : AuditableEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Open;

    public ICollection<PayrollRun> Runs { get; set; } = [];

    public int CalendarDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    public static PayrollPeriod ForMonth(int year, int month, int paymentDay = 28)
    {
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var payDay = Math.Clamp(paymentDay, 1, end.Day);

        return new PayrollPeriod
        {
            Year = year,
            Month = month,
            Name = start.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
            StartDate = start,
            EndDate = end,
            PaymentDate = new DateOnly(year, month, payDay)
        };
    }
}
