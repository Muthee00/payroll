using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PayMaster.Application;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Application.Common.Security;
using PayMaster.Infrastructure;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Persistence.Seed;
using PayMaster.Web.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    var resetAdminPasswordCommand = args.Any(arg =>
        string.Equals(arg, "--reset-admin-password", StringComparison.OrdinalIgnoreCase));
    if (resetAdminPasswordCommand)
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("The local password reset command is only available in Development.");

        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        var localConnection = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionString);
        if (!IsLoopbackMySqlHost(localConnection.Server))
            throw new InvalidOperationException("The local password reset command only connects to a localhost MySQL server.");

        // This one-off local command avoids the failing TLS handshake on loopback only.
        localConnection.SslMode = MySql.Data.MySqlClient.MySqlSslMode.Disabled;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = localConnection.ConnectionString
        });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        await ResetAdminPasswordAsync(builder.Build());
        return;
    }

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();

    var jobsConnectionBuilder = new MySqlConnector.MySqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
    {
        AllowUserVariables = true
    };
    if (builder.Configuration.GetValue<bool>("Database:AllowUnencryptedLocalConnection"))
    {
        if (!IsLoopbackMySqlHost(jobsConnectionBuilder.Server))
            throw new InvalidOperationException(
                "Unencrypted MySQL connections are only allowed for a localhost server.");

        jobsConnectionBuilder.SslMode = MySqlConnector.MySqlSslMode.None;
    }
    var jobsConnection = jobsConnectionBuilder.ConnectionString;
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseStorage(new MySqlStorage(jobsConnection, new MySqlStorageOptions
        {
            TablesPrefix = "Hangfire_",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(5)
        })));
    builder.Services.AddHangfireServer(options => options.WorkerCount = Math.Max(2, Environment.ProcessorCount / 2));
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "PayMaster.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = AuthCookieEvents.ValidatePrincipalAsync;
    });

    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    {
        // Picks up role/permission changes and deactivations within minutes.
        options.ValidationInterval = TimeSpan.FromMinutes(5);
        options.OnRefreshingPrincipal = AuthCookieEvents.PreserveSessionClaim;
    });

    builder.Services.AddAuthorization(options =>
    {
        foreach (var permission in Permissions.All)
            options.AddPolicy(permission, policy => policy.RequireClaim(Permissions.ClaimType, permission));
    });

    builder.Services.AddControllersWithViews(options =>
    {
        // Secure by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
        options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<ForcePasswordChangeFilter>();
        options.Filters.Add<FluentValidationFilter>();
        options.Filters.Add<AppExceptionFilter>();
    });
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));
    });

    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/error/{0}");

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorization()],
        DashboardTitle = "PayMaster background jobs",
        AppPath = "/"
    });

    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PayMaster.Application.Performance.PerformanceReminderJob>(
        "performance-reminders", job => job.RunAsync(CancellationToken.None), "0 6 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "E. Africa Standard Time" : "Africa/Nairobi") });

    app.MapStaticAssets();
    app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Dashboard}/{action=Index}/{id?}")
        .WithStaticAssets();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "PayMaster terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task ResetAdminPasswordAsync(WebApplication app)
{
    var email = app.Configuration["Seed:AdminEmail"];
    if (string.IsNullOrWhiteSpace(email))
        throw new InvalidOperationException("Seed:AdminEmail is not configured for this development environment.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PayMaster.Infrastructure.Identity.ApplicationUser>>();
    var user = await userManager.FindByEmailAsync(email)
        ?? throw new InvalidOperationException($"No administrator account exists for {email}. Start the app once to seed it, then run this command again.");

    var password = ReadHiddenInput("New password: ");
    var confirmation = ReadHiddenInput("Confirm password: ");
    if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        throw new InvalidOperationException("The passwords did not match. No changes were made.");

    var token = await userManager.GeneratePasswordResetTokenAsync(user);
    var reset = await userManager.ResetPasswordAsync(user, token, password);
    if (!reset.Succeeded)
        throw new InvalidOperationException($"Password reset failed: {string.Join("; ", reset.Errors.Select(error => error.Description))}");

    user.AccessFailedCount = 0;
    user.LockoutEnd = null;
    var unlock = await userManager.UpdateAsync(user);
    if (!unlock.Succeeded)
        throw new InvalidOperationException($"Password was reset, but account unlock failed: {string.Join("; ", unlock.Errors.Select(error => error.Description))}");

    Console.WriteLine($"Password reset for {email}.");
}

static bool IsLoopbackMySqlHost(string host) =>
    host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
    || host == "127.0.0.1"
    || host == "::1"
    || host == ".";

static string ReadHiddenInput(string prompt)
{
    if (Console.IsInputRedirected)
        throw new InvalidOperationException("Run this command in an interactive terminal so the password can be entered privately.");

    Console.Write(prompt);
    var value = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
            break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length > 0)
                value.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar))
            value.Append(key.KeyChar);
    }
    Console.WriteLine();
    return value.ToString();
}

public partial class Program;
