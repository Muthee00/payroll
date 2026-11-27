using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll.Calculation;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Statutory;

public record StatutoryVersionListItem(
    int Id, int Version, string Name, DateOnly EffectiveFrom, DateOnly? EffectiveTo, StatutoryConfigurationStatus Status,
    bool IsLocked, int PayrollRunCount, DateTime? ActivatedAt);

public record StatutoryRateRow(StatutoryKeyInfo Info, decimal? StoredValue, string? Notes);

public record StatutoryVersionDetails(
    int Id, int Version, string Name, string? Description, string? LegalReference, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    StatutoryConfigurationStatus Status, bool IsLocked, bool IsEditable, DateTime? ActivatedAt, int PayrollRunCount,
    IReadOnlyList<TaxBand> Bands, IReadOnlyList<StatutoryRateRow> Rates, IReadOnlyList<string> ValidationProblems);

public class TaxBandInput
{
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal RatePercent { get; set; }
}

public class StatutoryRateInput
{
    public StatutoryRateKey Key { get; set; }
    /// <summary>Percent for rate keys, KES for amounts, 0/1 for flags.</summary>
    public decimal Value { get; set; }
    public string? Notes { get; set; }
}

public class StatutoryConfigInput
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? LegalReference { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public List<TaxBandInput> Bands { get; set; } = [];
    public List<StatutoryRateInput> Rates { get; set; } = [];
}

