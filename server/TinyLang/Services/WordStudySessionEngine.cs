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
    public async Task<WordStudySessionStateResponse> FinishSummaryAsync(
        Guid userId, Guid sessionId, WordStudySessionType expectedType, bool skipSpelling,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await db.WordStudySessions.Include(x => x.Items).ThenInclude(x => x.Word!)
            .ThenInclude(x => x.Senses).ThenInclude(x => x.Examples)
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.SessionType != expectedType) throw ConflictException.Create(ErrorCodes.WordStudySessionTypeConflict);
        if (session.Status != WordStudySessionStatus.Active ||
            (session.Phase != WordStudyPhase.Summary && !(skipSpelling && session.Phase == WordStudyPhase.Spelling)))
            throw ConflictException.Create(ErrorCodes.WordStudyPhaseConflict);
        if (skipSpelling)
        {
            var now = timeProvider.GetUtcNow();
            foreach (var item in session.Items.Where(x => x.Status == WordStudySessionItemStatus.Pending))
            {
                item.Status = WordStudySessionItemStatus.Completed;
                item.SkipReason = WordStudySkipReason.SpellingSkipped;
                item.CompletedAt = now; item.ConcurrencyStamp = Guid.NewGuid();
                await AddActivityIfMissingAsync(userId, item, expectedType, now, cancellationToken);
            }
            session.Status = WordStudySessionStatus.Completed; session.CompletedAt = now;
            if (expectedType == WordStudySessionType.Learning) await AddCheckInIfMissingAsync(userId, now, cancellationToken);
        }
        else session.Phase = WordStudyPhase.Spelling;
        session.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return await new WordStudySessionProjector(db, timeProvider).ProjectAsync(userId, session, cancellationToken);
    }

    public async Task<WordStudyCommandResponse> ExcludeFromReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        ExcludeWordFromReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await db.WordStudySessions
            .AsSplitQuery()
            .Include(value => value.Items)
                .ThenInclude(value => value.Word!)
                    .ThenInclude(value => value.Senses)
                        .ThenInclude(value => value.Examples)
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.SessionType != WordStudySessionType.Review)
            throw ConflictException.Create(ErrorCodes.WordStudySessionTypeConflict);
        if (session.Status != WordStudySessionStatus.Active)
            throw ConflictException.Create(ErrorCodes.WordStudySessionNotActive);
        var current = session.Items.Where(value =>
                value.Status == WordStudySessionItemStatus.Pending &&
                (session.Phase != WordStudyPhase.Memorization ||
                    value.MemorizationPassedAt == null))
            .OrderBy(value => session.Phase == WordStudyPhase.Memorization
                ? value.MemorizationQueueOrder
                : value.SpellingQueueOrder)
            .FirstOrDefault();
        if (current is null || current.Id != itemId)
            throw ConflictException.Create(ErrorCodes.WordStudyQueueConflict);
        if (request.ItemConcurrencyStamp == Guid.Empty || current.ConcurrencyStamp != request.ItemConcurrencyStamp)
            throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        await NormalizeCurrentItemAsync(session, cancellationToken);
        if (current.Status == WordStudySessionItemStatus.Skipped)
            return await PersistNormalizedSessionAsync(
                userId,
                session,
                transaction,
                cancellationToken);
        var progress = await db.UserWordProgress.SingleOrDefaultAsync(value =>
            value.UserId == userId && value.WordId == current.WordId, cancellationToken);
        if (progress is null)
            throw NotFoundException.Create(ErrorCodes.WordReviewProgressNotFound);
        var now = timeProvider.GetUtcNow();
        current.Status = WordStudySessionItemStatus.Excluded;
        current.CompletedAt = now;
        current.ConcurrencyStamp = Guid.NewGuid();
        progress.IsReviewExcluded = true;
        progress.ReviewExcludedAt = now;
        progress.NextReviewAt = null;
        progress.ConcurrencyStamp = Guid.NewGuid();
        if (session.Items.All(value => value.Status != WordStudySessionItemStatus.Pending))
        {
            session.Status = WordStudySessionStatus.Completed;
            session.CompletedAt = now;
        }
        else if (session.Phase == WordStudyPhase.Memorization &&
            session.Items.Where(value => value.Status == WordStudySessionItemStatus.Pending)
            .All(value => value.MemorizationPassedAt is not null))
        {
            session.Phase = WordStudyPhase.Summary;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WordStudyCommandResponse(
            null,
            await new WordStudySessionProjector(db, timeProvider).ProjectAsync(userId, session, cancellationToken));
    }
    public async Task<WordStudyCommandResponse> SubmitSpellingAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        WordStudySessionType expectedType,
        SubmitWordSpellingRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await db.WordStudySessions
            .AsSplitQuery()
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
        if (session.Phase != WordStudyPhase.Spelling)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyPhaseConflict);
        }
        var current = session.Items
            .Where(value => value.Status == WordStudySessionItemStatus.Pending)
            .OrderBy(value => value.SpellingQueueOrder)
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
        await NormalizeCurrentItemAsync(session, cancellationToken);
        if (current.Status == WordStudySessionItemStatus.Skipped)
            return await PersistNormalizedSessionAsync(
                userId,
                session,
                transaction,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        var correctAnswer = current.Word!.Headword;
        var correct = !request.Skip && WordStudySchedule.IsCorrectSpelling(
            request.Answer,
            correctAnswer);
        current.SpellingAttemptCount++;
        if (!correct)
        {
            current.HadSpellingFailure = true;
            current.SpellingQueueOrder = session.Items.Max(value => value.SpellingQueueOrder) + 1;
        }
        else
        {
            current.Status = WordStudySessionItemStatus.Completed;
            current.CompletedAt = now;
            // Existing sessions already in spelling retain their original scheduling path.
            // FSRS-rated items never schedule again from spelling (even after an incorrect answer).
            if (!await db.WordReviewLogs.AnyAsync(x => x.SessionItemId == current.Id, cancellationToken))
            {
                if (expectedType == WordStudySessionType.Learning &&
                    !await db.UserWordProgress.AnyAsync(value =>
                        value.UserId == userId && value.WordId == current.WordId,
                        cancellationToken))
                {
                    var schedule = WordStudySchedule.AfterInitialLearning(now);
                    db.UserWordProgress.Add(new UserWordProgress
                    {
                        UserId = userId,
                        WordId = current.WordId,
                        FirstStudiedAt = now,
                        LastStudiedAt = now,
                        ReviewStage = schedule.Stage,
                        NextReviewAt = schedule.NextReviewAt
                    });
                }
                else if (expectedType == WordStudySessionType.Review)
                {
                    var progress = await db.UserWordProgress.SingleOrDefaultAsync(value =>
                        value.UserId == userId && value.WordId == current.WordId,
                        cancellationToken) ?? throw NotFoundException.Create(ErrorCodes.WordReviewProgressNotFound);
                    var schedule = WordStudySchedule.AfterReview(
                        progress.ReviewStage,
                        current.HadMemorizationFailure || current.HadSpellingFailure,
                        now);
                    progress.ReviewStage = schedule.Stage;
                    progress.NextReviewAt = schedule.NextReviewAt;
                    progress.LastReviewedAt = now;
                    progress.LastStudiedAt = now;
                    progress.ReviewCount++;
                    if (current.HadMemorizationFailure || current.HadSpellingFailure)
                        progress.FailedReviewCount++;
                    else
                        progress.SuccessfulReviewCount++;
                    progress.ConcurrencyStamp = Guid.NewGuid();
                }

            }

            await AddActivityIfMissingAsync(
                userId,
                current,
                expectedType,
                now,
                cancellationToken);
        }
        current.ConcurrencyStamp = Guid.NewGuid();
        if (session.Items.All(value =>
            value.Status != WordStudySessionItemStatus.Pending))
        {
            session.Status = WordStudySessionStatus.Completed;
            session.CompletedAt = now;
            session.ConcurrencyStamp = Guid.NewGuid();
            if (expectedType == WordStudySessionType.Learning)
            {
                await AddCheckInIfMissingAsync(userId, now, cancellationToken);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WordStudyCommandResponse(
            new WordSpellingOutcomeResponse(
                correct ? WordSpellingResult.Correct : WordSpellingResult.Incorrect,
                correctAnswer),
            await new WordStudySessionProjector(db, timeProvider)
                .ProjectAsync(userId, session, cancellationToken));
    }

    public async Task<WordStudyCommandResponse> SubmitMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        WordStudySessionType expectedType,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await db.WordStudySessions
            .AsSplitQuery()
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
            .Where(value => value.Status == WordStudySessionItemStatus.Pending &&
                value.MemorizationPassedAt == null)
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
        await NormalizeCurrentItemAsync(session, cancellationToken);
        if (current.Status == WordStudySessionItemStatus.Skipped)
            return await PersistNormalizedSessionAsync(
                userId,
                session,
                transaction,
                cancellationToken);
        if (request.Result is null || !Enum.IsDefined(request.Result.Value))
        {
            throw new RequestValidationException(
                ErrorCodes.WordStudyMemorizationResultInvalid);
        }
        var now = timeProvider.GetUtcNow();
        var progress = await db.UserWordProgress.SingleOrDefaultAsync(x =>
            x.UserId == userId && x.WordId == current.WordId, cancellationToken);
        var legacy = progress is not null && progress.FsrsState is null;
        var retention = await db.Users.Where(x => x.Id == userId)
            .Select(x => x.DesiredRetention).SingleAsync(cancellationToken);
        var before = FsrsWordScheduler.Snapshot(progress);
        var rating = FsrsWordScheduler.Normalize(request.Result.Value);
        var card = FsrsWordScheduler.Rate(progress, rating, now, retention);
        if (progress is null)
        {
            progress = new UserWordProgress { UserId = userId, WordId = current.WordId, FirstStudiedAt = now };
            db.UserWordProgress.Add(progress);
        }
        progress.ImportedFromLegacy |= legacy;
        FsrsWordScheduler.Apply(progress, card, now);
        if (expectedType == WordStudySessionType.Review)
        {
            progress.LastReviewedAt = now;
            progress.ReviewCount++;
            if (rating == WordMemorizationResult.Again) progress.FailedReviewCount++;
            else progress.SuccessfulReviewCount++;
        }
        db.WordReviewLogs.Add(new WordReviewLog
        {
            UserId = userId, WordId = current.WordId, SessionItemId = current.Id,
            SubmissionStamp = request.ItemConcurrencyStamp, Rating = rating,
            RatedAt = now, DueAt = new DateTimeOffset(card.Due), DesiredRetention = retention,
            SchedulerVersion = FsrsWordScheduler.Version, ImportedFromLegacy = legacy,
            BeforeStateJson = before, AfterStateJson = FsrsWordScheduler.Snapshot(progress)
        });
        current.MemorizationAttemptCount++;
        current.HadMemorizationFailure |= rating == WordMemorizationResult.Again;
        if (card.Due - now.UtcDateTime >= TimeSpan.FromDays(1))
        {
            current.MemorizationPassedAt = now;
            current.MemorizationAvailableAt = null;
        }
        else
        {
            // Sub-day schedules are practice queue positions, not blocking timers.
            current.MemorizationAvailableAt = null;
            current.MemorizationQueueOrder = session.Items.Max(x => x.MemorizationQueueOrder) + 1;
        }
        current.ConcurrencyStamp = Guid.NewGuid();
        if (session.Items.Where(value => value.Status == WordStudySessionItemStatus.Pending)
            .All(value => value.MemorizationPassedAt is not null))
        {
            session.Phase = WordStudyPhase.Summary;
            session.ConcurrencyStamp = Guid.NewGuid();
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WordStudyCommandResponse(
            null,
            await new WordStudySessionProjector(db, timeProvider)
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
                     .Where(value =>
                         value.Status == WordStudySessionItemStatus.Pending &&
                         (session.Phase != WordStudyPhase.Memorization ||
                             value.MemorizationPassedAt == null))
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
            session.Phase = WordStudyPhase.Summary;
        }
    }

    private async Task<WordStudyCommandResponse> PersistNormalizedSessionAsync(
        Guid userId,
        WordStudySession session,
        IApplicationDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (session.SessionType == WordStudySessionType.Learning &&
            session.Status == WordStudySessionStatus.Completed)
        {
            await AddCheckInIfMissingAsync(
                userId,
                session.CompletedAt ?? timeProvider.GetUtcNow(),
                cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WordStudyCommandResponse(
            null,
            await new WordStudySessionProjector(db, timeProvider)
                .ProjectAsync(userId, session, cancellationToken));
    }

    private async Task AddActivityIfMissingAsync(
        Guid userId,
        WordStudySessionItem item,
        WordStudySessionType sessionType,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        var activityType = sessionType == WordStudySessionType.Learning
            ? WordStudyActivityType.Learning
            : WordStudyActivityType.Review;
        var exists = await db.WordStudyActivities.AnyAsync(value =>
            value.SessionItemId == item.Id && value.ActivityType == activityType,
            cancellationToken);
        if (exists)
        {
            return;
        }
        db.WordStudyActivities.Add(new WordStudyActivity
        {
            UserId = userId,
            WordId = item.WordId,
            SessionId = item.SessionId,
            SessionItemId = item.Id,
            ActivityType = activityType,
            CompletedAtUtc = completedAtUtc,
            CreatedAt = completedAtUtc
        });
    }

    private async Task AddCheckInIfMissingAsync(
        Guid userId,
        DateTimeOffset checkedInAtUtc,
        CancellationToken cancellationToken)
    {
        var studyDate = new DateTimeOffset(
            checkedInAtUtc.UtcDateTime.Date,
            TimeSpan.Zero);
        var exists = await db.WordStudyCheckIns.AnyAsync(value =>
            value.UserId == userId && value.StudyDateUtc == studyDate,
            cancellationToken);
        if (exists)
        {
            return;
        }
        db.WordStudyCheckIns.Add(new WordStudyCheckIn
        {
            UserId = userId,
            StudyDateUtc = studyDate,
            CheckedInAtUtc = checkedInAtUtc,
            CreatedAt = checkedInAtUtc
        });
    }
}
