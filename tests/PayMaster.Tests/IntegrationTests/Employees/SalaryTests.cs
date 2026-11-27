using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Salary;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests.Employees;

[Trait("Category", "MySql")]
[Collection(PayMaster.Tests.TestSupport.MySqlCollection.Name)]
public class SalaryTests(EmployeeFixture f) : IClassFixture<EmployeeFixture>
{
    private const int Hr = 1;
    private const int Approver = 2;

    /// <summary>Creates an employee with an approved starting salary of 100,000 on the engineer structure (from 1 Jan 2026).</summary>
    private async Task<int> EmployeeOnStructureAsync(string last)
    {
        f.AsHr();
        f.Db.User.UserId = Hr;
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(positionId: f.SoftwareEngineer, last: last);
        input.SalaryStructureId = f.EngineerStructure;
        input.BasicSalary = 100_000m;
        var id = await f.Employees(ctx).CreateAsync(input);

        f.Db.User.UserId = Approver;
        var pending = await ctx.EmployeeSalaries.SingleAsync(s => s.EmployeeId == id);
        await f.SalaryChanges(ctx).ApproveAsync(pending.Id);
        f.Db.User.UserId = Hr;
        return id;
    }

    [Fact]
    public async Task Approved_change_supersedes_previous_revision_without_overwriting_it()
    {
        var id = await EmployeeOnStructureAsync("Raise");
        await using var ctx = f.Db.CreateContext();
        var startingAllowances = await ctx.EmployeeSalaryComponents.Where(c => c.EmployeeSalary.EmployeeId == id).SumAsync(c => c.Amount);

        var proposal = await f.SalaryChanges(ctx).BuildProposalAsync(id);
        proposal.BasicSalary = 110_000m;
        proposal.EffectiveFrom = new DateOnly(2026, 11, 1);
        proposal.Reason = "Annual review";
        var revisionId = await f.SalaryChanges(ctx).ProposeAsync(proposal);

        f.Db.User.UserId = Approver;
        await f.SalaryChanges(ctx).ApproveAsync(revisionId);

        await using var verify = f.Db.CreateContext();
        var history = await verify.EmployeeSalaries.Include(s => s.Components)
            .Where(s => s.EmployeeId == id).OrderBy(s => s.EffectiveFrom).ToListAsync();
        history.Count.ShouldBe(2);
        history[0].BasicSalary.ShouldBe(100_000m);
        history[0].Status.ShouldBe(SalaryRevisionStatus.Superseded);
        history[0].EffectiveTo.ShouldBe(new DateOnly(2026, 10, 31));
        history[1].Status.ShouldBe(SalaryRevisionStatus.Approved);
        history[1].Components.Sum(c => c.Amount).ShouldBe(startingAllowances); // allowances carried from the current salary
        (await verify.AuditLogs.AnyAsync(a => a.Action == "Salary.ChangeApproved" && a.EntityId == id.ToString())).ShouldBeTrue();
    }

    [Fact]
    public async Task Requester_cannot_approve_their_own_change()
    {
        var id = await EmployeeOnStructureAsync("SelfApprove");
        await using var ctx = f.Db.CreateContext();
        var proposal = await f.SalaryChanges(ctx).BuildProposalAsync(id);
        proposal.BasicSalary = 105_000m;
        proposal.Reason = "Adjustment";
        var revisionId = await f.SalaryChanges(ctx).ProposeAsync(proposal);

        await Should.ThrowAsync<DomainException>(() => f.SalaryChanges(ctx).ApproveAsync(revisionId));
    }

    [Fact]
    public async Task Only_one_pending_change_and_dates_must_move_forward()
    {
        var id = await EmployeeOnStructureAsync("Guards");
        await using var ctx = f.Db.CreateContext();
        var proposal = await f.SalaryChanges(ctx).BuildProposalAsync(id);
        proposal.Reason = "Backdated";
        proposal.EffectiveFrom = new DateOnly(2025, 12, 1);
        await Should.ThrowAsync<DomainException>(() => f.SalaryChanges(ctx).ProposeAsync(proposal));

        proposal.EffectiveFrom = new DateOnly(2026, 11, 1);
        await f.SalaryChanges(ctx).ProposeAsync(proposal);
        await Should.ThrowAsync<ConflictException>(() => f.SalaryChanges(ctx).ProposeAsync(proposal));
    }

