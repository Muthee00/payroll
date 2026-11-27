using Microsoft.EntityFrameworkCore;
using PayMaster.Infrastructure.Persistence;

namespace PayMaster.Infrastructure.Identity;

/// <summary>Server-side session registry backing "active sessions" and remote sign-out.</summary>
public sealed class SessionService(ApplicationDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan PersistentLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<UserSession> CreateAsync(int userId, string? ipAddress, string? userAgent, bool persistent)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = Now,
            LastSeenAt = Now,
            ExpiresAt = Now + (persistent ? PersistentLifetime : Lifetime),
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>True when the session is still valid; refreshes last-seen at most once a minute.</summary>
    public async Task<bool> ValidateAsync(Guid sessionId, int userId)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session is null || !session.IsActive(Now))
            return false;

        if (Now - session.LastSeenAt > TouchInterval)
        {
            session.LastSeenAt = Now;
            await db.SaveChangesAsync();
        }
        return true;
    }

    public Task<List<UserSession>> ListActiveAsync(int userId) =>
        db.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > Now)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync();

    public async Task RevokeAsync(Guid sessionId, int userId)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session is { RevokedAt: null })
        {
            session.RevokedAt = Now;
            await db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllAsync(int userId, Guid? exceptSessionId = null)
    {
        await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, Now));
    }
}
