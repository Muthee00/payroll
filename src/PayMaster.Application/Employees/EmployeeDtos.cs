using PayMaster.Application.Common.Models;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Employees;

public record EmployeeQuery : PageQuery
{
    public int? DepartmentId { get; init; }
    public EmploymentStatus? Status { get; init; }
    public EmploymentType? EmploymentType { get; init; }
}

public record EmployeeListItemDto(
    int Id,
    string EmployeeNumber,
    string FullName,
    string? JobTitle,
    string DepartmentName,
    EmploymentType EmploymentType,
    EmploymentStatus Status,
    DateOnly EmploymentDate,
    string? WorkEmail,
    string? PhoneNumber,
    decimal? BasicSalary,
    decimal? GrossSalary);

public record SalaryRevisionDto(
    int Id,
    decimal BasicSalary,
    decimal GrossSalary,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    SalaryRevisionStatus Status,
    string ChangeReason,
    string? StructureName,
    IReadOnlyList<(string Name, decimal Amount)> Components,
    DateTime CreatedAt,
    DateTime? ApprovedAt);

public record EmploymentHistoryDto(
    EmploymentChangeType ChangeType,
    DateOnly EffectiveDate,
    string? FromDepartment,
    string? ToDepartment,
    string? FromPosition,
    string? ToPosition,
    EmploymentStatus? FromStatus,
    EmploymentStatus? ToStatus,
    string? Notes,
    DateTime CreatedAt);

public record EmployeeDocumentDto(
    int Id,
    EmployeeDocumentType DocumentType,
    string Title,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateOnly? ExpiryDate,
    DateTime CreatedAt);

public record RecurringDeductionDto(
    int Id,
    string ComponentName,
    decimal MonthlyAmount,
    decimal? TotalAmount,
    decimal AmountRecovered,
    decimal? RemainingBalance,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Reference,
    bool IsActive);

public record EmployeeDetailsDto
{
    public int Id { get; init; }
    public string EmployeeNumber { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = "";
    public string FullName { get; init; } = "";
    public Gender Gender { get; init; }
    public DateOnly DateOfBirth { get; init; }
    public string NationalId { get; init; } = "";
    public string? KraPin { get; init; }
    public string? NssfNumber { get; init; }
    public string? ShaNumber { get; init; }
    public string? PersonalEmail { get; init; }
    public string? WorkEmail { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PhysicalAddress { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactRelationship { get; init; }
    public string? EmergencyContactPhone { get; init; }

    public int DepartmentId { get; init; }
    public string DepartmentName { get; init; } = "";
    public int? PositionId { get; init; }
    public string? JobTitle { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public EmploymentStatus Status { get; init; }
    public DateOnly EmploymentDate { get; init; }
    public DateOnly? ContractEndDate { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public string? TerminationReason { get; init; }
    public int? ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string? WorkLocation { get; init; }

    public PayFrequency PayFrequency { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public string? BankName { get; init; }
    public string? BankBranch { get; init; }

    /// <summary>Full number only when the viewer may see bank details; otherwise masked.</summary>
    public string? BankAccountNumber { get; init; }
    public string? MobileMoneyNumber { get; init; }
    public bool BankDetailsMasked { get; init; }
    public bool HasDisabilityExemption { get; init; }
    public string? DisabilityExemptionCertificateNumber { get; init; }

    public bool HasLogin { get; init; }
    public DateTime CreatedAt { get; init; }

    public SalaryRevisionDto? CurrentSalary { get; init; }
    public SalaryRevisionDto? PendingSalary { get; init; }
    public bool CanViewSalary { get; init; }
    public IReadOnlyList<SalaryRevisionDto> SalaryHistory { get; init; } = [];
    public IReadOnlyList<EmploymentHistoryDto> EmploymentHistory { get; init; } = [];
    public IReadOnlyList<EmployeeDocumentDto> Documents { get; init; } = [];
    public IReadOnlyList<RecurringDeductionDto> RecurringDeductions { get; init; } = [];
    public IReadOnlyList<(int Id, string Name, string? JobTitle)> DirectReports { get; init; } = [];
}

/// <summary>Create/edit input. Initial salary fields are used on create only.</summary>
public record EmployeeInput
{
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string NationalId { get; set; } = "";
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? PersonalEmail { get; set; }
    public string? WorkEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public int DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public DateOnly? EmploymentDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public int? ManagerId { get; set; }
    public string? WorkLocation { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? MobileMoneyNumber { get; set; }
    public bool HasDisabilityExemption { get; set; }
    public string? DisabilityExemptionCertificateNumber { get; set; }

    // Initial salary (create only)
    public int? SalaryStructureId { get; set; }
    public decimal? BasicSalary { get; set; }
    public DateOnly? SalaryEffectiveDate { get; set; }

    /// <summary>Edit only: keeps the stored account number when the masked value is posted back unchanged.</summary>
    public bool KeepExistingBankAccount { get; set; }
}

public record LookupItem(int Id, string Name, string? Extra = null);

public record EmployeeLookups(
    IReadOnlyList<LookupItem> Departments,
    IReadOnlyList<LookupItem> Positions,
    IReadOnlyList<LookupItem> Managers,
    IReadOnlyList<LookupItem> SalaryStructures);
