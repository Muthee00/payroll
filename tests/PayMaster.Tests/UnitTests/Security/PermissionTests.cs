using PayMaster.Application.Common.Security;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Security;

public class PermissionTests
{
    [Fact]
    public void Permission_names_are_unique_and_discovered()
    {
        Permissions.All.Count.ShouldBeGreaterThan(20);
        Permissions.All.Distinct().Count().ShouldBe(Permissions.All.Count);
        Permissions.All.ShouldNotContain(Permissions.ClaimType);
    }

    [Fact]
    public void Every_role_has_a_default_permission_set_of_known_permissions()
    {
        foreach (var role in Roles.All)
        {
            Permissions.DefaultRolePermissions.ShouldContainKey(role);
            Permissions.DefaultRolePermissions[role].ShouldAllBe(p => Permissions.All.Contains(p));
        }
    }

    [Fact]
    public void Only_super_administrator_can_approve_or_reverse_payroll_and_manage_statutory_rules()
    {
        foreach (var (role, permissions) in Permissions.DefaultRolePermissions)
        {
            var isSuperAdmin = role == Roles.SuperAdmin;
            permissions.Contains(Permissions.Payroll.Approve).ShouldBe(isSuperAdmin, role);
            permissions.Contains(Permissions.Payroll.Reverse).ShouldBe(isSuperAdmin, role);
            permissions.Contains(Permissions.Statutory.Manage).ShouldBe(isSuperAdmin, role);
            permissions.Contains(Permissions.Administration.ManageUsers).ShouldBe(isSuperAdmin, role);
        }
    }

    [Fact]
    public void Employee_role_is_self_service_only()
    {
        Permissions.DefaultRolePermissions[Roles.Employee].ShouldBe([Permissions.Self.Access]);
    }

    [Fact]
    public void Manager_cannot_see_payroll_or_bank_details()
    {
        var manager = Permissions.DefaultRolePermissions[Roles.Manager];
        manager.ShouldNotContain(Permissions.Payroll.View);
        manager.ShouldNotContain(Permissions.Employees.ViewBankDetails);
        manager.ShouldNotContain(Permissions.Payslips.ViewAll);
    }

    [Fact]
    public void Payroll_officer_processes_but_cannot_approve()
    {
        var officer = Permissions.DefaultRolePermissions[Roles.PayrollOfficer];
        officer.ShouldContain(Permissions.Payroll.Process);
        officer.ShouldContain(Permissions.Payroll.Finalize);
        officer.ShouldNotContain(Permissions.Payroll.Approve);
    }
}
