using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Employees;

public class Employee : SoftDeletableEntity, IAuditTracked
{
    // Personal information
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? PersonalEmail { get; set; }
    public string? WorkEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhysicalAddress { get; set; }

    // Emergency contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // Employment information
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? PositionId { get; set; }
    public Position? Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;
    public DateOnly EmploymentDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public string? WorkLocation { get; set; }

    // Payment information
    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? MobileMoneyNumber { get; set; }

    // Tax status
    /// <summary>Holds a valid KRA disability exemption certificate.</summary>
    public bool HasDisabilityExemption { get; set; }
    public string? DisabilityExemptionCertificateNumber { get; set; }

    /// <summary>Identity user linked for self-service (nullable: not every employee logs in).</summary>
    public int? UserId { get; set; }

    public ICollection<Employee> DirectReports { get; set; } = [];
    public ICollection<EmployeeSalary> SalaryHistory { get; set; } = [];
    public ICollection<EmploymentHistory> EmploymentHistory { get; set; } = [];
    public ICollection<EmployeeDocument> Documents { get; set; } = [];
    public ICollection<EmployeeDeduction> RecurringDeductions { get; set; } = [];

    public string FullName =>
        string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Initials =>
        $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpperInvariant();

    public bool IsPayable => Status is EmploymentStatus.Active or EmploymentStatus.OnLeave;

    public void Terminate(DateOnly date, string reason)
    {
        if (Status == EmploymentStatus.Terminated)
            throw new DomainException("Employee is already terminated.");
        if (date < EmploymentDate)
            throw new DomainException("Termination date cannot be before the employment date.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A termination reason is required.");

        Status = EmploymentStatus.Terminated;
        TerminationDate = date;
        TerminationReason = reason.Trim();
    }

    public void Deactivate()
    {
        if (Status == EmploymentStatus.Terminated)
            throw new DomainException("A terminated employee cannot be deactivated.");
        Status = EmploymentStatus.Deactivated;
    }

    public void Reactivate()
    {
        if (Status == EmploymentStatus.Active)
            throw new DomainException("Employee is already active.");
        Status = EmploymentStatus.Active;
        TerminationDate = null;
        TerminationReason = null;
    }
}
