using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TinyLang.Entities.Common;

namespace TinyLang.Database;

/// <summary>
/// 在保存变更前为可审计实体维护 UTC 创建和更新时间。
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is not null)
        {
            var utcNow = DateTimeOffset.UtcNow;
            foreach (var entry in dbContext.ChangeTracker.Entries<BaseAuditableEntity>())
            {
                if (entry.State is EntityState.Added)
                {
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                }

                if (entry.State is EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = utcNow;
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
