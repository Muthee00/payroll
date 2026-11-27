using PayMaster.Application.Common.Models;
using PayMaster.Application.Employees;

namespace PayMaster.Web.ViewModels.Employees;

public class EmployeeIndexViewModel
{
    public required EmployeeQuery Query { get; init; }
    public required PagedResult<EmployeeListItemDto> Result { get; init; }
    public required IReadOnlyList<LookupItem> Departments { get; init; }
    public bool ShowSalary { get; init; }
}

public class EmployeeFormViewModel
{
    public int? Id { get; init; }
    public string? EmployeeNumber { get; init; }
    public EmployeeInput Input { get; init; } = new();
    public required EmployeeLookups Lookups { get; init; }
    public bool IsCreate => Id is null;
}

public class EmployeeImportViewModel
{
    public ImportResult? Result { get; init; }
}
