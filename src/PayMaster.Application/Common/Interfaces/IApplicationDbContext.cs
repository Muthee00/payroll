using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Domain.Entities.Employees;
using PayMaster.Domain.Entities.Organization;
using PayMaster.Domain.Entities.Payroll;
using PayMaster.Domain.Entities.Performance;
using PayMaster.Domain.Entities.Salary;
using PayMaster.Domain.Entities.Statutory;

namespace PayMaster.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }

    DbSet<Employee> Employees { get; }
    DbSet<EmploymentHistory> EmploymentHistories { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }

    DbSet<SalaryComponent> SalaryComponents { get; }
    DbSet<SalaryGrade> SalaryGrades { get; }
    DbSet<SalaryStructure> SalaryStructures { get; }
    DbSet<SalaryStructureComponent> SalaryStructureComponents { get; }
    DbSet<EmployeeSalary> EmployeeSalaries { get; }
    DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents { get; }
    DbSet<EmployeeDeduction> EmployeeDeductions { get; }

    DbSet<StatutoryConfiguration> StatutoryConfigurations { get; }
    DbSet<TaxBracket> TaxBrackets { get; }
    DbSet<StatutoryRate> StatutoryRates { get; }

    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollEmployee> PayrollEmployees { get; }
    DbSet<PayrollEarning> PayrollEarnings { get; }
    DbSet<PayrollDeduction> PayrollDeductions { get; }
    DbSet<PayrollEmployerContribution> PayrollEmployerContributions { get; }
    DbSet<PayrollInput> PayrollInputs { get; }
    DbSet<Payslip> Payslips { get; }

    DbSet<Kpi> Kpis { get; }
    DbSet<PerformanceGoal> PerformanceGoals { get; }
    DbSet<GoalProgressUpdate> GoalProgressUpdates { get; }
    DbSet<ReviewCycle> ReviewCycles { get; }
    DbSet<PerformanceReview> PerformanceReviews { get; }
    DbSet<PerformanceReviewScore> PerformanceReviewScores { get; }
    DbSet<PerformanceFeedback> PerformanceFeedback { get; }
    DbSet<RatingScaleLevel> RatingScaleLevels { get; }
    DbSet<DevelopmentPlan> DevelopmentPlans { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<CompanySetting> CompanySettings { get; }

    DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