    [Fact]
    public async Task Change_cannot_take_effect_inside_a_finalized_payroll_period()
    {
        var id = await EmployeeOnStructureAsync("Finalized");
        await using var ctx = f.Db.CreateContext();

        var period = await new Application.Payroll.PayrollPeriodService(ctx, f.Db.Clock).EnsureAsync(2026, 10);
        var run = new PayrollRun { Reference = $"PR-TEST-{id}", PayrollPeriodId = period.Id };
        run.Employees.Add(new PayrollEmployee { EmployeeId = id, PayrollPeriodId = period.Id, Status = PayrollLineStatus.Calculated, EmployeeNumber = "x", EmployeeName = "x", DepartmentName = "x" });
        run.BeginCalculation();
        run.RecalculateTotals();
        run.CompleteCalculation(Hr, DateTime.UtcNow);
        run.Submit(Hr, DateTime.UtcNow);
        run.Approve(Approver, DateTime.UtcNow, true);
        run.Finalize(Hr, DateTime.UtcNow);
        ctx.PayrollRuns.Add(run);
        await ctx.SaveChangesAsync();

        var proposal = await f.SalaryChanges(ctx).BuildProposalAsync(id);
        proposal.Reason = "Retro";
        proposal.EffectiveFrom = new DateOnly(2026, 10, 15);
        var ex = await Should.ThrowAsync<DomainException>(() => f.SalaryChanges(ctx).ProposeAsync(proposal));
        ex.Message.ShouldContain("finalized");
    }

    [Fact]
    public async Task Editing_a_structure_does_not_change_existing_salaries_but_revision_proposes_for_all()
    {
        var a = await EmployeeOnStructureAsync("StructA");
        var b = await EmployeeOnStructureAsync("StructB");
        await using var ctx = f.Db.CreateContext();
        var structures = f.Structures(ctx);

        var edit = await structures.GetForEditAsync(f.EngineerStructure);
        edit.Lines.Single(l => l.AmountType == AmountType.Fixed).Amount = 15_000m;
        await structures.UpdateAsync(f.EngineerStructure, edit);

        await using (var verify = f.Db.CreateContext())
        {
            var current = await verify.EmployeeSalaries.Include(s => s.Components)
                .SingleAsync(s => s.EmployeeId == a && s.Status == SalaryRevisionStatus.Approved);
            current.Components.Sum(c => c.Amount).ShouldBe(30_000m); // unchanged: 20,000 housing + 10,000 transport
        }

        var result = await structures.ProposeRevisionAsync(f.EngineerStructure,
            new StructureRevisionInput { IncreasePercent = 10m, EffectiveFrom = new DateOnly(2027, 1, 1), Reason = "COLA 2027" });
        result.Proposed.ShouldBeGreaterThanOrEqualTo(2);

        await using var check = f.Db.CreateContext();
        var proposed = await check.EmployeeSalaries.Include(s => s.Components)
            .SingleAsync(s => s.EmployeeId == b && s.Status == SalaryRevisionStatus.PendingApproval);
        proposed.BasicSalary.ShouldBe(110_000m);
        proposed.Components.Select(c => c.Amount).OrderBy(x => x).ShouldBe([15_000m, 22_000m]); // new fixed amount, 20% of new basic
    }

