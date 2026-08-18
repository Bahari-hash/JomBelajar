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
        var existing = await LoadLearningSessionAsync(userId, null, cancellationToken);
        if (existing is not null)
        {
            return await ProjectStateAsync(userId, existing, cancellationToken);
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
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
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
        return await GetLearningSessionAsync(userId, session.Id, cancellationToken);
    }

    public async Task<WordStudySessionStateResponse> GetLearningSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
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
                progress.NextReviewAt != null
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
        var existing = await LoadReviewSessionAsync(userId, null, cancellationToken);
        if (existing is not null)
            return await ProjectStateAsync(userId, existing, cancellationToken);
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
                progress.NextReviewAt != null && progress.NextReviewAt <= now
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
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
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
        return await GetReviewSessionAsync(userId, session.Id, cancellationToken);
    }

    public async Task<WordStudySessionStateResponse> GetReviewSessionAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
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
        => await new WordStudySessionProjector(_db)
            .ProjectAsync(userId, session, cancellationToken);

}
