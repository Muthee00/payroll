using Microsoft.AspNetCore.Identity;

namespace PayMaster.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Linked employee record for self-service and manager scoping.</summary>
    public int? EmployeeId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole() { }

    public ApplicationRole(string name, string? description = null) : base(name)
    {
        Description = description;
    }

    public string? Description { get; set; }
}

/// <summary>
/// Server-side record of a signed-in browser session. The auth cookie carries the session id;
/// a revoked or expired session is rejected on the next request (logout everywhere, admin revoke).
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAt is null && ExpiresAt > nowUtc;
}
