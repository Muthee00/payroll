using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Domain.Entities.Salary;

/// <summary>
/// One immutable salary revision for an employee. A change creates a new revision
/// (pending approval); approval closes the previous revision by setting its
/// <see cref="EffectiveTo"/>. Rows are never overwritten.
/// </summary>
public class EmployeeSalary : AuditableEntity, IAuditTracked
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? SalaryStructureId { get; set; }
    public SalaryStructure? SalaryStructure { get; set; }

    public decimal BasicSalary { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public SalaryRevisionStatus Status { get; set; } = SalaryRevisionStatus.PendingApproval;
    public string ChangeReason { get; set; } = string.Empty;

    public int? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public ICollection<EmployeeSalaryComponent> Components { get; set; } = [];

    public decimal TotalRecurringEarnings => Money.Round(Components.Sum(c => c.Amount));

    /// <summary>Basic plus recurring allowances (excludes variable inputs like overtime).</summary>
    public decimal GrossSalary => Money.Round(BasicSalary + TotalRecurringEarnings);

    public bool IsEffectiveOn(DateOnly date) =>
        Status == SalaryRevisionStatus.Approved
        && EffectiveFrom <= date
        && (EffectiveTo is null || EffectiveTo >= date);

    public void Approve(int approverId, DateTime approvedAtUtc)
    {
        if (Status != SalaryRevisionStatus.PendingApproval)
            throw new DomainException($"Only pending salary revisions can be approved (current: {Status}).");
        if (CreatedById == approverId)
            throw new DomainException("A salary change must be approved by someone other than the requester.");

        Status = SalaryRevisionStatus.Approved;
        ApprovedById = approverId;
        ApprovedAt = approvedAtUtc;
    }

    public void Reject(int approverId, DateTime rejectedAtUtc, string reason)
    {
        if (Status != SalaryRevisionStatus.PendingApproval)
            throw new DomainException("Only pending salary revisions can be rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A rejection reason is required.");

        Status = SalaryRevisionStatus.Rejected;
        ApprovedById = approverId;
        ApprovedAt = rejectedAtUtc;
        RejectionReason = reason.Trim();
    }

    /// <summary>Closes this revision because a newer one took effect.</summary>
    public void Supersede(DateOnly newRevisionEffectiveFrom)
    {
        if (Status != SalaryRevisionStatus.Approved)
            throw new DomainException("Only approved revisions can be superseded.");
        if (newRevisionEffectiveFrom <= EffectiveFrom)
            throw new DomainException("A new salary revision must take effect after the current one.");

        EffectiveTo = newRevisionEffectiveFrom.AddDays(-1);
        Status = SalaryRevisionStatus.Superseded;
    }
}

/// <summary>Recurring earning (resolved KES amount) on a salary revision.</summary>
public class EmployeeSalaryComponent : BaseEntity
{
    public int EmployeeSalaryId { get; set; }
    public EmployeeSalary EmployeeSalary { get; set; } = null!;
    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;
    public decimal Amount { get; set; }
}
