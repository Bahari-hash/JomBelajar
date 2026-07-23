using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Interfaces;

namespace TinyLang.Services;

public sealed class UserSessionService(IApplicationDbContext db) : IUserSessionService
{
    public async Task InvalidateAllAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        user.TokenVersion++;
        var now = DateTimeOffset.UtcNow;
        await db.RefreshTokens
            .Where(x => x.UserId == user.Id && !x.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRevoked, true)
                .SetProperty(x => x.RevokedAt, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
