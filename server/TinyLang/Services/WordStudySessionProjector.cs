using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 将持久化会话投影为不泄露拼写答案的用户响应。
/// </summary>
public sealed class WordStudySessionProjector(IApplicationDbContext db)
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
        var current = session.Items
            .Where(value => value.Status == WordStudySessionItemStatus.Pending)
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
                value.CompletedAt is not null),
            session.Items.Count(value => value.Status == WordStudySessionItemStatus.Excluded),
            session.Items.Count(value => value.Status == WordStudySessionItemStatus.Skipped),
            session.StartedAt,
            session.CompletedAt,
            current is null ? null : ProjectCurrent(current, session.Phase, favoriteWordIds));
    }

    private static WordStudyCurrentItemResponse ProjectCurrent(
        WordStudySessionItem item,
        WordStudyPhase phase,
        IReadOnlySet<Guid> favoriteWordIds)
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
                word.AudioResourceId)
            : null;
        var spelling = phase == WordStudyPhase.Spelling
            ? new WordSpellingPromptResponse(
                word.Senses.OrderBy(value => value.SortOrder)
                    .Select(value => new WordSpellingSenseResponse(
                        value.PartOfSpeech,
                        value.Definition,
                        value.UsageNote,
                        value.SortOrder))
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
