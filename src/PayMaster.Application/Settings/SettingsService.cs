using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Settings;

public class CompanySettingsInput
{
    public string CompanyName { get; set; } = "";
    public string? LegalName { get; set; }
    public string? KraPin { get; set; }
    public string? NssfEmployerNumber { get; set; }
    public string? ShaEmployerNumber { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? PostalAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public int PaymentDay { get; set; } = 28;
    public bool RequireSeparatePayrollApprover { get; set; } = true;
    public decimal MinimumNetPayPercent { get; set; } = 33.33m;
    public string EmployeeNumberPrefix { get; set; } = "EMP";
    public string? PayslipFooter { get; set; }
    public bool HasLogo { get; set; }
}

public sealed class CompanySettingsInputValidator : AbstractValidator<CompanySettingsInput>
{
    public CompanySettingsInputValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.KraPin).Matches("^[AP][0-9]{9}[A-Z]$").When(x => !string.IsNullOrWhiteSpace(x.KraPin)).WithMessage("KRA PIN must look like P051234567Q.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PaymentDay).InclusiveBetween(1, 31);
        RuleFor(x => x.MinimumNetPayPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployeeNumberPrefix).NotEmpty().MaximumLength(10).Matches("^[A-Z0-9]+$").WithMessage("Prefix must be upper-case letters/digits.");
        RuleFor(x => x.PayslipFooter).MaximumLength(1000);
    }
}

public record DepartmentDto(int Id, string Code, string Name, string? Description, bool IsActive, int? HeadEmployeeId, string? HeadName, int EmployeeCount, int PositionCount);
public record PositionDto(int Id, string Code, string Title, string? Description, bool IsActive, int? DepartmentId, string? DepartmentName, int? SalaryGradeId, string? GradeName, int EmployeeCount);

