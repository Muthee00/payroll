namespace PayMaster.Application.Common.Security;

public static class Roles
{
    public const string SuperAdmin = "Super Administrator";
    public const string HrAdmin = "HR Administrator";
    public const string PayrollOfficer = "Payroll Officer";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, HrAdmin, PayrollOfficer, Manager, Employee];
}
