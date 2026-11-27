using System.Reflection;

namespace PayMaster.Application.Common.Security;

/// <summary>
/// Fine-grained permissions, stored as role claims and enforced server-side through
/// authorization policies named after each permission.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Employees
    {
        public const string View = "employees.view";
        public const string Manage = "employees.manage";
        public const string Terminate = "employees.terminate";
        public const string ViewBankDetails = "employees.bank.view";
        public const string Import = "employees.import";
        public const string Export = "employees.export";
    }

    public static class Organization
    {
        public const string Manage = "organization.manage";
    }

    public static class Salary
    {
        public const string View = "salary.view";
        public const string ManageStructures = "salary.structures.manage";
        public const string ManageEmployeeSalary = "salary.employee.manage";
        public const string Approve = "salary.approve";

        /// <summary>Employee earnings/deductions and per-period payroll inputs.</summary>
        public const string ManagePayItems = "salary.payitems.manage";
    }

    public static class Statutory
    {
        public const string View = "statutory.view";
        public const string Manage = "statutory.manage";
    }

    public static class Payroll
    {
        public const string View = "payroll.view";
        public const string Process = "payroll.process";
        public const string Approve = "payroll.approve";
        public const string Finalize = "payroll.finalize";
        public const string MarkPaid = "payroll.markpaid";
        public const string Reverse = "payroll.reverse";
        public const string Export = "payroll.export";
    }

    public static class Payslips
    {
        public const string ViewAll = "payslips.view.all";
        public const string Generate = "payslips.generate";
    }

    public static class Reports
    {
        public const string Payroll = "reports.payroll";
        public const string Performance = "reports.performance";
    }

    public static class Performance
    {
        public const string ViewAll = "performance.view.all";
        public const string ManageTeam = "performance.team.manage";
        public const string ManageCycles = "performance.cycles.manage";
        public const string Configure = "performance.configure";
    }

    public static class Administration
    {
        public const string ManageUsers = "admin.users.manage";
        public const string ManageSettings = "admin.settings.manage";
        public const string ViewAuditLog = "admin.audit.view";
    }

    /// <summary>Self-service: own profile, payslips, goals, reviews. Every employee user has it.</summary>
    public static class Self
    {
        public const string Access = "self.access";
    }

    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string) && f.Name != nameof(ClaimType))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    /// <summary>Default permission set per role, applied by the seeder.</summary>
    public static IReadOnlyDictionary<string, string[]> DefaultRolePermissions { get; } = new Dictionary<string, string[]>
    {
        [Roles.SuperAdmin] = [.. All],

        [Roles.HrAdmin] =
        [
            Employees.View, Employees.Manage, Employees.Terminate, Employees.ViewBankDetails,
            Employees.Import, Employees.Export, Organization.Manage,
            Salary.View, Salary.ManageStructures, Salary.ManageEmployeeSalary, Salary.Approve,
            Statutory.View, Payroll.View, Payslips.ViewAll, Reports.Payroll, Reports.Performance,
            Performance.ViewAll, Performance.ManageTeam, Performance.ManageCycles, Performance.Configure,
            Self.Access
        ],

        [Roles.PayrollOfficer] =
        [
            Employees.View, Employees.ViewBankDetails, Employees.Export,
            Salary.View, Salary.ManagePayItems, Statutory.View,
            Payroll.View, Payroll.Process, Payroll.Finalize, Payroll.MarkPaid, Payroll.Export,
            Payslips.ViewAll, Payslips.Generate, Reports.Payroll,
            Self.Access
        ],

        [Roles.Manager] =
        [
            Employees.View, Performance.ManageTeam, Reports.Performance,
            Self.Access
        ],

        [Roles.Employee] = [Self.Access]
    };
}
