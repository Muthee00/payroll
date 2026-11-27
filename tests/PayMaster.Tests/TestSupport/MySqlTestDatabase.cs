using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using MySql.Data.MySqlClient;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Persistence.Interceptors;

namespace PayMaster.Tests.TestSupport;

/// <summary>
/// A throwaway MySQL database (same provider as production) created for one test class and
/// dropped afterwards. Connection: env var PAYMASTER_TEST_MYSQL, else the web app's
/// user-secrets connection string (the database name is replaced).
/// </summary>
public sealed class MySqlTestDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databaseName = $"paymaster_test_{Guid.NewGuid():N}"[..40];

    public TestCurrentUser User { get; } = new();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

    public MySqlTestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConnection = configuration["PAYMASTER_TEST_MYSQL"]
                             ?? configuration.GetConnectionString("DefaultConnection")
                             ?? throw new InvalidOperationException(
                                 "Integration tests need MySQL. Set PAYMASTER_TEST_MYSQL or the web app's DefaultConnection user secret.");

        var builder = new MySqlConnectionStringBuilder(baseConnection) { Database = _databaseName };
        _connectionString = builder.ConnectionString;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySQL(_connectionString)
            .AddInterceptors(new AuditingInterceptor(User, Clock))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    public void Dispose()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
    }
}
