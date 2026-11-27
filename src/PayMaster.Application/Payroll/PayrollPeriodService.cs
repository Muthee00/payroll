using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Domain.Entities.Payroll;

namespace PayMaster.Application.Payroll;

public record PeriodOption(int Year, int Month, string Name)
{
    public string Key => $"{Year:D4}-{Month:D2}";
}

public sealed class PayrollPeriodService(IApplicationDbContext db, TimeProvider clock)
{
    /// <summary>Returns the period for a month, creating it (with the company pay day) on first use.</summary>
    public async Task<PayrollPeriod> EnsureAsync(int year, int month, CancellationToken ct = default)
    {
        var period = await db.PayrollPeriods.SingleOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is not null)
            return period;

        var paymentDay = await db.CompanySettings.Select(c => (int?)c.PaymentDay).FirstOrDefaultAsync(ct) ?? 28;
        period = PayrollPeriod.ForMonth(year, month, paymentDay);
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return period;
    }

    public PeriodOption Current()
    {
        var now = clock.GetUtcNow().UtcDateTime.AddHours(3); // East Africa Time
        return Option(now.Year, now.Month);
    }

    /// <summary>The last <paramref name="back"/> months through <paramref name="ahead"/> months ahead, newest first.</summary>
    public IReadOnlyList<PeriodOption> Recent(int back = 12, int ahead = 2)
    {
        var current = Current();
        var anchor = new DateOnly(current.Year, current.Month, 1);
        return Enumerable.Range(-ahead, back + ahead + 1)
            .Select(offset => anchor.AddMonths(-offset))
            .Select(d => Option(d.Year, d.Month))
            .ToList();
    }

    public static PeriodOption Option(int year, int month) =>
        new(year, month, new DateOnly(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));

    public static bool TryParse(string? key, out int year, out int month)
    {
        year = month = 0;
        return key is { Length: 7 } && int.TryParse(key[..4], out year) && int.TryParse(key[5..], out month) && month is >= 1 and <= 12;
    }
}
