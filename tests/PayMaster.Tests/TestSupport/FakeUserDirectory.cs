using PayMaster.Application.Notifications;

namespace PayMaster.Tests.TestSupport;

public sealed class FakeUserDirectory : IUserDirectory
{
    public List<int> PermissionHolders { get; } = [100];
    public List<int> DisabledLogins { get; } = [];

    public Task<IReadOnlyList<int>> GetUserIdsWithPermissionAsync(string permission, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<int>>(PermissionHolders);

    public Task<int?> GetUserIdForEmployeeAsync(int employeeId, CancellationToken ct = default) => Task.FromResult<int?>(null);

    public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IEnumerable<int> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, string>>(userIds.Distinct().ToDictionary(id => id, id => $"User {id}"));

    public Task DisableLoginAsync(int userId, CancellationToken ct = default)
    {
        DisabledLogins.Add(userId);
        return Task.CompletedTask;
    }
}
