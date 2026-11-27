using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Common;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Employees;

public record ImportError(int Row, string Message);

public record ImportResult(int Imported, IReadOnlyList<ImportError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Bulk import (all-or-nothing: any invalid row rejects the whole file) and filtered export.
/// </summary>
public sealed class EmployeeImportExportService(
    IApplicationDbContext db,
    EmployeeService employees,
    EmployeeAccessScope scope,
    IValidator<EmployeeInput> validator,
    ISpreadsheetService spreadsheets,
    INotificationService notifications,
    IAuditService audit)
{
    public const int MaxRows = 2_000;

    private static readonly (string Header, string Note, string Example)[] Columns =
    [
        ("FirstName", "Required.", "Jane"),
        ("MiddleName", "Optional.", "Wairimu"),
        ("LastName", "Required.", "Kamau"),
        ("Gender", "Male, Female or Other.", "Female"),
        ("DateOfBirth", "Required. Format yyyy-mm-dd.", "1992-04-18"),
        ("NationalId", "Required. 6–10 digits, unique.", "31234567"),
        ("KraPin", "Format A123456789Z. Unique.", "A012345678K"),
        ("NssfNumber", "Digits only.", "2001234567"),
        ("ShaNumber", "SHA / SHIF member number.", "CR5001234567"),
        ("PersonalEmail", "Optional.", "jane.kamau@example.com"),
        ("WorkEmail", "Optional.", "jane.kamau@company.co.ke"),
        ("PhoneNumber", "Kenyan number, e.g. +254712345678 or 0712345678.", "0712345678"),
        ("PhysicalAddress", "Optional.", "Kilimani, Nairobi"),
        ("DepartmentCode", "Required. Department code (see Settings › Departments).", "ENG"),
        ("PositionCode", "Optional. Job title code within the department.", "SWE"),
        ("EmploymentType", "Permanent, Contract, Probation, Casual or Intern.", "Permanent"),
        ("EmploymentDate", "Required. Format yyyy-mm-dd.", "2026-11-01"),
        ("ContractEndDate", "Required for Contract. Format yyyy-mm-dd.", ""),
        ("ManagerEmployeeNumber", "Optional. Existing employee number of the reporting manager.", "EMP-0006"),
        ("WorkLocation", "Optional.", "Nairobi HQ"),
        ("PaymentMethod", "BankTransfer, MobileMoney, Cheque or Cash.", "BankTransfer"),
        ("BankName", "Required for BankTransfer.", "Equity Bank"),
        ("BankBranch", "Optional.", "Westlands"),
        ("BankAccountNumber", "Required for BankTransfer.", "0123456789012"),
        ("MobileMoneyNumber", "Required for MobileMoney.", ""),
        ("SalaryStructureCode", "Optional. Applies the structure's allowances.", "SS-SWE"),
        ("BasicSalary", "Optional. Monthly basic in KES; creates a salary revision pending approval.", "120000"),
        ("EmergencyContactName", "Optional.", "Peter Kamau"),
        ("EmergencyContactRelationship", "Optional.", "Spouse"),
        ("EmergencyContactPhone", "Optional.", "0722000000")
    ];

    public byte[] CreateTemplate() => spreadsheets.CreateTemplate("Employees", Columns);

    public async Task<ImportResult> ImportAsync(Stream workbook, CancellationToken ct = default)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows;
        try
        {
            rows = spreadsheets.ReadRows(workbook);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ImportResult(0, [new ImportError(0, "The file could not be read. Upload an .xlsx file based on the template.")]);
        }

        if (rows.Count == 0)
            return new ImportResult(0, [new ImportError(0, "The file has no data rows.")]);
        if (rows.Count > MaxRows)
            return new ImportResult(0, [new ImportError(0, $"A single import is limited to {MaxRows} rows.")]);

        var missing = Columns.Select(c => c.Header).Where(h => h is "FirstName" or "LastName" or "NationalId" or "DepartmentCode")
            .Where(h => !rows[0].ContainsKey(h)).ToList();
        if (missing.Count > 0)
            return new ImportResult(0, [new ImportError(1, $"Missing required column(s): {string.Join(", ", missing)}.")]);

        var departments = await db.Departments.Where(d => d.IsActive).ToDictionaryAsync(d => d.Code.ToUpper(), d => d.Id, ct);
        var positions = await db.Positions.Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Code, p.DepartmentId }).ToListAsync(ct);
        var structures = await db.SalaryStructures.Where(s => s.Status == SalaryStructureStatus.Active)
            .ToDictionaryAsync(s => s.Code.ToUpper(), s => s.Id, ct);
        var employeeNumbers = await db.Employees.ToDictionaryAsync(e => e.EmployeeNumber.ToUpper(), e => e.Id, ct);

        var errors = new List<ImportError>();
        var inputs = new List<(int Row, EmployeeInput Input)>();
        var seenIds = new HashSet<string>();
        var seenPins = new HashSet<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2; // header is row 1
            var row = rows[i];
            var rowErrors = new List<string>();
            string Get(string key) => row.TryGetValue(key, out var v) ? v.Trim() : string.Empty;

            var input = new EmployeeInput
            {
                FirstName = Get("FirstName"),
                MiddleName = NullIfEmpty(Get("MiddleName")),
                LastName = Get("LastName"),
                NationalId = Get("NationalId"),
                KraPin = NullIfEmpty(Get("KraPin"))?.ToUpperInvariant(),
                NssfNumber = NullIfEmpty(Get("NssfNumber")),
                ShaNumber = NullIfEmpty(Get("ShaNumber")),
                PersonalEmail = NullIfEmpty(Get("PersonalEmail")),
                WorkEmail = NullIfEmpty(Get("WorkEmail")),
                PhoneNumber = NullIfEmpty(Get("PhoneNumber")),
                PhysicalAddress = NullIfEmpty(Get("PhysicalAddress")),
                WorkLocation = NullIfEmpty(Get("WorkLocation")),
                BankName = NullIfEmpty(Get("BankName")),
                BankBranch = NullIfEmpty(Get("BankBranch")),
                BankAccountNumber = NullIfEmpty(Get("BankAccountNumber")),
                MobileMoneyNumber = NullIfEmpty(Get("MobileMoneyNumber")),
                EmergencyContactName = NullIfEmpty(Get("EmergencyContactName")),
                EmergencyContactRelationship = NullIfEmpty(Get("EmergencyContactRelationship")),
                EmergencyContactPhone = NullIfEmpty(Get("EmergencyContactPhone"))
            };

            input.Gender = ParseEnum<Gender>(Get("Gender"), "Gender", rowErrors) ?? default;
            input.EmploymentType = ParseEnum<EmploymentType>(Get("EmploymentType"), "EmploymentType", rowErrors, EmploymentType.Permanent)!.Value;
            input.PaymentMethod = ParseEnum<PaymentMethod>(Get("PaymentMethod"), "PaymentMethod", rowErrors, PaymentMethod.BankTransfer)!.Value;
            input.DateOfBirth = ParseDate(Get("DateOfBirth"), "DateOfBirth", rowErrors);
            input.EmploymentDate = ParseDate(Get("EmploymentDate"), "EmploymentDate", rowErrors);
            input.ContractEndDate = ParseDate(Get("ContractEndDate"), "ContractEndDate", rowErrors);

            var departmentCode = Get("DepartmentCode").ToUpperInvariant();
            if (departments.TryGetValue(departmentCode, out var departmentId))
                input.DepartmentId = departmentId;
            else
                rowErrors.Add($"Department code '{departmentCode}' was not found.");

            var positionCode = Get("PositionCode").ToUpperInvariant();
            if (positionCode.Length > 0)
            {
                var position = positions.FirstOrDefault(p => p.Code.ToUpper() == positionCode && (p.DepartmentId == null || p.DepartmentId == input.DepartmentId));
                if (position is null)
                    rowErrors.Add($"Position code '{positionCode}' was not found in department {departmentCode}.");
                else
                    input.PositionId = position.Id;
            }

            var managerNumber = Get("ManagerEmployeeNumber").ToUpperInvariant();
            if (managerNumber.Length > 0)
            {
                if (employeeNumbers.TryGetValue(managerNumber, out var managerId))
                    input.ManagerId = managerId;
                else
                    rowErrors.Add($"Manager employee number '{managerNumber}' was not found.");
            }

            var structureCode = Get("SalaryStructureCode").ToUpperInvariant();
            if (structureCode.Length > 0)
            {
                if (structures.TryGetValue(structureCode, out var structureId))
                    input.SalaryStructureId = structureId;
                else
                    rowErrors.Add($"Salary structure '{structureCode}' was not found or is not active.");
            }

            var basic = Get("BasicSalary").Replace(",", "");
            if (basic.Length > 0)
            {
                if (decimal.TryParse(basic, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    input.BasicSalary = amount;
                else
                    rowErrors.Add("BasicSalary must be a number.");
            }

            if (!string.IsNullOrEmpty(input.NationalId) && !seenIds.Add(input.NationalId))
                rowErrors.Add($"National ID {input.NationalId} appears more than once in the file.");
            if (input.KraPin is not null && !seenPins.Add(input.KraPin))
                rowErrors.Add($"KRA PIN {input.KraPin} appears more than once in the file.");

            var validation = await validator.ValidateAsync(input, ct);
            rowErrors.AddRange(validation.Errors.Select(e => e.ErrorMessage));

            if (rowErrors.Count > 0)
                errors.AddRange(rowErrors.Distinct().Select(m => new ImportError(rowNumber, m)));
            else
                inputs.Add((rowNumber, input));
        }

        if (errors.Count > 0)
            return new ImportResult(0, errors);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var (rowNumber, input) in inputs)
            {
                try
                {
                    await employees.CreateCoreAsync(input, notifySalaryApprovers: false, ct);
                }
                catch (Exception ex) when (ex is DomainException or Common.Exceptions.ConflictException)
                {
                    errors.Add(new ImportError(rowNumber, ex.Message));
                }
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(ct);
                return new ImportResult(0, errors);
            }

            audit.Record("Employee.BulkImported", "Employee", null, $"{inputs.Count} employees imported");
            if (inputs.Any(i => i.Input.BasicSalary is not null))
            {
                await notifications.NotifyPermissionHoldersAsync(Permissions.Salary.Approve, "Salaries awaiting approval",
                    $"{inputs.Count(i => i.Input.BasicSalary is not null)} imported starting salaries need approval.",
                    NotificationType.Warning, "/SalaryStructures/Approvals", ct);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new ImportResult(inputs.Count, []);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<byte[]> ExportAsync(EmployeeQuery query, CancellationToken ct = default)
    {
        var rows = await employees.ListAllAsync(query, ct);
        var ids = rows.Select(i => i.Id).ToList();
        var details = await db.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new
            {
                e.Id, e.NationalId, e.KraPin, e.NssfNumber, e.ShaNumber, e.Gender, e.DateOfBirth, e.PaymentMethod,
                e.BankName, e.BankBranch, e.BankAccountNumber, e.MobileMoneyNumber, e.ContractEndDate, e.WorkLocation,
                Manager = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null
            })
            .ToDictionaryAsync(e => e.Id, ct);

        var showBank = scope.CanViewBankDetails;
        var showSalary = scope.CanViewSalary;
        var columns = new List<SpreadsheetColumn<EmployeeListItemDto>>
        {
            new("Employee No.", r => r.EmployeeNumber),
            new("Name", r => r.FullName),
            new("Department", r => r.DepartmentName),
            new("Job Title", r => r.JobTitle),
            new("Employment Type", r => r.EmploymentType),
            new("Status", r => r.Status),
            new("Employment Date", r => r.EmploymentDate),
            new("Contract End", r => details[r.Id].ContractEndDate),
            new("Manager", r => details[r.Id].Manager),
            new("Gender", r => details[r.Id].Gender),
            new("Date of Birth", r => details[r.Id].DateOfBirth),
            new("National ID", r => details[r.Id].NationalId),
            new("KRA PIN", r => details[r.Id].KraPin),
            new("NSSF No.", r => details[r.Id].NssfNumber),
            new("SHA No.", r => details[r.Id].ShaNumber),
            new("Work Email", r => r.WorkEmail),
            new("Phone", r => r.PhoneNumber),
            new("Work Location", r => details[r.Id].WorkLocation),
            new("Payment Method", r => details[r.Id].PaymentMethod)
        };
        if (showSalary)
        {
            columns.Add(new("Basic Salary", r => r.BasicSalary, "#,##0.00"));
            columns.Add(new("Gross Salary", r => r.GrossSalary, "#,##0.00"));
        }
        if (showBank)
        {
            columns.Add(new("Bank", r => details[r.Id].BankName));
            columns.Add(new("Branch", r => details[r.Id].BankBranch));
            columns.Add(new("Account No.", r => details[r.Id].BankAccountNumber));
            columns.Add(new("Mobile Money", r => details[r.Id].MobileMoneyNumber));
        }

        audit.Record("Employee.Exported", "Employee", null, $"{rows.Count} employees exported (bank details: {(showBank ? "yes" : "no")})");
        await db.SaveChangesAsync(ct);
        return spreadsheets.Export("Employees", rows, columns, "Employee Register");
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static TEnum? ParseEnum<TEnum>(string value, string column, List<string> errors, TEnum? fallback = null)
        where TEnum : struct, Enum
    {
        if (value.Length == 0)
        {
            if (fallback is null)
                errors.Add($"{column} is required.");
            return fallback;
        }
        if (Enum.TryParse<TEnum>(value.Replace(" ", ""), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        errors.Add($"{column} '{value}' is not valid. Use one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return fallback;
    }

    private static DateOnly? ParseDate(string value, string column, List<string> errors)
    {
        if (value.Length == 0)
            return null;
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors.Add($"{column} '{value}' is not a valid date (use yyyy-mm-dd).");
        return null;
    }
}
