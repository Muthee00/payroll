using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PayMaster.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores <see cref="DateOnly"/> as a MySQL DATE via <see cref="DateTime"/>. The Oracle
/// MySql.EntityFrameworkCore provider writes DateOnly but fails to materialise it
/// ("Unable to cast DateTime to DateOnly"), so the conversion is explicit.
/// </summary>
public sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    d => d.ToDateTime(TimeOnly.MinValue),
    d => DateOnly.FromDateTime(d));
