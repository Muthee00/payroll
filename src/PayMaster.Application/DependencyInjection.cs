using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PayMaster.Application.Employees;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Dashboard;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payslips;
using PayMaster.Application.Reports;
using PayMaster.Application.Statutory;
using PayMaster.Application.Payroll;
using PayMaster.Application.Performance;
using PayMaster.Application.Salary;

namespace PayMaster.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<EmployeeAccessScope>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<EmployeeDocumentService>();
        services.AddScoped<EmployeeImportExportService>();

        services.AddScoped<PayrollPeriodService>();
        services.AddScoped<SalaryChangeService>();
        services.AddScoped<SalaryStructureService>();
        services.AddScoped<PayItemService>();

        services.AddScoped<JobUserContext>();
        services.AddScoped<StatutoryConfigService>();
        services.AddScoped<PayrollCalculationService>();
        services.AddScoped<PayrollService>();
        services.AddScoped<PayrollHistoryService>();
        services.AddScoped<PayslipService>();
        services.AddScoped<PayrollReportService>();
        services.AddScoped<ReportExporter>();
        services.AddScoped<PerformanceReportService>();

        services.AddScoped<PerformanceScope>();
        services.AddScoped<KpiService>();
        services.AddScoped<GoalService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<PerformanceDashboardService>();
        services.AddScoped<PerformanceReminderJob>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
