using PayMaster.Application.Common.Security;
using PayMaster.Application.Employees;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payroll;
using PayMaster.Application.Salary;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Services;
using PayMaster.Tests.TestSupport;

namespace PayMaster.Tests.IntegrationTests.Employees;

/// <summary>One MySQL database per test class with a small organisation: two departments, positions and a structure.</summary>
public sealed class EmployeeFixture : IDisposable
{
    public MySqlTestDatabase Db { get; } = new();
    public FakeUserDirectory Users { get; } = new();
    public int Engineering { get; }
    public int Finance { get; }
    public int SoftwareEngineer { get; }
    public int SeniorEngineer { get; }
    public int Accountant { get; }
    public int EngineerStructure { get; }

    private int _sequence = 10_000_000;

    public EmployeeFixture()
    {
        using var ctx = Db.CreateContext();
        ctx.CompanySettings.Add(new CompanySetting { CompanyName = "Test Co", EmployeeNumberPrefix = "EMP" });

        var g4 = new SalaryGrade { Code = "G4", Name = "Grade 4", MinimumSalary = 70_000, MaximumSalary = 130_000 };
        var g6 = new SalaryGrade { Code = "G6", Name = "Grade 6", MinimumSalary = 180_000, MaximumSalary = 320_000 };
        var eng = new Department { Code = "ENG", Name = "Engineering" };
        var fin = new Department { Code = "FIN", Name = "Finance" };
        var swe = new Position { Code = "SWE", Title = "Software Engineer", Department = eng, SalaryGrade = g4 };
        var sswe = new Position { Code = "SSWE", Title = "Senior Software Engineer", Department = eng, SalaryGrade = g6 };
        var acc = new Position { Code = "ACC", Title = "Accountant", Department = fin, SalaryGrade = g4 };

        var house = new SalaryComponent { Code = "HOUSE", Name = "Housing Allowance", Type = ComponentType.Earning, Category = ComponentCategory.Allowance, TaxTreatment = TaxTreatment.Taxable };
        var transport = new SalaryComponent { Code = "TRANSPORT", Name = "Transport Allowance", Type = ComponentType.Earning, Category = ComponentCategory.Allowance, TaxTreatment = TaxTreatment.Taxable };
        var structure = new SalaryStructure
        {
            Code = "SS-SWE", Name = "Software Engineer", Status = SalaryStructureStatus.Active, BasicSalary = 100_000,
            EffectiveFrom = new DateOnly(2025, 1, 1),
            Components =
            [
                new SalaryStructureComponent { SalaryComponent = house, AmountType = AmountType.PercentageOfBasic, Amount = 20 },
                new SalaryStructureComponent { SalaryComponent = transport, AmountType = AmountType.Fixed, Amount = 10_000 }
            ]
        };

        ctx.AddRange(g4, g6, eng, fin, swe, sswe, acc, structure);
        ctx.SaveChanges();

        (Engineering, Finance, SoftwareEngineer, SeniorEngineer, Accountant, EngineerStructure) =
            (eng.Id, fin.Id, swe.Id, sswe.Id, acc.Id, structure.Id);
    }

    public EmployeeService Employees(ApplicationDbContext ctx) =>
        new(ctx, Scope(ctx), Db.User, new NotificationService(ctx, Users, Db.Clock), Users, Db.Clock);

    public EmployeeAccessScope Scope(ApplicationDbContext ctx) => new(ctx, Db.User);

    public EmployeeImportExportService ImportExport(ApplicationDbContext ctx) =>
        new(ctx, Employees(ctx), Scope(ctx), new EmployeeInputValidator(Db.Clock), new ClosedXmlSpreadsheetService(),
            new NotificationService(ctx, Users, Db.Clock), new AuditService(ctx, Db.User, Db.Clock));

    public SalaryChangeService SalaryChanges(ApplicationDbContext ctx) =>
        new(ctx, Db.User, new NotificationService(ctx, Users, Db.Clock), Users, new AuditService(ctx, Db.User, Db.Clock), Db.Clock);

    public SalaryStructureService Structures(ApplicationDbContext ctx) =>
        new(ctx, SalaryChanges(ctx), new NotificationService(ctx, Users, Db.Clock), new AuditService(ctx, Db.User, Db.Clock), Db.Clock);

    public PayItemService PayItems(ApplicationDbContext ctx) => new(ctx, new PayrollPeriodService(ctx, Db.Clock), Db.Clock);

    /// <summary>Signs in as an HR administrator (sees everyone, including bank details).</summary>
    public void AsHr()
    {
        Db.User.UserId = 1;
        Db.User.EmployeeId = null;
        Db.User.Permissions.Clear();
        foreach (var p in Permissions.DefaultRolePermissions[Roles.HrAdmin])
            Db.User.Permissions.Add(p);
    }

    public void As(string role, int? employeeId, int userId = 50)
    {
        Db.User.UserId = userId;
        Db.User.EmployeeId = employeeId;
        Db.User.Permissions.Clear();
        foreach (var p in Permissions.DefaultRolePermissions[role])
            Db.User.Permissions.Add(p);
    }

    /// <summary>A valid input with unique identifiers.</summary>
    public EmployeeInput NewInput(int? departmentId = null, int? positionId = null, string first = "Jane", string last = "Doe")
    {
        var n = Interlocked.Increment(ref _sequence);
        return new EmployeeInput
        {
            FirstName = first,
            LastName = last,
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1993, 3, 14),
            NationalId = n.ToString(),
            KraPin = $"A{n:D9}"[..10] + "K",
            DepartmentId = departmentId ?? Engineering,
            PositionId = positionId,
            EmploymentType = EmploymentType.Permanent,
            EmploymentDate = new DateOnly(2026, 1, 1),
            PaymentMethod = PaymentMethod.BankTransfer,
            BankName = "Equity Bank",
            BankAccountNumber = $"01{n}"
        };
    }

    public void Dispose() => Db.Dispose();
}
