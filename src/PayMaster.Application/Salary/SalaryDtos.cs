using PayMaster.Application.Common.Models;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Salary;

public record SalaryStructureListItem(
    int Id, string Code, string Name, string? GradeName, string? DepartmentName, string? PositionTitle,
    SalaryStructureStatus Status, decimal BasicSalary, decimal GrossSalary, int ComponentCount, int EmployeeCount,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record StructureLineDto(int SalaryComponentId, string ComponentName, AmountType AmountType, decimal Amount, decimal ResolvedAmount);

public record StructureEmployeeDto(int EmployeeId, string EmployeeNumber, string Name, string DepartmentName, decimal BasicSalary, decimal GrossSalary, DateOnly EffectiveFrom);

public record SalaryStructureDetails(
    int Id, string Code, string Name, string? Description, SalaryStructureStatus Status,
    int? SalaryGradeId, string? GradeName, decimal? GradeMin, decimal? GradeMax,
    int? DepartmentId, string? DepartmentName, int? PositionId, string? PositionTitle,
    decimal BasicSalary, decimal GrossSalary, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyList<StructureLineDto> Lines, IReadOnlyList<StructureEmployeeDto> Employees, DateTime CreatedAt, DateTime? UpdatedAt);

public class StructureLineInput
{
    public int SalaryComponentId { get; set; }
    public AmountType AmountType { get; set; } = AmountType.Fixed;
    public decimal Amount { get; set; }
}

public class SalaryStructureInput
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? SalaryGradeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public decimal BasicSalary { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public List<StructureLineInput> Lines { get; set; } = [];
}

public class SalaryGradeInput
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal MinimumSalary { get; set; }
    public decimal MaximumSalary { get; set; }
}

public record SalaryGradeDto(int Id, string Code, string Name, string? Description, decimal MinimumSalary, decimal MaximumSalary, int StructureCount);

public class StructureRevisionInput
{
    public decimal IncreasePercent { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public string Reason { get; set; } = "";
}

public record StructureRevisionResult(int Proposed, IReadOnlyList<string> SkippedEmployees);

// ------------------------------------------------------------- Employee salary changes

public class ComponentAmountInput
{
    public int SalaryComponentId { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
}

public class SalaryChangeInput
{
    public int EmployeeId { get; set; }
    public int? SalaryStructureId { get; set; }
    public decimal BasicSalary { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public string Reason { get; set; } = "";
    public List<ComponentAmountInput> Components { get; set; } = [];
}

public record PendingSalaryChangeDto(
    int Id, int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName,
    decimal? CurrentBasic, decimal? CurrentGross, decimal NewBasic, decimal NewGross, DateOnly EffectiveFrom,
    string Reason, string? StructureName, int? RequestedById, string? RequestedBy, DateTime RequestedAt)
{
    public decimal? ChangePercent => CurrentGross is > 0 ? Math.Round((NewGross - CurrentGross.Value) / CurrentGross.Value * 100m, 1) : null;
}

// ------------------------------------------------------------- Pay items

public record SalaryComponentDto(
    int Id, string Code, string Name, string? Description, ComponentType Type, ComponentCategory Category,
    TaxTreatment TaxTreatment, bool IsPensionable, bool IsRecurring, bool IsActive, int DisplayOrder);

public class SalaryComponentInput
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public ComponentType Type { get; set; } = ComponentType.Earning;
    public ComponentCategory Category { get; set; } = ComponentCategory.Allowance;
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Taxable;
    public bool IsPensionable { get; set; } = true;
    public bool IsRecurring { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public record RecurringDeductionQuery : PageQuery
{
    public int? SalaryComponentId { get; init; }
    public bool? Active { get; init; } = true;
}

public record RecurringDeductionListItem(
    int Id, int EmployeeId, string EmployeeNumber, string EmployeeName, string ComponentName, ComponentCategory Category,
    decimal MonthlyAmount, decimal? TotalAmount, decimal AmountRecovered, DateOnly StartDate, DateOnly? EndDate,
    string? Reference, bool IsActive)
{
    public decimal? RemainingBalance => TotalAmount is null ? null : Math.Max(0, TotalAmount.Value - AmountRecovered);
}

public class RecurringDeductionInput
{
    public int EmployeeId { get; set; }
    public int SalaryComponentId { get; set; }
    public decimal MonthlyAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal AmountRecovered { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reference { get; set; }
}

public record PayrollInputListItem(
    int Id, int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName,
    int SalaryComponentId, string ComponentName, ComponentType Type, decimal? Quantity, decimal? Rate, decimal Amount,
    string? Description, bool IsLocked);

public class PayrollInputInput
{
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int SalaryComponentId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
}

public record PeriodInputsSummary(int Year, int Month, string PeriodName, IReadOnlyList<PayrollInputListItem> Items,
    decimal TotalEarnings, decimal TotalDeductions);
