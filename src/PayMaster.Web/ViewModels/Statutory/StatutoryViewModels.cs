using PayMaster.Application.Payroll.Calculation;
using PayMaster.Application.Statutory;

namespace PayMaster.Web.ViewModels.Statutory;

public class StatutoryIndexViewModel
{
    public IReadOnlyList<StatutoryVersionListItem> Versions { get; init; } = [];
    public StatutoryVersionDetails? Current { get; init; }
}

public class StatutoryDetailsViewModel
{
    public required StatutoryVersionDetails Details { get; init; }
    public decimal PreviewGross { get; init; }
    public EmployeePayResult? Preview { get; set; }
}

public class StatutoryEditViewModel
{
    public int Id { get; init; }
    public required StatutoryConfigInput Input { get; init; }
}
