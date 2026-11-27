using PayMaster.Application.Common.Models;
using PayMaster.Application.Payroll;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Payslips;

public record PayslipCompany(
    string Name, string? LegalName, string? KraPin, string? NssfNumber, string? Address, string? PostalAddress,
    string? Phone, string? Email, string? Website, byte[]? Logo, string? Footer);

/// <summary>Everything needed to render a payslip, built from the immutable payroll snapshot.</summary>
public record PayslipDocument(
    string PayslipNumber,
    PayslipCompany Company,
    string PeriodName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly PaymentDate,
    bool IsReversed,
    PayrollLineDetails Line);

public record PayslipQuery : PageQuery
{
    public int? Year { get; init; }
    public int? Month { get; init; }
    public int? DepartmentId { get; init; }
}

public record PayslipListItem(
    int Id, string PayslipNumber, int EmployeeId, string EmployeeNumber, string EmployeeName, string DepartmentName,
    string PeriodName, int Year, int Month, DateOnly PaymentDate, decimal GrossEarnings, decimal TotalDeductions, decimal NetPay,
    PayrollRunStatus RunStatus, PayslipEmailStatus EmailStatus, DateTime? EmailedAt, DateTime GeneratedAt);

public interface IPayslipRenderer
{
    byte[] Render(PayslipDocument document);

    /// <summary>One PDF containing every payslip (for printing a whole run).</summary>
    byte[] RenderMany(IReadOnlyList<PayslipDocument> documents);
}
