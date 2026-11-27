using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Email;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Payslips;

/// <summary>
/// Payslip access, PDF rendering and email. Employees can only ever reach their own payslips;
/// payroll staff need payslips.view.all. Content always comes from the finalized payroll snapshot.
/// </summary>
public sealed class PayslipService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPayslipRenderer renderer,
    IEmailSender email,
    IFileStorage storage,
    IBackgroundJobs jobs,
    INotificationService notifications,
    IAuditService audit,
    TimeProvider clock,
    ILogger<PayslipService> logger)
{
    public const int BackgroundEmailThreshold = 25;

    private bool CanViewAll => currentUser.HasPermission(Permissions.Payslips.ViewAll);

    public async Task<PagedResult<PayslipListItem>> ListAsync(PayslipQuery query, bool ownOnly, CancellationToken ct = default)
    {
        var payslips = db.Payslips.AsNoTracking().AsQueryable();
        if (ownOnly || !CanViewAll)
        {
            var self = currentUser.EmployeeId;
            payslips = self is int employeeId ? payslips.Where(p => p.EmployeeId == employeeId) : payslips.Where(_ => false);
        }
        if (query.Year is int year)
            payslips = payslips.Where(p => p.PayrollEmployee.PayrollRun.PayrollPeriod.Year == year);
        if (query.Month is int month)
            payslips = payslips.Where(p => p.PayrollEmployee.PayrollRun.PayrollPeriod.Month == month);
        if (query.DepartmentId is int departmentId)
            payslips = payslips.Where(p => p.PayrollEmployee.Employee.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            payslips = payslips.Where(p => p.PayrollEmployee.EmployeeName.Contains(term) || p.PayrollEmployee.EmployeeNumber.Contains(term)
                                           || p.PayslipNumber.Contains(term));
        }

        return await payslips
            .OrderByDescending(p => p.PayrollEmployee.PayrollRun.PayrollPeriod.Year)
            .ThenByDescending(p => p.PayrollEmployee.PayrollRun.PayrollPeriod.Month)
            .ThenBy(p => p.PayrollEmployee.EmployeeName)
            .Select(p => new PayslipListItem(p.Id, p.PayslipNumber, p.EmployeeId, p.PayrollEmployee.EmployeeNumber, p.PayrollEmployee.EmployeeName,
                p.PayrollEmployee.DepartmentName, p.PayrollEmployee.PayrollRun.PayrollPeriod.Name, p.PayrollEmployee.PayrollRun.PayrollPeriod.Year,
                p.PayrollEmployee.PayrollRun.PayrollPeriod.Month, p.PayrollEmployee.PayrollRun.PayrollPeriod.PaymentDate,
                p.PayrollEmployee.GrossEarnings, p.PayrollEmployee.TotalDeductions, p.PayrollEmployee.NetPay,
                p.PayrollEmployee.PayrollRun.Status, p.EmailStatus, p.EmailedAt, p.GeneratedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<PayslipDocument> GetDocumentAsync(int payslipId, CancellationToken ct = default)
    {
        var payslip = await LoadAsync(payslipId, ct);
        EnsureAccess(payslip);
        return await BuildDocumentAsync(payslip, ct);
    }

    public async Task<(byte[] Pdf, string FileName)> RenderPdfAsync(int payslipId, CancellationToken ct = default)
    {
        var document = await GetDocumentAsync(payslipId, ct);
        audit.Record("Payslip.Downloaded", nameof(Payslip), payslipId, document.PayslipNumber);
        await db.SaveChangesAsync(ct);
        return (renderer.Render(document), FileName(document));
    }

    public async Task<(byte[] Pdf, string FileName)> RenderRunAsync(int runId, CancellationToken ct = default)
    {
        if (!CanViewAll)
            throw new ForbiddenAccessException();
        var ids = await db.Payslips.Where(p => p.PayrollEmployee.PayrollRunId == runId)
            .OrderBy(p => p.PayrollEmployee.EmployeeName).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count == 0)
            throw new DomainException("No payslips have been issued for this payroll yet. Finalize it first.");

        var documents = new List<PayslipDocument>();
        var company = await CompanyAsync(ct);
        foreach (var id in ids)
            documents.Add(await BuildDocumentAsync(await LoadAsync(id, ct), ct, company));
        var reference = await db.PayrollRuns.Where(r => r.Id == runId).Select(r => r.Reference).SingleAsync(ct);
        audit.Record("Payslip.BulkDownloaded", nameof(PayrollRun), runId, $"{documents.Count} payslips");
        await db.SaveChangesAsync(ct);
        return (renderer.RenderMany(documents), $"Payslips-{reference}.pdf");
    }

    public async Task EmailAsync(int payslipId, CancellationToken ct = default)
    {
        var payslip = await LoadAsync(payslipId, ct, tracking: true);
        EnsureAccess(payslip);
        await SendAsync(payslip, await CompanyAsync(ct), ct);
        await db.SaveChangesAsync(ct);
        if (payslip.EmailStatus == PayslipEmailStatus.Failed)
            throw new DomainException($"The payslip could not be emailed: {payslip.EmailError}");
    }

    /// <summary>Emails every payslip of a run, in the background for larger runs.</summary>
    /// <returns>True when queued as a background job.</returns>
    public async Task<bool> EmailRunAsync(int runId, CancellationToken ct = default)
    {
        if (!currentUser.HasPermission(Permissions.Payslips.Generate))
            throw new ForbiddenAccessException();
        var count = await db.Payslips.CountAsync(p => p.PayrollEmployee.PayrollRunId == runId, ct);
        if (count == 0)
            throw new DomainException("No payslips have been issued for this payroll yet.");
        if (count > BackgroundEmailThreshold)
        {
            await db.Payslips.Where(p => p.PayrollEmployee.PayrollRunId == runId && p.EmailStatus != PayslipEmailStatus.Sent)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.EmailStatus, PayslipEmailStatus.Queued), ct);
            jobs.EnqueuePayslipGeneration(runId, currentUser.RequireUserId(), email: true);
            return true;
        }
        await ProcessRunAsync(runId, true, currentUser.RequireUserId(), ct);
        return false;
    }

    /// <summary>Background/bulk processing: emails payslips not yet sent and notifies the requester.</summary>
    public async Task ProcessRunAsync(int runId, bool sendEmail, int userId, CancellationToken ct = default)
    {
        var company = await CompanyAsync(ct);
        var ids = await db.Payslips.Where(p => p.PayrollEmployee.PayrollRunId == runId && p.EmailStatus != PayslipEmailStatus.Sent)
            .Select(p => p.Id).ToListAsync(ct);
        int sent = 0, failed = 0;
        foreach (var id in ids)
        {
            if (!sendEmail)
                break;
            var payslip = await LoadAsync(id, ct, tracking: true);
            await SendAsync(payslip, company, ct);
            await db.SaveChangesAsync(ct);
            if (payslip.EmailStatus == PayslipEmailStatus.Sent) sent++; else failed++;
        }

        var reference = await db.PayrollRuns.Where(r => r.Id == runId).Select(r => r.Reference).SingleAsync(ct);
        audit.Record("Payslip.BulkEmailed", nameof(PayrollRun), runId, $"{reference}: {sent} sent, {failed} failed");
        notifications.Notify([userId], "Payslips emailed", $"{reference}: {sent} payslips sent{(failed > 0 ? $", {failed} failed" : "")}.",
            failed > 0 ? NotificationType.Warning : NotificationType.Success, $"/Payslips?search={reference}");
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task SendAsync(Payslip payslip, PayslipCompany company, CancellationToken ct)
    {
        var employee = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Id == payslip.EmployeeId).Select(e => new { e.WorkEmail, e.PersonalEmail, e.FirstName }).SingleAsync(ct);
        var to = employee.WorkEmail ?? employee.PersonalEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            payslip.EmailStatus = PayslipEmailStatus.Failed;
            payslip.EmailError = "Employee has no email address.";
            return;
        }

        var document = await BuildDocumentAsync(payslip, ct, company);
        try
        {
            await email.SendAsync(new EmailMessage(
                to,
                $"Your payslip for {document.PeriodName}",
                $"""
                 <p>Dear {WebUtility.HtmlEncode(employee.FirstName)},</p>
                 <p>Please find attached your payslip for <strong>{WebUtility.HtmlEncode(document.PeriodName)}</strong>.
                 Net pay: <strong>{Money.Format(document.Line.NetPay)}</strong>, paid on {document.PaymentDate:dd MMM yyyy}.</p>
                 <p>Payslips are also available any time in PayMaster self-service.</p>
                 <p>Regards,<br/>{WebUtility.HtmlEncode(company.Name)} Payroll</p>
                 """,
                document.Line.EmployeeName,
                [new EmailAttachment(FileName(document), "application/pdf", renderer.Render(document))]), ct);
            payslip.EmailStatus = PayslipEmailStatus.Sent;
            payslip.EmailedAt = clock.GetUtcNow().UtcDateTime;
            payslip.EmailError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to email payslip {PayslipId}", payslip.Id);
            payslip.EmailStatus = PayslipEmailStatus.Failed;
            payslip.EmailError = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
        }
    }

    private async Task<Payslip> LoadAsync(int id, CancellationToken ct, bool tracking = false)
    {
        var query = db.Payslips
            .Include(p => p.PayrollEmployee).ThenInclude(l => l.PayrollRun).ThenInclude(r => r.PayrollPeriod)
            .Include(p => p.PayrollEmployee).ThenInclude(l => l.Earnings)
            .Include(p => p.PayrollEmployee).ThenInclude(l => l.Deductions)
            .Include(p => p.PayrollEmployee).ThenInclude(l => l.EmployerContributions)
            .AsSplitQuery();
        if (!tracking)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Payslip", id);
    }

    private void EnsureAccess(Payslip payslip)
    {
        if (payslip.EmployeeId == currentUser.EmployeeId || CanViewAll)
            return;
        throw new ForbiddenAccessException("You can only view your own payslips.");
    }

    private async Task<PayslipDocument> BuildDocumentAsync(Payslip payslip, CancellationToken ct, PayslipCompany? company = null)
    {
        var line = payslip.PayrollEmployee;
        var period = line.PayrollRun.PayrollPeriod;
        var isOwn = payslip.EmployeeId == currentUser.EmployeeId;
        return new PayslipDocument(payslip.PayslipNumber, company ?? await CompanyAsync(ct), period.Name, period.StartDate,
            period.EndDate, period.PaymentDate, line.PayrollRun.Status == PayrollRunStatus.Reversed,
            PayrollService.ToLineDetails(line, showBank: isOwn || currentUser.HasPermission(Permissions.Employees.ViewBankDetails)));
    }

    private async Task<PayslipCompany> CompanyAsync(CancellationToken ct)
    {
        var c = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        byte[]? logo = null;
        if (c?.LogoPath is { Length: > 0 } path)
        {
            try
            {
                await using var stream = await storage.OpenReadAsync(path, ct);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                logo = buffer.ToArray();
            }
            catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
            {
                logger.LogWarning("Company logo {Path} could not be loaded", path);
            }
        }
        return new PayslipCompany(c?.CompanyName ?? "Company", c?.LegalName, c?.KraPin, c?.NssfEmployerNumber, c?.PhysicalAddress,
            c?.PostalAddress, c?.PhoneNumber, c?.Email, c?.Website, logo, c?.PayslipFooter);
    }

    private static string FileName(PayslipDocument d) =>
        $"Payslip-{d.Line.EmployeeNumber}-{d.PeriodStart:yyyy-MM}.pdf";
}
