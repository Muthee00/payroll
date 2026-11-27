using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll;
using PayMaster.Application.Statutory;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Services;
using PayMaster.Tests.IntegrationTests.Employees;
using PayMaster.Tests.TestSupport;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests.Payroll;

public sealed class FakeBackgroundJobs : IBackgroundJobs
{
    public List<int> Calculations { get; } = [];
    public void EnqueuePayrollCalculation(int runId, int userId) => Calculations.Add(runId);
    public void EnqueuePayslipGeneration(int runId, int userId, bool email) { }
}

[Trait("Category", "MySql")]
[Collection(MySqlCollection.Name)]
public class PayrollTests : IClassFixture<EmployeeFixture>
{
    private const int Officer = 10;
    private const int Admin = 20;
    private readonly EmployeeFixture f;
    private readonly FakeBackgroundJobs _jobs = new();
    private static int _month; // each test uses its own payroll month to stay independent

    public PayrollTests(EmployeeFixture fixture)
    {
        f = fixture;
        using var ctx = f.Db.CreateContext();
        if (!ctx.StatutoryConfigurations.Any())
        {
            var config = VerifiedKenyaRules.Configuration(9_000m, 108_000m);
            config.Id = 0;
            config.EffectiveFrom = new DateOnly(2026, 2, 1);
            ctx.StatutoryConfigurations.Add(config);
            ctx.SaveChanges();
        }
    }

    private static (int Year, int Month) NextPeriod()
    {
        var n = Interlocked.Increment(ref _month);
        var date = new DateOnly(2027, 1, 1).AddMonths(n);
        return (date.Year, date.Month);
    }

    private PayrollService Service(ApplicationDbContext ctx)
    {
        var notifications = new NotificationService(ctx, f.Users, f.Db.Clock);
        var audit = new AuditService(ctx, f.Db.User, f.Db.Clock);
        var statutory = new StatutoryConfigService(ctx, f.Db.User, notifications, audit, f.Db.Clock);
        var calculator = new PayrollCalculationService(ctx, statutory, notifications, audit, f.Db.Clock, NullLogger<PayrollCalculationService>.Instance);
        return new PayrollService(ctx, new PayrollPeriodService(ctx, f.Db.Clock), calculator, _jobs, f.Db.User, notifications, f.Users, audit, f.Db.Clock);
    }

    private void As(int userId) => f.Db.User.UserId = userId;

