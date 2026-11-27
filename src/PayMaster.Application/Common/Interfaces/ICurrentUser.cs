namespace PayMaster.Application.Common.Interfaces;

/// <summary>The authenticated user making the current request (or the job runner for background work).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? UserName { get; }

    /// <summary>Employee record linked to the user, if any (self-service and Manager scoping).</summary>
    int? EmployeeId { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);

    /// <summary>User id, or throws when there is no authenticated user.</summary>
    int RequireUserId() => UserId ?? throw new UnauthorizedAccessException("No authenticated user.");
}
