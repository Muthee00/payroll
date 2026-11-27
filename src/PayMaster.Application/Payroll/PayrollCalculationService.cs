using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll.Calculation;
using PayMaster.Application.Statutory;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;

namespace PayMaster.Application.Payroll;

/// <summary>
/// Gathers each employee's pay data for the period, runs <see cref="PayrollCalculationEngine"/> and stores
/// the results. The backend is the only source of payroll figures; nothing is accepted from the browser.
/// </summary>
public sealed class PayrollCalculationService(
    IApplicationDbContext db,
    StatutoryConfigService statutory,
    INotificationService notifications,
    IAuditService audit,
    TimeProvider clock,
    ILogger<PayrollCalculationService> logger)
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Calculates (or recalculates) every line of a run inside one transaction.</summary>
    public async Task CalculateAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.Include(r => r.PayrollPeriod)
            .SingleOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status != PayrollRunStatus.Calculating)
            run.BeginCalculation();
        await db.SaveChangesAsync(ct);

        try
        {
            await CalculateLinesAsync(run, ct);
            run.CompleteCalculation(userId, clock.GetUtcNow().UtcDateTime);
            audit.Record("Payroll.Calculated", nameof(PayrollRun), run.Id,
                $"{run.Reference}: {run.EmployeeCount} employees, gross {run.TotalGross:N2}, net {run.TotalNetPay:N2}, {run.ErrorCount} errors");
            notifications.Notify([userId], "Payroll calculation completed",
                $"{run.Reference} ({run.PayrollPeriod.Name}): {run.EmployeeCount} employees, {run.ErrorCount} with errors.",
                run.ErrorCount > 0 ? NotificationType.Warning : NotificationType.Success, $"/Payroll/Run/{run.Id}");
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Payroll calculation failed for run {RunId}", runId);
            db.ChangeTracker.Clear();
            var failed = await db.PayrollRuns.SingleAsync(r => r.Id == runId, CancellationToken.None);
            if (failed.Status == PayrollRunStatus.Calculating)
                failed.FailCalculation(ex is DomainException ? ex.Message : "Calculation failed. See the system log for details.");
            notifications.Notify([userId], "Payroll calculation failed", $"{failed.Reference}: {failed.StatusReason}",
                NotificationType.Error, $"/Payroll/Run/{failed.Id}");
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task CalculateLinesAsync(PayrollRun run, CancellationToken ct)
    {
        var period = run.PayrollPeriod;
        var rules = await statutory.GetRulesForDateAsync(period.EndDate, ct);
        var minimumNetRatio = await db.CompanySettings.Select(c => (decimal?)c.MinimumNetPayRatio).FirstOrDefaultAsync(ct) ?? 0.3333m;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Clear previous results (recalculation before approval).
        var lineIds = await db.PayrollEmployees.Where(l => l.PayrollRunId == run.Id).Select(l => l.Id).ToListAsync(ct);
        await db.PayrollEarnings.Where(e => lineIds.Contains(e.PayrollEmployeeId)).ExecuteDeleteAsync(ct);
        await db.PayrollDeductions.Where(d => lineIds.Contains(d.PayrollEmployeeId)).ExecuteDeleteAsync(ct);
        await db.PayrollEmployerContributions.Where(c => lineIds.Contains(c.PayrollEmployeeId)).ExecuteDeleteAsync(ct);

        var lines = await db.PayrollEmployees.Where(l => l.PayrollRunId == run.Id).ToListAsync(ct);
        var employeeIds = lines.Select(l => l.EmployeeId).ToList();

        var employees = await db.Employees.IgnoreQueryFilters()
            .Include(e => e.Department).Include(e => e.Position)
            .Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var revisions = await db.EmployeeSalaries.AsNoTracking()
            .Include(s => s.Components).ThenInclude(c => c.SalaryComponent)
            .Where(s => employeeIds.Contains(s.EmployeeId)
                        && (s.Status == SalaryRevisionStatus.Approved || s.Status == SalaryRevisionStatus.Superseded)
                        && s.EffectiveFrom <= period.EndDate && (s.EffectiveTo == null || s.EffectiveTo >= period.StartDate))
            .ToListAsync(ct);
        var recurring = await db.EmployeeDeductions.AsNoTracking().Include(d => d.SalaryComponent)
            .Where(d => employeeIds.Contains(d.EmployeeId) && d.IsActive).ToListAsync(ct);
        var inputs = await db.PayrollInputs.AsNoTracking().Include(i => i.SalaryComponent)
            .Where(i => i.PayrollPeriodId == period.Id && employeeIds.Contains(i.EmployeeId)).ToListAsync(ct);

        foreach (var line in lines)
        {
            var employee = employees[line.EmployeeId];
            Snapshot(line, employee);

            var (start, end) = PayableRange(employee, period);
            line.DaysInPeriod = period.CalendarDays;
            line.DaysPayable = end < start ? 0 : end.DayNumber - start.DayNumber + 1;

            var errors = new List<string>();
            var warnings = new List<string>();
            CheckEmployeeData(employee, errors, warnings);

            // Salary in force at the end of the payable range (a mid-month change applies to the whole month).
            var asOf = end < start ? period.EndDate : end;
            var employeeRevisions = revisions.Where(r => r.EmployeeId == employee.Id).ToList();
            var salary = employeeRevisions
                .Where(r => r.EffectiveFrom <= asOf && (r.EffectiveTo == null || r.EffectiveTo >= asOf))
                .MaxBy(r => r.EffectiveFrom);
            if (employeeRevisions.Count > 1)
                warnings.Add("Salary changed during this period; the salary in force at period end is applied to the whole month.");

            if (salary is null)
            {
                errors.Add("No approved salary in force for this period.");
                ApplyResult(line, null, errors, warnings, run);
                continue;
            }

            var input = new EmployeePayInput
            {
                BasicSalary = salary.BasicSalary,
                DaysInPeriod = line.DaysInPeriod,
                DaysPayable = line.DaysPayable,
                HasDisabilityExemption = employee.HasDisabilityExemption,
                MinimumNetPayRatio = minimumNetRatio,
                Earnings = salary.Components.Select(c => ToLine(c.SalaryComponent, c.Amount, prorate: true))
                    .Concat(inputs.Where(i => i.EmployeeId == employee.Id && i.SalaryComponent.IsEarning)
                        .Select(i => ToLine(i.SalaryComponent, i.Amount, prorate: false, i.Description)))
                    .ToList(),
                Deductions = recurring.Where(d => d.EmployeeId == employee.Id)
                    .Select(d => (Deduction: d, Amount: d.AmountDueFor(period.StartDate, period.EndDate)))
                    .Where(x => x.Amount > 0)
                    .Select(x => ToLine(x.Deduction.SalaryComponent, x.Amount, prorate: false, x.Deduction.Reference, x.Deduction.Id))
                    .Concat(inputs.Where(i => i.EmployeeId == employee.Id && !i.SalaryComponent.IsEarning)
                        .Select(i => ToLine(i.SalaryComponent, i.Amount, prorate: false, i.Description)))
                    .ToList()
            };

            EmployeePayResult result;
            try
            {
                result = PayrollCalculationEngine.Calculate(input, rules);
            }
            catch (Exception ex) when (ex is ArgumentException or DomainException)
            {
                errors.Add(ex.Message);
                ApplyResult(line, null, errors, warnings, run);
                continue;
            }

            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);
            ApplyResult(line, result, errors, warnings, run);
        }

        run.Employees = lines;
        run.RecalculateTotals();
        run.StatutoryConfigurationId = rules.ConfigurationId;
        run.StatutorySnapshotJson = JsonSerializer.Serialize(rules, JsonOptions);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void ApplyResult(PayrollEmployee line, EmployeePayResult? r, List<string> errors, List<string> warnings, PayrollRun run)
    {
        line.Errors = errors.Count == 0 ? null : Truncate(string.Join("\n", errors.Distinct()));
        line.Warnings = warnings.Count == 0 ? null : Truncate(string.Join("\n", warnings.Distinct()));
        line.Status = errors.Count > 0 ? PayrollLineStatus.Error : PayrollLineStatus.Calculated;

        if (r is null)
        {
            line.BasicSalary = line.GrossEarnings = line.TaxableEarnings = line.NonTaxableEarnings = line.PensionablePay = 0;
            line.NssfTier1 = line.NssfTier2 = line.Nssf = line.Sha = line.HousingLevy = 0;
            line.AllowableDeductions = line.TaxableIncome = line.TaxBeforeReliefs = line.PersonalRelief = 0;
            line.InsuranceRelief = line.HousingLevyRelief = line.Paye = 0;
            line.OtherDeductions = line.TotalDeductions = line.NetPay = 0;
            line.EmployerNssf = line.EmployerHousingLevy = line.TotalEmployerContributions = 0;
            line.CalculationBreakdownJson = null;
            return;
        }

        line.BasicSalary = r.BasicSalary;
        line.GrossEarnings = r.GrossEarnings;
        line.TaxableEarnings = r.TaxableEarnings;
        line.NonTaxableEarnings = r.NonTaxableEarnings;
        line.PensionablePay = r.PensionablePay;
        line.NssfTier1 = r.Nssf.Tier1;
        line.NssfTier2 = r.Nssf.Tier2;
        line.Nssf = r.Nssf.Employee;
        line.Sha = r.Sha;
        line.HousingLevy = r.HousingLevy;
        line.AllowableDeductions = r.AllowableDeductions;
        line.TaxableIncome = r.TaxableIncome;
        line.TaxBeforeReliefs = r.Paye.TaxBeforeReliefs;
        line.PersonalRelief = r.Paye.PersonalRelief;
        line.InsuranceRelief = r.Paye.InsuranceRelief;
        line.HousingLevyRelief = r.Paye.HousingLevyRelief;
        line.Paye = r.Paye.Paye;
        line.OtherDeductions = r.OtherDeductions;
        line.TotalDeductions = r.TotalDeductions;
        line.NetPay = r.NetPay;
        line.EmployerNssf = r.Nssf.Employer;
        line.EmployerHousingLevy = r.EmployerHousingLevy;
        line.TotalEmployerContributions = r.TotalEmployerContributions;
        line.CalculationBreakdownJson = JsonSerializer.Serialize(r.Steps, JsonOptions);

        var order = 0;
        foreach (var e in r.Earnings)
            db.PayrollEarnings.Add(new PayrollEarning
            {
                PayrollEmployeeId = line.Id, SalaryComponentId = e.SalaryComponentId, Code = e.Code, Name = e.Name,
                Category = e.Category ?? ComponentCategory.OtherEarning, TaxTreatment = e.TaxTreatment ?? TaxTreatment.Taxable,
                IsPensionable = e.IsPensionable, Amount = e.Amount, DisplayOrder = order++
            });
        order = 0;
        foreach (var d in r.Deductions.Where(d => d.Amount != 0 || d.Kind == DeductionKind.Statutory))
            db.PayrollDeductions.Add(new PayrollDeduction
            {
                PayrollEmployeeId = line.Id, SalaryComponentId = d.SalaryComponentId, EmployeeDeductionId = d.EmployeeDeductionId,
                Code = d.Code, Name = d.Name, Kind = d.Kind, Category = d.Category, TaxTreatment = d.TaxTreatment,
                Amount = d.Amount, DisplayOrder = order++
            });
        db.PayrollEmployerContributions.Add(new PayrollEmployerContribution { PayrollEmployeeId = line.Id, Code = "NSSF_ER", Name = "NSSF (Employer)", Amount = r.Nssf.Employer });
        db.PayrollEmployerContributions.Add(new PayrollEmployerContribution { PayrollEmployeeId = line.Id, Code = "AHL_ER", Name = "Housing Levy (Employer)", Amount = r.EmployerHousingLevy });
    }

    /// <summary>Snapshot employee details so history and payslips are unaffected by later edits.</summary>
    internal static void Snapshot(PayrollEmployee line, Employee employee)
    {
        line.EmployeeNumber = employee.EmployeeNumber;
        line.EmployeeName = employee.FullName;
        line.DepartmentName = employee.Department.Name;
        line.JobTitle = employee.Position?.Title;
        line.KraPin = employee.KraPin;
        line.NssfNumber = employee.NssfNumber;
        line.ShaNumber = employee.ShaNumber;
        line.NationalId = employee.NationalId;
        line.PaymentMethod = employee.PaymentMethod;
        line.BankName = employee.BankName;
        line.BankBranch = employee.BankBranch;
        line.BankAccountNumber = employee.BankAccountNumber;
        line.MobileMoneyNumber = employee.MobileMoneyNumber;
    }

    internal static (DateOnly Start, DateOnly End) PayableRange(Employee employee, PayrollPeriod period)
    {
        var start = employee.EmploymentDate > period.StartDate ? employee.EmploymentDate : period.StartDate;
        var end = employee.TerminationDate is DateOnly left && left < period.EndDate ? left : period.EndDate;
        return (start, end);
    }

    /// <summary>Data problems that block payment (errors) or statutory filing (warnings).</summary>
    internal static void CheckEmployeeData(Employee employee, List<string> errors, List<string> warnings)
    {
        switch (employee.PaymentMethod)
        {
            case PaymentMethod.BankTransfer when string.IsNullOrWhiteSpace(employee.BankAccountNumber) || string.IsNullOrWhiteSpace(employee.BankName):
                errors.Add("Bank name and account number are required for bank transfer.");
                break;
            case PaymentMethod.MobileMoney when string.IsNullOrWhiteSpace(employee.MobileMoneyNumber):
                errors.Add("A mobile money number is required.");
                break;
        }
        if (string.IsNullOrWhiteSpace(employee.KraPin))
            warnings.Add("KRA PIN missing (required for PAYE returns).");
        if (string.IsNullOrWhiteSpace(employee.NssfNumber))
            warnings.Add("NSSF number missing.");
        if (string.IsNullOrWhiteSpace(employee.ShaNumber))
            warnings.Add("SHA number missing.");
        if (employee.Status == EmploymentStatus.Suspended)
            warnings.Add("Employee is suspended.");
    }

    private static PayLine ToLine(SalaryComponent component, decimal amount, bool prorate, string? note = null, int? employeeDeductionId = null) =>
        new(component.Code, string.IsNullOrWhiteSpace(note) || component.IsEarning ? component.Name : $"{component.Name} ({note})",
            amount, component.Category, component.TaxTreatment, component.IsEarning && component.IsPensionable,
            prorate, component.Id, employeeDeductionId);

    private static string Truncate(string text) => text.Length <= 2000 ? text : text[..1997] + "...";
}
