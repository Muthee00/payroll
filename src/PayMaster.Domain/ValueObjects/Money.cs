namespace PayMaster.Domain.ValueObjects;

/// <summary>
/// Single source of truth for monetary precision. All amounts are KES held as
/// <see cref="decimal"/> and rounded half away from zero to 2 decimal places.
/// </summary>
public static class Money
{
    public const string Currency = "KES";
    public const int Scale = 2;

    public static decimal Round(decimal amount) =>
        Math.Round(amount, Scale, MidpointRounding.AwayFromZero);

    public static decimal NonNegative(decimal amount) => amount < 0 ? 0 : amount;

    public static decimal Percent(decimal amount, decimal rate) => Round(amount * rate);

    public static string Format(decimal amount) => $"KSh {amount:N2}";
}
