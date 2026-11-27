using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Security;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Infrastructure.Persistence.Seed;

/// <summary>
/// Realistic, deterministic development data: departments, grades, positions, salary
/// structures, ~48 employees with approved salary history, recurring deductions and
/// one login per role. Never runs unless <c>Seed:DemoData</c> is true.
/// </summary>
internal sealed class DemoDataSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger logger)
{
    private readonly Random _random = new(2026);

    private static readonly string[] Banks =
        ["Equity Bank", "KCB Bank", "Co-operative Bank", "NCBA Bank", "Stanbic Bank", "Absa Bank Kenya", "I&M Bank", "Standard Chartered"];

    private static readonly string[] Branches = ["Westlands", "Moi Avenue", "Upper Hill", "Kilimani", "Thika Road Mall", "Karen", "Mombasa Road"];

    // (Code, Name, Min, Max, TransportAllowance, MedicalAllowance)
    private static readonly (string Code, string Name, decimal Min, decimal Max, decimal Transport, decimal Medical)[] Grades =
    [
        ("G1", "Grade 1 - Support", 15_000, 30_000, 2_000, 0),
        ("G2", "Grade 2 - Associate", 30_000, 50_000, 3_000, 0),
        ("G3", "Grade 3 - Officer", 45_000, 80_000, 5_000, 2_000),
        ("G4", "Grade 4 - Senior Officer", 70_000, 130_000, 8_000, 4_000),
        ("G5", "Grade 5 - Specialist", 110_000, 200_000, 10_000, 5_000),
        ("G6", "Grade 6 - Lead / Manager", 180_000, 320_000, 15_000, 8_000),
        ("G7", "Grade 7 - Senior Manager", 280_000, 500_000, 20_000, 10_000)
    ];

    // (DeptCode, DeptName, [(PositionCode, Title, Grade)])
    private static readonly (string Code, string Name, (string Code, string Title, string Grade)[] Positions)[] Departments =
    [
        ("ENG", "Engineering", [("SWE", "Software Engineer", "G5"), ("SSWE", "Senior Software Engineer", "G6"), ("QAE", "QA Engineer", "G4"), ("ENGM", "Engineering Manager", "G7")]),
        ("PRD", "Product", [("PM", "Product Manager", "G6"), ("PD", "Product Designer", "G5")]),
        ("FIN", "Finance", [("ACC", "Accountant", "G4"), ("PAYO", "Payroll Officer", "G4"), ("FINM", "Finance Manager", "G7")]),
        ("HR", "Human Resources", [("HRO", "HR Officer", "G4"), ("HRM", "HR Manager", "G6")]),
        ("SAL", "Sales", [("SE", "Sales Executive", "G3"), ("SM", "Sales Manager", "G6")]),
        ("OPS", "Operations", [("OPO", "Operations Officer", "G3"), ("CSA", "Customer Support Agent", "G2"), ("OA", "Office Assistant", "G1")]),
        ("MKT", "Marketing", [("MO", "Marketing Officer", "G4"), ("MM", "Marketing Manager", "G6")])
    ];

