using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Exceptions;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Models;
using PayMaster.Application.Common.Security;
using PayMaster.Application.Notifications;
using PayMaster.Domain.Common;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Domain.ValueObjects;

namespace PayMaster.Application.Employees;

public sealed class EmployeeService(
    IApplicationDbContext db,
    EmployeeAccessScope scope,
    ICurrentUser currentUser,
    INotificationService notifications,
    IUserDirectory users,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    // ---------------------------------------------------------------- Queries

    public async Task<PagedResult<EmployeeListItemDto>> ListAsync(EmployeeQuery query, CancellationToken ct = default)
    {
        var page = await (await BuildListQueryAsync(query, ct)).ToPagedResultAsync(query, ct);
        return page with { Items = await AttachSalariesAsync(page.Items, ct) };
    }

    /// <summary>Every matching row (no paging) - used by exports.</summary>
    public async Task<IReadOnlyList<EmployeeListItemDto>> ListAllAsync(EmployeeQuery query, CancellationToken ct = default)
    {
        var rows = await (await BuildListQueryAsync(query, ct)).ToListAsync(ct);
        return await AttachSalariesAsync(rows, ct);
    }

    private async Task<IQueryable<EmployeeListItemDto>> BuildListQueryAsync(EmployeeQuery query, CancellationToken ct)
    {
        var employees = await scope.ApplyAsync(db.Employees.AsNoTracking(), ct);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            employees = employees.Where(e =>
                e.EmployeeNumber.Contains(term) || e.FirstName.Contains(term) || e.LastName.Contains(term)
                || (e.MiddleName != null && e.MiddleName.Contains(term)) || e.NationalId.Contains(term)
                || (e.KraPin != null && e.KraPin.Contains(term)) || (e.WorkEmail != null && e.WorkEmail.Contains(term))
                || e.Department.Name.Contains(term) || (e.Position != null && e.Position.Title.Contains(term)));
        }
        if (query.DepartmentId is int departmentId)
            employees = employees.Where(e => e.DepartmentId == departmentId);
        if (query.Status is EmploymentStatus status)
            employees = employees.Where(e => e.Status == status);
        if (query.EmploymentType is EmploymentType type)
            employees = employees.Where(e => e.EmploymentType == type);

        employees = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("number", false) => employees.OrderBy(e => e.EmployeeNumber),
            ("number", true) => employees.OrderByDescending(e => e.EmployeeNumber),
            ("department", false) => employees.OrderBy(e => e.Department.Name).ThenBy(e => e.LastName),
            ("department", true) => employees.OrderByDescending(e => e.Department.Name).ThenBy(e => e.LastName),
            ("hired", false) => employees.OrderBy(e => e.EmploymentDate),
            ("hired", true) => employees.OrderByDescending(e => e.EmploymentDate),
            ("status", false) => employees.OrderBy(e => e.Status).ThenBy(e => e.LastName),
            ("status", true) => employees.OrderByDescending(e => e.Status).ThenBy(e => e.LastName),
            (_, true) => employees.OrderByDescending(e => e.FirstName).ThenByDescending(e => e.LastName),
            _ => employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
        };

        return employees.Select(e => new EmployeeListItemDto(
            e.Id, e.EmployeeNumber,
            e.FirstName + " " + e.LastName,
            e.Position != null ? e.Position.Title : null,
            e.Department.Name, e.EmploymentType, e.Status, e.EmploymentDate,
            e.WorkEmail, e.PhoneNumber, null, null));
    }

    private async Task<IReadOnlyList<EmployeeListItemDto>> AttachSalariesAsync(IReadOnlyList<EmployeeListItemDto> rows, CancellationToken ct)
    {
        if (!scope.CanViewSalary || rows.Count == 0)
            return rows;

        var salaries = await CurrentSalariesAsync(rows.Select(i => i.Id).ToList(), Today, ct);
        return rows
            .Select(i => salaries.TryGetValue(i.Id, out var s) ? i with { BasicSalary = s.Basic, GrossSalary = s.Gross } : i)
            .ToList();
    }

    /// <summary>Approved salary in force on <paramref name="asOf"/> for each employee (basic and recurring gross).</summary>
    public async Task<Dictionary<int, (decimal Basic, decimal Gross)>> CurrentSalariesAsync(
        IReadOnlyCollection<int> employeeIds, DateOnly asOf, CancellationToken ct = default)
    {
        var revisions = await db.EmployeeSalaries.AsNoTracking()
            .Where(s => employeeIds.Contains(s.EmployeeId)
                        && (s.Status == SalaryRevisionStatus.Approved || s.Status == SalaryRevisionStatus.Superseded)
                        && s.EffectiveFrom <= asOf && (s.EffectiveTo == null || s.EffectiveTo >= asOf))
            .Select(s => new { s.EmployeeId, s.EffectiveFrom, s.BasicSalary, Allowances = s.Components.Sum(c => (decimal?)c.Amount) ?? 0m })
            .ToListAsync(ct);

        return revisions
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g =>
            {
                var current = g.OrderByDescending(r => r.EffectiveFrom).First();
                return (current.BasicSalary, Money.Round(current.BasicSalary + current.Allowances));
            });
    }

    public async Task<EmployeeDetailsDto> GetAsync(int id, CancellationToken ct = default)
    {
        await scope.EnsureCanViewAsync(id, ct);

        var e = await db.Employees.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.Manager)
            .Include(x => x.DirectReports).ThenInclude(r => r.Position)
            .Include(x => x.EmploymentHistory)
            .Include(x => x.Documents)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == id, ct);

        var isSelf = currentUser.EmployeeId == id;
        var canViewSalary = scope.CanViewSalary || isSelf;
        var canViewBank = scope.CanViewBankDetails || isSelf;

        IReadOnlyList<SalaryRevisionDto> salaryHistory = [];
        IReadOnlyList<RecurringDeductionDto> deductions = [];
        if (canViewSalary)
        {
            var revisions = await db.EmployeeSalaries.AsNoTracking()
                .Include(s => s.SalaryStructure)
                .Include(s => s.Components).ThenInclude(c => c.SalaryComponent)
                .Where(s => s.EmployeeId == id)
                .OrderByDescending(s => s.EffectiveFrom).ThenByDescending(s => s.Id)
                .ToListAsync(ct);
            salaryHistory = revisions.Select(ToDto).ToList();

            deductions = await db.EmployeeDeductions.AsNoTracking()
                .Where(d => d.EmployeeId == id)
                .OrderByDescending(d => d.IsActive).ThenBy(d => d.StartDate)
                .Select(d => new RecurringDeductionDto(d.Id, d.SalaryComponent.Name, d.MonthlyAmount, d.TotalAmount,
                    d.AmountRecovered, d.TotalAmount == null ? null : d.TotalAmount - d.AmountRecovered,
                    d.StartDate, d.EndDate, d.Reference, d.IsActive))
                .ToListAsync(ct);
        }

        var hasLogin = e.UserId is not null;

        return new EmployeeDetailsDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            FullName = e.FullName,
            Gender = e.Gender,
            DateOfBirth = e.DateOfBirth,
            NationalId = e.NationalId,
            KraPin = e.KraPin,
            NssfNumber = e.NssfNumber,
            ShaNumber = e.ShaNumber,
            PersonalEmail = e.PersonalEmail,
            WorkEmail = e.WorkEmail,
            PhoneNumber = e.PhoneNumber,
            PhysicalAddress = e.PhysicalAddress,
            EmergencyContactName = e.EmergencyContactName,
            EmergencyContactRelationship = e.EmergencyContactRelationship,
            EmergencyContactPhone = e.EmergencyContactPhone,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.Department.Name,
            PositionId = e.PositionId,
            JobTitle = e.Position?.Title,
            EmploymentType = e.EmploymentType,
            Status = e.Status,
            EmploymentDate = e.EmploymentDate,
            ContractEndDate = e.ContractEndDate,
            TerminationDate = e.TerminationDate,
            TerminationReason = e.TerminationReason,
            ManagerId = e.ManagerId,
            ManagerName = e.Manager?.FullName,
            WorkLocation = e.WorkLocation,
            PayFrequency = e.PayFrequency,
            PaymentMethod = e.PaymentMethod,
            BankName = e.BankName,
            BankBranch = e.BankBranch,
            BankAccountNumber = canViewBank ? e.BankAccountNumber : Mask(e.BankAccountNumber),
            MobileMoneyNumber = canViewBank ? e.MobileMoneyNumber : Mask(e.MobileMoneyNumber),
            BankDetailsMasked = !canViewBank,
            HasDisabilityExemption = e.HasDisabilityExemption,
            DisabilityExemptionCertificateNumber = e.DisabilityExemptionCertificateNumber,
            HasLogin = hasLogin,
            CreatedAt = e.CreatedAt,
            CanViewSalary = canViewSalary,
            SalaryHistory = salaryHistory,
            CurrentSalary = salaryHistory.FirstOrDefault(s =>
                s.Status is SalaryRevisionStatus.Approved or SalaryRevisionStatus.Superseded
                && s.EffectiveFrom <= Today && (s.EffectiveTo is null || s.EffectiveTo >= Today)),
            PendingSalary = salaryHistory.FirstOrDefault(s => s.Status == SalaryRevisionStatus.PendingApproval),
            RecurringDeductions = deductions,
            EmploymentHistory = e.EmploymentHistory
                .OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id)
                .Select(h => new EmploymentHistoryDto(h.ChangeType, h.EffectiveDate, h.FromDepartment, h.ToDepartment,
                    h.FromPosition, h.ToPosition, h.FromStatus, h.ToStatus, h.Notes, h.CreatedAt))
                .ToList(),
            Documents = e.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new EmployeeDocumentDto(d.Id, d.DocumentType, d.Title, d.OriginalFileName, d.ContentType,
                    d.SizeBytes, d.ExpiryDate, d.CreatedAt))
                .ToList(),
            DirectReports = e.DirectReports
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.FirstName)
                .Select(r => (r.Id, r.FullName, r.Position?.Title))
                .ToList()
        };
    }

    public async Task<EmployeeInput> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Employee", id);
        return new EmployeeInput
        {
            FirstName = e.FirstName, MiddleName = e.MiddleName, LastName = e.LastName, Gender = e.Gender,
            DateOfBirth = e.DateOfBirth, NationalId = e.NationalId, KraPin = e.KraPin, NssfNumber = e.NssfNumber,
            ShaNumber = e.ShaNumber, PersonalEmail = e.PersonalEmail, WorkEmail = e.WorkEmail, PhoneNumber = e.PhoneNumber,
            PhysicalAddress = e.PhysicalAddress, EmergencyContactName = e.EmergencyContactName,
            EmergencyContactRelationship = e.EmergencyContactRelationship, EmergencyContactPhone = e.EmergencyContactPhone,
            DepartmentId = e.DepartmentId, PositionId = e.PositionId, EmploymentType = e.EmploymentType,
            EmploymentDate = e.EmploymentDate, ContractEndDate = e.ContractEndDate, ManagerId = e.ManagerId,
            WorkLocation = e.WorkLocation, PaymentMethod = e.PaymentMethod, BankName = e.BankName, BankBranch = e.BankBranch,
            BankAccountNumber = scope.CanViewBankDetails ? e.BankAccountNumber : Mask(e.BankAccountNumber),
            KeepExistingBankAccount = !scope.CanViewBankDetails && e.BankAccountNumber is not null,
            MobileMoneyNumber = e.MobileMoneyNumber, HasDisabilityExemption = e.HasDisabilityExemption,
            DisabilityExemptionCertificateNumber = e.DisabilityExemptionCertificateNumber
        };
    }

    public async Task<EmployeeLookups> GetLookupsAsync(CancellationToken ct = default)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new LookupItem(d.Id, d.Name, d.Code)).ToListAsync(ct);
        var positions = await db.Positions.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Title)
            .Select(p => new LookupItem(p.Id, p.Title, p.DepartmentId.ToString())).ToListAsync(ct);
        var managers = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnLeave)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName, e.Position != null ? e.Position.Title : null))
            .ToListAsync(ct);
        var structures = await db.SalaryStructures.AsNoTracking()
            .Where(s => s.Status == SalaryStructureStatus.Active).OrderBy(s => s.Name)
            .Select(s => new LookupItem(s.Id, s.Name, s.BasicSalary.ToString())).ToListAsync(ct);
        return new EmployeeLookups(departments, positions, managers, structures);
    }

    // ---------------------------------------------------------------- Commands

    public Task<int> CreateAsync(EmployeeInput input, CancellationToken ct = default) =>
        CreateCoreAsync(input, notifySalaryApprovers: true, ct);

    internal async Task<int> CreateCoreAsync(EmployeeInput input, bool notifySalaryApprovers, CancellationToken ct)
    {
        await EnsureUniqueAsync(input, null, ct);
        var (department, position) = await ResolveAssignmentAsync(input, ct);
        await EnsureManagerAsync(input.ManagerId, null, ct);

        var employee = new Employee { EmployeeNumber = await NextEmployeeNumberAsync(ct) };
        Apply(employee, input, isCreate: true);
        employee.DepartmentId = department.Id;
        employee.EmploymentHistory.Add(new EmploymentHistory
        {
            ChangeType = EmploymentChangeType.Hired,
            EffectiveDate = employee.EmploymentDate,
            ToDepartment = department.Name,
            ToPosition = position?.Title,
            ToStatus = EmploymentStatus.Active,
            Notes = "Employee record created"
        });

        if (input.BasicSalary is decimal basic)
        {
            employee.SalaryHistory.Add(await BuildInitialSalaryAsync(input, basic, employee.EmploymentDate, ct));
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        if (input.BasicSalary is not null && notifySalaryApprovers)
        {
            await notifications.NotifyPermissionHoldersAsync(Permissions.Salary.Approve,
                "Salary awaiting approval",
                $"Starting salary for {employee.FullName} ({employee.EmployeeNumber}) needs approval.",
                NotificationType.Warning, $"/Employees/Details/{employee.Id}#salary");
            await db.SaveChangesAsync(ct);
        }

        return employee.Id;
    }

    public async Task UpdateAsync(int id, EmployeeInput input, CancellationToken ct = default)
    {
        var employee = await db.Employees
            .Include(e => e.Department)
            .Include(e => e.Position).ThenInclude(p => p!.SalaryGrade)
            .SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee", id);

        await EnsureUniqueAsync(input, id, ct);
        var (department, position) = await ResolveAssignmentAsync(input, ct);
        await EnsureManagerAsync(input.ManagerId, id, ct);

        var departmentChanged = employee.DepartmentId != department.Id;
        var positionChanged = employee.PositionId != position?.Id;
        if (departmentChanged || positionChanged)
        {
            employee.EmploymentHistory.Add(new EmploymentHistory
            {
                ChangeType = ClassifyMove(employee, departmentChanged, position),
                EffectiveDate = Today,
                FromDepartment = employee.Department.Name,
                ToDepartment = department.Name,
                FromPosition = employee.Position?.Title,
                ToPosition = position?.Title
            });
        }

        Apply(employee, input, isCreate: false);
        employee.DepartmentId = department.Id;
        employee.Department = department;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, string reason, CancellationToken ct = default)
    {
        var employee = await LoadAsync(id, ct);
        var from = employee.Status;
        employee.Deactivate();
        AddStatusHistory(employee, from, EmploymentChangeType.StatusChange, Today, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(int id, CancellationToken ct = default)
    {
        var employee = await LoadAsync(id, ct);
        var from = employee.Status;
        employee.Reactivate();
        AddStatusHistory(employee, from, EmploymentChangeType.Reactivation, Today, "Reactivated");
        await db.SaveChangesAsync(ct);
    }

    public async Task SetOnLeaveAsync(int id, bool onLeave, CancellationToken ct = default)
    {
        var employee = await LoadAsync(id, ct);
        var from = employee.Status;
        if (onLeave && from != EmploymentStatus.Active)
            throw new DomainException("Only active employees can be placed on leave.");
        if (!onLeave && from != EmploymentStatus.OnLeave)
            throw new DomainException("Employee is not on leave.");
        employee.Status = onLeave ? EmploymentStatus.OnLeave : EmploymentStatus.Active;
        AddStatusHistory(employee, from, EmploymentChangeType.StatusChange, Today, onLeave ? "Placed on leave" : "Returned from leave");
        await db.SaveChangesAsync(ct);
    }

    public async Task TerminateAsync(int id, DateOnly date, string reason, CancellationToken ct = default)
    {
        var employee = await LoadAsync(id, ct);
        var from = employee.Status;
        employee.Terminate(date, reason);
        AddStatusHistory(employee, from, EmploymentChangeType.Termination, date, reason);
        await db.SaveChangesAsync(ct);

        if (employee.UserId is int userId)
            await users.DisableLoginAsync(userId, ct);
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<Employee> LoadAsync(int id, CancellationToken ct) =>
        await db.Employees.SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee", id);

    private static void AddStatusHistory(Employee employee, EmploymentStatus from, EmploymentChangeType type, DateOnly date, string notes) =>
        employee.EmploymentHistory.Add(new EmploymentHistory
        {
            ChangeType = type, EffectiveDate = date, FromStatus = from, ToStatus = employee.Status, Notes = notes
        });

    private static EmploymentChangeType ClassifyMove(Employee employee, bool departmentChanged, Domain.Entities.Organization.Position? newPosition)
    {
        var oldMin = employee.Position?.SalaryGrade?.MinimumSalary;
        var newMin = newPosition?.SalaryGrade?.MinimumSalary;
        if (oldMin is not null && newMin is not null && oldMin != newMin)
            return newMin > oldMin ? EmploymentChangeType.Promotion : EmploymentChangeType.Demotion;
        return departmentChanged ? EmploymentChangeType.Transfer : EmploymentChangeType.Promotion;
    }

    private void Apply(Employee e, EmployeeInput input, bool isCreate)
    {
        e.FirstName = input.FirstName.Trim();
        e.MiddleName = Clean(input.MiddleName);
        e.LastName = input.LastName.Trim();
        e.Gender = input.Gender;
        e.DateOfBirth = input.DateOfBirth!.Value;
        e.NationalId = input.NationalId.Trim();
        e.KraPin = Clean(input.KraPin)?.ToUpperInvariant();
        e.NssfNumber = Clean(input.NssfNumber);
        e.ShaNumber = Clean(input.ShaNumber);
        e.PersonalEmail = Clean(input.PersonalEmail)?.ToLowerInvariant();
        e.WorkEmail = Clean(input.WorkEmail)?.ToLowerInvariant();
        e.PhoneNumber = NormalizePhone(input.PhoneNumber);
        e.PhysicalAddress = Clean(input.PhysicalAddress);
        e.EmergencyContactName = Clean(input.EmergencyContactName);
        e.EmergencyContactRelationship = Clean(input.EmergencyContactRelationship);
        e.EmergencyContactPhone = NormalizePhone(input.EmergencyContactPhone);
        e.PositionId = input.PositionId;
        e.EmploymentType = input.EmploymentType;
        e.EmploymentDate = input.EmploymentDate!.Value;
        e.ContractEndDate = input.EmploymentType == EmploymentType.Contract ? input.ContractEndDate : null;
        e.ManagerId = input.ManagerId;
        e.WorkLocation = Clean(input.WorkLocation);
        e.PaymentMethod = input.PaymentMethod;
        e.BankName = Clean(input.BankName);
        e.BankBranch = Clean(input.BankBranch);
        if (isCreate || !input.KeepExistingBankAccount)
            e.BankAccountNumber = Clean(input.BankAccountNumber)?.Replace(" ", "");
        e.MobileMoneyNumber = NormalizePhone(input.MobileMoneyNumber);
        e.HasDisabilityExemption = input.HasDisabilityExemption;
        e.DisabilityExemptionCertificateNumber = input.HasDisabilityExemption ? Clean(input.DisabilityExemptionCertificateNumber) : null;
    }

    private async Task EnsureUniqueAsync(EmployeeInput input, int? excludeId, CancellationToken ct)
    {
        var nationalId = input.NationalId.Trim();
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.NationalId == nationalId && e.Id != excludeId, ct))
            throw new ConflictException($"An employee with National ID {nationalId} already exists.");

        var kraPin = Clean(input.KraPin)?.ToUpperInvariant();
        if (kraPin is not null && await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.KraPin == kraPin && e.Id != excludeId, ct))
            throw new ConflictException($"An employee with KRA PIN {kraPin} already exists.");
    }

    private async Task<(Domain.Entities.Organization.Department Department, Domain.Entities.Organization.Position? Position)>
        ResolveAssignmentAsync(EmployeeInput input, CancellationToken ct)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == input.DepartmentId, ct)
                         ?? throw new DomainException("The selected department does not exist.");
        if (!department.IsActive)
            throw new DomainException($"Department {department.Name} is inactive.");

        Domain.Entities.Organization.Position? position = null;
        if (input.PositionId is int positionId)
        {
            position = await db.Positions.Include(p => p.SalaryGrade).SingleOrDefaultAsync(p => p.Id == positionId, ct)
                       ?? throw new DomainException("The selected job title does not exist.");
            if (position.DepartmentId is not null && position.DepartmentId != department.Id)
                throw new DomainException($"{position.Title} belongs to a different department.");
        }
        return (department, position);
    }

    private async Task EnsureManagerAsync(int? managerId, int? employeeId, CancellationToken ct)
    {
        if (managerId is null)
            return;
        if (managerId == employeeId)
            throw new DomainException("An employee cannot report to themselves.");
        if (!await db.Employees.AnyAsync(e => e.Id == managerId, ct))
            throw new DomainException("The selected reporting manager does not exist.");

        // Prevent reporting cycles (A → B → A).
        var cursor = managerId;
        for (var depth = 0; cursor is not null && depth < 50; depth++)
        {
            if (cursor == employeeId)
                throw new DomainException("This reporting line would create a cycle.");
            cursor = await db.Employees.Where(e => e.Id == cursor).Select(e => e.ManagerId).FirstOrDefaultAsync(ct);
        }
    }

    private async Task<EmployeeSalary> BuildInitialSalaryAsync(EmployeeInput input, decimal basic, DateOnly employmentDate, CancellationToken ct)
    {
        var salary = new EmployeeSalary
        {
            BasicSalary = Money.Round(basic),
            EffectiveFrom = input.SalaryEffectiveDate ?? employmentDate,
            Status = SalaryRevisionStatus.PendingApproval,
            ChangeReason = "Starting salary"
        };

        if (input.SalaryStructureId is int structureId)
        {
            var structure = await db.SalaryStructures
                .Include(s => s.Components).ThenInclude(c => c.SalaryComponent)
                .SingleOrDefaultAsync(s => s.Id == structureId && s.Status == SalaryStructureStatus.Active, ct)
                ?? throw new DomainException("The selected salary structure is not active.");

            salary.SalaryStructureId = structure.Id;
            var sized = new SalaryStructure { BasicSalary = salary.BasicSalary };
            foreach (var line in structure.Components.Where(c => c.SalaryComponent.IsEarning))
                salary.Components.Add(new EmployeeSalaryComponent
                {
                    SalaryComponentId = line.SalaryComponentId,
                    Amount = sized.ResolveAmount(line)
                });
        }

        return salary;
    }

    private async Task<string> NextEmployeeNumberAsync(CancellationToken ct)
    {
        var prefix = await db.CompanySettings.Select(c => c.EmployeeNumberPrefix).FirstOrDefaultAsync(ct) ?? "EMP";
        var numbers = await db.Employees.IgnoreQueryFilters()
            .Where(e => e.EmployeeNumber.StartsWith(prefix + "-"))
            .Select(e => e.EmployeeNumber)
            .ToListAsync(ct);
        var max = numbers
            .Select(n => int.TryParse(n[(prefix.Length + 1)..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}-{max + 1:0000}";
    }

    internal static SalaryRevisionDto ToDto(EmployeeSalary s) => new(
        s.Id, s.BasicSalary, s.GrossSalary, s.EffectiveFrom, s.EffectiveTo, s.Status, s.ChangeReason,
        s.SalaryStructure?.Name,
        s.Components.OrderBy(c => c.SalaryComponent.DisplayOrder).Select(c => (c.SalaryComponent.Name, c.Amount)).ToList(),
        s.CreatedAt, s.ApprovedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Normalises Kenyan numbers to +2547XXXXXXXX / +2541XXXXXXXX.</summary>
    public static string? NormalizePhone(string? phone)
    {
        var digits = Clean(phone)?.Replace(" ", "").Replace("-", "");
        if (digits is null)
            return null;
        if (digits.StartsWith('0') && digits.Length == 10)
            return "+254" + digits[1..];
        if (digits.StartsWith("254"))
            return "+" + digits;
        return digits;
    }

    private static string? Mask(string? value) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= 4 ? "****" : $"••••{value[^4..]}";
}
