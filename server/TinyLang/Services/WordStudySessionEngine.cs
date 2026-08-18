using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Dtos;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 提供会话队列规范化边界，后续命令状态机复用此组件。
/// </summary>
public sealed class WordStudySessionEngine(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<WordStudyCommandResponse> SubmitMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        WordStudySessionType expectedType,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var session = await db.WordStudySessions
            .Include(value => value.Items)
                .ThenInclude(value => value.Word!)
                    .ThenInclude(value => value.Senses)
                        .ThenInclude(value => value.Examples)
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.SessionType != expectedType)
        {
            throw ConflictException.Create(ErrorCodes.WordStudySessionTypeConflict);
        }
        if (session.Status != WordStudySessionStatus.Active)
        {
            throw ConflictException.Create(ErrorCodes.WordStudySessionNotActive);
        }
        if (session.Phase != WordStudyPhase.Memorization)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyPhaseConflict);
        }
        var current = session.Items
            .Where(value => value.Status == WordStudySessionItemStatus.Pending)
            .OrderBy(value => value.MemorizationQueueOrder)
            .FirstOrDefault();
        if (current is null || current.Id != itemId)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyQueueConflict);
        }
        if (request.ItemConcurrencyStamp == Guid.Empty ||
            current.ConcurrencyStamp != request.ItemConcurrencyStamp)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        }
        if (request.Result is null || !Enum.IsDefined(request.Result.Value))
        {
            throw new RequestValidationException(ErrorCodes.WordStudyResultInvalid);
        }
        current.MemorizationAttemptCount++;
        if (request.Result == WordMemorizationResult.Forgotten)
        {
            current.HadMemorizationFailure = true;
            current.MemorizationQueueOrder = session.Items
                .Max(value => value.MemorizationQueueOrder) + 1;
        }
        else
        {
            current.MemorizationPassedAt = timeProvider.GetUtcNow();
        }
        current.ConcurrencyStamp = Guid.NewGuid();
        if (session.Items.Where(value => value.Status == WordStudySessionItemStatus.Pending)
            .All(value => value.MemorizationPassedAt is not null))
        {
            session.Phase = WordStudyPhase.Spelling;
            session.ConcurrencyStamp = Guid.NewGuid();
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WordStudyCommandResponse(
            null,
            await new WordStudySessionProjector(db)
                .ProjectAsync(userId, session, cancellationToken));
    }
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