    // (First, Last, Gender, Dept, Position, Basic or 0 for random within grade)
    private static readonly (string First, string Last, Gender Gender, string Dept, string Position, decimal Basic)[] People =
    [
        ("John", "Mwangi", Gender.Male, "ENG", "SWE", 100_000),
        ("Mary", "Wanjiku", Gender.Female, "PRD", "PM", 150_000),
        ("David", "Ochieng", Gender.Male, "FIN", "ACC", 80_000),
        ("Grace", "Njeri", Gender.Female, "HR", "HRO", 70_000),
        ("Peter", "Kamau", Gender.Male, "SAL", "SE", 55_000),
        ("James", "Otieno", Gender.Male, "ENG", "ENGM", 380_000),
        ("Faith", "Achieng", Gender.Female, "FIN", "PAYO", 85_000),
        ("Lucy", "Wambui", Gender.Female, "HR", "HRM", 240_000),
        ("Brian", "Kiprono", Gender.Male, "ENG", "SSWE", 0),
        ("Esther", "Nyambura", Gender.Female, "ENG", "SWE", 0),
        ("Kevin", "Mutua", Gender.Male, "ENG", "SWE", 0),
        ("Diana", "Chebet", Gender.Female, "ENG", "QAE", 0),
        ("Samuel", "Kariuki", Gender.Male, "ENG", "SSWE", 0),
        ("Ann", "Muthoni", Gender.Female, "ENG", "SWE", 0),
        ("Dennis", "Omondi", Gender.Male, "ENG", "QAE", 0),
        ("Joyce", "Akinyi", Gender.Female, "PRD", "PD", 0),
        ("Victor", "Kibet", Gender.Male, "PRD", "PD", 0),
        ("Caroline", "Wairimu", Gender.Female, "FIN", "FINM", 0),
        ("George", "Njoroge", Gender.Male, "FIN", "ACC", 0),
        ("Mercy", "Atieno", Gender.Female, "FIN", "ACC", 0),
        ("Collins", "Rotich", Gender.Male, "SAL", "SM", 0),
        ("Janet", "Moraa", Gender.Female, "SAL", "SE", 0),
        ("Stephen", "Maina", Gender.Male, "SAL", "SE", 0),
        ("Purity", "Jepkosgei", Gender.Female, "SAL", "SE", 0),
        ("Moses", "Wekesa", Gender.Male, "SAL", "SE", 0),
        ("Beatrice", "Nduta", Gender.Female, "SAL", "SE", 0),
        ("Paul", "Odhiambo", Gender.Male, "OPS", "OPO", 0),
        ("Nancy", "Wangari", Gender.Female, "OPS", "OPO", 0),
        ("Erick", "Kimani", Gender.Male, "OPS", "CSA", 0),
        ("Sharon", "Cherono", Gender.Female, "OPS", "CSA", 0),
        ("Joseph", "Mwaura", Gender.Male, "OPS", "CSA", 0),
        ("Irene", "Adhiambo", Gender.Female, "OPS", "CSA", 0),
        ("Martin", "Langat", Gender.Male, "OPS", "OA", 0),
        ("Agnes", "Wanjiru", Gender.Female, "OPS", "OA", 0),
        ("Daniel", "Koech", Gender.Male, "MKT", "MM", 0),
        ("Cynthia", "Awino", Gender.Female, "MKT", "MO", 0),
        ("Timothy", "Gitau", Gender.Male, "MKT", "MO", 0),
        ("Rose", "Kerubo", Gender.Female, "HR", "HRO", 0),
        ("Felix", "Ouma", Gender.Male, "ENG", "SWE", 0),
        ("Winnie", "Jeruto", Gender.Female, "ENG", "QAE", 0),
        ("Antony", "Macharia", Gender.Male, "FIN", "ACC", 0),
        ("Lilian", "Nyokabi", Gender.Female, "PRD", "PD", 0),
        ("Hillary", "Kipchumba", Gender.Male, "SAL", "SE", 0),
        ("Eunice", "Mumbi", Gender.Female, "MKT", "MO", 0),
        ("Patrick", "Waweru", Gender.Male, "OPS", "OPO", 0),
        ("Gladys", "Kemunto", Gender.Female, "OPS", "CSA", 0),
        ("Ian", "Muriuki", Gender.Male, "ENG", "SWE", 0),
        ("Sylvia", "Anyango", Gender.Female, "SAL", "SE", 0)
    ];

    public async Task SeedAsync(ApplicationUser? admin, CancellationToken ct)
    {
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(ct))
            return;

        logger.LogInformation("Seeding demo organisation data");
        var adminId = admin?.Id;
        var components = await db.SalaryComponents.ToDictionaryAsync(c => c.Code, ct);

        var grades = Grades.ToDictionary(g => g.Code, g => new SalaryGrade
        {
            Code = g.Code, Name = g.Name, MinimumSalary = g.Min, MaximumSalary = g.Max
        });
        db.SalaryGrades.AddRange(grades.Values);

        var departments = new Dictionary<string, Department>();
        var positions = new Dictionary<string, Position>();
        foreach (var (code, name, deptPositions) in Departments)
        {
            var department = new Department { Code = code, Name = name, Description = $"{name} department" };
            departments[code] = department;
            foreach (var (posCode, title, grade) in deptPositions)
            {
                var position = new Position { Code = posCode, Title = title, Department = department, SalaryGrade = grades[grade] };
                positions[posCode] = position;
            }
        }
        db.Departments.AddRange(departments.Values);
        db.Positions.AddRange(positions.Values);

        // Reusable salary structures, one per position, with grade-appropriate allowances.
        var structures = new Dictionary<string, SalaryStructure>();
        foreach (var (_, _, deptPositions) in Departments)
        {
            foreach (var (posCode, title, gradeCode) in deptPositions)
            {
                var grade = Grades.Single(g => g.Code == gradeCode);
                var structure = new SalaryStructure
                {
                    Code = $"SS-{posCode}",
                    Name = title,
                    Status = SalaryStructureStatus.Active,
                    BasicSalary = RoundTo(grade.Min + (grade.Max - grade.Min) / 3m, 1_000m),
                    EffectiveFrom = new DateOnly(2025, 1, 1),
                    SalaryGrade = grades[gradeCode],
                    Position = positions[posCode],
                    Department = positions[posCode].Department,
                    Components =
                    [
                        new SalaryStructureComponent { SalaryComponent = components["HOUSE"], AmountType = AmountType.PercentageOfBasic, Amount = 15m },
                        new SalaryStructureComponent { SalaryComponent = components["TRANSPORT"], Amount = grade.Transport }
                    ]
                };
                if (grade.Medical > 0)
                    structure.Components.Add(new SalaryStructureComponent { SalaryComponent = components["MEDICAL"], Amount = grade.Medical });
                structures[posCode] = structure;
            }
        }
        db.SalaryStructures.AddRange(structures.Values);
        await db.SaveChangesAsync(ct);

