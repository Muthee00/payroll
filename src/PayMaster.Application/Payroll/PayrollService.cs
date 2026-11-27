using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll.Calculation;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Payroll;

/// <summary>Payroll runs: creation, calculation dispatch, approval workflow, finalization and queries.</summary>
public sealed class PayrollService(
    IApplicationDbContext db,
    PayrollPeriodService periods,
    PayrollCalculationService calculator,
    IBackgroundJobs jobs,
    ICurrentUser currentUser,
    INotificationService notifications,
    IUserDirectory users,
    IAuditService audit,
    TimeProvider clock)
{
    /// <summary>Runs larger than this are calculated by a background job.</summary>
    public const int BackgroundThreshold = 150;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ================================================================= Creation

    public async Task<CreateRunResult> CreateRunAsync(CreatePayrollRunInput input, CancellationToken ct = default)
    {
        var period = await periods.EnsureAsync(input.Year, input.Month, ct);
        if (period.Status == PayrollPeriodStatus.Closed)
            throw new DomainException($"{period.Name} is closed.");

        var candidates = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentDate <= period.EndDate
                        && (e.TerminationDate == null || e.TerminationDate >= period.StartDate)
                        && e.Status != EmploymentStatus.Deactivated && e.Status != EmploymentStatus.Suspended);

        candidates = input.Scope switch
        {
            PayrollScope.Department when input.DepartmentId is int dept => candidates.Where(e => e.DepartmentId == dept),
            PayrollScope.Department => throw new DomainException("Choose a department."),
            PayrollScope.SelectedEmployees when input.EmployeeIds.Count > 0 => candidates.Where(e => input.EmployeeIds.Contains(e.Id)),
            PayrollScope.SelectedEmployees => throw new DomainException("Select at least one employee."),
            _ => candidates
        };

        var eligible = await candidates.Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeNumber }).ToListAsync(ct);
        var locked = (await db.PayrollEmployees
            .Where(p => p.PayrollPeriodId == period.Id && p.PeriodLock == true)
            .Select(p => new { p.EmployeeId, p.PayrollRun.Reference })
            .ToListAsync(ct)).ToDictionary(p => p.EmployeeId, p => p.Reference);

        var included = eligible.Where(e => !locked.ContainsKey(e.Id)).ToList();
        var skipped = eligible.Where(e => locked.ContainsKey(e.Id))
            .Select(e => $"{e.Name} ({e.EmployeeNumber}) is already in {locked[e.Id]}").ToList();
        if (input.Scope == PayrollScope.SelectedEmployees)
            skipped.AddRange(input.EmployeeIds.Except(eligible.Select(e => e.Id)).Select(id => $"Employee #{id} is not payable in {period.Name}"));

        if (included.Count == 0)
            throw new DomainException(skipped.Count > 0
                ? $"Every selected employee already has payroll for {period.Name}."
                : $"No payable employees found for {period.Name}.");

        var run = new PayrollRun
        {
            Reference = await NextReferenceAsync(period, ct),
            PayrollPeriodId = period.Id,
            Scope = input.Scope,
            DepartmentId = input.Scope == PayrollScope.Department ? input.DepartmentId : null,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };
        foreach (var employee in included)
            run.Employees.Add(new PayrollEmployee
            {
                EmployeeId = employee.Id, PayrollPeriodId = period.Id, EmployeeNumber = employee.EmployeeNumber,
                EmployeeName = employee.Name, DepartmentName = string.Empty, Status = PayrollLineStatus.Pending
            });
        run.EmployeeCount = run.Employees.Count;
        db.PayrollRuns.Add(run);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            throw new ConflictException($"Another payroll for {period.Name} was created at the same time. Refresh and try again.");
        }

        audit.Record("Payroll.Created", nameof(PayrollRun), run.Id, $"{run.Reference} for {period.Name}: {run.EmployeeCount} employees ({input.Scope})");
        await db.SaveChangesAsync(ct);
        return new CreateRunResult(run.Id, run.EmployeeCount, skipped);
    }

    // ================================================================= Calculation

    /// <returns>True when calculation ran in the background.</returns>
    public async Task<bool> CalculateAsync(int runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.EmployeeCount > BackgroundThreshold)
        {
            run.BeginCalculation();
            await db.SaveChangesAsync(ct);
            jobs.EnqueuePayrollCalculation(runId, currentUser.RequireUserId());
            return true;
        }

        await calculator.CalculateAsync(runId, currentUser.RequireUserId(), ct);
        return false;
    }

    /// <summary>Pre-calculation check for missing employee data and salaries.</summary>
    public async Task<IReadOnlyList<ValidationIssue>> ValidateAsync(int runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayrollPeriod)
            .SingleOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run", runId);
        var employeeIds = await db.PayrollEmployees.Where(l => l.PayrollRunId == runId).Select(l => l.EmployeeId).ToListAsync(ct);
        var employees = await db.Employees.IgnoreQueryFilters().AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToListAsync(ct);
        var period = run.PayrollPeriod;
        var withSalary = (await db.EmployeeSalaries
            .Where(s => employeeIds.Contains(s.EmployeeId)
                        && (s.Status == SalaryRevisionStatus.Approved || s.Status == SalaryRevisionStatus.Superseded)
                        && s.EffectiveFrom <= period.EndDate && (s.EffectiveTo == null || s.EffectiveTo >= period.StartDate))
            .Select(s => s.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();
        var pendingSalary = (await db.EmployeeSalaries
            .Where(s => employeeIds.Contains(s.EmployeeId) && s.Status == SalaryRevisionStatus.PendingApproval && s.EffectiveFrom <= period.EndDate)
            .Select(s => s.EmployeeId).ToListAsync(ct)).ToHashSet();

        var issues = new List<ValidationIssue>();
        foreach (var e in employees.OrderBy(e => e.FirstName))
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            PayrollCalculationService.CheckEmployeeData(e, errors, warnings);
            if (!withSalary.Contains(e.Id))
                errors.Add("No approved salary in force for this period.");
            if (pendingSalary.Contains(e.Id))
                warnings.Add("A salary change for this period is still awaiting approval.");
            if (e.EmploymentDate > period.StartDate)
                warnings.Add($"Joined on {e.EmploymentDate:dd MMM yyyy}; pay will be prorated.");
            if (e.TerminationDate is DateOnly left && left < period.EndDate)
                warnings.Add($"Left on {left:dd MMM yyyy}; pay will be prorated.");

            issues.AddRange(errors.Select(m => new ValidationIssue(e.Id, e.EmployeeNumber, e.FullName, "Error", m)));
            issues.AddRange(warnings.Select(m => new ValidationIssue(e.Id, e.EmployeeNumber, e.FullName, "Warning", m)));
        }
        return issues;
    }

    public async Task RemoveEmployeeAsync(int runId, int lineId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        run.EnsureEditable();
        var line = await db.PayrollEmployees.SingleOrDefaultAsync(l => l.Id == lineId && l.PayrollRunId == runId, ct)
                   ?? throw new NotFoundException("Payroll line", lineId);
        db.PayrollEmployees.Remove(line);
        await db.SaveChangesAsync(ct);

        run = await db.PayrollRuns.Include(r => r.Employees).SingleAsync(r => r.Id == runId, ct);
        run.RecalculateTotals();
        audit.Record("Payroll.EmployeeRemoved", nameof(PayrollRun), runId, $"{line.EmployeeName} ({line.EmployeeNumber}) removed from {run.Reference}");
        await db.SaveChangesAsync(ct);
    }

    // ================================================================= Workflow

    public async Task SubmitAsync(int runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        run.Submit(currentUser.RequireUserId(), Now);
        audit.Record("Payroll.Submitted", nameof(PayrollRun), runId, run.Reference);
        await notifications.NotifyPermissionHoldersAsync(Permissions.Payroll.Approve, "Payroll awaiting approval",
            $"{run.Reference} ({run.PayrollPeriod.Name}): net pay {Money.Format(run.TotalNetPay)} for {run.EmployeeCount} employees.",
            NotificationType.Warning, $"/Payroll/Run/{runId}", ct);
        await SaveWorkflowAsync(ct);
    }

    public async Task ApproveAsync(int runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        var separate = await db.CompanySettings.Select(c => (bool?)c.RequireSeparatePayrollApprover).FirstOrDefaultAsync(ct) ?? true;
        run.Approve(currentUser.RequireUserId(), Now, separate);
        audit.Record("Payroll.Approved", nameof(PayrollRun), runId, $"{run.Reference}: net {run.TotalNetPay:N2}");
        NotifyPreparer(run, "Payroll approved", $"{run.Reference} was approved and can be finalized.", NotificationType.Success);
        await notifications.NotifyPermissionHoldersAsync(Permissions.Payroll.Finalize, "Payroll approved",
            $"{run.Reference} is approved and ready to finalize.", NotificationType.Success, $"/Payroll/Run/{runId}", ct);
        await SaveWorkflowAsync(ct);
    }

    public async Task RejectAsync(int runId, string reason, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        run.Reject(currentUser.RequireUserId(), Now, reason);
        audit.Record("Payroll.Rejected", nameof(PayrollRun), runId, $"{run.Reference}: {reason}");
        NotifyPreparer(run, "Payroll rejected", $"{run.Reference}: {reason}", NotificationType.Error);
        await SaveWorkflowAsync(ct);
    }

    /// <summary>
    /// Locks the run and its statutory version, records loan/advance recoveries and issues payslips.
    /// Everything happens in one transaction so a failure leaves the run Approved.
    /// </summary>
    public async Task FinalizeAsync(int runId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var run = await LoadRunAsync(runId, ct);
        run.Finalize(userId, Now);

        if (run.StatutoryConfigurationId is int configId)
        {
            var config = await db.StatutoryConfigurations.SingleAsync(c => c.Id == configId, ct);
            config.Lock();
        }

        var recoveries = await db.PayrollDeductions
            .Where(d => d.PayrollEmployee.PayrollRunId == runId && d.EmployeeDeductionId != null)
            .Select(d => new { d.EmployeeDeductionId, d.Amount }).ToListAsync(ct);
        var deductionIds = recoveries.Select(r => r.EmployeeDeductionId!.Value).Distinct().ToList();
        var deductions = await db.EmployeeDeductions.IgnoreQueryFilters().Where(d => deductionIds.Contains(d.Id)).ToListAsync(ct);
        foreach (var recovery in recoveries)
            deductions.Single(d => d.Id == recovery.EmployeeDeductionId).RecordRecovery(recovery.Amount);

        var lines = await db.PayrollEmployees.Where(l => l.PayrollRunId == runId && l.Status == PayrollLineStatus.Calculated)
            .Select(l => new { l.Id, l.EmployeeId, l.EmployeeNumber }).ToListAsync(ct);
        var existing = (await db.Payslips.Where(p => p.PayrollEmployee.PayrollRunId == runId).Select(p => p.PayrollEmployeeId).ToListAsync(ct)).ToHashSet();
        foreach (var line in lines.Where(l => !existing.Contains(l.Id)))
            db.Payslips.Add(new Payslip
            {
                PayrollEmployeeId = line.Id,
                EmployeeId = line.EmployeeId,
                PayslipNumber = $"PS-{run.PayrollPeriod.Year}{run.PayrollPeriod.Month:D2}-{run.Id}-{line.EmployeeNumber}",
                GeneratedAt = Now
            });

        audit.Record("Payroll.Finalized", nameof(PayrollRun), runId,
            $"{run.Reference}: {lines.Count} payslips issued, {recoveries.Count} loan/advance recoveries recorded");
        await notifications.NotifyPermissionHoldersAsync(Permissions.Payroll.View, "Payroll finalized",
            $"{run.Reference} ({run.PayrollPeriod.Name}) is finalized. Net pay {Money.Format(run.TotalNetPay)}.",
            NotificationType.Success, $"/Payroll/Run/{runId}", ct);
        await NotifyEmployeesOfPayslipsAsync(runId, run.PayrollPeriod.Name, ct);

        await SaveWorkflowAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task MarkPaidAsync(int runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        run.MarkPaid(currentUser.RequireUserId(), Now);
        audit.Record("Payroll.MarkedPaid", nameof(PayrollRun), runId, run.Reference);
        await SaveWorkflowAsync(ct);
    }

    public async Task CancelAsync(int runId, string reason, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.Include(r => r.PayrollPeriod).Include(r => r.Employees)
            .SingleOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run", runId);
        run.Cancel(currentUser.RequireUserId(), Now, reason);
        audit.Record("Payroll.Cancelled", nameof(PayrollRun), runId, $"{run.Reference}: {reason}");
        await SaveWorkflowAsync(ct);
    }

    /// <summary>
    /// Controlled reversal of a finalized run: loan recoveries are undone, the employee/period locks are
    /// released and a correction run can then be created. The original figures remain in history.
    /// </summary>
    public async Task<int?> ReverseAsync(int runId, string reason, bool createCorrection, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var run = await db.PayrollRuns.Include(r => r.PayrollPeriod).Include(r => r.Employees)
            .SingleOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run", runId);
        var employeeIds = run.Employees.Select(e => e.EmployeeId).ToList();
        run.Reverse(currentUser.RequireUserId(), Now, reason);

        var recoveries = await db.PayrollDeductions
            .Where(d => d.PayrollEmployee.PayrollRunId == runId && d.EmployeeDeductionId != null)
            .Select(d => new { d.EmployeeDeductionId, d.Amount }).ToListAsync(ct);
        var deductionIds = recoveries.Select(r => r.EmployeeDeductionId!.Value).Distinct().ToList();
        var deductions = await db.EmployeeDeductions.IgnoreQueryFilters().Where(d => deductionIds.Contains(d.Id)).ToListAsync(ct);
        foreach (var recovery in recoveries)
            deductions.Single(d => d.Id == recovery.EmployeeDeductionId).ReverseRecovery(recovery.Amount);

        audit.Record("Payroll.Reversed", nameof(PayrollRun), runId, $"{run.Reference}: {reason}");
        await SaveWorkflowAsync(ct);

        int? correctionId = null;
        if (createCorrection)
        {
            var correction = new PayrollRun
            {
                Reference = await NextReferenceAsync(run.PayrollPeriod, ct),
                PayrollPeriodId = run.PayrollPeriodId,
                RunType = PayrollRunType.Correction,
                OriginalRunId = run.Id,
                Scope = PayrollScope.SelectedEmployees,
                Notes = $"Correction of {run.Reference}: {reason}"
            };
            foreach (var line in run.Employees)
                correction.Employees.Add(new PayrollEmployee
                {
                    EmployeeId = line.EmployeeId, PayrollPeriodId = run.PayrollPeriodId, EmployeeNumber = line.EmployeeNumber,
                    EmployeeName = line.EmployeeName, DepartmentName = line.DepartmentName, Status = PayrollLineStatus.Pending
                });
            correction.EmployeeCount = correction.Employees.Count;
            db.PayrollRuns.Add(correction);
            await db.SaveChangesAsync(ct);
            audit.Record("Payroll.CorrectionCreated", nameof(PayrollRun), correction.Id, $"{correction.Reference} corrects {run.Reference}");
            await db.SaveChangesAsync(ct);
            correctionId = correction.Id;
        }

        await transaction.CommitAsync(ct);
        return correctionId;
    }

    // ================================================================= Queries

    public async Task<PagedResult<PayrollRunListItem>> ListRunsAsync(PayrollRunQuery query, CancellationToken ct = default)
    {
        var runs = db.PayrollRuns.AsNoTracking().AsQueryable();
        if (query.Year is int year)
            runs = runs.Where(r => r.PayrollPeriod.Year == year);
        if (query.Status is PayrollRunStatus status)
            runs = runs.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
            runs = runs.Where(r => r.Reference.Contains(query.Search) || r.PayrollPeriod.Name.Contains(query.Search));

        var page = await runs
            .OrderByDescending(r => r.PayrollPeriod.Year).ThenByDescending(r => r.PayrollPeriod.Month).ThenByDescending(r => r.Id)
            .Select(ToListItem())
            .ToPagedResultAsync(query, ct);
        return page with { Items = await WithUserNamesAsync(page.Items, ct) };
    }

    public async Task<PayrollRunDetails> GetRunAsync(int runId, PayrollLineQuery lineQuery, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayrollPeriod).Include(r => r.Department)
            .Include(r => r.StatutoryConfiguration).Include(r => r.OriginalRun)
            .SingleOrDefaultAsync(r => r.Id == runId, ct) ?? throw new NotFoundException("Payroll run", runId);

        var summary = (await WithUserNamesAsync([await db.PayrollRuns.AsNoTracking().Where(r => r.Id == runId).Select(ToListItem()).SingleAsync(ct)], ct))[0];

        var lines = db.PayrollEmployees.AsNoTracking().Where(l => l.PayrollRunId == runId);
        if (!string.IsNullOrWhiteSpace(lineQuery.Search))
        {
            var term = lineQuery.Search.Trim();
            lines = lines.Where(l => l.EmployeeName.Contains(term) || l.EmployeeNumber.Contains(term) || l.DepartmentName.Contains(term));
        }
        if (lineQuery.Status is PayrollLineStatus lineStatus)
            lines = lines.Where(l => l.Status == lineStatus);
        if (lineQuery.WithWarnings == true)
            lines = lines.Where(l => l.Warnings != null);

        var linePage = await lines.OrderBy(l => l.Status == PayrollLineStatus.Error ? 0 : 1).ThenBy(l => l.EmployeeName)
            .Select(l => new PayrollLineListItem(l.Id, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.DepartmentName, l.JobTitle,
                l.BasicSalary, l.GrossEarnings, l.Paye, l.Sha, l.HousingLevy, l.Nssf, l.OtherDeductions, l.TotalDeductions, l.NetPay,
                l.Status, l.Errors, l.Warnings, l.DaysPayable, l.DaysInPeriod))
            .ToPagedResultAsync(lineQuery, ct);

        var names = await users.GetDisplayNamesAsync(new[] { run.SubmittedById, run.FinalizedById }.OfType<int>(), ct);
        return new PayrollRunDetails
        {
            Summary = summary,
            PeriodStart = run.PayrollPeriod.StartDate,
            PeriodEnd = run.PayrollPeriod.EndDate,
            PaymentDate = run.PayrollPeriod.PaymentDate,
            StatutoryConfigurationId = run.StatutoryConfigurationId,
            StatutoryVersionName = run.StatutoryConfiguration is { } c ? $"v{c.Version} — {c.Name}" : null,
            StatusReason = run.StatusReason,
            Notes = run.Notes,
            OriginalRunId = run.OriginalRunId,
            OriginalRunReference = run.OriginalRun?.Reference,
            SubmittedById = run.SubmittedById,
            CalculatedById = run.CalculatedById,
            SubmittedBy = run.SubmittedById is int s ? names.GetValueOrDefault(s) : null,
            SubmittedAt = run.SubmittedAt,
            FinalizedBy = run.FinalizedById is int f ? names.GetValueOrDefault(f) : null,
            PaidAt = run.PaidAt,
            WarningCount = await db.PayrollEmployees.CountAsync(l => l.PayrollRunId == runId && l.Warnings != null, ct),
            PayslipCount = await db.Payslips.CountAsync(p => p.PayrollEmployee.PayrollRunId == runId, ct),
            RequireSeparateApprover = await db.CompanySettings.Select(cs => (bool?)cs.RequireSeparatePayrollApprover).FirstOrDefaultAsync(ct) ?? true,
            Lines = linePage
        };
    }

    public async Task<PayrollLineDetails> GetLineAsync(int runId, int lineId, CancellationToken ct = default)
    {
        var line = await db.PayrollEmployees.AsNoTracking()
            .Include(l => l.PayrollRun).ThenInclude(r => r.PayrollPeriod)
            .Include(l => l.Earnings).Include(l => l.Deductions).Include(l => l.EmployerContributions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(l => l.Id == lineId && l.PayrollRunId == runId, ct) ?? throw new NotFoundException("Payroll line", lineId);
        return ToLineDetails(line, currentUser.HasPermission(Permissions.Employees.ViewBankDetails));
    }

    internal static PayrollLineDetails ToLineDetails(PayrollEmployee l, bool showBank)
    {
        var steps = string.IsNullOrEmpty(l.CalculationBreakdownJson)
            ? []
            : JsonSerializer.Deserialize<List<BreakdownStep>>(l.CalculationBreakdownJson, PayrollCalculationService.JsonOptions) ?? [];
        var account = l.PaymentMethod == PaymentMethod.MobileMoney ? l.MobileMoneyNumber : l.BankAccountNumber;
        return new PayrollLineDetails(
            l.Id, l.PayrollRunId, l.PayrollRun.Reference, l.PayrollRun.Status, l.PayrollRun.PayrollPeriod.Name,
            l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.DepartmentName, l.JobTitle, l.KraPin, l.NssfNumber, l.ShaNumber,
            l.PaymentMethod, l.BankName,
            account is null ? null : showBank ? account : account.Length <= 4 ? "****" : $"••••{account[^4..]}",
            l.DaysPayable, l.DaysInPeriod, l.Status,
            l.Errors?.Split('\n') ?? [], l.Warnings?.Split('\n') ?? [],
            l.Earnings.OrderBy(e => e.DisplayOrder).Select(e => new LineAmount(e.Code, e.Name, e.Amount)).ToList(),
            l.Deductions.OrderBy(d => d.Kind).ThenBy(d => d.DisplayOrder).Select(d => new LineAmount(d.Code, d.Name, d.Amount, d.Kind.ToString())).ToList(),
            l.EmployerContributions.Select(c => new LineAmount(c.Code, c.Name, c.Amount)).ToList(),
            l.GrossEarnings, l.TaxableEarnings, l.NonTaxableEarnings, l.AllowableDeductions, l.TaxableIncome,
            l.TaxBeforeReliefs, l.PersonalRelief, l.InsuranceRelief, l.Paye, l.NssfTier1, l.NssfTier2, l.Sha, l.HousingLevy,
            l.TotalDeductions, l.NetPay, l.TotalEmployerContributions, steps);
    }

    /// <summary>Totals for a month: the latest live run in the period (or zeros if none yet).</summary>
    public async Task<PeriodSummary> GetPeriodSummaryAsync(int year, int month, CancellationToken ct = default)
    {
        var name = PayrollPeriodService.Option(year, month).Name;
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var active = await db.Employees.CountAsync(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnLeave, ct);
        var newThisMonth = await db.Employees.CountAsync(e => e.EmploymentDate >= monthStart && e.EmploymentDate <= monthEnd, ct);

        var runs = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.PayrollPeriod.Year == year && r.PayrollPeriod.Month == month
                        && r.Status != PayrollRunStatus.Cancelled && r.Status != PayrollRunStatus.Reversed)
            .OrderByDescending(r => r.Id).ToListAsync(ct);
        if (runs.Count == 0)
            return new PeriodSummary(year, month, name, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, active, newThisMonth);

        var latest = runs[0];
        return new PeriodSummary(year, month, name, latest.Id, latest.Reference, latest.Status,
            runs.Sum(r => r.EmployeeCount), runs.Sum(r => r.TotalGross), runs.Sum(r => r.TotalDeductions), runs.Sum(r => r.TotalNetPay),
            runs.Sum(r => r.TotalPaye), runs.Sum(r => r.TotalSha), runs.Sum(r => r.TotalHousingLevy), runs.Sum(r => r.TotalNssf),
            runs.Sum(r => r.TotalOtherDeductions), runs.Sum(r => r.TotalEmployerContributions), active, newThisMonth);
    }

    /// <summary>Monthly totals of finalized/paid payroll for charts.</summary>
    public async Task<IReadOnlyList<PayrollTrendPoint>> GetTrendAsync(int months = 12, CancellationToken ct = default)
    {
        var rows = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid)
            .GroupBy(r => new { r.PayrollPeriod.Year, r.PayrollPeriod.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month, Gross = g.Sum(r => r.TotalGross), Deductions = g.Sum(r => r.TotalDeductions),
                Net = g.Sum(r => r.TotalNetPay), Employees = g.Sum(r => r.EmployeeCount)
            })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).Take(months)
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .Select(r => new PayrollTrendPoint(r.Year, r.Month, new DateOnly(r.Year, r.Month, 1).ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture),
                r.Gross, r.Deductions, r.Net, r.Employees))
            .ToList();
    }

    /// <summary>Full payroll register for a run (Excel).</summary>
    public async Task<(byte[] Content, string FileName)> ExportRunAsync(int runId, ISpreadsheetService spreadsheets, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayrollPeriod).SingleOrDefaultAsync(r => r.Id == runId, ct)
                  ?? throw new NotFoundException("Payroll run", runId);
        var lines = await db.PayrollEmployees.AsNoTracking().Where(l => l.PayrollRunId == runId).OrderBy(l => l.EmployeeName).ToListAsync(ct);
        var showBank = currentUser.HasPermission(Permissions.Employees.ViewBankDetails);

        var columns = new List<SpreadsheetColumn<PayrollEmployee>>
        {
            new("Employee No.", l => l.EmployeeNumber), new("Name", l => l.EmployeeName), new("Department", l => l.DepartmentName),
            new("Job Title", l => l.JobTitle), new("KRA PIN", l => l.KraPin), new("NSSF No.", l => l.NssfNumber), new("SHA No.", l => l.ShaNumber),
            new("Days Paid", l => l.DaysPayable), new("Basic", l => l.BasicSalary, "#,##0.00"), new("Gross", l => l.GrossEarnings, "#,##0.00"),
            new("Taxable Income", l => l.TaxableIncome, "#,##0.00"), new("PAYE", l => l.Paye, "#,##0.00"), new("SHIF", l => l.Sha, "#,##0.00"),
            new("Housing Levy", l => l.HousingLevy, "#,##0.00"), new("NSSF Tier I", l => l.NssfTier1, "#,##0.00"),
            new("NSSF Tier II", l => l.NssfTier2, "#,##0.00"), new("Other Deductions", l => l.OtherDeductions, "#,##0.00"),
            new("Total Deductions", l => l.TotalDeductions, "#,##0.00"), new("Net Pay", l => l.NetPay, "#,##0.00"),
            new("Employer NSSF", l => l.EmployerNssf, "#,##0.00"), new("Employer Housing Levy", l => l.EmployerHousingLevy, "#,##0.00"),
            new("Status", l => l.Status), new("Errors", l => l.Errors), new("Warnings", l => l.Warnings)
        };
        if (showBank)
        {
            columns.Insert(8, new("Payment Method", l => l.PaymentMethod));
            columns.Insert(9, new("Bank", l => l.BankName));
            columns.Insert(10, new("Account / Mobile", l => l.PaymentMethod == PaymentMethod.MobileMoney ? l.MobileMoneyNumber : l.BankAccountNumber));
        }

        audit.Record("Payroll.Exported", nameof(PayrollRun), runId, $"{run.Reference}: {lines.Count} lines (bank details: {(showBank ? "yes" : "no")})");
        await db.SaveChangesAsync(ct);
        return (spreadsheets.Export("Payroll", lines, columns, $"Payroll register — {run.Reference} ({run.PayrollPeriod.Name})"),
            $"Payroll-{run.Reference}.xlsx");
    }

    // ================================================================= Helpers

    private async Task<PayrollRun> LoadRunAsync(int runId, CancellationToken ct) =>
        await db.PayrollRuns.Include(r => r.PayrollPeriod).SingleOrDefaultAsync(r => r.Id == runId, ct)
        ?? throw new NotFoundException("Payroll run", runId);

    private async Task SaveWorkflowAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This payroll was changed by someone else. Refresh the page and try again.");
        }
    }

    private void NotifyPreparer(PayrollRun run, string title, string message, NotificationType type)
    {
        var preparer = run.SubmittedById ?? run.CalculatedById;
        if (preparer is int id && id != currentUser.UserId)
            notifications.Notify([id], title, message, type, $"/Payroll/Run/{run.Id}");
    }

    private async Task NotifyEmployeesOfPayslipsAsync(int runId, string periodName, CancellationToken ct)
    {
        var employeeIds = await db.PayrollEmployees.Where(l => l.PayrollRunId == runId && l.Status == PayrollLineStatus.Calculated)
            .Select(l => l.EmployeeId).ToListAsync(ct);
        var userIds = new List<int>();
        foreach (var employeeId in employeeIds)
            if (await users.GetUserIdForEmployeeAsync(employeeId, ct) is int userId)
                userIds.Add(userId);
        notifications.Notify(userIds, "Your payslip is ready", $"Your payslip for {periodName} is available.", NotificationType.Info, "/Payslips");
    }

    private async Task<string> NextReferenceAsync(PayrollPeriod period, CancellationToken ct)
    {
        var count = await db.PayrollRuns.CountAsync(r => r.PayrollPeriodId == period.Id, ct);
        return $"PR-{period.Year}-{period.Month:D2}-{count + 1:D2}";
    }

    private static System.Linq.Expressions.Expression<Func<PayrollRun, PayrollRunListItem>> ToListItem() => r =>
        new PayrollRunListItem(r.Id, r.Reference, r.PayrollPeriod.Year, r.PayrollPeriod.Month, r.PayrollPeriod.Name, r.RunType, r.Scope,
            r.Department != null ? r.Department.Name : null, r.Status, r.EmployeeCount, r.ErrorCount,
            r.TotalGross, r.TotalPaye, r.TotalSha, r.TotalHousingLevy, r.TotalNssf, r.TotalDeductions, r.TotalNetPay, r.TotalEmployerContributions,
            r.CreatedAt, r.CalculatedAt, r.ApprovedAt, r.FinalizedAt,
            r.CalculatedById ?? r.CreatedById, r.ApprovedById, null, null);

    private async Task<List<PayrollRunListItem>> WithUserNamesAsync(IReadOnlyList<PayrollRunListItem> items, CancellationToken ct)
    {
        var ids = items.SelectMany(i => new[] { i.ProcessedById, i.ApprovedById }).OfType<int>().Distinct().ToList();
        var names = await users.GetDisplayNamesAsync(ids, ct);
        string? Name(int? id) => id is int value ? names.GetValueOrDefault(value, "Unknown user") : null;
        return items.Select(i => i with { ProcessedBy = Name(i.ProcessedById), ApprovedBy = Name(i.ApprovedById) }).ToList();
    }

    private static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true;
}
