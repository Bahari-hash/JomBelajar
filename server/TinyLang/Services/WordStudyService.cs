using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 实现固定选词会话、实时内容检查、累计进度和并发幂等规则。
/// </summary>
public sealed class WordStudyService : IWordStudyService
{
    private const string ActiveSessionUniqueIndex =
        "IX_word_study_sessions_UserId";
    private const string DailySessionUniqueIndex =
        "IX_word_study_sessions_UserId_StudyDateUtc";
    private const string ProgressUniqueIndex =
        "IX_user_word_progress_UserId_WordId";

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WordStudyService> _logger;

    /// <summary>
    /// 使用数据库、约束分类器、时间源和结构化日志创建背诵服务。
    /// </summary>
    public WordStudyService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider,
        ILogger<WordStudyService> logger)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WordStudyTodayResponse> GetTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = GetStudyDateUtc();
        var dailyCount = await _db.Users.AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBanned)
            .Select(user => (int?)user.DailyWordStudyCount)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
        var session = await GetTodaySessionAsync(userId, today, cancellationToken);
        return new WordStudyTodayResponse(
            today,
            dailyCount,
            session is null
                ? WordStudyTodayState.NotStarted
                : session.Status == WordStudySessionStatus.Active
                    ? WordStudyTodayState.Active
                    : WordStudyTodayState.Completed,
            session);
    }

    /// <inheritdoc />
    public async Task<WordStudySessionResponse> StartTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = GetStudyDateUtc();
        var dailyCount = await _db.Users.AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBanned)
            .Select(user => (int?)user.DailyWordStudyCount)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

        try
        {
            return await StartTodayCoreAsync(
                userId,
                today,
                dailyCount,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                DailySessionUniqueIndex))
        {
            _db.ClearTrackedChanges();
            return await GetTodaySessionAsync(userId, today, cancellationToken)
                ?? throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        }
    }

    /// <summary>
    /// 在事务中恢复或创建今日会话，并将跨日活动会话标记为放弃。
    /// </summary>
    private async Task<WordStudySessionResponse> StartTodayCoreAsync(
        Guid userId,
        DateTimeOffset today,
        int dailyCount,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var existingToday = await GetTodaySessionAsync(userId, today, cancellationToken);
        if (existingToday is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existingToday;
        }

        var wordIds = await SelectWordIdsAsync(
            userId,
            dailyCount,
            includePreviouslyStudied: true,
            WordStudySelectionMode.Sequential,
            languageTag: null,
            cancellationToken);
        if (wordIds.Count == 0)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyNoEligibleWords);
        }

        var staleActive = await _db.WordStudySessions.SingleOrDefaultAsync(
            value => value.UserId == userId &&
                value.Status == WordStudySessionStatus.Active &&
                value.StudyDateUtc < today,
            cancellationToken);
        if (staleActive is not null)
        {
            staleActive.Status = WordStudySessionStatus.Abandoned;
            staleActive.AbandonedAt = _timeProvider.GetUtcNow();
            staleActive.ConcurrencyStamp = Guid.NewGuid();
        }

        var now = _timeProvider.GetUtcNow();
        var session = new WordStudySession
        {
            UserId = userId,
            RequestedCount = dailyCount,
            ActualCount = wordIds.Count,
            IncludePreviouslyStudied = true,
            SelectionMode = WordStudySelectionMode.Sequential,
            StudyDateUtc = today,
            StartedAt = now,
            Items = wordIds.Select((wordId, position) => new WordStudySessionItem
            {
                WordId = wordId,
                Position = position
            }).ToArray()
        };
        _db.WordStudySessions.Add(session);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetSessionAsync(userId, session.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WordStudySessionResponse> CreateSessionAsync(
        Guid userId,
        CreateWordStudySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        var languageTag = request.LanguageTag is null
            ? null
            : WordTextNormalizer.NormalizeLanguageTag(request.LanguageTag);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        if (await _db.WordStudySessions.AsNoTracking().AnyAsync(
            value => value.UserId == userId &&
                value.Status == WordStudySessionStatus.Active,
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.WordStudyActiveSessionExists);
        }

        var wordIds = await SelectWordIdsAsync(
            userId,
            request.WordCount,
            request.IncludePreviouslyStudied,
            request.SelectionMode,
            languageTag,
            cancellationToken);
        if (wordIds.Count == 0)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyNoEligibleWords);
        }

        var now = _timeProvider.GetUtcNow();
        var session = new WordStudySession
        {
            UserId = userId,
            RequestedCount = request.WordCount,
            ActualCount = wordIds.Count,
            IncludePreviouslyStudied = request.IncludePreviouslyStudied,
            SelectionMode = request.SelectionMode,
            LanguageTag = languageTag,
            StudyDateUtc = NormalizeStudyDate(now),
            StartedAt = now,
            Items = wordIds.Select((wordId, position) =>
                new WordStudySessionItem
                {
                    WordId = wordId,
                    Position = position
                }).ToArray()
        };
        _db.WordStudySessions.Add(session);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                ActiveSessionUniqueIndex) ||
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                DailySessionUniqueIndex))
        {
            throw ConflictException.Create(ErrorCodes.WordStudyActiveSessionExists);
        }

        _logger.LogInformation(
            "Created word study session {SessionId} for user {UserId} with {ActualCount} items",
            session.Id,
            userId,
            session.ActualCount);
        return await GetSessionAsync(userId, session.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WordStudySessionResponse?> GetActiveSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await ProjectSessionSummary(_db.WordStudySessions.AsNoTracking()
                .Where(value => value.UserId == userId &&
                    value.Status == WordStudySessionStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<WordStudySessionResponse> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await ProjectSessionSummary(_db.WordStudySessions.AsNoTracking()
                .Where(value => value.UserId == userId && value.Id == sessionId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WordStudySessionItemResponse>> GetSessionItemsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var ownsSession = await _db.WordStudySessions.AsNoTracking().AnyAsync(
            value => value.UserId == userId && value.Id == sessionId,
            cancellationToken);
        if (!ownsSession)
        {
            throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        }

        var itemRows = await _db.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == sessionId)
            .OrderBy(value => value.Position)
            .ThenBy(value => value.Id)
            .Select(value => new
            {
                ItemId = value.Id,
                value.WordId,
                value.Position,
                value.Status
            })
            .ToListAsync(cancellationToken);
        var wordIds = itemRows.Select(value => value.WordId).ToArray();
        var visibleContent = await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
            .Where(value => wordIds.Contains(value.Id))
            .Select(word => new
            {
                word.Id,
                Content = new WordStudySessionItemContentResponse(
                    word.Headword,
                    word.Senses.OrderBy(sense => sense.SortOrder)
                        .ThenBy(sense => sense.Id)
                        .Select(sense => new WordSenseResponse(
                            sense.PartOfSpeech,
                            sense.Definition,
                            sense.DefinitionLanguageTag,
                            sense.UsageNote,
                            sense.SortOrder,
                            sense.Examples.OrderBy(example => example.SortOrder)
                                .ThenBy(example => example.Id)
                                .Select(example => new ExampleSentenceResponse(
                                    example.Sentence,
                                    example.LanguageTag,
                                    example.Translation,
                                    example.TranslationLanguageTag,
                                    example.AudioClipId,
                                    example.SortOrder))
                                .ToList()))
                        .ToList(),
                    word.Pronunciations.OrderBy(value => value.SortOrder)
                        .ThenBy(value => value.Id)
                        .Select(value => new WordPronunciationResponse(
                            value.AudioClipId,
                            value.AccentTag,
                            value.Ipa,
                            value.IsDefault,
                            value.SortOrder))
                        .ToList())
            })
            .ToDictionaryAsync(
                value => value.Id,
                value => value.Content,
                cancellationToken);

        return itemRows.Select(row =>
        {
            var contentAvailable = visibleContent.TryGetValue(
                row.WordId,
                out var content);
            return new WordStudySessionItemResponse(
                row.ItemId,
                row.WordId,
                row.Position,
                row.Status,
                contentAvailable,
                content);
        }).ToArray();
    }

    /// <inheritdoc />
    public async Task<WordStudyNextItemResponse?> GetNextItemAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var session = await _db.WordStudySessions.SingleOrDefaultAsync(
            value => value.UserId == userId && value.Id == sessionId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        EnsureActive(session);

        var pendingItems = await _db.WordStudySessionItems
            .Where(value => value.SessionId == sessionId &&
                value.Status == WordStudySessionItemStatus.Pending)
            .OrderBy(value => value.Position)
            .ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        var response = await (
            from item in _db.WordStudySessionItems.AsNoTracking()
            join word in WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
                on item.WordId equals word.Id
            where item.SessionId == sessionId &&
                item.Status == WordStudySessionItemStatus.Pending
            orderby item.Position, item.Id
            select new WordStudyNextItemResponse(
                session.Id,
                item.Id,
                item.Position,
                session.ActualCount,
                word.Id,
                word.Headword,
                word.LanguageTag,
                word.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => new WordSenseResponse(
                        sense.PartOfSpeech,
                        sense.Definition,
                        sense.DefinitionLanguageTag,
                        sense.UsageNote,
                        sense.SortOrder,
                        sense.Examples.OrderBy(example => example.SortOrder)
                            .ThenBy(example => example.Id)
                            .Select(example => new ExampleSentenceResponse(
                                example.Sentence,
                                example.LanguageTag,
                                example.Translation,
                                example.TranslationLanguageTag,
                                example.AudioClipId,
                                example.SortOrder))
                            .ToList()))
                    .ToList(),
                word.Pronunciations.OrderBy(value => value.SortOrder)
                    .ThenBy(value => value.Id)
                    .Select(value => new WordPronunciationResponse(
                        value.AudioClipId,
                        value.AccentTag,
                        value.Ipa,
                        value.IsDefault,
                        value.SortOrder))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var changed = false;

        foreach (var item in pendingItems.Where(item => response is null ||
                     item.Position < response.Position ||
                     (item.Position == response.Position &&
                      item.Id.CompareTo(response.ItemId) < 0)))
        {
            MarkContentUnavailable(item, now);
            changed = true;
        }

        if (response is null)
        {
            CompleteSession(session, now);
            changed = true;
        }
        else if (changed)
        {
            session.ConcurrencyStamp = Guid.NewGuid();
        }

        if (changed)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    /// <inheritdoc />
    public async Task<WordStudySessionResponse> SubmitResultAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordStudyResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = ValidateResult(request.Result);
        try
        {
            return await SubmitResultCoreAsync(
                userId,
                sessionId,
                itemId,
                result,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveResultConcurrencyAsync(
                userId,
                sessionId,
                itemId,
                result,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                ProgressUniqueIndex))
        {
            return await ResolveResultConcurrencyAsync(
                userId,
                sessionId,
                itemId,
                result,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<WordStudySessionResponse> AbandonSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await AbandonSessionCoreAsync(userId, sessionId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ClearTrackedChanges();
            var session = await _db.WordStudySessions.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.UserId == userId && value.Id == sessionId,
                    cancellationToken)
                ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
            if (session.Status == WordStudySessionStatus.Abandoned)
            {
                return await GetSessionAsync(userId, sessionId, cancellationToken);
            }

            throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
        }
    }

    /// <summary>
    /// 防御性校验创建参数，避免 service 被非 HTTP 调用绕过 validator。
    /// </summary>
    private static void ValidateCreateRequest(CreateWordStudySessionRequest request)
    {
        if (request.WordCount is < WordStudyConstraints.MinWordCount or
            > WordStudyConstraints.MaxWordCount)
        {
            throw new RequestValidationException(ErrorCodes.WordStudyWordCountInvalid);
        }
        if (!Enum.IsDefined(request.SelectionMode))
        {
            throw new RequestValidationException(
                ErrorCodes.WordStudySelectionModeInvalid);
        }
        if (request.LanguageTag is { } languageTag &&
            (string.IsNullOrWhiteSpace(languageTag) ||
             languageTag.Length > WordConstraints.MaxLanguageTagLength ||
             !MediaValidationPatterns.LanguageTag().IsMatch(languageTag)))
        {
            throw new RequestValidationException(ErrorCodes.WordLanguageInvalid);
        }
    }

    /// <summary>
    /// 获取当前时间对应的 UTC 日期零点。
    /// </summary>
    private DateTimeOffset GetStudyDateUtc()
        => NormalizeStudyDate(_timeProvider.GetUtcNow());

    /// <summary>
    /// 将任意带偏移时间归一化为 UTC 日期零点。
    /// </summary>
    private static DateTimeOffset NormalizeStudyDate(DateTimeOffset value)
        => new(value.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>
    /// 查询用户在指定 UTC 日期的唯一会话摘要。
    /// </summary>
    private async Task<WordStudySessionResponse?> GetTodaySessionAsync(
        Guid userId,
        DateTimeOffset today,
        CancellationToken cancellationToken)
        => await ProjectSessionSummary(_db.WordStudySessions.AsNoTracking()
                .Where(value => value.UserId == userId && value.StudyDateUtc == today))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 防御性校验 result 枚举只包含模块支持的两个值。
    /// </summary>
    private static WordStudyResult ValidateResult(WordStudyResult? result)
    {
        if (result is not { } value || !Enum.IsDefined(value))
        {
            throw new RequestValidationException(ErrorCodes.WordStudyResultInvalid);
        }

        return value;
    }

    /// <summary>
    /// 根据模式从实时可见范围中选择不重复且有界的 WordId。
    /// </summary>
    private async Task<IReadOnlyList<Guid>> SelectWordIdsAsync(
        Guid userId,
        int wordCount,
        bool includePreviouslyStudied,
        WordStudySelectionMode selectionMode,
        string? languageTag,
        CancellationToken cancellationToken)
    {
        var visible = WordVisibilityPolicy.Apply(_db.Words.AsNoTracking());
        if (languageTag is not null)
        {
            visible = visible.Where(value => value.LanguageTag == languageTag);
        }

        if (selectionMode == WordStudySelectionMode.Random)
        {
            var candidates = includePreviouslyStudied
                ? visible
                : visible.Where(word => !_db.UserWordProgress.AsNoTracking().Any(
                    progress => progress.UserId == userId &&
                        progress.WordId == word.Id));
            return await candidates
                .OrderBy(_ => Guid.NewGuid())
                .Take(wordCount)
                .Select(value => value.Id)
                .ToListAsync(cancellationToken);
        }

        var newWordIds = await visible
            .Where(word => !_db.UserWordProgress.AsNoTracking().Any(
                progress => progress.UserId == userId &&
                    progress.WordId == word.Id))
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.Id)
            .Take(wordCount)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        if (!includePreviouslyStudied || newWordIds.Count == wordCount)
        {
            return newWordIds;
        }

        var remainingCount = wordCount - newWordIds.Count;
        var studiedWordIds = await (
            from word in visible
            join progress in _db.UserWordProgress.AsNoTracking()
                    .Where(value => value.UserId == userId)
                on word.Id equals progress.WordId
            orderby progress.LastStudiedAt, word.Id
            select word.Id)
            .Take(remainingCount)
            .ToListAsync(cancellationToken);
        return [.. newWordIds, .. studiedWordIds];
    }

    /// <summary>
    /// 在一个事务中首次处理 item、累计 progress 并按需完成 session。
    /// </summary>
    private async Task<WordStudySessionResponse> SubmitResultCoreAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        WordStudyResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var session = await _db.WordStudySessions.SingleOrDefaultAsync(
            value => value.UserId == userId && value.Id == sessionId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        var item = await _db.WordStudySessionItems.SingleOrDefaultAsync(
            value => value.Id == itemId && value.SessionId == sessionId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudyItemNotFound);

        if (session.Status == WordStudySessionStatus.Abandoned)
        {
            throw ConflictException.Create(ErrorCodes.WordStudySessionNotActive);
        }
        if (ItemMatchesResult(item.Status, result))
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetSessionAsync(userId, sessionId, cancellationToken);
        }
        if (item.Status != WordStudySessionItemStatus.Pending)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyItemResultConflict);
        }
        EnsureActive(session);

        var now = _timeProvider.GetUtcNow();
        var contentIsVisible = await WordVisibilityPolicy
            .Apply(_db.Words.AsNoTracking())
            .AnyAsync(value => value.Id == item.WordId, cancellationToken);
        if (!contentIsVisible)
        {
            MarkContentUnavailable(item, now);
            var hasOtherPendingItem = await _db.WordStudySessionItems
                .AsNoTracking()
                .AnyAsync(
                    value => value.SessionId == sessionId &&
                        value.Id != itemId &&
                        value.Status == WordStudySessionItemStatus.Pending,
                    cancellationToken);
            if (hasOtherPendingItem)
            {
                session.ConcurrencyStamp = Guid.NewGuid();
            }
            else
            {
                CompleteSession(session, now);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetSessionAsync(userId, sessionId, cancellationToken);
        }

        item.Status = ToItemStatus(result);
        item.AnsweredAt = now;
        item.ConcurrencyStamp = Guid.NewGuid();

        var progress = await _db.UserWordProgress.SingleOrDefaultAsync(
            value => value.UserId == userId && value.WordId == item.WordId,
            cancellationToken);
        if (progress is null)
        {
            progress = new UserWordProgress
            {
                UserId = userId,
                WordId = item.WordId,
                ReviewCount = 1,
                RememberedCount = result == WordStudyResult.Remembered ? 1 : 0,
                ForgottenCount = result == WordStudyResult.Forgotten ? 1 : 0,
                LastResult = result,
                FirstStudiedAt = now,
                LastStudiedAt = now
            };
            _db.UserWordProgress.Add(progress);
        }
        else
        {
            progress.ReviewCount++;
            if (result == WordStudyResult.Remembered)
            {
                progress.RememberedCount++;
            }
            else
            {
                progress.ForgottenCount++;
            }

            progress.LastResult = result;
            progress.LastStudiedAt = now;
        }

        var hasOtherPending = await _db.WordStudySessionItems.AsNoTracking().AnyAsync(
            value => value.SessionId == sessionId &&
                value.Id != itemId &&
                value.Status == WordStudySessionItemStatus.Pending,
            cancellationToken);
        if (hasOtherPending)
        {
            session.ConcurrencyStamp = Guid.NewGuid();
        }
        else
        {
            CompleteSession(session, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Recorded word study result {Result} for session {SessionId} item {ItemId} word {WordId} user {UserId}",
            result,
            sessionId,
            itemId,
            item.WordId,
            userId);
        return await GetSessionAsync(userId, sessionId, cancellationToken);
    }

    /// <summary>
    /// 在写入竞争后从干净查询读取最终状态并实现相同结果幂等归并。
    /// </summary>
    private async Task<WordStudySessionResponse> ResolveResultConcurrencyAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        WordStudyResult result,
        CancellationToken cancellationToken)
    {
        _db.ClearTrackedChanges();
        var session = await _db.WordStudySessions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UserId == userId && value.Id == sessionId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        var item = await _db.WordStudySessionItems.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == itemId && value.SessionId == sessionId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudyItemNotFound);

        if (session.Status == WordStudySessionStatus.Abandoned)
        {
            throw ConflictException.Create(ErrorCodes.WordStudySessionNotActive);
        }
        if (ItemMatchesResult(item.Status, result))
        {
            return await GetSessionAsync(userId, sessionId, cancellationToken);
        }
        if (item.Status != WordStudySessionItemStatus.Pending)
        {
            throw ConflictException.Create(ErrorCodes.WordStudyItemResultConflict);
        }

        throw ConflictException.Create(ErrorCodes.WordStudyConcurrencyConflict);
    }

    /// <summary>
    /// 在一个事务中执行首次放弃，并直接返回重复放弃的当前摘要。
    /// </summary>
    private async Task<WordStudySessionResponse> AbandonSessionCoreAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var session = await _db.WordStudySessions.SingleOrDefaultAsync(
            value => value.UserId == userId && value.Id == sessionId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordStudySessionNotFound);
        if (session.Status == WordStudySessionStatus.Abandoned)
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetSessionAsync(userId, sessionId, cancellationToken);
        }
        EnsureActive(session);

        session.Status = WordStudySessionStatus.Abandoned;
        session.AbandonedAt = _timeProvider.GetUtcNow();
        session.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Abandoned word study session {SessionId} for user {UserId}",
            sessionId,
            userId);
        return await GetSessionAsync(userId, sessionId, cancellationToken);
    }

    /// <summary>
    /// 将会话切换为完成状态并维护对应时间和并发标识。
    /// </summary>
    private static void CompleteSession(
        WordStudySession session,
        DateTimeOffset completedAt)
    {
        session.Status = WordStudySessionStatus.Completed;
        session.CompletedAt = completedAt;
        session.ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// 将实时不可见的待处理项标记为不产生学习进度的跳过状态。
    /// </summary>
    private static void MarkContentUnavailable(
        WordStudySessionItem item,
        DateTimeOffset answeredAt)
    {
        item.Status = WordStudySessionItemStatus.Skipped;
        item.SkipReason = WordStudySkipReason.ContentUnavailable;
        item.AnsweredAt = answeredAt;
        item.ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// 确保只有活动会话可以继续获取内容或改变状态。
    /// </summary>
    private static void EnsureActive(WordStudySession session)
    {
        if (session.Status != WordStudySessionStatus.Active)
        {
            throw ConflictException.Create(ErrorCodes.WordStudySessionNotActive);
        }
    }

    /// <summary>
    /// 判断 item 的终态是否等于客户端重试提交的结果。
    /// </summary>
    private static bool ItemMatchesResult(
        WordStudySessionItemStatus status,
        WordStudyResult result)
        => (status == WordStudySessionItemStatus.Remembered &&
                result == WordStudyResult.Remembered) ||
            (status == WordStudySessionItemStatus.Forgotten &&
                result == WordStudyResult.Forgotten);

    /// <summary>
    /// 将提交结果转换为会话项对应的终态。
    /// </summary>
    private static WordStudySessionItemStatus ToItemStatus(WordStudyResult result)
        => result == WordStudyResult.Remembered
            ? WordStudySessionItemStatus.Remembered
            : WordStudySessionItemStatus.Forgotten;

    /// <summary>
    /// 将会话及其 item 状态聚合为不暴露实体的查询 projection。
    /// </summary>
    private static IQueryable<WordStudySessionResponse> ProjectSessionSummary(
        IQueryable<WordStudySession> query)
        => query.Select(session => new WordStudySessionResponse(
            session.Id,
            session.RequestedCount,
            session.ActualCount,
            session.IncludePreviouslyStudied,
            session.SelectionMode,
            session.LanguageTag,
            session.Status,
            session.Items.Count(item =>
                item.Status != WordStudySessionItemStatus.Pending),
            session.Items.Count(item =>
                item.Status == WordStudySessionItemStatus.Remembered),
            session.Items.Count(item =>
                item.Status == WordStudySessionItemStatus.Forgotten),
            session.Items.Count(item =>
                item.Status == WordStudySessionItemStatus.Skipped),
            session.StartedAt,
            session.CompletedAt,
            session.AbandonedAt));
}
