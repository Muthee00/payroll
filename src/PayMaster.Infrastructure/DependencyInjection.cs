using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaster.Application.Common.Email;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Notifications;
using PayMaster.Application.Payslips;
using PayMaster.Infrastructure.BackgroundJobs;
using PayMaster.Infrastructure.Identity;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Persistence.Interceptors;
using PayMaster.Infrastructure.Persistence.Seed;
using PayMaster.Infrastructure.Services;

namespace PayMaster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Set it via user-secrets or the ConnectionStrings__DefaultConnection environment variable.");

        if (configuration.GetValue<bool>("Database:AllowUnencryptedLocalConnection"))
        {
            var localConnection = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionString);
            if (!IsLoopbackMySqlHost(localConnection.Server))
                throw new InvalidOperationException(
                    "Unencrypted MySQL connections are only allowed for a localhost server.");

            localConnection.SslMode = MySql.Data.MySqlClient.MySqlSslMode.Disabled;
            connectionString = localConnection.ConnectionString;
        }

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMySQL(connectionString, mysql =>
            {
                mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                mysql.CommandTimeout(120);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());

            // Soft-deleted employees/components stay referenced by history (payroll lines, salary revisions);
            // history queries that must include them use IgnoreQueryFilters explicitly.
            options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<PayMasterClaimsPrincipalFactory>();

        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

        services.AddScoped<SessionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.AddTransient<IEmailSender, EmailSender>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.Section));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<ISpreadsheetService, ClosedXmlSpreadsheetService>();

        services.AddSingleton<IPayslipRenderer, QuestPdfPayslipRenderer>();
        services.AddSingleton<PayMaster.Application.Reports.IReportPdfRenderer, QuestPdfReportRenderer>();
        services.AddScoped<IBackgroundJobs, HangfireBackgroundJobs>();
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<PayrollJobs>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static bool IsLoopbackMySqlHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host == "127.0.0.1"
        || host == "::1"
        || host == ".";

}
