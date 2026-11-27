using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Payroll;

/// <summary>
/// A payroll batch for one period. All state changes go through the methods below so the
/// workflow (Draft → Calculating → PendingApproval → Approved → Finalized → Paid) cannot be
/// bypassed. Lines and totals are an immutable snapshot once finalized.
/// </summary>
public class PayrollRun : AuditableEntity, IAuditTracked
{
    public string Reference { get; set; } = string.Empty;
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public PayrollRunType RunType { get; set; } = PayrollRunType.Regular;
    public int? OriginalRunId { get; set; }
    public PayrollRun? OriginalRun { get; set; }

    public PayrollScope Scope { get; set; } = PayrollScope.AllActive;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public PayrollRunStatus Status { get; private set; } = PayrollRunStatus.Draft;

    // Statutory rules used - id plus a full JSON snapshot of the values applied.
    public int? StatutoryConfigurationId { get; set; }
    public StatutoryConfiguration? StatutoryConfiguration { get; set; }
    public string? StatutorySnapshotJson { get; set; }

    // Totals
    public int EmployeeCount { get; set; }
    public int ErrorCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalTaxableIncome { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalSha { get; set; }
    public decimal TotalHousingLevy { get; set; }
    public decimal TotalNssf { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal TotalEmployerContributions { get; set; }

    // Workflow trail
    public DateTime? CalculatedAt { get; set; }
    public int? CalculatedById { get; set; }
    public DateTime? SubmittedAt { get; private set; }
    public int? SubmittedById { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public int? ApprovedById { get; private set; }
    public DateTime? FinalizedAt { get; private set; }
    public int? FinalizedById { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public int? PaidById { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public int? CancelledById { get; private set; }
    public DateTime? ReversedAt { get; private set; }
    public int? ReversedById { get; private set; }
    public string? StatusReason { get; private set; }
    public string? Notes { get; set; }

    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ICollection<PayrollEmployee> Employees { get; set; } = [];

    public bool IsEditable => Status == PayrollRunStatus.Draft;
    public bool IsCalculated => CalculatedAt is not null;
    public bool IsLockedForHistory => Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid or PayrollRunStatus.Reversed;
    public bool HoldsPeriodLock => Status is not (PayrollRunStatus.Cancelled or PayrollRunStatus.Reversed);

    public void BeginCalculation()
    {
        EnsureStatus("calculate", PayrollRunStatus.Draft, PayrollRunStatus.PendingApproval);
        SubmittedAt = null;
        SubmittedById = null;
        Transition(PayrollRunStatus.Calculating);
    }

    public void CompleteCalculation(int userId, DateTime nowUtc)
    {
        EnsureStatus("complete calculation", PayrollRunStatus.Calculating);
        CalculatedById = userId;
        CalculatedAt = nowUtc;
        Transition(PayrollRunStatus.Draft);
    }

    public void FailCalculation(string reason)
    {
        EnsureStatus("fail calculation", PayrollRunStatus.Calculating);
        CalculatedAt = null;
        StatusReason = reason;
        Transition(PayrollRunStatus.Draft);
    }

    public void Submit(int userId, DateTime nowUtc)
    {
        EnsureStatus("submit", PayrollRunStatus.Draft);
        if (!IsCalculated)
            throw new DomainException("Calculate the payroll before submitting it for approval.");
        if (EmployeeCount == 0)
            throw new DomainException("The payroll has no employees.");
        if (ErrorCount > 0)
            throw new DomainException($"Resolve {ErrorCount} employee error(s) before submitting.");

        SubmittedById = userId;
        SubmittedAt = nowUtc;
        StatusReason = null;
        Transition(PayrollRunStatus.PendingApproval);
    }

    public void Reject(int userId, DateTime nowUtc, string reason)
    {
        EnsureStatus("reject", PayrollRunStatus.PendingApproval);
        RequireReason(reason);
        SubmittedAt = null;
        SubmittedById = null;
        StatusReason = $"Rejected: {reason.Trim()}";
        Transition(PayrollRunStatus.Draft);
    }

    public void Approve(int userId, DateTime nowUtc, bool requireSeparateApprover)
    {
        EnsureStatus("approve", PayrollRunStatus.PendingApproval);
        if (requireSeparateApprover && (userId == SubmittedById || userId == CalculatedById))
            throw new DomainException("Payroll must be approved by a different user from the one who prepared it.");

        ApprovedById = userId;
        ApprovedAt = nowUtc;
        Transition(PayrollRunStatus.Approved);
    }

    public void Finalize(int userId, DateTime nowUtc)
    {
        EnsureStatus("finalize", PayrollRunStatus.Approved);
        FinalizedById = userId;
        FinalizedAt = nowUtc;
        Transition(PayrollRunStatus.Finalized);
    }

    public void MarkPaid(int userId, DateTime nowUtc)
    {
        EnsureStatus("mark as paid", PayrollRunStatus.Finalized);
        PaidById = userId;
        PaidAt = nowUtc;
        Transition(PayrollRunStatus.Paid);
    }

    public void Cancel(int userId, DateTime nowUtc, string reason)
    {
        EnsureStatus("cancel", PayrollRunStatus.Draft, PayrollRunStatus.PendingApproval, PayrollRunStatus.Approved);
        RequireReason(reason);
        CancelledById = userId;
        CancelledAt = nowUtc;
        StatusReason = reason.Trim();
        ReleasePeriodLocks();
        Transition(PayrollRunStatus.Cancelled);
    }

    /// <summary>Controlled reversal of a finalized run; a correction run may then replace it.</summary>
    public void Reverse(int userId, DateTime nowUtc, string reason)
    {
        EnsureStatus("reverse", PayrollRunStatus.Finalized, PayrollRunStatus.Paid);
        RequireReason(reason);
        ReversedById = userId;
        ReversedAt = nowUtc;
        StatusReason = reason.Trim();
        ReleasePeriodLocks();
        Transition(PayrollRunStatus.Reversed);
    }

    public void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException($"Payroll {Reference} is {Status} and can no longer be edited.");
    }

    /// <summary>Recomputes header totals from the calculated lines.</summary>
    public void RecalculateTotals()
    {
        var lines = Employees.ToList();
        EmployeeCount = lines.Count;
        ErrorCount = lines.Count(l => l.Status == PayrollLineStatus.Error);
        var ok = lines.Where(l => l.Status == PayrollLineStatus.Calculated).ToList();
        TotalGross = ok.Sum(l => l.GrossEarnings);
        TotalTaxableIncome = ok.Sum(l => l.TaxableIncome);
        TotalPaye = ok.Sum(l => l.Paye);
        TotalSha = ok.Sum(l => l.Sha);
        TotalHousingLevy = ok.Sum(l => l.HousingLevy);
        TotalNssf = ok.Sum(l => l.Nssf);
        TotalOtherDeductions = ok.Sum(l => l.OtherDeductions);
        TotalDeductions = ok.Sum(l => l.TotalDeductions);
        TotalNetPay = ok.Sum(l => l.NetPay);
        TotalEmployerContributions = ok.Sum(l => l.TotalEmployerContributions);
    }

    private void ReleasePeriodLocks()
    {
        foreach (var line in Employees)
            line.PeriodLock = null;
    }

    private void EnsureStatus(string action, params PayrollRunStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException(
                $"Cannot {action} payroll {Reference} while it is {Status}. Allowed from: {string.Join(", ", allowed)}.");
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required.");
    }

    private void Transition(PayrollRunStatus next)
    {
        Status = next;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
