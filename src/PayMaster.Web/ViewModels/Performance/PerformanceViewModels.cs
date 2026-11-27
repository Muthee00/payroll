using PayMaster.Application.Common.Models;
using PayMaster.Application.Employees;
using PayMaster.Application.Performance;
using PayMaster.Domain.Entities.Performance;

namespace PayMaster.Web.ViewModels.Performance;

public class GoalsIndexViewModel
{
    public string Tab { get; init; } = "goals";
    public GoalQuery Query { get; init; } = new();
    public PagedResult<GoalListItem>? Goals { get; init; }
    public IReadOnlyList<KpiDto> Kpis { get; init; } = [];
    public IReadOnlyList<RatingScaleLevel> RatingScale { get; init; } = [];
    public IReadOnlyList<DevelopmentPlanDto> Plans { get; init; } = [];
    public IReadOnlyList<LookupItem> Departments { get; init; } = [];
    public bool CanCreate { get; init; }
    public bool CanConfigure { get; init; }
}

public class GoalFormViewModel
{
    public int? Id { get; init; }
    public GoalInput Input { get; init; } = new();
    public IReadOnlyList<LookupItem> Employees { get; init; } = [];
}

public class KpiFormViewModel
{
    public int? Id { get; init; }
    public KpiInput Input { get; init; } = new();
    public required EmployeeLookups Lookups { get; init; }
}

public class PlanFormViewModel
{
    public DevelopmentPlanInput Input { get; init; } = new();
    public IReadOnlyList<LookupItem> Employees { get; init; } = [];
}

public class RatingLevelInput
{
    public int Value { get; set; }
    public string Label { get; set; } = "";
    public string? Description { get; set; }
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
}

public class ReviewsIndexViewModel
{
    public required ReviewQuery Query { get; init; }
    public required PagedResult<ReviewListItem> Reviews { get; init; }
    public IReadOnlyList<ReviewCycleDto> Cycles { get; init; } = [];
    public IReadOnlyList<LookupItem> Departments { get; init; } = [];
    public bool CanManageCycles { get; init; }
}
