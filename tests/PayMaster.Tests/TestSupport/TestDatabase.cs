using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using PayMaster.Application.Common.Interfaces;
using PayMaster.Infrastructure.Persistence;
using PayMaster.Infrastructure.Persistence.Interceptors;

namespace PayMaster.Tests.TestSupport;

public sealed class TestCurrentUser : ICurrentUser
{
    public int? UserId { get; set; } = 1;
    public string? UserName { get; set; } = "tester@paymaster.local";
    public int? EmployeeId { get; set; }
    public string? IpAddress { get; set; } = "127.0.0.1";
    public bool IsAuthenticated => UserId is not null;
    public HashSet<string> Roles { get; } = [];
    public HashSet<string> Permissions { get; } = [];
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

/// <summary>
/// Real EF Core model on an in-memory SQLite connection, with the production auditing
/// interceptor. Each instance is an isolated database.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestCurrentUser User { get; } = new();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A fresh context (empty change tracker) over the same database.</summary>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditingInterceptor(User, Clock))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
