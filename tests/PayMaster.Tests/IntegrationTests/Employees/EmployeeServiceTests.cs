using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests.Employees;

[Trait("Category", "MySql")]
[Collection(PayMaster.Tests.TestSupport.MySqlCollection.Name)]
public class EmployeeServiceTests(EmployeeFixture f) : IClassFixture<EmployeeFixture>
{
    [Fact]
    public async Task Create_assigns_sequential_number_records_hire_and_pending_salary_from_structure()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(positionId: f.SoftwareEngineer) with { };
        input.SalaryStructureId = f.EngineerStructure;
        input.BasicSalary = 120_000m;

        var id = await f.Employees(ctx).CreateAsync(input);
        var second = await f.Employees(ctx).CreateAsync(f.NewInput());

        await using var verify = f.Db.CreateContext();
        var employee = await verify.Employees.Include(e => e.EmploymentHistory)
            .Include(e => e.SalaryHistory).ThenInclude(s => s.Components).SingleAsync(e => e.Id == id);
        var next = await verify.Employees.SingleAsync(e => e.Id == second);

        employee.EmployeeNumber.ShouldStartWith("EMP-");
        int.Parse(next.EmployeeNumber[4..]).ShouldBe(int.Parse(employee.EmployeeNumber[4..]) + 1);
        employee.EmploymentHistory.ShouldHaveSingleItem().ChangeType.ShouldBe(EmploymentChangeType.Hired);

