using System.Security.Claims;
using PayMaster.Application.Common.Security;

namespace PayMaster.Web.Navigation;

/// <param name="AnyOf">Shown when the user holds any of these permissions (empty = every signed-in user).</param>
/// <param name="HideIfAny">Hidden when the user holds any of these (avoids duplicate self-service links for admins).</param>
public record NavItem(string Title, string Icon, string Controller, string Action, string[] AnyOf, string? Section = null, string[]? HideIfAny = null);

/// <summary>
/// Sidebar definition. Visibility mirrors the authorization policies on each controller;
/// hiding a link is a convenience, never the access control itself.
/// </summary>
public static class NavigationMenu
{
    private static readonly NavItem[] Items =
    [
        new("Dashboard", "bi-grid-1x2", "Dashboard", "Index", []),
        new("Employees", "bi-people", "Employees", "Index", [Permissions.Employees.View]),
        new("My Profile", "bi-person-badge", "Employees", "Me", [Permissions.Self.Access], HideIfAny: [Permissions.Employees.View]),
        new("Payroll", "bi-cash-stack", "Payroll", "Index", [Permissions.Payroll.View], "Payroll"),
        new("Salary Structures", "bi-diagram-3", "SalaryStructures", "Index", [Permissions.Salary.View]),
        new("Deductions", "bi-dash-circle", "Deductions", "Index", [Permissions.Salary.ManagePayItems, Permissions.Salary.View]),
        new("Statutory Settings", "bi-bank", "Statutory", "Index", [Permissions.Statutory.View]),
        new("Payslips", "bi-receipt", "Payslips", "Index", [Permissions.Payslips.ViewAll, Permissions.Self.Access]),
        new("Payroll History", "bi-clock-history", "PayrollHistory", "Index", [Permissions.Payroll.View]),
        new("Performance", "bi-graph-up-arrow", "Performance", "Index",
            [Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access], "Performance"),
        new("Goals & KPIs", "bi-bullseye", "Goals", "Index",
            [Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access]),
        new("Performance Reviews", "bi-clipboard-check", "Reviews", "Index",
            [Permissions.Performance.ViewAll, Permissions.Performance.ManageTeam, Permissions.Self.Access]),
        new("Reports", "bi-bar-chart-line", "Reports", "Index", [Permissions.Reports.Payroll, Permissions.Reports.Performance], "Insights"),
        new("Users & Roles", "bi-shield-lock", "Users", "Index", [Permissions.Administration.ManageUsers], "Administration"),
        new("Audit Log", "bi-journal-text", "AuditLog", "Index", [Permissions.Administration.ViewAuditLog]),
        new("Settings", "bi-gear", "Settings", "Index", [Permissions.Administration.ManageSettings])
    ];

    public static IEnumerable<NavItem> For(ClaimsPrincipal user) =>
        Items.Where(i => (i.AnyOf.Length == 0 || i.AnyOf.Any(p => user.HasClaim(Permissions.ClaimType, p)))
                         && (i.HideIfAny is null || !i.HideIfAny.Any(p => user.HasClaim(Permissions.ClaimType, p))));
}