        var employees = new List<Employee>();
        for (var i = 0; i < People.Length; i++)
        {
            var person = People[i];
            var position = positions[person.Position];
            var grade = Grades.Single(g => g.Code == position.SalaryGrade!.Code);
            var structure = structures[person.Position];
            var hired = i < 8
                ? new DateOnly(2019 + i % 5, 1 + (i * 3) % 12, 1)
                : new DateOnly(2019 + _random.Next(0, 7), _random.Next(1, 13), _random.Next(1, 28));
            if (hired > new DateOnly(2026, 8, 31))
                hired = new DateOnly(2026, 3, 1);

            var employee = new Employee
            {
                EmployeeNumber = $"EMP-{i + 1:0000}",
                FirstName = person.First,
                LastName = person.Last,
                Gender = person.Gender,
                DateOfBirth = new DateOnly(_random.Next(1975, 2001), _random.Next(1, 13), _random.Next(1, 28)),
                NationalId = (20_000_000 + i * 104_729 + _random.Next(0, 9_999)).ToString(),
                KraPin = $"A{_random.Next(0, 10)}{(10_000_000 + i * 97_213):D8}{(char)('A' + i % 26)}".Substring(0, 11),
                NssfNumber = (2_000_000_000L + i * 7_919L).ToString(),
                ShaNumber = $"CR{(5_000_000_000L + i * 31_337L)}",
                PersonalEmail = $"{person.First}.{person.Last}@example.com".ToLowerInvariant(),
                WorkEmail = $"{person.First}.{person.Last}@paymaster.local".ToLowerInvariant(),
                PhoneNumber = $"+2547{_random.Next(10_000_000, 99_999_999)}",
                PhysicalAddress = $"{Branches[i % Branches.Length]}, Nairobi",
                EmergencyContactName = $"{People[(i + 7) % People.Length].First} {person.Last}",
                EmergencyContactRelationship = i % 3 == 0 ? "Spouse" : i % 3 == 1 ? "Parent" : "Sibling",
                EmergencyContactPhone = $"+2547{_random.Next(10_000_000, 99_999_999)}",
                Department = position.Department!,
                Position = position,
                EmploymentType = i % 11 == 10 ? EmploymentType.Contract : EmploymentType.Permanent,
                EmploymentDate = hired,
                WorkLocation = i % 9 == 0 ? "Mombasa" : "Nairobi HQ",
                PaymentMethod = PaymentMethod.BankTransfer,
                BankName = Banks[i % Banks.Length],
                BankBranch = Branches[(i * 5) % Branches.Length],
                BankAccountNumber = $"{_random.Next(100_000, 999_999)}{_random.Next(1_000_000, 9_999_999)}",
                HasDisabilityExemption = i == 30,
                DisabilityExemptionCertificateNumber = i == 30 ? "DEC/2025/00417" : null
            };
            if (employee.EmploymentType == EmploymentType.Contract)
                employee.ContractEndDate = new DateOnly(2027, 6, 30);

            employee.EmploymentHistory.Add(new EmploymentHistory
            {
                ChangeType = EmploymentChangeType.Hired,
                EffectiveDate = hired,
                ToDepartment = position.Department!.Name,
                ToPosition = position.Title,
                ToStatus = EmploymentStatus.Active,
                Notes = "Initial hire"
            });

            var basic = person.Basic > 0
                ? person.Basic
                : RoundTo(grade.Min + (decimal)_random.NextDouble() * (grade.Max - grade.Min), 500m);

            // Employees hired before 2026 get a superseded 2025 revision, then a ~7% raise in Jan 2026.
            var raiseDate = new DateOnly(2026, 1, 1);
            if (hired < raiseDate)
            {
                var previousBasic = RoundTo(basic / 1.07m, 500m);
                employee.SalaryHistory.Add(BuildSalary(structure, previousBasic, hired, raiseDate.AddDays(-1),
                    SalaryRevisionStatus.Superseded, "Starting salary", adminId));
                employee.SalaryHistory.Add(BuildSalary(structure, basic, raiseDate, null,
                    SalaryRevisionStatus.Approved, "Annual salary review 2026", adminId));
            }
            else
            {
                employee.SalaryHistory.Add(BuildSalary(structure, basic, hired, null,
                    SalaryRevisionStatus.Approved, "Starting salary", adminId));
            }

            employees.Add(employee);
        }

