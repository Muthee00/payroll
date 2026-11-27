using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Entities.Statutory;
using PayMaster.Domain.Interfaces;
using PayMaster.Infrastructure.Identity;
using PayMaster.Infrastructure.Persistence.Converters;

namespace PayMaster.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options), IApplicationDbContext
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentHistory> EmploymentHistories => Set<EmploymentHistory>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<SalaryGrade> SalaryGrades => Set<SalaryGrade>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryStructureComponent> SalaryStructureComponents => Set<SalaryStructureComponent>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();
    public DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents => Set<EmployeeSalaryComponent>();
    public DbSet<EmployeeDeduction> EmployeeDeductions => Set<EmployeeDeduction>();

    public DbSet<StatutoryConfiguration> StatutoryConfigurations => Set<StatutoryConfiguration>();
    public DbSet<TaxBracket> TaxBrackets => Set<TaxBracket>();
    public DbSet<StatutoryRate> StatutoryRates => Set<StatutoryRate>();

    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollEmployee> PayrollEmployees => Set<PayrollEmployee>();
    public DbSet<PayrollEarning> PayrollEarnings => Set<PayrollEarning>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();
    public DbSet<PayrollEmployerContribution> PayrollEmployerContributions => Set<PayrollEmployerContribution>();
    public DbSet<PayrollInput> PayrollInputs => Set<PayrollInput>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

    public DbSet<Kpi> Kpis => Set<Kpi>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<GoalProgressUpdate> GoalProgressUpdates => Set<GoalProgressUpdate>();
    public DbSet<ReviewCycle> ReviewCycles => Set<ReviewCycle>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<PerformanceReviewScore> PerformanceReviewScores => Set<PerformanceReviewScore>();
    public DbSet<PerformanceFeedback> PerformanceFeedback => Set<PerformanceFeedback>();
    public DbSet<RatingScaleLevel> RatingScaleLevels => Set<RatingScaleLevel>();
    public DbSet<DevelopmentPlan> DevelopmentPlans => Set<DevelopmentPlan>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money by default; rates/weights override precision explicitly in configurations.
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.Property(u => u.FirstName).HasMaxLength(100);
            b.Property(u => u.LastName).HasMaxLength(100);
            b.HasIndex(u => u.EmployeeId).IsUnique();
        });
        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles");
            b.Property(r => r.Description).HasMaxLength(500);
        });
        builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

        builder.Entity<UserSession>(b =>
        {
            b.ToTable("UserSessions");
            b.Property(s => s.IpAddress).HasMaxLength(64);
            b.Property(s => s.UserAgent).HasMaxLength(512);
            b.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(s => new { s.UserId, s.RevokedAt });
        });

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplySoftDeleteFilters(builder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDelete.IsDeleted)));
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
