using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 编排单词学习、复习会话及其概览查询。
/// </summary>
public sealed class WordStudyService : IWordStudyService
{
    private const string ActiveLearningIndex =
        "IX_word_study_sessions_UserId_ActiveLearning";
    private const string ActiveReviewIndex =
        "IX_word_study_sessions_UserId_ActiveReview";
    private const string ProgressUniqueIndex =
        "IX_user_word_progress_UserId_WordId";

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;

    public WordStudyService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
    }

    public Task<WordStudySessionStateResponse> FinishSummaryAsync(Guid userId, Guid sessionId,
        WordStudySessionType type, bool skipSpelling, CancellationToken cancellationToken = default)
        => new WordStudySessionEngine(_db, _timeProvider).FinishSummaryAsync(userId, sessionId, type, skipSpelling, cancellationToken);

    /// <summary>Allows a waiting group to add new words or finish its separate spelling practice.</summary>
    public async Task<WordStudySessionStateResponse> ContinueWaitingSessionAsync(
        Guid userId, Guid sessionId, WordStudySessionType type, bool addNewWords,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = (type == WordStudySessionType.Learning
            ? await LoadLearningSessionAsync(userId, sessionId, cancellationToken)
            : await LoadReviewSessionAsync(userId, sessionId, cancellationToken))
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.Status != WordStudySessionStatus.Active || session.Phase != WordStudyPhase.Memorization)
            throw ConflictException.Create(ErrorCodes.WordStudyPhaseConflict);
        var engine = new WordStudySessionEngine(_db, _timeProvider);
        await engine.NormalizeCurrentItemAsync(session, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        // A card becoming due between the click and this transaction wins over either action.
        if (session.Status == WordStudySessionStatus.Active && session.Phase == WordStudyPhase.Memorization &&
            !session.Items.Any(x => x.Status == WordStudySessionItemStatus.Pending &&
                x.MemorizationPassedAt == null && (x.MemorizationAvailableAt == null || x.MemorizationAvailableAt <= now)))
        {
            if (addNewWords)
            {
                if (type != WordStudySessionType.Learning || session.ActualCount >= 100)
                    throw ConflictException.Create(ErrorCodes.WordStudyNoEligibleWords);
                var count = await _db.Users.Where(x => x.Id == userId).Select(x => x.DailyWordStudyCount)
                    .SingleAsync(cancellationToken);
                var ids = await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
                    .Where(w => !_db.UserWordProgress.Any(p => p.UserId == userId && p.WordId == w.Id) &&
                        !_db.WordStudySessionItems.Any(i => i.WordId == w.Id && i.Session!.UserId == userId &&
                            i.Session.Status == WordStudySessionStatus.Active))
                    .OrderBy(w => w.StudyOrder).Take(Math.Min(count, 100 - session.ActualCount))
                    .Select(w => w.Id).ToListAsync(cancellationToken);
                if (ids.Count == 0) throw ConflictException.Create(ErrorCodes.WordStudyNoEligibleWords);
                var position = session.Items.Max(x => x.Position) + 1;
                var memoryOrder = session.Items.Max(x => x.MemorizationQueueOrder) + 1;
                var spellingOrder = session.Items.Max(x => x.SpellingQueueOrder) + 1;
                session.Items = session.Items.ToList();
                foreach (var id in ids)
                {
                    var item = new WordStudySessionItem { SessionId = session.Id, WordId = id,
                        Position = position++, MemorizationQueueOrder = memoryOrder++, SpellingQueueOrder = spellingOrder++ };
                    session.Items.Add(item);
                    _db.WordStudySessionItems.Add(item);
                }
                session.ActualCount += ids.Count;
                session.RequestedCount = Math.Max(session.RequestedCount, session.ActualCount);
            }
            else
            {
                // This ends this group's recall round, not the persistent FSRS learning state.
                // Completing spelling releases scheduled cards to the review queue at their original due times.
                if (session.Items.Any(x => x.Status == WordStudySessionItemStatus.Pending && x.MemorizationAttemptCount == 0))
                    throw ConflictException.Create(ErrorCodes.WordStudyQueueConflict);
                session.Phase = WordStudyPhase.Spelling;
            }
            session.ConcurrencyStamp = Guid.NewGuid();
        }
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var loaded = type == WordStudySessionType.Learning
            ? await LoadLearningSessionAsync(userId, sessionId, cancellationToken)
            : await LoadReviewSessionAsync(userId, sessionId, cancellationToken);
        return await ProjectStateAsync(userId, loaded!, cancellationToken);
    }

    public async Task<WordLearningOverviewResponse> GetLearningOverviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var total = await _db.UserWordProgress.AsNoTracking()
            .CountAsync(value => value.UserId == userId, cancellationToken);
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var today = await _db.UserWordProgress.AsNoTracking()
            .CountAsync(value => value.UserId == userId &&
                value.FirstStudiedAt >= todayStart && value.FirstStudiedAt < todayStart.AddDays(1),
                cancellationToken);
        var hasMore = await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
            .AnyAsync(word => !_db.UserWordProgress.Any(progress =>
                progress.UserId == userId && progress.WordId == word.Id),
                cancellationToken);
        var active = await LoadLearningSessionAsync(userId, null, cancellationToken);
        return new WordLearningOverviewResponse(
            total,
            today,
            hasMore,
            active is null ? null : await ProjectStateAsync(userId, active, cancellationToken));
    }

    public async Task<WordStudySessionStateResponse> StartLearningSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var createdSessionId = Guid.Empty;
        try
        {
            await using (var transaction = await _db.BeginTransactionAsync(cancellationToken))
            {
                await _db.AcquireWordStudySessionLockAsync(
                    userId,
                    WordStudySessionType.Learning,
                    cancellationToken);
                var existing = await LoadLearningSessionAsync(userId, null, cancellationToken);
                if (existing is not null)
                {
                    var response = await ProjectStateAsync(userId, existing, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return response;
                }
                var count = await _db.Users.AsNoTracking()
                    .Where(value => value.Id == userId)
                    .Select(value => (int?)value.DailyWordStudyCount)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
                var wordIds = await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
                    .Where(word => !_db.UserWordProgress.AsNoTracking().Any(progress =>
                        progress.UserId == userId && progress.WordId == word.Id))
                    .OrderBy(word => word.StudyOrder)
                    .Take(count)
                    .Select(word => word.Id)
                    .ToListAsync(cancellationToken);
                if (wordIds.Count == 0)
                {
                    throw ConflictException.Create(ErrorCodes.WordStudyNoEligibleWords);
                }
                var now = _timeProvider.GetUtcNow();
                var session = new WordStudySession
                {
                    UserId = userId,
                    RequestedCount = count,
                    ActualCount = wordIds.Count,
                    SessionType = WordStudySessionType.Learning,
                    Phase = WordStudyPhase.Memorization,
                    StartedAt = now,
                    Items = wordIds.Select((wordId, position) => new WordStudySessionItem
                    {
                        WordId = wordId,
                        Position = position,
                        MemorizationQueueOrder = position,
                        SpellingQueueOrder = position
                    }).ToArray()
                };
                _db.WordStudySessions.Add(session);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                createdSessionId = session.Id;
            }
            return await GetLearningSessionAsync(userId, createdSessionId, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                ActiveLearningIndex))
        {
            _db.ClearTrackedChanges();
            var concurrent = await LoadLearningSessionAsync(
                userId,
                null,
                cancellationToken)
                ?? throw ConflictException.Create(
                    ErrorCodes.WordStudyConcurrencyConflict);
            return await ProjectStateAsync(userId, concurrent, cancellationToken);
        }
    }

    public async Task<WordStudySessionStateResponse> GetLearningSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await LoadLearningSessionAsync(userId, sessionId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        var engine = new WordStudySessionEngine(_db, _timeProvider);
        await engine.NormalizeCurrentItemAsync(session, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var response = await ProjectStateAsync(userId, session, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<WordStudyCompletedItemResponse>> GetCompletedSessionItemsAsync(
        Guid userId,
        Guid sessionId,
        WordStudySessionType expectedType,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.WordStudySessions.AsNoTracking()
            .Include(value => value.Items)
                .ThenInclude(value => value.Word)
            .SingleOrDefaultAsync(value => value.UserId == userId && value.Id == sessionId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.SessionType != expectedType)
            throw ConflictException.Create(ErrorCodes.WordStudySessionTypeConflict);
        return session.Items
            .Where(value => value.Status != WordStudySessionItemStatus.Pending)
            .OrderBy(value => value.Position)
            .Select(value => new WordStudyCompletedItemResponse(
                value.Id,
                value.WordId,
                value.Position,
                value.Status,
                value.Word?.Headword,
                value.HadMemorizationFailure,
                value.HadSpellingFailure))
            .ToArray();
    }

    public Task<WordStudyCommandResponse> SubmitLearningMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(
            () => new WordStudySessionEngine(_db, _timeProvider)
                .SubmitMemorizationAsync(
                    userId,
                    sessionId,
                    itemId,
                    WordStudySessionType.Learning,
                    request,
                    cancellationToken),
            cancellationToken);

    public Task<WordStudyCommandResponse> SubmitReviewMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(
            () => new WordStudySessionEngine(_db, _timeProvider)
                .SubmitMemorizationAsync(
                    userId,
                    sessionId,
                    itemId,
                    WordStudySessionType.Review,
                    request,
                    cancellationToken),
            cancellationToken);

    public Task<WordStudyCommandResponse> SubmitLearningSpellingAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordSpellingRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(
            () => new WordStudySessionEngine(_db, _timeProvider)
                .SubmitSpellingAsync(
                    userId,
                    sessionId,
                    itemId,
                    WordStudySessionType.Learning,
                    request,
                    cancellationToken),
            cancellationToken);

    public Task<WordStudyCommandResponse> SubmitReviewSpellingAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordSpellingRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(
            () => new WordStudySessionEngine(_db, _timeProvider)
                .SubmitSpellingAsync(
                    userId,
                    sessionId,
                    itemId,
                    WordStudySessionType.Review,
                    request,
                    cancellationToken),
            cancellationToken);

    public async Task<WordReviewOverviewResponse> GetReviewOverviewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var eligible =
            from progress in _db.UserWordProgress.AsNoTracking()
            join word in WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
                on progress.WordId equals word.Id
            where progress.UserId == userId && !progress.IsReviewExcluded &&
                progress.NextReviewAt != null && !_db.WordStudySessionItems.Any(item =>
                    item.WordId == progress.WordId && item.Session!.UserId == userId &&
                    item.Session.Status == WordStudySessionStatus.Active)
            select progress.NextReviewAt;
        var due = await eligible.CountAsync(
            nextReviewAt => nextReviewAt <= now,
            cancellationToken);
        var overdue = await eligible.CountAsync(
            nextReviewAt => nextReviewAt < dayStart,
            cancellationToken);
        var active = await LoadReviewSessionAsync(userId, null, cancellationToken);
        return new WordReviewOverviewResponse(
            due,
            overdue,
            active is null ? null : await ProjectStateAsync(userId, active, cancellationToken));
    }

    public async Task<WordStudySessionStateResponse> StartReviewSessionAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var createdSessionId = Guid.Empty;
        try
        {
            await using (var transaction = await _db.BeginTransactionAsync(cancellationToken))
            {
                await _db.AcquireWordStudySessionLockAsync(
                    userId,
                    WordStudySessionType.Learning,
                    cancellationToken);
                var existing = await LoadReviewSessionAsync(userId, null, cancellationToken);
                if (existing is not null)
                {
                    var response = await ProjectStateAsync(userId, existing, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return response;
                }
                var count = await _db.Users.AsNoTracking().Where(value => value.Id == userId)
                    .Select(value => (int?)value.DailyWordReviewCount)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
                var now = _timeProvider.GetUtcNow();
                var ids = await (
                    from progress in _db.UserWordProgress.AsNoTracking()
                    join word in WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
                        on progress.WordId equals word.Id
                    where progress.UserId == userId && !progress.IsReviewExcluded &&
                        progress.NextReviewAt != null && progress.NextReviewAt <= now &&
                        !_db.WordStudySessionItems.Any(item => item.WordId == progress.WordId &&
                            item.Session!.UserId == userId && item.Session.Status == WordStudySessionStatus.Active)
                    orderby progress.NextReviewAt, word.StudyOrder
                    select word.Id)
                    .Take(count)
                    .ToListAsync(cancellationToken);
                if (ids.Count == 0)
                    throw ConflictException.Create(ErrorCodes.WordReviewNoDueWords);
                var session = new WordStudySession
                {
                    UserId = userId,
                    RequestedCount = count,
                    ActualCount = ids.Count,
                    SessionType = WordStudySessionType.Review,
                    Phase = WordStudyPhase.Memorization,
                    StartedAt = now,
                    Items = ids.Select((id, position) => new WordStudySessionItem
                    {
                        WordId = id,
                        Position = position,
                        MemorizationQueueOrder = position,
                        SpellingQueueOrder = position
                    }).ToArray()
                };
                _db.WordStudySessions.Add(session);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                createdSessionId = session.Id;
            }
            return await GetReviewSessionAsync(userId, createdSessionId, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                ActiveReviewIndex))
        {
            _db.ClearTrackedChanges();
            var concurrent = await LoadReviewSessionAsync(
                userId,
                null,
                cancellationToken)
                ?? throw ConflictException.Create(
                    ErrorCodes.WordStudyConcurrencyConflict);
            return await ProjectStateAsync(userId, concurrent, cancellationToken);
        }
    }

    public async Task<WordStudySessionStateResponse> GetReviewSessionAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireWordStudySessionLockAsync(userId, WordStudySessionType.Learning, cancellationToken);
        var session = await LoadReviewSessionAsync(userId, sessionId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        await new WordStudySessionEngine(_db, _timeProvider)
            .NormalizeCurrentItemAsync(session, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var response = await ProjectStateAsync(userId, session, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public Task<WordStudyCommandResponse> ExcludeFromReviewAsync(
        Guid userId, Guid sessionId, Guid itemId, ExcludeWordFromReviewRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(
            () => new WordStudySessionEngine(_db, _timeProvider)
                .ExcludeFromReviewAsync(
                    userId,
                    sessionId,
                    itemId,
                    request,
                    cancellationToken),
            cancellationToken);

    public async Task<WordStudyTodayReviewResponse> GetTodayReviewAsync(
        Guid userId,
        WordStudyTodayReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime.Date);
        var dayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var query = _db.WordStudyActivities.AsNoTracking()
            .AsSplitQuery()
            .Include(value => value.Word!)
                .ThenInclude(value => value.Senses)
                    .ThenInclude(value => value.Examples)
            .Where(value => value.UserId == userId &&
                value.CompletedAtUtc >= dayStart && value.CompletedAtUtc < dayEnd &&
                value.Word != null && !value.Word.IsDeleted && value.Word.Senses.Any())
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenBy(value => value.WordId)
            .ThenBy(value => value.Id);
        var total = await query.CountAsync(cancellationToken);
        var activities = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var favoriteIds = await _db.UserWordFavorites.AsNoTracking()
            .Where(value => value.UserId == userId && activities.Select(item => item.WordId).Contains(value.WordId))
            .Select(value => value.WordId)
            .ToHashSetAsync(cancellationToken);
        var items = activities.Select(value =>
        {
            var word = value.Word!;
            return new WordStudyTodayReviewItemResponse(
                word.Id,
                word.Headword,
                value.ActivityType,
                value.CompletedAtUtc,
                word.Senses.OrderBy(sense => sense.SortOrder).ThenBy(sense => sense.Id)
                    .Select(sense => new WordSenseResponse(
                        sense.PartOfSpeech,
                        sense.Definition,
                        sense.UsageNote,
                        sense.SortOrder,
                        sense.Examples.OrderBy(example => example.SortOrder).ThenBy(example => example.Id)
                            .Select(example => new ExampleSentenceResponse(
                                example.Sentence,
                                example.Translation,
                                example.AudioResourceId,
                                example.SortOrder))
                            .ToArray()))
                    .ToArray(),
                word.AudioResourceId,
                favoriteIds.Contains(word.Id));
        }).ToArray();
        return new WordStudyTodayReviewResponse(
            today,
            items,
            request.Page,
            request.PageSize,
            total,
            total == 0 ? 0 : (total + request.PageSize - 1) / request.PageSize);
    }

    public async Task<WordStudyCheckInCalendarResponse> GetCheckInCalendarAsync(
        Guid userId,
        WordStudyCheckInCalendarRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var year = request.Year ?? now.UtcDateTime.Year;
        var month = request.Month ?? now.UtcDateTime.Month;
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var monthStartUtc = GetUtcMonthStart(monthStart);
        var monthEndUtc = GetUtcMonthStart(monthEnd);
        var rowValues = await _db.WordStudyCheckIns.AsNoTracking()
            .Where(value => value.UserId == userId &&
                value.StudyDateUtc >= monthStartUtc &&
                value.StudyDateUtc < monthEndUtc)
            .OrderBy(value => value.StudyDateUtc)
            .Select(value => new { value.StudyDateUtc, value.CheckedInAtUtc })
            .ToListAsync(cancellationToken);
        var rows = rowValues
            .Select(value => new WordStudyCheckInResponse(
                DateOnly.FromDateTime(value.StudyDateUtc.UtcDateTime),
                value.CheckedInAtUtc))
            .ToArray();
        var allDates = await _db.WordStudyCheckIns.AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => value.StudyDateUtc)
            .ToListAsync(cancellationToken);
        var dates = allDates
            .Select(value => DateOnly.FromDateTime(value.UtcDateTime))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var today = DateOnly.FromDateTime(now.UtcDateTime.Date);
        var current = GetCurrentStreak(dates, today);
        var longest = GetLongestStreak(dates);
        return new WordStudyCheckInCalendarResponse(
            year,
            month,
            rows,
            current,
            longest,
            dates.Length);
    }

    private static int GetCurrentStreak(IReadOnlyList<DateOnly> dates, DateOnly today)
    {
        if (dates.Count == 0) return 0;
        var target = dates.Contains(today) ? today : today.AddDays(-1);
        var set = dates.ToHashSet();
        if (!set.Contains(target)) return 0;
        var count = 0;
        while (set.Contains(target))
        {
            count++;
            target = target.AddDays(-1);
        }
        return count;
    }

    internal static DateTimeOffset GetUtcMonthStart(DateOnly date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);

    private static int GetLongestStreak(IReadOnlyList<DateOnly> dates)
    {
        var longest = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var date in dates)
        {
            current = previous.HasValue && date == previous.Value.AddDays(1) ? current + 1 : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }
        return longest;
    }

    private async Task<WordStudyCommandResponse> ExecuteCommandAsync(
        Func<Task<WordStudyCommandResponse>> command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await command();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ClearTrackedChanges();
            throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                ProgressUniqueIndex))
        {
            _db.ClearTrackedChanges();
            throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        }
    }

    private async Task<WordStudySession?> LoadLearningSessionAsync(
        Guid userId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var query = _db.WordStudySessions
            .AsSplitQuery()
            .Include(value => value.Items)
                .ThenInclude(value => value.Word!)
                    .ThenInclude(value => value.Senses)
                        .ThenInclude(value => value.Examples)
            .Where(value => value.UserId == userId &&
                value.SessionType == WordStudySessionType.Learning);
        if (sessionId.HasValue)
        {
            query = query.Where(value => value.Id == sessionId.Value);
        }
        else
        {
            query = query.Where(value => value.Status == WordStudySessionStatus.Active);
        }
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<WordStudySession?> LoadReviewSessionAsync(
        Guid userId, Guid? sessionId, CancellationToken cancellationToken)
    {
        var query = _db.WordStudySessions
            .AsSplitQuery()
            .Include(value => value.Items)
                .ThenInclude(value => value.Word!)
                    .ThenInclude(value => value.Senses)
                        .ThenInclude(value => value.Examples)
            .Where(value => value.UserId == userId && value.SessionType == WordStudySessionType.Review);
        query = sessionId.HasValue
            ? query.Where(value => value.Id == sessionId.Value)
            : query.Where(value => value.Status == WordStudySessionStatus.Active);
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<WordStudySessionStateResponse> ProjectStateAsync(
        Guid userId,
        WordStudySession session,
        CancellationToken cancellationToken)
        => await new WordStudySessionProjector(_db, _timeProvider)
            .ProjectAsync(userId, session, cancellationToken);

}
