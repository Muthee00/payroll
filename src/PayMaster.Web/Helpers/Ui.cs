using System.Globalization;
using System.Security.Claims;
using PayMaster.Application.Common.Security;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Web.Helpers;

/// <summary>Presentation helpers shared by views.</summary>
public static class Ui
{
    private static readonly CultureInfo Kenya = CultureInfo.GetCultureInfo("en-KE");
    private static readonly TimeZoneInfo Nairobi = ResolveNairobi();

    public static string Money(decimal amount) => $"KSh {amount.ToString("N2", Kenya)}";

    /// <summary>Whole-shilling format for dashboards and summary cards.</summary>
    public static string MoneyShort(decimal amount) => $"KSh {amount.ToString("N0", Kenya)}";

    public static string Number(decimal value, int decimals = 0) => value.ToString($"N{decimals}", Kenya);

    public static string Percent(decimal fraction, int decimals = 1) => (fraction * 100m).ToString($"N{decimals}", Kenya) + "%";

    // Invariant month names ("Sep", not en-KE's "Sept") keep dates compact and consistent.
    private static readonly CultureInfo Dates = CultureInfo.InvariantCulture;

    public static string Date(DateOnly? date) => date?.ToString("dd MMM yyyy", Dates) ?? "—";

    public static string Date(DateTime? utc) => utc is null ? "—" : ToLocal(utc.Value).ToString("dd MMM yyyy", Dates);

    public static string DateTime(DateTime? utc) => utc is null ? "—" : ToLocal(utc.Value).ToString("dd MMM yyyy, h:mm tt", Dates);

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), Nairobi);

    public static DateOnly TodayLocal() => DateOnly.FromDateTime(ToLocal(System.DateTime.UtcNow));

    public static string Initials(string? name)
    {
        var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    /// <summary>Stable colour class per person so avatars are consistent across pages.</summary>
    public static string AvatarClass(string? key)
    {
        var hash = 0;
        foreach (var c in key ?? string.Empty)
            hash = unchecked(hash * 31 + c);
        return $"avatar-c{Math.Abs(hash % 6)}";
    }

    public static string MaskAccount(string? account) =>
        string.IsNullOrEmpty(account) ? "—" : account.Length <= 4 ? "****" : $"•••• {account[^4..]}";

    public static string DisplayName(ClaimsPrincipal user) =>
        user.FindFirstValue(PayMasterClaimTypes.FullName) ?? user.Identity?.Name ?? "User";

    /// <summary>Most privileged role, for the top bar.</summary>
    public static string PrimaryRole(ClaimsPrincipal user) =>
        Roles.All.FirstOrDefault(user.IsInRole) ?? "User";

    public static bool Can(this ClaimsPrincipal user, string permission) =>
        user.HasClaim(Permissions.ClaimType, permission);

    /// <summary>Splits PascalCase enum names for display ("PendingApproval" → "Pending Approval").</summary>
    public static string Humanize(Enum value) => Humanize(value.ToString());

    public static string Humanize(string text) =>
        string.Concat(text.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]) ? " " + c : c.ToString()));

    public static string StatusCss(PayMaster.Domain.Enums.EmploymentStatus status) => status switch
    {
        PayMaster.Domain.Enums.EmploymentStatus.Active => "status-success",
        PayMaster.Domain.Enums.EmploymentStatus.OnLeave => "status-info",
        PayMaster.Domain.Enums.EmploymentStatus.Suspended => "status-warning",
        PayMaster.Domain.Enums.EmploymentStatus.Deactivated => "status-neutral",
        _ => "status-danger"
    };

    public static string StatusCss(PayMaster.Domain.Enums.SalaryRevisionStatus status) => status switch
    {
        PayMaster.Domain.Enums.SalaryRevisionStatus.Approved => "status-success",
        PayMaster.Domain.Enums.SalaryRevisionStatus.PendingApproval => "status-warning",
        PayMaster.Domain.Enums.SalaryRevisionStatus.Rejected => "status-danger",
        _ => "status-neutral"
    };

    public static string StatusCss(PayMaster.Domain.Enums.PayrollRunStatus status) => status switch
    {
        PayMaster.Domain.Enums.PayrollRunStatus.Draft => "status-neutral",
        PayMaster.Domain.Enums.PayrollRunStatus.Calculating => "status-info",
        PayMaster.Domain.Enums.PayrollRunStatus.PendingApproval => "status-warning",
        PayMaster.Domain.Enums.PayrollRunStatus.Approved => "status-purple",
        PayMaster.Domain.Enums.PayrollRunStatus.Finalized => "status-success",
        PayMaster.Domain.Enums.PayrollRunStatus.Paid => "status-success",
        _ => "status-danger"
    };

    public static string StatusLabel(PayMaster.Domain.Enums.PayrollRunStatus status) =>
        status == PayMaster.Domain.Enums.PayrollRunStatus.Paid ? "Paid" : Humanize(status);

    public static string StatusCss(PayMaster.Domain.Enums.PayrollLineStatus status) => status switch
    {
        PayMaster.Domain.Enums.PayrollLineStatus.Calculated => "status-success",
        PayMaster.Domain.Enums.PayrollLineStatus.Error => "status-danger",
        _ => "status-warning"
    };

    public static string StatusCss(PayMaster.Domain.Enums.GoalStatus status) => status switch
    {
        PayMaster.Domain.Enums.GoalStatus.Completed => "status-success",
        PayMaster.Domain.Enums.GoalStatus.InProgress => "status-info",
        PayMaster.Domain.Enums.GoalStatus.Overdue => "status-danger",
        PayMaster.Domain.Enums.GoalStatus.Cancelled => "status-neutral",
        _ => "status-warning"
    };

    public static string StatusCss(PayMaster.Domain.Enums.ReviewStatus status) => status switch
    {
        PayMaster.Domain.Enums.ReviewStatus.Finalized => "status-success",
        PayMaster.Domain.Enums.ReviewStatus.Cancelled => "status-neutral",
        PayMaster.Domain.Enums.ReviewStatus.PendingHrFinalization => "status-purple",
        _ => "status-warning"
    };

    public static string ReviewStage(PayMaster.Domain.Enums.ReviewStatus status) => status switch
    {
        PayMaster.Domain.Enums.ReviewStatus.PendingManagerEvaluation => "Manager evaluation",
        PayMaster.Domain.Enums.ReviewStatus.PendingSelfAssessment => "Self-assessment",
        PayMaster.Domain.Enums.ReviewStatus.PendingManagerFeedback => "Manager feedback",
        PayMaster.Domain.Enums.ReviewStatus.PendingAcknowledgement => "Employee acknowledgement",
        PayMaster.Domain.Enums.ReviewStatus.PendingHrFinalization => "HR finalization",
        _ => Humanize(status)
    };

    public static string FileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:N0} KB",
        _ => $"{bytes / 1024d / 1024d:N1} MB"
    };

    private static TimeZoneInfo ResolveNairobi()
    {
        foreach (var id in new[] { "Africa/Nairobi", "E. Africa Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
                return tz;
        }
        return TimeZoneInfo.CreateCustomTimeZone("EAT", TimeSpan.FromHours(3), "East Africa Time", "EAT");
    }
}