        // Status variety for realistic payroll scenarios.
        employees[33].Status = EmploymentStatus.OnLeave;
        employees[46].Terminate(new DateOnly(2026, 9, 15), "Resignation");
        employees[46].EmploymentHistory.Add(new EmploymentHistory
        {
            ChangeType = EmploymentChangeType.Termination, EffectiveDate = new DateOnly(2026, 9, 15),
            FromStatus = EmploymentStatus.Active, ToStatus = EmploymentStatus.Terminated, Notes = "Resignation"
        });

        db.Employees.AddRange(employees);
        await db.SaveChangesAsync(ct);

        // Reporting lines: department heads manage their departments.
        var heads = new Dictionary<string, Employee>
        {
            ["ENG"] = employees[5], ["HR"] = employees[7], ["FIN"] = employees[17], ["SAL"] = employees[20],
            ["MKT"] = employees[34], ["PRD"] = employees[1], ["OPS"] = employees[26]
        };
        foreach (var (code, head) in heads)
            departments[code].HeadEmployeeId = head.Id;
        foreach (var employee in employees)
        {
            var head = heads[departments.Single(d => d.Value.Id == employee.DepartmentId).Key];
            if (head.Id != employee.Id)
                employee.ManagerId = head.Id;
        }

        // Recurring voluntary deductions.
        AddDeduction(employees[0], components["HELB"], 3_500m, null, "HELB/0012345");
        AddDeduction(employees[2], components["LOAN"], 10_000m, 120_000m, "LN-2026-004", amountRecovered: 40_000m);
        AddDeduction(employees[4], components["SACCO"], 5_000m, null, "SACCO-118");
        AddDeduction(employees[1], components["INSURANCE"], 4_000m, null, "POL-77821");
        AddDeduction(employees[5], components["PENSION"], 25_000m, null, "PEN-0091");
        AddDeduction(employees[9], components["HELB"], 2_500m, null, "HELB/0099120");
        AddDeduction(employees[12], components["PENSION"], 12_000m, null, "PEN-0103");
        AddDeduction(employees[21], components["LOAN"], 6_000m, 36_000m, "LN-2026-011");

        await db.SaveChangesAsync(ct);
        await SeedDemoUsersAsync(employees);
        logger.LogInformation("Seeded {Count} demo employees", employees.Count);
    }

    private EmployeeSalary BuildSalary(SalaryStructure structure, decimal basic, DateOnly from, DateOnly? to,
        SalaryRevisionStatus status, string reason, int? adminId)
    {
        var salary = new EmployeeSalary
        {
            SalaryStructure = structure,
            BasicSalary = basic,
            EffectiveFrom = from,
            EffectiveTo = to,
            Status = status,
            ChangeReason = reason,
            CreatedById = adminId,
            ApprovedById = adminId,
            ApprovedAt = DateTime.UtcNow
        };

        var sized = new SalaryStructure { BasicSalary = basic };
        foreach (var line in structure.Components)
            salary.Components.Add(new EmployeeSalaryComponent
            {
                SalaryComponent = line.SalaryComponent,
                Amount = RoundTo(sized.ResolveAmount(line), 100m)
            });
        return salary;
    }

    private void AddDeduction(Employee employee, SalaryComponent component, decimal monthly, decimal? total,
        string reference, decimal amountRecovered = 0m) =>
        db.EmployeeDeductions.Add(new EmployeeDeduction
        {
            EmployeeId = employee.Id,
            SalaryComponent = component,
            MonthlyAmount = monthly,
            TotalAmount = total,
            AmountRecovered = amountRecovered,
            StartDate = new DateOnly(2026, 1, 1),
            Reference = reference
        });

    private async Task SeedDemoUsersAsync(List<Employee> employees)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword not set; demo role users were not created.");
            return;
        }

        var demoUsers = new (string Email, string Role, Employee Employee)[]
        {
            ("hr@paymaster.local", Roles.HrAdmin, employees[7]),
            ("payroll@paymaster.local", Roles.PayrollOfficer, employees[6]),
            ("manager@paymaster.local", Roles.Manager, employees[5]),
            ("employee@paymaster.local", Roles.Employee, employees[0])
        };

        foreach (var (email, role, employee) in demoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                EmployeeId = employee.Id,
                CreatedAt = DateTime.UtcNow
            };
            DatabaseSeeder.EnsureSucceeded(await userManager.CreateAsync(user, password), $"create {email}");
            DatabaseSeeder.EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"assign {role}");
            if (role != Roles.Employee)
                DatabaseSeeder.EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.Employee), "assign Employee");
            employee.UserId = user.Id;
        }

        await db.SaveChangesAsync();
    }

    private static decimal RoundTo(decimal value, decimal step) =>
        Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
}