        var salary = employee.SalaryHistory.ShouldHaveSingleItem();
        salary.Status.ShouldBe(SalaryRevisionStatus.PendingApproval);
        salary.BasicSalary.ShouldBe(120_000m);
        salary.Components.Select(c => c.Amount).OrderBy(a => a).ShouldBe([10_000m, 24_000m]); // 20% of the new basic, not the template's
        (await verify.Notifications.AnyAsync(n => n.Title == "Salary awaiting approval")).ShouldBeTrue();
    }

    [Fact]
    public async Task Duplicate_national_id_or_kra_pin_is_rejected()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var first = f.NewInput();
        await f.Employees(ctx).CreateAsync(first);

        var sameId = f.NewInput();
        sameId.NationalId = first.NationalId;
        await Should.ThrowAsync<ConflictException>(() => f.Employees(ctx).CreateAsync(sameId));

        var samePin = f.NewInput();
        samePin.KraPin = first.KraPin;
        await Should.ThrowAsync<ConflictException>(() => f.Employees(ctx).CreateAsync(samePin));
    }

    [Fact]
    public async Task Position_must_belong_to_the_selected_department()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(departmentId: f.Finance, positionId: f.SoftwareEngineer);
        var ex = await Should.ThrowAsync<DomainException>(() => f.Employees(ctx).CreateAsync(input));
        ex.Message.ShouldContain("different department");
    }

    [Fact]
    public async Task Reporting_cycles_are_rejected()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var service = f.Employees(ctx);
        var a = await service.CreateAsync(f.NewInput(first: "Alice"));
        var bInput = f.NewInput(first: "Bob");
        bInput.ManagerId = a;
        var b = await service.CreateAsync(bInput);

        var aEdit = await service.GetForEditAsync(a);
        aEdit.ManagerId = b;
        await Should.ThrowAsync<DomainException>(() => service.UpdateAsync(a, aEdit));
    }

    [Fact]
    public async Task Moving_to_a_higher_grade_records_a_promotion()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var service = f.Employees(ctx);
        var id = await service.CreateAsync(f.NewInput(positionId: f.SoftwareEngineer));

        var edit = await service.GetForEditAsync(id);
        edit.PositionId = f.SeniorEngineer;
        await service.UpdateAsync(id, edit);

        edit = await service.GetForEditAsync(id);
        edit.DepartmentId = f.Finance;
        edit.PositionId = f.Accountant;
        await service.UpdateAsync(id, edit);

        await using var verify = f.Db.CreateContext();
        var changes = await verify.EmploymentHistories.Where(h => h.EmployeeId == id).OrderBy(h => h.Id).ToListAsync();
        changes.Select(c => c.ChangeType).ShouldBe([EmploymentChangeType.Hired, EmploymentChangeType.Promotion, EmploymentChangeType.Demotion]);
        changes[2].FromDepartment.ShouldBe("Engineering");
        changes[2].ToDepartment.ShouldBe("Finance");
    }

    [Fact]
    public async Task Termination_records_history_and_disables_the_linked_login()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var service = f.Employees(ctx);
        var id = await service.CreateAsync(f.NewInput());
        var employee = await ctx.Employees.SingleAsync(e => e.Id == id);
        employee.UserId = 777;
        await ctx.SaveChangesAsync();

        await service.TerminateAsync(id, new DateOnly(2026, 9, 30), "End of contract");

        await using var verify = f.Db.CreateContext();
        var saved = await verify.Employees.Include(e => e.EmploymentHistory).SingleAsync(e => e.Id == id);
        saved.Status.ShouldBe(EmploymentStatus.Terminated);
        saved.TerminationDate.ShouldBe(new DateOnly(2026, 9, 30));
        saved.EmploymentHistory.ShouldContain(h => h.ChangeType == EmploymentChangeType.Termination && h.ToStatus == EmploymentStatus.Terminated);
        f.Users.DisabledLogins.ShouldContain(777);

        await Should.ThrowAsync<DomainException>(() => service.TerminateAsync(id, new DateOnly(2026, 9, 30), "again"));
    }

    [Fact]
    public async Task Managers_see_only_their_team_and_employees_see_only_themselves()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var service = f.Employees(ctx);
        var manager = await service.CreateAsync(f.NewInput(first: "Mgr", last: "Scope"));
        var reportInput = f.NewInput(departmentId: f.Finance, first: "Report", last: "Scope");
        reportInput.ManagerId = manager;
        var report = await service.CreateAsync(reportInput);
        var outsider = await service.CreateAsync(f.NewInput(departmentId: f.Finance, first: "Outsider", last: "Scope"));

        f.As(Roles.Manager, manager);
        var visible = await f.Employees(ctx).ListAsync(new EmployeeQuery { Search = "Scope", PageSize = 50 });
        visible.Items.Select(i => i.Id).ShouldBe([manager, report], ignoreOrder: true);
        visible.Items.ShouldAllBe(i => i.BasicSalary == null); // managers do not see pay
        await Should.ThrowAsync<ForbiddenAccessException>(() => f.Employees(ctx).GetAsync(outsider));

        f.As(Roles.Employee, report);
        (await f.Employees(ctx).GetAsync(report)).Id.ShouldBe(report);
        await Should.ThrowAsync<ForbiddenAccessException>(() => f.Employees(ctx).GetAsync(manager));
        await Should.ThrowAsync<NotFoundException>(() => f.Employees(ctx).GetAsync(int.MaxValue));
    }

    [Fact]
    public async Task Bank_account_is_masked_for_users_without_bank_permission()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(first: "Bank", last: "Mask");
        input.BankAccountNumber = "0987654321234";
        var id = await f.Employees(ctx).CreateAsync(input);

        (await f.Employees(ctx).GetAsync(id)).BankAccountNumber.ShouldBe("0987654321234");

        f.AsHr();
        f.Db.User.Permissions.Remove(Permissions.Employees.ViewBankDetails);
        var masked = await f.Employees(ctx).GetAsync(id);
        masked.BankAccountNumber.ShouldBe("••••1234");
        masked.BankDetailsMasked.ShouldBeTrue();

        // Editing without bank permission keeps the stored number.
        var edit = await f.Employees(ctx).GetForEditAsync(id);
        edit.KeepExistingBankAccount.ShouldBeTrue();
        edit.PhoneNumber = "0712345678";
        await f.Employees(ctx).UpdateAsync(id, edit);
        await using var verify = f.Db.CreateContext();
        var saved = await verify.Employees.SingleAsync(e => e.Id == id);
        saved.BankAccountNumber.ShouldBe("0987654321234");
        saved.PhoneNumber.ShouldBe("+254712345678");
    }

    [Fact]
    public async Task List_shows_current_approved_salary_and_ignores_pending_revisions()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(first: "Salary", last: "Lister");
        input.SalaryStructureId = f.EngineerStructure;
        input.BasicSalary = 100_000m;
        var id = await f.Employees(ctx).CreateAsync(input);

        var pending = await f.Employees(ctx).ListAsync(new EmployeeQuery { Search = "Lister" });
        pending.Items.Single().BasicSalary.ShouldBeNull();

        var revision = await ctx.EmployeeSalaries.SingleAsync(s => s.EmployeeId == id);
        revision.Approve(approverId: 2, f.Db.Clock.GetUtcNow().UtcDateTime);
        await ctx.SaveChangesAsync();

        var approved = await f.Employees(ctx).ListAsync(new EmployeeQuery { Search = "Lister" });
        approved.Items.Single().BasicSalary.ShouldBe(100_000m);
        approved.Items.Single().GrossSalary.ShouldBe(130_000m);
    }
}