public class DepartmentInput
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? HeadEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PositionInput
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public int? SalaryGradeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SettingsService(IApplicationDbContext db, IFileStorage storage)
{
    public const long MaxLogoBytes = 1024 * 1024;

    public async Task<CompanySettingsInput> GetCompanyAsync(CancellationToken ct = default)
    {
        var c = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new CompanySetting { CompanyName = "My Company" };
        return new CompanySettingsInput
        {
            CompanyName = c.CompanyName, LegalName = c.LegalName, KraPin = c.KraPin, NssfEmployerNumber = c.NssfEmployerNumber,
            ShaEmployerNumber = c.ShaEmployerNumber, PhysicalAddress = c.PhysicalAddress, PostalAddress = c.PostalAddress,
            PhoneNumber = c.PhoneNumber, Email = c.Email, Website = c.Website, PaymentDay = c.PaymentDay,
            RequireSeparatePayrollApprover = c.RequireSeparatePayrollApprover, MinimumNetPayPercent = Math.Round(c.MinimumNetPayRatio * 100m, 2),
            EmployeeNumberPrefix = c.EmployeeNumberPrefix, PayslipFooter = c.PayslipFooter, HasLogo = c.LogoPath is not null
        };
    }

    public async Task SaveCompanyAsync(CompanySettingsInput input, CancellationToken ct = default)
    {
        var c = await db.CompanySettings.FirstOrDefaultAsync(ct) ?? db.CompanySettings.Add(new CompanySetting()).Entity;
        c.CompanyName = input.CompanyName.Trim();
        c.LegalName = Clean(input.LegalName);
        c.KraPin = Clean(input.KraPin)?.ToUpperInvariant();
        c.NssfEmployerNumber = Clean(input.NssfEmployerNumber);
        c.ShaEmployerNumber = Clean(input.ShaEmployerNumber);
        c.PhysicalAddress = Clean(input.PhysicalAddress);
        c.PostalAddress = Clean(input.PostalAddress);
        c.PhoneNumber = Clean(input.PhoneNumber);
        c.Email = Clean(input.Email);
        c.Website = Clean(input.Website);
        c.PaymentDay = input.PaymentDay;
        c.RequireSeparatePayrollApprover = input.RequireSeparatePayrollApprover;
        c.MinimumNetPayRatio = Math.Round(input.MinimumNetPayPercent / 100m, 6);
        c.EmployeeNumberPrefix = input.EmployeeNumberPrefix.Trim().ToUpperInvariant();
        c.PayslipFooter = Clean(input.PayslipFooter);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveLogoAsync(Stream content, string fileName, long length, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
            throw new DomainException("Upload a PNG or JPEG logo.");
        if (length > MaxLogoBytes)
            throw new DomainException("The logo must be 1 MB or smaller.");
        var c = await db.CompanySettings.FirstAsync(ct);
        c.LogoPath = await storage.SaveAsync("branding", content, extension, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(Stream Content, string ContentType)?> LogoAsync(CancellationToken ct = default)
    {
        var path = await db.CompanySettings.Select(c => c.LogoPath).FirstOrDefaultAsync(ct);
        if (path is null)
            return null;
        return (await storage.OpenReadAsync(path, ct), path.EndsWith(".png") ? "image/png" : "image/jpeg");
    }

    // ---------------------------------------------------------------- Departments & positions

    public async Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct = default) =>
        await db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name, d.Description, d.IsActive, d.HeadEmployeeId,
                d.HeadEmployee != null ? d.HeadEmployee.FirstName + " " + d.HeadEmployee.LastName : null,
                d.Employees.Count(e => e.Status != EmploymentStatus.Terminated), d.Positions.Count))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PositionDto>> PositionsAsync(CancellationToken ct = default) =>
        await db.Positions.AsNoTracking().OrderBy(p => p.Department == null ? "" : p.Department.Name).ThenBy(p => p.Title)
            .Select(p => new PositionDto(p.Id, p.Code, p.Title, p.Description, p.IsActive, p.DepartmentId,
                p.Department != null ? p.Department.Name : null, p.SalaryGradeId, p.SalaryGrade != null ? p.SalaryGrade.Name : null,
                db.Employees.Count(e => e.PositionId == p.Id && e.Status != EmploymentStatus.Terminated)))
            .ToListAsync(ct);

    public async Task SaveDepartmentAsync(int? id, DepartmentInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Name))
            throw new DomainException("Code and name are required.");
        if (await db.Departments.IgnoreQueryFilters().AnyAsync(d => (d.Code == code || d.Name == input.Name.Trim()) && d.Id != id, ct))
            throw new ConflictException("A department with that code or name already exists.");

        var department = id is null ? db.Departments.Add(new Department()).Entity
            : await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Department", id);
        if (!input.IsActive && id is not null && await db.Employees.AnyAsync(e => e.DepartmentId == id && e.Status != EmploymentStatus.Terminated, ct))
            throw new DomainException("Move employees out of this department before deactivating it.");
        department.Code = code;
        department.Name = input.Name.Trim();
        department.Description = Clean(input.Description);
        department.HeadEmployeeId = input.HeadEmployeeId;
        department.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task SavePositionAsync(int? id, PositionInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Title))
            throw new DomainException("Code and title are required.");
        if (await db.Positions.IgnoreQueryFilters().AnyAsync(p => p.Code == code && p.Id != id, ct))
            throw new ConflictException("A job title with that code already exists.");

        var position = id is null ? db.Positions.Add(new Position()).Entity
            : await db.Positions.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Job title", id);
        position.Code = code;
        position.Title = input.Title.Trim();
        position.Description = Clean(input.Description);
        position.DepartmentId = input.DepartmentId;
        position.SalaryGradeId = input.SalaryGradeId;
        position.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public record AuditLogQuery : PageQuery
{
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public record AuditLogItem(long Id, DateTime Timestamp, string? UserName, string Action, string EntityName, string? EntityId,
    string? Details, string? OldValues, string? NewValues, string? IpAddress);

public sealed class AuditLogService(IApplicationDbContext db)
{
    public Task<PagedResult<AuditLogItem>> ListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var logs = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Action))
            logs = logs.Where(l => l.Action.StartsWith(query.Action));
        if (!string.IsNullOrWhiteSpace(query.EntityName))
            logs = logs.Where(l => l.EntityName == query.EntityName);
        if (!string.IsNullOrWhiteSpace(query.EntityId))
            logs = logs.Where(l => l.EntityId == query.EntityId);
        if (query.From is DateOnly from)
            logs = logs.Where(l => l.Timestamp >= from.ToDateTime(TimeOnly.MinValue));
        if (query.To is DateOnly to)
            logs = logs.Where(l => l.Timestamp < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (!string.IsNullOrWhiteSpace(query.Search))
            logs = logs.Where(l => (l.UserName != null && l.UserName.Contains(query.Search)) || (l.Details != null && l.Details.Contains(query.Search)));

        return logs.OrderByDescending(l => l.Id)
            .Select(l => new AuditLogItem(l.Id, l.Timestamp, l.UserName, l.Action, l.EntityName, l.EntityId, l.Details, l.OldValues, l.NewValues, l.IpAddress))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<string>> EntityNamesAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking().Select(l => l.EntityName).Distinct().OrderBy(n => n).ToListAsync(ct);
}
