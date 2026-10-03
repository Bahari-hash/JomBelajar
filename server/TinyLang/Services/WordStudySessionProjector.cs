using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 将持久化会话投影为不泄露拼写答案的用户响应。
/// </summary>
public sealed class WordStudySessionProjector(IApplicationDbContext db, TimeProvider? clock = null)
{
    public async Task<WordStudySessionStateResponse> ProjectAsync(
        Guid userId,
        WordStudySession session,
        CancellationToken cancellationToken = default)
    {
        var favoriteWordIds = await db.UserWordFavorites.AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => value.WordId)
            .ToHashSetAsync(cancellationToken);
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var progress = await db.UserWordProgress.AsNoTracking()
            .Where(x => x.UserId == userId && session.Items.Select(i => i.WordId).Contains(x.WordId))
            .ToDictionaryAsync(x => x.WordId, cancellationToken);
        var retention = await db.Users.Where(x => x.Id == userId)
            .Select(x => (double?)x.DesiredRetention).SingleOrDefaultAsync(cancellationToken) ?? 0.9;
        var pending = session.Items.Where(x => x.Status == WordStudySessionItemStatus.Pending && x.MemorizationPassedAt == null).ToArray();
        var logs = session.Phase == WordStudyPhase.Summary
            ? await db.WordReviewLogs.AsNoTracking().Where(x => session.Items.Select(i => i.Id).Contains(x.SessionItemId)).ToListAsync(cancellationToken)
            : [];
        var summary = session.Phase == WordStudyPhase.Summary ? new WordStudyGroupSummary(
            session.Items.Count(x => x.MemorizationPassedAt != null), logs.Count,
            logs.Count(x => x.Rating == WordMemorizationResult.Again), logs.Count(x => x.Rating == WordMemorizationResult.Hard),
            logs.Count(x => x.Rating == WordMemorizationResult.Good), logs.Count(x => x.Rating == WordMemorizationResult.Easy)) : null;
        var current = session.Items
            .Where(value =>
                session.Phase != WordStudyPhase.Summary && value.Status == WordStudySessionItemStatus.Pending &&
                (session.Phase != WordStudyPhase.Memorization ||
                    value.MemorizationPassedAt == null))
            .OrderBy(value => session.Phase == WordStudyPhase.Memorization
                ? value.MemorizationQueueOrder
                : value.SpellingQueueOrder)
            .FirstOrDefault();
        return new WordStudySessionStateResponse(
            session.Id,
            session.SessionType,
            session.Phase,
            session.Status,
            session.ActualCount,
            session.Items.Count(value => value.Status == WordStudySessionItemStatus.Completed),
            session.Items.Count(value => value.MemorizationPassedAt is not null),
            session.Items.Count(value =>
                value.Status == WordStudySessionItemStatus.Completed &&
                value.CompletedAt is not null && value.SkipReason != WordStudySkipReason.SpellingSkipped && session.Phase != WordStudyPhase.Summary),
            session.Items.Count(value => value.Status == WordStudySessionItemStatus.Excluded),
            session.Items.Count(value => value.Status == WordStudySessionItemStatus.Skipped),
            session.StartedAt,
            session.CompletedAt,
            current is null ? null : ProjectCurrent(current, session.Phase, favoriteWordIds,
                progress.GetValueOrDefault(current.WordId), now, retention),
            null,
            pending.Count(x => !progress.ContainsKey(x.WordId)),
            pending.Count(x => progress.TryGetValue(x.WordId, out var p) && p.FsrsState is "Learning" or "Relearning"),
            pending.Count(x => progress.TryGetValue(x.WordId, out var p) && p.FsrsState is not ("Learning" or "Relearning")), summary);
    }

    private static WordStudyCurrentItemResponse ProjectCurrent(
        WordStudySessionItem item,
        WordStudyPhase phase,
        IReadOnlySet<Guid> favoriteWordIds, UserWordProgress? progress, DateTimeOffset now, double retention)
    {
        var word = item.Word ?? throw new InvalidOperationException("Study item word is not loaded.");
        var memorization = phase == WordStudyPhase.Memorization
            ? new WordMemorizationContentResponse(
                word.Headword,
                word.Senses.OrderBy(value => value.SortOrder)
                    .Select(value => new WordSenseResponse(
                        value.PartOfSpeech,
                        value.Definition,
                        value.UsageNote,
                        value.SortOrder,
                        value.Examples.OrderBy(example => example.SortOrder)
                            .Select(example => new ExampleSentenceResponse(
                                example.Sentence,
                                example.Translation,
                                example.AudioResourceId,
                                example.SortOrder))
                            .ToArray()))
                    .ToArray(),
                word.AudioResourceId, FsrsWordScheduler.Preview(progress, now, retention))
            : null;
        var spelling = phase == WordStudyPhase.Spelling
            ? new WordSpellingPromptResponse(
                word.Senses.OrderBy(value => value.SortOrder)
                    .Select(value => new WordSpellingSenseResponse(
                        value.PartOfSpeech,
                        value.Definition))
                    .ToArray())
            : null;
        return new WordStudyCurrentItemResponse(
            phase,
            item.Id,
            item.WordId,
            item.ConcurrencyStamp,
            favoriteWordIds.Contains(item.WordId),
            memorization,
            spelling);
    }
}