    [Fact]
    public async Task Basic_salary_must_sit_inside_the_grade_band()
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var grade = await ctx.SalaryGrades.SingleAsync(g => g.Code == "G4");
        var input = new SalaryStructureInput
        {
            Code = "SS-BAND", Name = "Out of band", BasicSalary = 500_000m, SalaryGradeId = grade.Id, EffectiveFrom = new DateOnly(2026, 1, 1)
        };
        var ex = await Should.ThrowAsync<DomainException>(() => f.Structures(ctx).CreateAsync(input));
        ex.Message.ShouldContain("band");
    }

    [Fact]
    public async Task Payroll_input_computes_quantity_times_rate_and_freezes_after_submission()
    {
        var id = await EmployeeOnStructureAsync("Overtime");
        await using var ctx = f.Db.CreateContext();
        var payItems = f.PayItems(ctx);
        var overtimeId = await ctx.SalaryComponents.Where(c => c.Code == "OT_TEST").Select(c => c.Id).FirstOrDefaultAsync();
        if (overtimeId == 0)
            overtimeId = await payItems.SaveComponentAsync(null, new SalaryComponentInput
            {
                Code = "OT_TEST", Name = "Overtime", Type = ComponentType.Earning, Category = ComponentCategory.Overtime,
                TaxTreatment = TaxTreatment.Taxable, IsRecurring = false
            });

        var inputId = await payItems.SaveInputAsync(null, new PayrollInputInput
        {
            EmployeeId = id, Year = 2026, Month = 12, SalaryComponentId = overtimeId, Quantity = 12.5m, Rate = 850m
        });
        var summary = await payItems.ListInputsAsync(2026, 12, "Overtime");
        summary.Items.Single(i => i.Id == inputId).Amount.ShouldBe(10_625m);

        // Recurring allowances belong on the salary, not as period items.
        var house = await ctx.SalaryComponents.SingleAsync(c => c.Code == "HOUSE");
        await Should.ThrowAsync<DomainException>(() => payItems.SaveInputAsync(null, new PayrollInputInput
        {
            EmployeeId = id, Year = 2026, Month = 12, SalaryComponentId = house.Id, Amount = 1_000m
        }));

        var period = await ctx.PayrollPeriods.SingleAsync(p => p.Year == 2026 && p.Month == 12);
        var run = new PayrollRun { Reference = $"PR-INPUT-{id}", PayrollPeriodId = period.Id };
        run.Employees.Add(new PayrollEmployee { EmployeeId = id, PayrollPeriodId = period.Id, Status = PayrollLineStatus.Calculated, EmployeeNumber = "x", EmployeeName = "x", DepartmentName = "x" });
        run.BeginCalculation();
        run.RecalculateTotals();
        run.CompleteCalculation(Hr, DateTime.UtcNow);
        run.Submit(Hr, DateTime.UtcNow);
        ctx.PayrollRuns.Add(run);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<DomainException>(() => payItems.DeleteInputAsync(inputId));
        (await payItems.ListInputsAsync(2026, 12, "Overtime")).Items.Single(i => i.Id == inputId).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task Component_tax_treatment_is_locked_once_used()
    {
        var id = await EmployeeOnStructureAsync("Locked");
        await using var ctx = f.Db.CreateContext();
        var house = await ctx.SalaryComponents.SingleAsync(c => c.Code == "HOUSE");
        var input = await f.PayItems(ctx).GetComponentAsync(house.Id);
        input.TaxTreatment = TaxTreatment.NonTaxable;
        await Should.ThrowAsync<DomainException>(() => f.PayItems(ctx).SaveComponentAsync(house.Id, input));
        id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Recurring_loan_can_be_created_with_carried_balance_and_stopped()
    {
        var id = await EmployeeOnStructureAsync("Loan");
        await using var ctx = f.Db.CreateContext();
        var payItems = f.PayItems(ctx);
        var loanComponent = await ctx.SalaryComponents.Where(c => c.Code == "LOAN_TEST").Select(c => c.Id).FirstOrDefaultAsync();
        if (loanComponent == 0)
            loanComponent = await payItems.SaveComponentAsync(null, new SalaryComponentInput
            {
                Code = "LOAN_TEST", Name = "Staff loan", Type = ComponentType.Deduction, Category = ComponentCategory.Loan,
                TaxTreatment = TaxTreatment.PostTax
            });

        var deductionId = await payItems.SaveDeductionAsync(null, new RecurringDeductionInput
        {
            EmployeeId = id, SalaryComponentId = loanComponent, MonthlyAmount = 5_000m, TotalAmount = 50_000m,
            AmountRecovered = 20_000m, StartDate = new DateOnly(2026, 10, 1), Reference = "LN-1"
        });

        var list = await payItems.ListDeductionsAsync(new RecurringDeductionQuery { Search = "LN-1" });
        list.Items.Single().RemainingBalance.ShouldBe(30_000m);

        await payItems.StopDeductionAsync(deductionId);
        await Should.ThrowAsync<DomainException>(() => payItems.StopDeductionAsync(deductionId));
        (await payItems.ListDeductionsAsync(new RecurringDeductionQuery { Search = "LN-1", Active = false })).Items.ShouldHaveSingleItem();
    }
}
