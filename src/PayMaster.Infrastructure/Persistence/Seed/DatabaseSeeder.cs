using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayMaster.Application.Common.Security;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Enums;
using PayMaster.Infrastructure.Identity;

namespace PayMaster.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeding. Reference data (roles, permissions, statutory rules, pay components,
/// rating scale) is always ensured. Demo organisation data is only created when
/// <c>Seed:DemoData</c> is true (development).
/// </summary>
public sealed class DatabaseSeeder(
    ApplicationDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedCompanyAsync(ct);
        await SeedSalaryComponentsAsync(ct);
        await SeedRatingScaleAsync(ct);
        await SeedStatutoryAsync(ct);
        var admin = await SeedAdminAsync();

        if (configuration.GetValue<bool>("Seed:DemoData"))
            await new DemoDataSeeder(db, userManager, configuration, logger).SeedAsync(admin, ct);
    }

    private async Task SeedRolesAsync()
    {
        var descriptions = new Dictionary<string, string>
        {
            [Roles.SuperAdmin] = "Full system access, approves payroll, configures statutory rules",
            [Roles.HrAdmin] = "Manages employees, salary structures, departments and performance",
            [Roles.PayrollOfficer] = "Processes payroll, payslips and payroll reports",
            [Roles.Manager] = "Manages goals and reviews for their department",
            [Roles.Employee] = "Self-service access to own payslips and performance"
        };

        foreach (var roleName in Roles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName, descriptions[roleName]);
                EnsureSucceeded(await roleManager.CreateAsync(role), $"create role {roleName}");
            }

            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var permission in Permissions.DefaultRolePermissions[roleName].Except(existing))
                EnsureSucceeded(await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission)),
                    $"grant {permission} to {roleName}");
        }
    }

    private async Task<ApplicationUser?> SeedAdminAsync()
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!await userManager.Users.AnyAsync())
                logger.LogWarning("No users exist and Seed:AdminEmail / Seed:AdminPassword are not configured; no administrator was created.");
            return null;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is not null)
            return admin;

        admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = configuration["Seed:AdminFirstName"] ?? "System",
            LastName = configuration["Seed:AdminLastName"] ?? "Administrator",
            CreatedAt = DateTime.UtcNow,
            MustChangePassword = !configuration.GetValue<bool>("Seed:DemoData")
        };
        EnsureSucceeded(await userManager.CreateAsync(admin, password), "create administrator");
        EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.SuperAdmin), "assign administrator role");
        logger.LogInformation("Created administrator {Email}", email);
        return admin;
    }

    private async Task SeedCompanyAsync(CancellationToken ct)
    {
        if (await db.CompanySettings.AnyAsync(ct))
            return;

        db.CompanySettings.Add(new CompanySetting
        {
            CompanyName = configuration["Seed:CompanyName"] ?? "PayMaster Demo Ltd",
            LegalName = configuration["Seed:CompanyName"] ?? "PayMaster Demo Limited",
            PhysicalAddress = "Westlands Business Park, Nairobi",
            PostalAddress = "P.O. Box 00100, Nairobi, Kenya",
            PhoneNumber = "+254 700 000 000",
            Email = "payroll@paymaster.local",
            PaymentDay = 28,
            PayslipFooter = "This is a computer-generated payslip and does not require a signature."
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSalaryComponentsAsync(CancellationToken ct)
    {
        var components = new[]
        {
            Earning("HOUSE", "Housing Allowance", ComponentCategory.Allowance, TaxTreatment.Taxable, true, 10),
            Earning("TRANSPORT", "Transport Allowance", ComponentCategory.Allowance, TaxTreatment.Taxable, true, 20),
            Earning("MEDICAL", "Medical Allowance", ComponentCategory.Allowance, TaxTreatment.Taxable, true, 30),
            Earning("RESPONSIBILITY", "Responsibility Allowance", ComponentCategory.Allowance, TaxTreatment.Taxable, true, 40),
            Earning("OTHER_ALLOWANCE", "Other Taxable Allowance", ComponentCategory.Allowance, TaxTreatment.Taxable, true, 50),
            Earning("OVERTIME", "Overtime", ComponentCategory.Overtime, TaxTreatment.Taxable, false, 60),
            Earning("BONUS", "Bonus", ComponentCategory.Bonus, TaxTreatment.Taxable, false, 70),
            Earning("COMMISSION", "Commission", ComponentCategory.Commission, TaxTreatment.Taxable, false, 80),
            Earning("REIMBURSEMENT", "Expense Reimbursement", ComponentCategory.OtherEarning, TaxTreatment.NonTaxable, false, 90, pensionable: false),

            Deduction("HELB", "HELB Loan Repayment", ComponentCategory.Helb, TaxTreatment.PostTax, true, 110),
            Deduction("LOAN", "Staff Loan Repayment", ComponentCategory.Loan, TaxTreatment.PostTax, true, 120),
            Deduction("ADVANCE", "Salary Advance Recovery", ComponentCategory.SalaryAdvance, TaxTreatment.PostTax, false, 130),
            Deduction("INSURANCE", "Life Insurance Premium", ComponentCategory.Insurance, TaxTreatment.InsuranceRelief, true, 140),
            Deduction("PENSION", "Pension Contribution", ComponentCategory.Pension, TaxTreatment.AllowableDeduction, true, 150),
            Deduction("SACCO", "SACCO Contribution", ComponentCategory.Sacco, TaxTreatment.PostTax, true, 160),
            Deduction("ABSENT", "Absenteeism / Unpaid Leave", ComponentCategory.Absenteeism, TaxTreatment.ReducesGross, false, 170),
            Deduction("OTHER_DEDUCTION", "Other Deduction", ComponentCategory.OtherDeduction, TaxTreatment.PostTax, false, 180)
        };

        var existing = await db.SalaryComponents.IgnoreQueryFilters().Select(c => c.Code).ToListAsync(ct);
        db.SalaryComponents.AddRange(components.Where(c => !existing.Contains(c.Code)));
        await db.SaveChangesAsync(ct);

        static SalaryComponent Earning(string code, string name, ComponentCategory category, TaxTreatment tax,
            bool recurring, int order, bool pensionable = true) => new()
        {
            Code = code, Name = name, Type = ComponentType.Earning, Category = category, TaxTreatment = tax,
            IsRecurring = recurring, IsPensionable = pensionable, DisplayOrder = order
        };

        static SalaryComponent Deduction(string code, string name, ComponentCategory category, TaxTreatment tax,
            bool recurring, int order) => new()
        {
            Code = code, Name = name, Type = ComponentType.Deduction, Category = category, TaxTreatment = tax,
            IsRecurring = recurring, IsPensionable = false, DisplayOrder = order
        };
    }

    private async Task SeedRatingScaleAsync(CancellationToken ct)
    {
        if (await db.RatingScaleLevels.AnyAsync(ct))
            return;

        db.RatingScaleLevels.AddRange(
            new RatingScaleLevel { Value = 1, Label = "Needs Improvement", MinScore = 0m, MaxScore = 1.4999m, Color = "#dc2626" },
            new RatingScaleLevel { Value = 2, Label = "Below Expectations", MinScore = 1.5m, MaxScore = 2.4999m, Color = "#f97316" },
            new RatingScaleLevel { Value = 3, Label = "Meets Expectations", MinScore = 2.5m, MaxScore = 3.4999m, Color = "#2563eb" },
            new RatingScaleLevel { Value = 4, Label = "Exceeds Expectations", MinScore = 3.5m, MaxScore = 4.4999m, Color = "#16a34a" },
            new RatingScaleLevel { Value = 5, Label = "Outstanding", MinScore = 4.5m, MaxScore = 5m, Color = "#7c3aed" });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedStatutoryAsync(CancellationToken ct)
    {
        if (await db.StatutoryConfigurations.AnyAsync(ct))
            return;

        db.StatutoryConfigurations.AddRange(StatutorySeedData.Build());
        await db.SaveChangesAsync(ct);
    }

    internal static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Seeding failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
