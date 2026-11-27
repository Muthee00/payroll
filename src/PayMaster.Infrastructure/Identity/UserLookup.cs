using Microsoft.EntityFrameworkCore;
using PayMaster.Infrastructure.BackgroundJobs;
using PayMaster.Infrastructure.Persistence;

namespace PayMaster.Infrastructure.Identity;

public sealed class UserLookup(ApplicationDbContext db) : IUserLookup
{
    public Task<string?> GetUserNameAsync(int userId, CancellationToken ct) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.UserName).FirstOrDefaultAsync(ct);
}