    /// <summary>Creates an employee with an approved basic salary (no allowances) from 1 Jan 2026.</summary>
    private async Task<int> EmployeeAsync(decimal basic, string last, DateOnly? hired = null, Action<Employee>? configure = null)
    {
        f.AsHr();
        await using var ctx = f.Db.CreateContext();
        var input = f.NewInput(departmentId: f.Finance, last: last);
        input.EmploymentDate = hired ?? new DateOnly(2026, 1, 1);
        var id = await f.Employees(ctx).CreateAsync(input);
        var employee = await ctx.Employees.SingleAsync(e => e.Id == id);
        employee.NssfNumber = "2000000001";
        employee.ShaNumber = "CR1";
        configure?.Invoke(employee);
        ctx.EmployeeSalaries.Add(new EmployeeSalary
        {
            EmployeeId = id, BasicSalary = basic, EffectiveFrom = input.EmploymentDate!.Value,
            Status = SalaryRevisionStatus.Approved, ChangeReason = "Test", ApprovedById = Admin
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    private async Task<int> CreateRunAsync((int Year, int Month) period, params int[] employeeIds)
    {
        As(Officer);
        await using var ctx = f.Db.CreateContext();
        var result = await Service(ctx).CreateRunAsync(new CreatePayrollRunInput
        {
            Year = period.Year, Month = period.Month, Scope = PayrollScope.SelectedEmployees, EmployeeIds = [.. employeeIds]
        });
        return result.RunId;
    }

    [Fact]
    public async Task Calculated_lines_match_the_verified_statutory_figures()
    {
        var a = await EmployeeAsync(100_000m, "Calc100k");
        var b = await EmployeeAsync(30_000m, "Calc30k");
        var period = NextPeriod();
        var runId = await CreateRunAsync(period, a, b);

        await using var ctx = f.Db.CreateContext();
        (await Service(ctx).CalculateAsync(runId)).ShouldBeFalse(); // small run: calculated inline

        await using var verify = f.Db.CreateContext();
        var run = await verify.PayrollRuns.SingleAsync(r => r.Id == runId);
        var lines = await verify.PayrollEmployees.Include(l => l.Deductions).Include(l => l.EmployerContributions)
            .Where(l => l.PayrollRunId == runId).ToListAsync();
        var big = lines.Single(l => l.EmployeeId == a);
        var small = lines.Single(l => l.EmployeeId == b);

        run.Status.ShouldBe(PayrollRunStatus.Draft);
        run.CalculatedAt.ShouldNotBeNull();
        run.StatutorySnapshotJson.ShouldNotBeNull().ShouldContain("\"NssfUpperEarningsLimit\":108000");
        big.Paye.ShouldBe(19_308.35m);
        big.Nssf.ShouldBe(6_000m);
        big.Sha.ShouldBe(2_750m);
        big.HousingLevy.ShouldBe(1_500m);
        big.NetPay.ShouldBe(70_441.65m);
        big.EmployerContributions.Sum(c => c.Amount).ShouldBe(7_500m);
        big.Deductions.Where(d => d.Kind == DeductionKind.Statutory).Sum(d => d.Amount).ShouldBe(big.TotalDeductions);
        big.CalculationBreakdownJson.ShouldNotBeNull().ShouldContain("Tier II");
        small.Paye.ShouldBe(731.25m);
        small.NetPay.ShouldBe(26_193.75m);
        run.TotalNetPay.ShouldBe(70_441.65m + 26_193.75m);
        run.TotalEmployerContributions.ShouldBe(7_500m + 1_800m + 450m);
    }

    [Fact]
    public async Task An_employee_cannot_be_in_two_live_runs_for_the_same_period()
    {
        var a = await EmployeeAsync(50_000m, "DupA");
        var b = await EmployeeAsync(50_000m, "DupB");
        var period = NextPeriod();
        await CreateRunAsync(period, a);

        await using var ctx = f.Db.CreateContext();
        var second = await Service(ctx).CreateRunAsync(new CreatePayrollRunInput
        {
            Year = period.Year, Month = period.Month, Scope = PayrollScope.SelectedEmployees, EmployeeIds = [a, b]
        });
        second.EmployeeCount.ShouldBe(1);
        second.Skipped.ShouldHaveSingleItem().ShouldContain("already in PR-");

        await Should.ThrowAsync<DomainException>(() => Service(ctx).CreateRunAsync(new CreatePayrollRunInput
        {
            Year = period.Year, Month = period.Month, Scope = PayrollScope.SelectedEmployees, EmployeeIds = [a]
        }));

        // The database itself rejects a second locked line for the same employee and period.
        var existing = await ctx.PayrollEmployees.FirstAsync(l => l.EmployeeId == a && l.PeriodLock == true);
        ctx.PayrollEmployees.Add(new Domain.Entities.Payroll.PayrollEmployee
        {
            PayrollRunId = second.RunId, PayrollPeriodId = existing.PayrollPeriodId, EmployeeId = a,
            EmployeeNumber = "x", EmployeeName = "x", DepartmentName = "x"
        });
        await Should.ThrowAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Joiner_is_prorated_and_missing_salary_blocks_submission()
    {
        var period = NextPeriod();
        var joiner = await EmployeeAsync(62_000m, "Joiner", hired: new DateOnly(period.Year, period.Month, 1).AddDays(15));
        f.AsHr();
        int noSalary;
        await using (var setup = f.Db.CreateContext())
            noSalary = await f.Employees(setup).CreateAsync(f.NewInput(departmentId: f.Finance, last: "NoSalary"));

        var runId = await CreateRunAsync(period, joiner, noSalary);
        await using var ctx = f.Db.CreateContext();
        await Service(ctx).CalculateAsync(runId);

        await using var verify = f.Db.CreateContext();
        var lines = await verify.PayrollEmployees.Where(l => l.PayrollRunId == runId).ToListAsync();
        var joinerLine = lines.Single(l => l.EmployeeId == joiner);
        var days = DateTime.DaysInMonth(period.Year, period.Month);
        joinerLine.DaysInPeriod.ShouldBe(days);
        joinerLine.DaysPayable.ShouldBe(days - 15);
        joinerLine.BasicSalary.ShouldBe(Math.Round(62_000m * (days - 15) / days, 2, MidpointRounding.AwayFromZero));

        var errorLine = lines.Single(l => l.EmployeeId == noSalary);
        errorLine.Status.ShouldBe(PayrollLineStatus.Error);
        errorLine.Errors.ShouldNotBeNull().ShouldContain("No approved salary");

        As(Officer);
        await using var submitCtx = f.Db.CreateContext();
        await Should.ThrowAsync<DomainException>(() => Service(submitCtx).SubmitAsync(runId));

        // Removing the problem employee from the draft allows submission after recalculation.
        await Service(submitCtx).RemoveEmployeeAsync(runId, errorLine.Id);
        await Service(submitCtx).CalculateAsync(runId);
        await Service(submitCtx).SubmitAsync(runId);
        (await f.Db.CreateContext().PayrollRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe(PayrollRunStatus.PendingApproval);
    }

    [Fact]
    public async Task Preparer_cannot_approve_and_finalizing_issues_payslips_records_recoveries_and_locks_rules()
    {
        var loanComponent = new SalaryComponent { Code = $"LN{Guid.NewGuid():N}"[..20], Name = "Staff loan", Type = ComponentType.Deduction, Category = ComponentCategory.Loan, TaxTreatment = TaxTreatment.PostTax };
        var period = NextPeriod();
        var id = await EmployeeAsync(100_000m, "Finalize");
        int loanId;
        await using (var setup = f.Db.CreateContext())
        {
            var loan = new EmployeeDeduction
            {
                EmployeeId = id, SalaryComponent = loanComponent, MonthlyAmount = 10_000m, TotalAmount = 15_000m,
                AmountRecovered = 10_000m, StartDate = new DateOnly(2026, 1, 1)
            };
            setup.EmployeeDeductions.Add(loan);
            await setup.SaveChangesAsync();
            loanId = loan.Id;
        }

        var runId = await CreateRunAsync(period, id);
        await using var ctx = f.Db.CreateContext();
        var service = Service(ctx);
        await service.CalculateAsync(runId);
        var line = await ctx.PayrollEmployees.AsNoTracking().SingleAsync(l => l.PayrollRunId == runId);
        line.OtherDeductions.ShouldBe(5_000m);                 // only the remaining balance is deducted
        line.NetPay.ShouldBe(70_441.65m - 5_000m);

        await service.SubmitAsync(runId);
        await Should.ThrowAsync<DomainException>(() => service.ApproveAsync(runId)); // same user

        As(Admin);
        await using var approveCtx = f.Db.CreateContext();
        await Service(approveCtx).ApproveAsync(runId);
        As(Officer);
        await using var finalizeCtx = f.Db.CreateContext();
        await Service(finalizeCtx).FinalizeAsync(runId);

        await using var verify = f.Db.CreateContext();
        var run = await verify.PayrollRuns.Include(r => r.StatutoryConfiguration).SingleAsync(r => r.Id == runId);
        run.Status.ShouldBe(PayrollRunStatus.Finalized);
        run.StatutoryConfiguration!.IsLocked.ShouldBeTrue();
        (await verify.Payslips.CountAsync(p => p.PayrollEmployee.PayrollRunId == runId)).ShouldBe(1);
        var loanAfter = await verify.EmployeeDeductions.SingleAsync(d => d.Id == loanId);
        loanAfter.AmountRecovered.ShouldBe(15_000m);
        loanAfter.IsActive.ShouldBeFalse();
        (await verify.AuditLogs.AnyAsync(a => a.Action == "Payroll.Finalized" && a.EntityId == runId.ToString())).ShouldBeTrue();

        // Finalized payroll is immutable.
        await Should.ThrowAsync<DomainException>(() => Service(verify).CalculateAsync(runId));
        await Should.ThrowAsync<DomainException>(() => Service(verify).CancelAsync(runId, "x"));

        // Controlled reversal undoes recoveries and opens a correction run for the same employees.
        As(Admin);
        await using var reverseCtx = f.Db.CreateContext();
        var correctionId = await Service(reverseCtx).ReverseAsync(runId, "Bonus omitted", createCorrection: true);
        await using var after = f.Db.CreateContext();
        (await after.PayrollRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe(PayrollRunStatus.Reversed);
        (await after.EmployeeDeductions.SingleAsync(d => d.Id == loanId)).AmountRecovered.ShouldBe(10_000m);
        var correction = await after.PayrollRuns.Include(r => r.Employees).SingleAsync(r => r.Id == correctionId);
        correction.RunType.ShouldBe(PayrollRunType.Correction);
        correction.OriginalRunId.ShouldBe(runId);
        correction.Employees.ShouldHaveSingleItem().PeriodLock.ShouldBe(true);
        (await after.PayrollEmployees.Where(l => l.PayrollRunId == runId).Select(l => l.PeriodLock).ToListAsync()).ShouldAllBe(x => x == null);
    }

    [Fact]
    public async Task Period_inputs_and_recalculation_before_approval()
    {
        var period = NextPeriod();
        var id = await EmployeeAsync(100_000m, "Inputs");
        var runId = await CreateRunAsync(period, id);
        await using var ctx = f.Db.CreateContext();
        await Service(ctx).CalculateAsync(runId);
        await Service(ctx).SubmitAsync(runId);

        // Add a bonus while pending approval → payroll must be recalculated, which returns it to Draft.
        var bonus = new SalaryComponent { Code = $"BN{Guid.NewGuid():N}"[..20], Name = "Bonus", Type = ComponentType.Earning, Category = ComponentCategory.Bonus, TaxTreatment = TaxTreatment.Taxable, IsRecurring = false };
        var periodEntity = await ctx.PayrollPeriods.SingleAsync(p => p.Year == period.Year && p.Month == period.Month);
        ctx.PayrollInputs.Add(new Domain.Entities.Payroll.PayrollInput { EmployeeId = id, PayrollPeriodId = periodEntity.Id, SalaryComponent = bonus, Amount = 8_000m });
        await ctx.SaveChangesAsync();

        await Service(ctx).CalculateAsync(runId);
        await using var verify = f.Db.CreateContext();
        var run = await verify.PayrollRuns.SingleAsync(r => r.Id == runId);
        run.Status.ShouldBe(PayrollRunStatus.Draft);
        run.SubmittedById.ShouldBeNull();
        var line = await verify.PayrollEmployees.Include(l => l.Earnings).SingleAsync(l => l.PayrollRunId == runId);
        line.GrossEarnings.ShouldBe(108_000m);
        line.Earnings.ShouldContain(e => e.Name == "Bonus" && e.Amount == 8_000m);
        line.Nssf.ShouldBe(6_480m);
    }

    [Fact]
    public async Task Large_runs_are_calculated_in_the_background()
    {
        var period = NextPeriod();
        var ids = new List<int>();
        await using (var ctx = f.Db.CreateContext())
        {
            // Bulk-insert lightweight employees to exceed the background threshold.
            var dept = await ctx.Departments.SingleAsync(d => d.Id == f.Finance);
            for (var i = 0; i < PayrollService.BackgroundThreshold + 1; i++)
            {
                var input = f.NewInput(departmentId: f.Finance, last: $"Bulk{i}");
                ctx.Employees.Add(new Employee
                {
                    EmployeeNumber = $"BLK-{period.Month}-{i:0000}", FirstName = "Bulk", LastName = $"{i}", Gender = Gender.Male,
                    DateOfBirth = new DateOnly(1990, 1, 1), NationalId = input.NationalId, KraPin = input.KraPin,
                    Department = dept, EmploymentType = EmploymentType.Permanent, EmploymentDate = new DateOnly(2026, 1, 1),
                    BankName = "KCB", BankAccountNumber = "123456789"
                });
            }
            await ctx.SaveChangesAsync();
            ids = await ctx.Employees.Where(e => e.EmployeeNumber.StartsWith($"BLK-{period.Month}-")).Select(e => e.Id).ToListAsync();
        }

        var runId = await CreateRunAsync(period, [.. ids]);
        await using var runCtx = f.Db.CreateContext();
        (await Service(runCtx).CalculateAsync(runId)).ShouldBeTrue();
        _jobs.Calculations.ShouldContain(runId);
        (await f.Db.CreateContext().PayrollRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe(PayrollRunStatus.Calculating);
    }
}
