using PayMaster.Domain.Common;
using PayMaster.Domain.Interfaces;

namespace PayMaster.Domain.Entities.Administration;

/// <summary>Single-row company profile and payroll policy settings.</summary>
public class CompanySetting : AuditableEntity, IAuditTracked
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? KraPin { get; set; }
    public string? NssfEmployerNumber { get; set; }
    public string? ShaEmployerNumber { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? PostalAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }

    /// <summary>Default day of month salaries are paid.</summary>
    public int PaymentDay { get; set; } = 28;

    /// <summary>Approver of a payroll run must differ from its preparer (segregation of duties).</summary>
    public bool RequireSeparatePayrollApprover { get; set; } = true;

    /// <summary>Warn when net pay falls below this fraction of basic (Employment Act s.19(3): one third).</summary>
    public decimal MinimumNetPayRatio { get; set; } = 0.3333m;

    public string EmployeeNumberPrefix { get; set; } = "EMP";
    public string? PayslipFooter { get; set; }
}