/// <summary>
/// Version-controlled statutory rules. Only drafts are editable; activation closes the previous
/// version the day before the new one takes effect. Versions used by finalized payroll are locked.
/// </summary>
public sealed class StatutoryConfigService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    INotificationService notifications,
    IAuditService audit,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<StatutoryVersionListItem>> ListAsync(CancellationToken ct = default) =>
        await db.StatutoryConfigurations.AsNoTracking()
            .OrderByDescending(c => c.Version)
            .Select(c => new StatutoryVersionListItem(c.Id, c.Version, c.Name, c.EffectiveFrom, c.EffectiveTo, c.Status, c.IsLocked,
                db.PayrollRuns.Count(r => r.StatutoryConfigurationId == c.Id), c.ActivatedAt))
            .ToListAsync(ct);

    public async Task<StatutoryVersionDetails> GetAsync(int id, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, tracking: false, ct);
        var rates = c.Rates.ToDictionary(r => r.Key);
        var runs = await db.PayrollRuns.CountAsync(r => r.StatutoryConfigurationId == id, ct);
        return new StatutoryVersionDetails(c.Id, c.Version, c.Name, c.Description, c.LegalReference, c.EffectiveFrom, c.EffectiveTo,
            c.Status, c.IsLocked, c.IsEditable, c.ActivatedAt, runs,
            c.TaxBrackets.OrderBy(b => b.Order).Select(b => new TaxBand(b.LowerBound, b.UpperBound, b.Rate)).ToList(),
            StatutoryKeyMetadata.All.Select(info => new StatutoryRateRow(info,
                rates.TryGetValue(info.Key, out var r) ? r.Value : null, rates.TryGetValue(info.Key, out var n) ? n.Notes : null)).ToList(),
            StatutoryRules.Validate(c.TaxBrackets, c.Rates));
    }

    public async Task<StatutoryConfigInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, tracking: false, ct);
        c.EnsureEditable();
        var rates = c.Rates.ToDictionary(r => r.Key);
        return new StatutoryConfigInput
        {
            Name = c.Name, Description = c.Description, LegalReference = c.LegalReference, EffectiveFrom = c.EffectiveFrom,
            Bands = c.TaxBrackets.OrderBy(b => b.Order)
                .Select(b => new TaxBandInput { LowerBound = b.LowerBound, UpperBound = b.UpperBound, RatePercent = b.Rate * 100m }).ToList(),
            Rates = StatutoryKeyMetadata.All.Select(info => new StatutoryRateInput
            {
                Key = info.Key,
                Value = rates.TryGetValue(info.Key, out var r) ? StatutoryKeyMetadata.ToDisplay(info.Key, r.Value) : 0m,
                Notes = rates.TryGetValue(info.Key, out var n) ? n.Notes : null
            }).ToList()
        };
    }

    /// <summary>New draft version copied from <paramref name="sourceId"/> (or the latest version).</summary>
    public async Task<int> CreateDraftAsync(int? sourceId, CancellationToken ct = default)
    {
        if (await db.StatutoryConfigurations.AnyAsync(c => c.Status == StatutoryConfigurationStatus.Draft, ct))
            throw new ConflictException("A draft version already exists. Edit or delete it before creating another.");

        var source = sourceId is int id
            ? await LoadAsync(id, tracking: false, ct)
            : await db.StatutoryConfigurations.AsNoTracking().Include(c => c.TaxBrackets).Include(c => c.Rates)
                .OrderByDescending(c => c.Version).FirstOrDefaultAsync(ct);

        var nextVersion = (await db.StatutoryConfigurations.MaxAsync(c => (int?)c.Version, ct) ?? 0) + 1;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var draft = new StatutoryConfiguration
        {
            Version = nextVersion,
            Name = source is null ? $"Statutory rules v{nextVersion}" : $"{source.Name} (v{nextVersion} draft)",
            Description = source?.Description,
            LegalReference = source?.LegalReference,
            EffectiveFrom = new DateOnly(today.Year, today.Month, 1).AddMonths(1),
            Status = StatutoryConfigurationStatus.Draft,
            TaxBrackets = source?.TaxBrackets.Select(b => new TaxBracket
            {
                Order = b.Order, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate
            }).ToList() ?? [],
            Rates = source?.Rates.Select(r => new StatutoryRate { Key = r.Key, Value = r.Value, Notes = r.Notes }).ToList() ?? []
        };
        db.StatutoryConfigurations.Add(draft);
        await db.SaveChangesAsync(ct);
        return draft.Id;
    }

    public async Task UpdateDraftAsync(int id, StatutoryConfigInput input, CancellationToken ct = default)
    {
        var config = await LoadAsync(id, tracking: true, ct);
        config.EnsureEditable();

        config.Name = input.Name.Trim();
        config.Description = input.Description?.Trim();
        config.LegalReference = input.LegalReference?.Trim();
        config.EffectiveFrom = input.EffectiveFrom ?? throw new DomainException("Effective date is required.");

        db.TaxBrackets.RemoveRange(config.TaxBrackets);
        config.TaxBrackets.Clear();
        var order = 1;
        foreach (var band in input.Bands.Where(b => b.RatePercent > 0 || b.UpperBound is not null || b.LowerBound > 0).OrderBy(b => b.LowerBound))
            config.TaxBrackets.Add(new TaxBracket
            {
                Order = order++, LowerBound = band.LowerBound, UpperBound = band.UpperBound, Rate = band.RatePercent / 100m
            });

        db.StatutoryRates.RemoveRange(config.Rates);
        config.Rates.Clear();
        foreach (var rate in input.Rates)
            config.Rates.Add(new StatutoryRate
            {
                Key = rate.Key,
                Value = StatutoryKeyMetadata.FromDisplay(rate.Key, rate.Value),
                Notes = string.IsNullOrWhiteSpace(rate.Notes) ? null : rate.Notes.Trim()
            });

        var problems = StatutoryRules.Validate(config.TaxBrackets, config.Rates);
        if (problems.Count > 0)
            throw new DomainException(string.Join(" ", problems));

        audit.Record("Statutory.DraftUpdated", nameof(StatutoryConfiguration), id, $"Version {config.Version}",
            newValues: new
            {
                Bands = config.TaxBrackets.Select(b => new { b.LowerBound, b.UpperBound, b.Rate }),
                Rates = config.Rates.ToDictionary(r => r.Key.ToString(), r => r.Value)
            });
        await db.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(int id, CancellationToken ct = default)
    {
        var config = await LoadAsync(id, tracking: true, ct);
        StatutoryRules.From(config); // full validation

        var active = await db.StatutoryConfigurations
            .Where(c => c.Status == StatutoryConfigurationStatus.Active && c.Id != id).ToListAsync(ct);
        if (active.Any(a => a.EffectiveFrom >= config.EffectiveFrom))
            throw new DomainException("The new version must take effect after the version currently in force.");

        var finalizedAfter = await db.PayrollRuns.AnyAsync(r =>
            (r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid) && r.PayrollPeriod.EndDate >= config.EffectiveFrom, ct);
        if (finalizedAfter)
            throw new DomainException("Payroll is already finalized for a period on or after this effective date. Choose a later date.");

        foreach (var previous in active)
            previous.CloseBefore(config.EffectiveFrom);
        config.Activate(currentUser.RequireUserId(), clock.GetUtcNow().UtcDateTime);

        audit.Record("Statutory.Activated", nameof(StatutoryConfiguration), id,
            $"Version {config.Version} effective {config.EffectiveFrom:yyyy-MM-dd}; superseded {string.Join(", ", active.Select(a => $"v{a.Version}"))}");
        await notifications.NotifyPermissionHoldersAsync(Permissions.Payroll.Process, "Statutory rules updated",
            $"Version {config.Version} ({config.Name}) applies to payroll from {config.EffectiveFrom:dd MMM yyyy}.",
            NotificationType.Info, $"/Statutory/Details/{id}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var config = await LoadAsync(id, tracking: true, ct);
        config.EnsureEditable();
        db.StatutoryConfigurations.Remove(config);
        audit.Record("Statutory.DraftDeleted", nameof(StatutoryConfiguration), id, $"Version {config.Version}");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Rules in force on <paramref name="date"/> (the payroll period end date).</summary>
    public async Task<StatutoryRules> GetRulesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var config = await db.StatutoryConfigurations.AsNoTracking()
            .Include(c => c.TaxBrackets).Include(c => c.Rates)
            .Where(c => c.Status != StatutoryConfigurationStatus.Draft && c.EffectiveFrom <= date
                        && (c.EffectiveTo == null || c.EffectiveTo >= date))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException($"No active statutory configuration covers {date:dd MMM yyyy}. Configure Statutory Settings first.");
        return StatutoryRules.From(config);
    }

    /// <summary>Sample calculation with a version's rules (for checking a draft before activation).</summary>
    public async Task<EmployeePayResult> PreviewAsync(int id, decimal grossSalary, CancellationToken ct = default)
    {
        var config = await LoadAsync(id, tracking: false, ct);
        var rules = StatutoryRules.From(config);
        return PayrollCalculationEngine.Calculate(new EmployeePayInput { BasicSalary = grossSalary }, rules);
    }

    private async Task<StatutoryConfiguration> LoadAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = db.StatutoryConfigurations.Include(c => c.TaxBrackets).Include(c => c.Rates).AsQueryable();
        if (!tracking)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Statutory configuration", id);
    }
}
