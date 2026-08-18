using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 提供会话队列规范化边界，后续命令状态机复用此组件。
/// </summary>
public sealed class WordStudySessionEngine(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task NormalizeCurrentItemAsync(
        WordStudySession session,
        CancellationToken cancellationToken = default)
    {
        var visibleIds = await WordVisibilityPolicy.Apply(db.Words.AsNoTracking())
            .Select(value => value.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var item in session.Items
                     .Where(value => value.Status == WordStudySessionItemStatus.Pending)
                     .OrderBy(value => session.Phase == WordStudyPhase.Memorization
                         ? value.MemorizationQueueOrder
                         : value.SpellingQueueOrder)
                     .ToArray())
        {
            if (visibleIds.Contains(item.WordId))
            {
                break;
            }
            item.Status = WordStudySessionItemStatus.Skipped;
            item.SkipReason = WordStudySkipReason.ContentUnavailable;
            item.CompletedAt = timeProvider.GetUtcNow();
            item.ConcurrencyStamp = Guid.NewGuid();
        }
        var pending = session.Items.Count(value =>
            value.Status == WordStudySessionItemStatus.Pending);
        if (pending == 0)
        {
            session.Status = WordStudySessionStatus.Completed;
            session.CompletedAt ??= timeProvider.GetUtcNow();
        }
        else if (session.Phase == WordStudyPhase.Memorization &&
                 session.Items.Where(value => value.Status == WordStudySessionItemStatus.Pending)
                     .All(value => value.MemorizationPassedAt is not null))
        {
            session.Phase = WordStudyPhase.Spelling;
        }
    }
}
