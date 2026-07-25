using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 通过 token version 和批量 refresh token 撤销实现用户会话失效。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
public sealed class UserSessionService(IApplicationDbContext db) : IUserSessionService
{
    /// <inheritdoc />
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
