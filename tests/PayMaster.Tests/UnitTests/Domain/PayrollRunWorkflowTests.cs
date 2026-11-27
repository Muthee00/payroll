using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Enums;
using Shouldly;

namespace PayMaster.Tests.UnitTests.Domain;

public class PayrollRunWorkflowTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);
    private const int Preparer = 10;
    private const int Approver = 20;

    private static PayrollRun CalculatedRun(int employees = 2, int errors = 0)
    {
        var run = new PayrollRun { Reference = "PR-2026-10-01" };
        for (var i = 0; i < employees; i++)
            run.Employees.Add(new PayrollEmployee
            {
                EmployeeId = i + 1,
                Status = i < errors ? PayrollLineStatus.Error : PayrollLineStatus.Calculated,
                GrossEarnings = 100_000m, NetPay = 75_000m, TotalDeductions = 25_000m
            });
        run.BeginCalculation();
        run.RecalculateTotals();
        run.CompleteCalculation(Preparer, Now);
        return run;
    }

    [Fact]
    public void Happy_path_moves_through_every_stage()
    {
        var run = CalculatedRun();
        run.Status.ShouldBe(PayrollRunStatus.Draft);
        run.IsCalculated.ShouldBeTrue();

        run.Submit(Preparer, Now);
        run.Status.ShouldBe(PayrollRunStatus.PendingApproval);

        run.Approve(Approver, Now, requireSeparateApprover: true);
        run.Status.ShouldBe(PayrollRunStatus.Approved);

        run.Finalize(Preparer, Now);
        run.Status.ShouldBe(PayrollRunStatus.Finalized);
        run.IsLockedForHistory.ShouldBeTrue();

        run.MarkPaid(Preparer, Now);
        run.Status.ShouldBe(PayrollRunStatus.Paid);
    }

    [Fact]
    public void Cannot_submit_before_calculation()
    {
        var run = new PayrollRun { Reference = "PR-1" };
        Should.Throw<DomainException>(() => run.Submit(Preparer, Now)).Message.ShouldContain("Calculate");
    }

    [Fact]
    public void Cannot_submit_with_employee_errors()
    {
        var run = CalculatedRun(employees: 3, errors: 1);
        run.ErrorCount.ShouldBe(1);
        Should.Throw<DomainException>(() => run.Submit(Preparer, Now)).Message.ShouldContain("1 employee error");
    }

    [Fact]
    public void Cannot_submit_an_empty_payroll()
    {
        var run = CalculatedRun(employees: 0);
        Should.Throw<DomainException>(() => run.Submit(Preparer, Now));
    }

    [Fact]
    public void Approver_must_differ_from_preparer_when_required()
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);
        Should.Throw<DomainException>(() => run.Approve(Preparer, Now, requireSeparateApprover: true));
        run.Status.ShouldBe(PayrollRunStatus.PendingApproval);
    }

    [Fact]
    public void Same_user_may_approve_when_segregation_is_disabled()
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);
        run.Approve(Preparer, Now, requireSeparateApprover: false);
        run.Status.ShouldBe(PayrollRunStatus.Approved);
    }

    [Fact]
    public void Recalculation_is_allowed_while_pending_approval_and_requires_resubmission()
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);

        run.BeginCalculation();
        run.Status.ShouldBe(PayrollRunStatus.Calculating);
        run.SubmittedById.ShouldBeNull();

        run.CompleteCalculation(Preparer, Now);
        run.Status.ShouldBe(PayrollRunStatus.Draft);
    }

    [Theory]
    [InlineData(PayrollRunStatus.Approved)]
    [InlineData(PayrollRunStatus.Finalized)]
    [InlineData(PayrollRunStatus.Paid)]
    public void Recalculation_is_blocked_after_approval(PayrollRunStatus target)
    {
        var run = AdvanceTo(target);
        Should.Throw<DomainException>(run.BeginCalculation);
    }

    [Fact]
    public void Finalized_run_is_not_editable()
    {
        var run = AdvanceTo(PayrollRunStatus.Finalized);
        run.IsEditable.ShouldBeFalse();
        Should.Throw<DomainException>(run.EnsureEditable);
        Should.Throw<DomainException>(() => run.Cancel(Approver, Now, "mistake"));
    }

    [Fact]
    public void Rejection_returns_to_draft_with_reason()
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);
        run.Reject(Approver, Now, "Overtime for Engineering is missing");
        run.Status.ShouldBe(PayrollRunStatus.Draft);
        run.StatusReason.ShouldBe("Rejected: Overtime for Engineering is missing");
    }

    [Fact]
    public void Rejection_requires_a_reason()
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);
        Should.Throw<DomainException>(() => run.Reject(Approver, Now, "  "));
    }

    [Fact]
    public void Cancelling_releases_the_employee_period_locks()
    {
        var run = CalculatedRun();
        run.Employees.ShouldAllBe(e => e.PeriodLock == true);

        run.Cancel(Preparer, Now, "Wrong period selected");

        run.Status.ShouldBe(PayrollRunStatus.Cancelled);
        run.HoldsPeriodLock.ShouldBeFalse();
        run.Employees.ShouldAllBe(e => e.PeriodLock == null);
    }

    [Fact]
    public void Reversal_of_finalized_run_releases_locks_and_records_reason()
    {
        var run = AdvanceTo(PayrollRunStatus.Finalized);
        run.Reverse(Approver, Now, "Incorrect bonus amounts");
        run.Status.ShouldBe(PayrollRunStatus.Reversed);
        run.StatusReason.ShouldBe("Incorrect bonus amounts");
        run.Employees.ShouldAllBe(e => e.PeriodLock == null);
    }

    [Fact]
    public void Draft_run_cannot_be_reversed()
    {
        var run = CalculatedRun();
        Should.Throw<DomainException>(() => run.Reverse(Approver, Now, "x"));
    }

    [Fact]
    public void Each_transition_changes_the_concurrency_stamp()
    {
        var run = CalculatedRun();
        var before = run.ConcurrencyStamp;
        run.Submit(Preparer, Now);
        run.ConcurrencyStamp.ShouldNotBe(before);
    }

    [Fact]
    public void Totals_exclude_lines_in_error()
    {
        var run = CalculatedRun(employees: 3, errors: 1);
        run.EmployeeCount.ShouldBe(3);
        run.TotalGross.ShouldBe(200_000m);
        run.TotalNetPay.ShouldBe(150_000m);
    }

    private static PayrollRun AdvanceTo(PayrollRunStatus target)
    {
        var run = CalculatedRun();
        run.Submit(Preparer, Now);
        run.Approve(Approver, Now, true);
        if (target == PayrollRunStatus.Approved) return run;
        run.Finalize(Preparer, Now);
        if (target == PayrollRunStatus.Finalized) return run;
        run.MarkPaid(Preparer, Now);
        return run;
    }
}
