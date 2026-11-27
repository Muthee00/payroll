using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayMaster.Domain.Entities.Administration;
using PayMaster.Tests.TestSupport;
using Shouldly;

namespace PayMaster.Tests.IntegrationTests;

public class AuditingInterceptorTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Insert_sets_creation_stamps_and_writes_audit_entry_with_generated_id()
    {
        int employeeId;
        await using (var ctx = _db.CreateContext())
        {
            var employee = TestData.Employee(TestData.Department());
            ctx.Employees.Add(employee);
            await ctx.SaveChangesAsync();
            employeeId = employee.Id;

            employee.CreatedAt.ShouldBe(_db.Clock.GetUtcNow().UtcDateTime);
            employee.CreatedById.ShouldBe(1);
        }

        await using var verify = _db.CreateContext();
        var log = await verify.AuditLogs.SingleAsync(a => a.EntityName == "Employee");
        log.Action.ShouldBe("Created");
        log.EntityId.ShouldBe(employeeId.ToString());
        log.UserId.ShouldBe(1);
        log.IpAddress.ShouldBe("127.0.0.1");
        log.NewValues.ShouldNotBeNull().ShouldContain("\"FirstName\":\"John\"");
    }

    [Fact]
    public async Task Update_records_only_changed_fields_with_old_and_new_values()
    {
        var id = await SeedEmployeeAsync();
        _db.User.UserId = 2;
        _db.Clock.Advance(TimeSpan.FromHours(1));

        await using (var ctx = _db.CreateContext())
        {
            var employee = await ctx.Employees.SingleAsync(e => e.Id == id);
            employee.PhoneNumber = "+254711000111";
            await ctx.SaveChangesAsync();
            employee.UpdatedById.ShouldBe(2);
            employee.CreatedById.ShouldBe(1);
        }

        await using var verify = _db.CreateContext();
        var log = await verify.AuditLogs.SingleAsync(a => a.Action == "Updated");
        var oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.OldValues!)!;
        var newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.NewValues!)!;
        newValues.Keys.ShouldBe(["PhoneNumber"]);
        oldValues["PhoneNumber"].ValueKind.ShouldBe(JsonValueKind.Null);
        newValues["PhoneNumber"].GetString().ShouldBe("+254711000111");
    }

    [Fact]
    public async Task Bank_account_numbers_are_masked_in_the_audit_trail()
    {
        var id = await SeedEmployeeAsync();
        await using (var ctx = _db.CreateContext())
        {
            var employee = await ctx.Employees.SingleAsync(e => e.Id == id);
            employee.BankAccountNumber = "9988776655443";
            await ctx.SaveChangesAsync();
        }

        await using var verify = _db.CreateContext();
        var logs = await verify.AuditLogs.Select(a => (a.OldValues ?? "") + (a.NewValues ?? "")).ToListAsync();
        logs.ShouldAllBe(json => !json.Contains("9988776655443") && !json.Contains("0123456789012"));
        logs.ShouldContain(json => json.Contains("****5443"));
    }

    [Fact]
    public async Task Delete_of_soft_deletable_entity_marks_it_deleted_and_hides_it()
    {
        var id = await SeedEmployeeAsync();
        await using (var ctx = _db.CreateContext())
        {
            ctx.Employees.Remove(await ctx.Employees.SingleAsync(e => e.Id == id));
            await ctx.SaveChangesAsync();
        }

        await using var verify = _db.CreateContext();
        (await verify.Employees.AnyAsync(e => e.Id == id)).ShouldBeFalse();
        var row = await verify.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == id);
        row.IsDeleted.ShouldBeTrue();
        row.DeletedById.ShouldBe(1);
        (await verify.AuditLogs.AnyAsync(a => a.Action == "Deleted" && a.EntityId == id.ToString())).ShouldBeTrue();
    }

    [Fact]
    public async Task Audit_entries_cannot_be_modified_or_deleted()
    {
        await SeedEmployeeAsync();
        await using var ctx = _db.CreateContext();
        var log = await ctx.AuditLogs.FirstAsync();

        log.Action = "Tampered";
        await Should.ThrowAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());

        ctx.Entry(log).State = EntityState.Unchanged;
        ctx.AuditLogs.Remove(log);
        await Should.ThrowAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Untracked_entities_are_not_audited()
    {
        await using var ctx = _db.CreateContext();
        ctx.Notifications.Add(new Notification { UserId = 1, Title = "Hi", Message = "There", CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        (await ctx.AuditLogs.AnyAsync(a => a.EntityName == nameof(Notification))).ShouldBeFalse();
    }

    private async Task<int> SeedEmployeeAsync()
    {
        await using var ctx = _db.CreateContext();
        var employee = TestData.Employee(TestData.Department());
        ctx.Employees.Add(employee);
        await ctx.SaveChangesAsync();
        return employee.Id;
    }
}
