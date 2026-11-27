using PayMaster.Application.Common.Models;
using PayMaster.Application.Employees;
using PayMaster.Application.Payroll;
using PayMaster.Application.Salary;

namespace PayMaster.Web.ViewModels.Salary;

public class SalaryStructuresIndexViewModel
{
    public string Tab { get; init; } = "structures";
    public IReadOnlyList<SalaryStructureListItem> Structures { get; init; } = [];
    public IReadOnlyList<SalaryGradeDto> Grades { get; init; } = [];
    public IReadOnlyList<PendingSalaryChangeDto> Pending { get; init; } = [];
    public bool CanSeeApprovals { get; init; }
}

public class SalaryStructureFormViewModel
{
    public int? Id { get; init; }
    public SalaryStructureInput Input { get; init; } = new();
    public IReadOnlyList<SalaryComponentDto> Allowances { get; init; } = [];
    public IReadOnlyList<SalaryGradeDto> Grades { get; init; } = [];
    public EmployeeLookups Lookups { get; init; } = null!;
    public bool IsCreate => Id is null;
}

public class StructureRevisionViewModel
{
    public required SalaryStructureDetails Structure { get; init; }
    public StructureRevisionInput Input { get; init; } = new();
}

public class SalaryChangeViewModel
{
    public required EmployeeDetailsDto Employee { get; init; }
    public required SalaryChangeInput Input { get; init; }
    public IReadOnlyList<LookupItem> Structures { get; init; } = [];
}

public class DeductionsIndexViewModel
{
    public string Tab { get; init; } = "recurring";
    public RecurringDeductionQuery DeductionQuery { get; init; } = new();
    public PagedResult<RecurringDeductionListItem>? Deductions { get; init; }
    public PeriodInputsSummary? Inputs { get; init; }
    public string? InputSearch { get; init; }
    public IReadOnlyList<PeriodOption> Periods { get; init; } = [];
    public IReadOnlyList<SalaryComponentDto> Components { get; init; } = [];
    public bool CanManage { get; init; }
}

public class RecurringDeductionFormViewModel
{
    public int? Id { get; init; }
    public RecurringDeductionInput Input { get; init; } = new();
    public IReadOnlyList<(int Id, string Name)> Employees { get; init; } = [];
    public IReadOnlyList<SalaryComponentDto> DeductionTypes { get; init; } = [];
}

public class PayrollInputFormViewModel
{
    public int? Id { get; init; }
    public PayrollInputInput Input { get; init; } = new();
    public IReadOnlyList<(int Id, string Name)> Employees { get; init; } = [];
    public IReadOnlyList<SalaryComponentDto> ItemTypes { get; init; } = [];
    public IReadOnlyList<PeriodOption> Periods { get; init; } = [];
}

public class SalaryComponentFormViewModel
{
    public int? Id { get; init; }
    public SalaryComponentInput Input { get; init; } = new();
}
