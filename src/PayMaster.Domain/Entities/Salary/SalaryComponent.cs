using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Salary;

/// <summary>
/// Catalogue of non-statutory earnings and deductions (housing allowance, overtime,
/// HELB, loans...). Statutory deductions (PAYE/SHA/AHL/NSSF) are not components:
/// they are computed by the statutory engine.
/// </summary>
public class SalaryComponent : SoftDeletableEntity, IAuditTracked
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ComponentType Type { get; set; }
    public ComponentCategory Category { get; set; }
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>Included in NSSF pensionable pay (most cash earnings are).</summary>
    public bool IsPensionable { get; set; } = true;

    /// <summary>Recurring monthly item (allowance) vs. per-period variable input (overtime, bonus).</summary>
    public bool IsRecurring { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool IsEarning => Type == ComponentType.Earning;
}
