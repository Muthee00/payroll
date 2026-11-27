using PayMaster.Application.Common.Models;
using PayMaster.Application.Employees;
using PayMaster.Application.Payslips;

namespace PayMaster.Web.ViewModels.Payslips;

public class PayslipIndexViewModel
{
    public required PayslipQuery Query { get; init; }
    public required PagedResult<PayslipListItem> Result { get; init; }
    public bool OwnOnly { get; init; }
    public bool CanViewAll { get; init; }
    public IReadOnlyList<LookupItem> Departments { get; init; } = [];
}
