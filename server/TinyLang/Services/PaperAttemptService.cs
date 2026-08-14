using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现用户试卷测验的恢复、逐题保存、服务端判分和并发幂等规则。
/// </summary>
public sealed class PaperAttemptService : IPaperAttemptService
{
    private const string ActiveAttemptUniqueIndex =
        "IX_paper_attempts_UserId_PaperId";
    private const string AttemptNumberUniqueIndex =
        "IX_paper_attempts_UserId_PaperId_AttemptNumber";
    private const string AttemptAnswerUniqueIndex =
        "IX_paper_attempt_answers_AttemptId_QuestionId";
    private const int StartRetryCount = 2;

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaperAttemptService> _logger;

    /// <summary>
    /// 使用数据库、约束分类器、时间源和结构化日志创建测验服务。
    /// </summary>
    public PaperAttemptService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider,
        ILogger<PaperAttemptService> logger)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaperAttemptStartOutcome> StartAsync(
        Guid userId,
        Guid paperId,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < StartRetryCount; attempt++)
        {
            try
            {
                return await StartCoreAsync(userId, paperId, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                var resolved = await ResolveStartConflictAsync(
                    userId,
                    paperId,
                    cancellationToken);
                if (resolved is { } outcome)
                {
                    return outcome;
                }
            }
            catch (DbUpdateException exception) when (
                _databaseExceptionClassifier.IsUniqueConstraintViolation(
                    exception,
                    ActiveAttemptUniqueIndex,
                    AttemptNumberUniqueIndex))
            {
                var resolved = await ResolveStartConflictAsync(
                    userId,
                    paperId,
                    cancellationToken);
                if (resolved is { } outcome)
                {
                    return outcome;
                }
            }
        }

        throw ConflictException.Create(ErrorCodes.PaperAttemptConcurrencyConflict);
    }

    /// <inheritdoc />
    public async Task<UserPaperAttemptResponse> GetAttemptAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
        => await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.Id == attemptId && value.UserId == userId)
            .Select(ToUserAttemptProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<PaperAttemptSummaryResponse>> GetHistoryAsync(
        Guid userId,
        Guid paperId,
        PaperAttemptListRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperAttemptListRequestValidator());
        var query = _db.PaperAttempts.AsNoTracking().Where(value =>
            value.UserId == userId && value.PaperId == paperId);
        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0 && !await _db.Papers.AsNoTracking().AnyAsync(
            value => value.Id == paperId &&
                value.Status == PaperPublicationStatus.Published &&
                value.PublishedAt != null,
            cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.PaperNotFound);
        }

        var items = await query
            .OrderByDescending(value => value.StartedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new PaperAttemptSummaryResponse(
                value.Id,
                value.PaperId,
                value.AttemptNumber,
                value.Status,
                value.Score,
                value.PaperTotalScore,
                value.IsPassed,
                value.StartedAt,
                value.SubmittedAt))
            .ToListAsync(cancellationToken);
        return new PagedResponse<PaperAttemptSummaryResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (totalCount + request.PageSize - 1) / request.PageSize);
    }

    /// <inheritdoc />
    public async Task SaveAnswerAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(
            request,
            new SavePaperAttemptAnswerRequestValidator());
        try
        {
            await SaveAnswerCoreAsync(
                userId,
                attemptId,
                questionId,
                request,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await ResolveAnswerConflictAsync(
                userId,
                attemptId,
                questionId,
                request,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                AttemptAnswerUniqueIndex))
        {
            await ResolveAnswerConflictAsync(
                userId,
                attemptId,
                questionId,
                request,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task ClearAnswerAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ClearAnswerCoreAsync(
                userId,
                attemptId,
                questionId,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await ResolveClearConflictAsync(
                userId,
                attemptId,
                questionId,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<PaperAttemptResultResponse> SubmitAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await SubmitCoreAsync(userId, attemptId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveSubmitConflictAsync(
                userId,
                attemptId,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                AttemptAnswerUniqueIndex))
        {
            return await ResolveSubmitConflictAsync(
                userId,
                attemptId,
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<PaperAttemptResultResponse> GetResultAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        var status = await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.Id == attemptId && value.UserId == userId)
            .Select(value => (PaperAttemptStatus?)value.Status)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        if (status != PaperAttemptStatus.Submitted)
        {
            throw ConflictException.Create(ErrorCodes.PaperAttemptNotSubmitted);
        }

        return await ProjectResultAsync(userId, attemptId, cancellationToken);
    }

    /// <summary>
    /// 在事务中首次创建测验，或直接返回已经存在的活动测验。
    /// </summary>
    private async Task<PaperAttemptStartOutcome> StartCoreAsync(
        Guid userId,
        Guid paperId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var existingId = await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.UserId == userId && value.PaperId == paperId &&
                value.Status == PaperAttemptStatus.InProgress)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId is { } activeAttemptId)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaperAttemptStartOutcome(
                await GetAttemptAsync(userId, activeAttemptId, cancellationToken),
                WasCreated: false);
        }

        var paper = await _db.Papers.SingleOrDefaultAsync(
            value => value.Id == paperId &&
                value.Status == PaperPublicationStatus.Published &&
                value.PublishedAt != null,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);
        var previousNumber = await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.UserId == userId && value.PaperId == paperId)
            .Select(value => (int?)value.AttemptNumber)
            .MaxAsync(cancellationToken) ?? 0;
        if (previousNumber == int.MaxValue)
        {
            throw ConflictException.Create(ErrorCodes.PaperAttemptActiveConflict);
        }

        var now = _timeProvider.GetUtcNow();
        var paperAttempt = new PaperAttempt
        {
            PaperId = paper.Id,
            UserId = userId,
            AttemptNumber = previousNumber + 1,
            PaperTotalScore = paper.TotalScore,
            PaperPassingScore = paper.PassingScore,
            StartedAt = now
        };
        paper.ConcurrencyStamp = Guid.NewGuid();
        _db.PaperAttempts.Add(paperAttempt);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Started paper attempt {AttemptId} number {AttemptNumber} for paper {PaperId} user {UserId}",
            paperAttempt.Id,
            paperAttempt.AttemptNumber,
            paperId,
            userId);
        return new PaperAttemptStartOutcome(
            await GetAttemptAsync(userId, paperAttempt.Id, cancellationToken),
            WasCreated: true);
    }

    /// <summary>
    /// 在创建竞争后从干净查询恢复活动测验或决定是否可以重试。
    /// </summary>
    private async Task<PaperAttemptStartOutcome?> ResolveStartConflictAsync(
        Guid userId,
        Guid paperId,
        CancellationToken cancellationToken)
    {
        _db.ClearTrackedChanges();
        var existingId = await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.UserId == userId && value.PaperId == paperId &&
                value.Status == PaperAttemptStatus.InProgress)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId is { } attemptId)
        {
            return new PaperAttemptStartOutcome(
                await GetAttemptAsync(userId, attemptId, cancellationToken),
                WasCreated: false);
        }

        var isPublished = await _db.Papers.AsNoTracking().AnyAsync(
            value => value.Id == paperId &&
                value.Status == PaperPublicationStatus.Published &&
                value.PublishedAt != null,
            cancellationToken);
        if (!isPublished)
        {
            throw NotFoundException.Create(ErrorCodes.PaperNotFound);
        }
        return null;
    }

    /// <summary>
    /// 在一个数据库事务中首次保存或覆盖一道题目的用户答案。
    /// </summary>
    private async Task SaveAnswerCoreAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var attempt = await _db.PaperAttempts.SingleOrDefaultAsync(
            value => value.Id == attemptId && value.UserId == userId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        EnsureInProgress(attempt);
        var prepared = await PrepareAnswerAsync(
            attempt.PaperId,
            questionId,
            request,
            cancellationToken);
        var answer = await _db.PaperAttemptAnswers.SingleOrDefaultAsync(
            value => value.AttemptId == attemptId && value.QuestionId == questionId,
            cancellationToken);
        if (answer is not null && AnswerMatches(answer, prepared))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (answer is null)
        {
            answer = new PaperAttemptAnswer
            {
                AttemptId = attemptId,
                QuestionId = questionId
            };
            _db.PaperAttemptAnswers.Add(answer);
        }
        ApplyPreparedAnswer(answer, prepared, _timeProvider.GetUtcNow());
        attempt.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Saved answer for attempt {AttemptId} question {QuestionId} user {UserId}",
            attemptId,
            questionId,
            userId);
    }

    /// <summary>
    /// 在答案写入竞争后识别相同答案幂等成功或返回稳定冲突。
    /// </summary>
    private async Task ResolveAnswerConflictAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken)
    {
        _db.ClearTrackedChanges();
        var attempt = await _db.PaperAttempts.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == attemptId && value.UserId == userId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        EnsureInProgress(attempt);
        var prepared = await PrepareAnswerAsync(
            attempt.PaperId,
            questionId,
            request,
            cancellationToken);
        var answer = await _db.PaperAttemptAnswers.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.AttemptId == attemptId &&
                    value.QuestionId == questionId,
                cancellationToken);
        if (answer is not null && AnswerMatches(answer, prepared))
        {
            return;
        }

        throw ConflictException.Create(ErrorCodes.PaperAttemptConcurrencyConflict);
    }

    /// <summary>
    /// 在一个事务中幂等删除活动测验的一道已保存答案。
    /// </summary>
    private async Task ClearAnswerCoreAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var attempt = await _db.PaperAttempts.SingleOrDefaultAsync(
            value => value.Id == attemptId && value.UserId == userId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        EnsureInProgress(attempt);
        await EnsureQuestionBelongsToPaperAsync(
            attempt.PaperId,
            questionId,
            cancellationToken);
        var answer = await _db.PaperAttemptAnswers.SingleOrDefaultAsync(
            value => value.AttemptId == attemptId && value.QuestionId == questionId,
            cancellationToken);
        if (answer is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        _db.PaperAttemptAnswers.Remove(answer);
        attempt.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Cleared answer for attempt {AttemptId} question {QuestionId} user {UserId}",
            attemptId,
            questionId,
            userId);
    }

    /// <summary>
    /// 在清除竞争后识别已清除的幂等成功或返回稳定冲突。
    /// </summary>
    private async Task ResolveClearConflictAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        _db.ClearTrackedChanges();
        var attempt = await _db.PaperAttempts.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == attemptId && value.UserId == userId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        EnsureInProgress(attempt);
        await EnsureQuestionBelongsToPaperAsync(
            attempt.PaperId,
            questionId,
            cancellationToken);
        var answerExists = await _db.PaperAttemptAnswers.AsNoTracking().AnyAsync(
            value => value.AttemptId == attemptId && value.QuestionId == questionId,
            cancellationToken);
        if (!answerExists)
        {
            return;
        }

        throw ConflictException.Create(ErrorCodes.PaperAttemptConcurrencyConflict);
    }

    /// <summary>
    /// 在一个事务中补齐全部题目结果、判分并首次提交测验。
    /// </summary>
    private async Task<PaperAttemptResultResponse> SubmitCoreAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var attempt = await _db.PaperAttempts
            .AsSplitQuery()
            .Include(value => value.Paper)
                .ThenInclude(value => value!.Questions)
                    .ThenInclude(value => value.Options)
            .Include(value => value.Paper)
                .ThenInclude(value => value!.Questions)
                    .ThenInclude(value => value.AcceptedAnswers)
            .Include(value => value.Answers)
            .SingleOrDefaultAsync(
                value => value.Id == attemptId && value.UserId == userId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        if (attempt.Status == PaperAttemptStatus.Submitted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await ProjectResultAsync(userId, attemptId, cancellationToken);
        }
        EnsureInProgress(attempt);

        var answers = attempt.Answers.ToDictionary(value => value.QuestionId);
        var score = 0;
        foreach (var question in attempt.Paper!.Questions)
        {
            if (!answers.TryGetValue(question.Id, out var answer))
            {
                answer = new PaperAttemptAnswer
                {
                    AttemptId = attempt.Id,
                    Attempt = attempt,
                    QuestionId = question.Id,
                    Question = question,
                    IsAnswered = false
                };
                attempt.Answers.Add(answer);
                _db.PaperAttemptAnswers.Add(answer);
            }

            var isCorrect = ScoreAnswer(question, answer);
            answer.IsCorrect = isCorrect;
            answer.AwardedPoints = isCorrect ? question.Points : 0;
            answer.ConcurrencyStamp = Guid.NewGuid();
            score += answer.AwardedPoints.Value;
        }

        var now = _timeProvider.GetUtcNow();
        attempt.Score = score;
        attempt.IsPassed = score >= attempt.PaperPassingScore;
        attempt.Status = PaperAttemptStatus.Submitted;
        attempt.SubmittedAt = now;
        attempt.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Submitted paper attempt {AttemptId} for paper {PaperId} user {UserId} with score {Score}",
            attemptId,
            attempt.PaperId,
            userId,
            score);
        return await ProjectResultAsync(userId, attemptId, cancellationToken);
    }

    /// <summary>
    /// 在提交竞争后返回数据库已提交结果或稳定并发冲突。
    /// </summary>
    private async Task<PaperAttemptResultResponse> ResolveSubmitConflictAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        _db.ClearTrackedChanges();
        var status = await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.Id == attemptId && value.UserId == userId)
            .Select(value => (PaperAttemptStatus?)value.Status)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptNotFound);
        if (status == PaperAttemptStatus.Submitted)
        {
            return await ProjectResultAsync(userId, attemptId, cancellationToken);
        }
        throw ConflictException.Create(ErrorCodes.PaperAttemptConcurrencyConflict);
    }

    /// <summary>
    /// 根据数据库题型验证答案字段并生成可持久化的规范化值。
    /// </summary>
    private async Task<PreparedAnswer> PrepareAnswerAsync(
        Guid paperId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var question = await _db.PaperQuestions.AsNoTracking()
            .Include(value => value.Options)
            .SingleOrDefaultAsync(
                value => value.Id == questionId && value.PaperId == paperId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperAttemptQuestionNotFound);

        return question.Type switch
        {
            PaperQuestionType.SingleChoice => PrepareSingleChoice(question, request),
            PaperQuestionType.TrueFalse => PrepareTrueFalse(request),
            PaperQuestionType.FillBlank => PrepareFillBlank(question, request),
            _ => throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid)
        };
    }

    /// <summary>
    /// 验证题目标识属于当前 Attempt 绑定的试卷。
    /// </summary>
    private async Task EnsureQuestionBelongsToPaperAsync(
        Guid paperId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        if (!await _db.PaperQuestions.AsNoTracking().AnyAsync(
            value => value.Id == questionId && value.PaperId == paperId,
            cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.PaperAttemptQuestionNotFound);
        }
    }

    /// <summary>
    /// 验证单选题请求只提交属于当前题目的 OptionId。
    /// </summary>
    private static PreparedAnswer PrepareSingleChoice(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
    {
        if (request.SelectedOptionId is not { } selectedOptionId ||
            request.BooleanAnswer.HasValue || request.TextAnswer is not null)
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid);
        }
        if (!question.Options.Any(value => value.Id == selectedOptionId))
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptSelectedOptionInvalid);
        }
        return new PreparedAnswer(selectedOptionId, null, null, null);
    }

    /// <summary>
    /// 验证判断题请求只提交 BooleanAnswer。
    /// </summary>
    private static PreparedAnswer PrepareTrueFalse(
        SavePaperAttemptAnswerRequest request)
    {
        if (!request.BooleanAnswer.HasValue || request.SelectedOptionId.HasValue ||
            request.TextAnswer is not null)
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid);
        }
        return new PreparedAnswer(null, request.BooleanAnswer, null, null);
    }

    /// <summary>
    /// 验证填空题请求并生成与标准答案一致的比较键。
    /// </summary>
    private static PreparedAnswer PrepareFillBlank(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
    {
        if (request.TextAnswer is not { } textAnswer ||
            request.SelectedOptionId.HasValue || request.BooleanAnswer.HasValue)
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid);
        }

        string normalized;
        try
        {
            normalized = FillBlankAnswerNormalizer.Normalize(
                textAnswer,
                question.FillBlankCaseSensitive);
        }
        catch (ArgumentException)
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid);
        }
        if (normalized.Length is < 1 or > OnlineQuizConstraints.MaxAnswerTextLength)
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptAnswerShapeInvalid);
        }
        return new PreparedAnswer(null, null, textAnswer, normalized);
    }

    /// <summary>
    /// 将已验证答案写入 tracked answer 并清除任何旧题型字段。
    /// </summary>
    private static void ApplyPreparedAnswer(
        PaperAttemptAnswer answer,
        PreparedAnswer prepared,
        DateTimeOffset savedAt)
    {
        answer.SelectedOptionId = prepared.SelectedOptionId;
        answer.BooleanAnswer = prepared.BooleanAnswer;
        answer.TextAnswer = prepared.TextAnswer;
        answer.NormalizedTextAnswer = prepared.NormalizedTextAnswer;
        answer.IsAnswered = true;
        answer.IsCorrect = null;
        answer.AwardedPoints = null;
        answer.SavedAt = savedAt;
        answer.ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// 判断持久化答案是否与客户端重试的已验证答案完全相同。
    /// </summary>
    private static bool AnswerMatches(
        PaperAttemptAnswer answer,
        PreparedAnswer prepared)
        => answer.IsAnswered &&
            answer.SelectedOptionId == prepared.SelectedOptionId &&
            answer.BooleanAnswer == prepared.BooleanAnswer &&
            string.Equals(answer.TextAnswer, prepared.TextAnswer, StringComparison.Ordinal) &&
            string.Equals(
                answer.NormalizedTextAnswer,
                prepared.NormalizedTextAnswer,
                StringComparison.Ordinal);

    /// <summary>
    /// 根据题型和数据库标准答案确定一道题是否正确。
    /// </summary>
    private static bool ScoreAnswer(
        PaperQuestion question,
        PaperAttemptAnswer answer)
    {
        if (!answer.IsAnswered)
        {
            return false;
        }

        return question.Type switch
        {
            PaperQuestionType.SingleChoice => question.Options.Any(value =>
                value.IsCorrect && answer.SelectedOptionId == value.Id),
            PaperQuestionType.TrueFalse =>
                answer.BooleanAnswer == question.CorrectBoolean,
            PaperQuestionType.FillBlank =>
                answer.NormalizedTextAnswer is { } normalized &&
                question.AcceptedAnswers.Any(value =>
                    value.NormalizedText == normalized),
            _ => false
        };
    }

    /// <summary>
    /// 确保只有 InProgress 测验可以继续保存或首次提交。
    /// </summary>
    private static void EnsureInProgress(PaperAttempt attempt)
    {
        if (attempt.Status != PaperAttemptStatus.InProgress)
        {
            throw ConflictException.Create(ErrorCodes.PaperAttemptNotInProgress);
        }
    }

    /// <summary>
    /// 创建可由 EF Core 翻译且不含正确答案的测验恢复 projection。
    /// </summary>
    private static Expression<Func<PaperAttempt, UserPaperAttemptResponse>>
        ToUserAttemptProjection()
        => attempt => new UserPaperAttemptResponse(
            attempt.Id,
            attempt.PaperId,
            attempt.AttemptNumber,
            attempt.Status,
            attempt.Paper!.Title,
            attempt.Paper.Description,
            attempt.Paper.Instructions,
            attempt.Paper.Questions.Count,
            attempt.PaperTotalScore,
            attempt.PaperPassingScore,
            attempt.StartedAt,
            attempt.SubmittedAt,
            attempt.Paper.Questions.OrderBy(question => question.SortOrder)
                .ThenBy(question => question.Id)
                .Select(question => new UserPaperAttemptQuestionResponse(
                    question.Id,
                    question.Type,
                    question.Prompt,
                    question.Points,
                    question.SortOrder,
                    question.Options.OrderBy(option => option.SortOrder)
                        .ThenBy(option => option.Id)
                        .Select(option => new UserPaperAttemptOptionResponse(
                            option.Id,
                            option.Text,
                            option.SortOrder))
                        .ToList(),
                    attempt.Answers.Where(answer =>
                            answer.QuestionId == question.Id && answer.IsAnswered)
                        .Select(answer => new UserPaperAttemptSavedAnswerResponse(
                            answer.SelectedOptionId,
                            answer.BooleanAnswer,
                            answer.TextAnswer,
                            answer.SavedAt))
                        .SingleOrDefault()))
                .ToList());

    /// <summary>
    /// 查询当前用户已提交测验的稳定总成绩和逐题结果。
    /// </summary>
    private async Task<PaperAttemptResultResponse> ProjectResultAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken)
        => await _db.PaperAttempts.AsNoTracking()
            .Where(value => value.Id == attemptId && value.UserId == userId &&
                value.Status == PaperAttemptStatus.Submitted)
            .Select(attempt => new PaperAttemptResultResponse(
                attempt.Id,
                attempt.PaperId,
                attempt.AttemptNumber,
                attempt.Paper!.Title,
                attempt.Score!.Value,
                attempt.PaperTotalScore,
                attempt.PaperPassingScore,
                attempt.IsPassed!.Value,
                attempt.StartedAt,
                attempt.SubmittedAt!.Value,
                attempt.Answers.OrderBy(answer => answer.Question!.SortOrder)
                    .ThenBy(answer => answer.QuestionId)
                    .Select(answer => new PaperAttemptQuestionResultResponse(
                        answer.QuestionId,
                        answer.Question!.Type,
                        answer.Question.Prompt,
                        answer.Question.Explanation,
                        answer.Question.Points,
                        answer.Question.SortOrder,
                        answer.Question.Options.OrderBy(option => option.SortOrder)
                            .ThenBy(option => option.Id)
                            .Select(option => new PaperAttemptResultOptionResponse(
                                option.Id,
                                option.Text,
                                option.SortOrder))
                            .ToList(),
                        answer.SelectedOptionId,
                        answer.BooleanAnswer,
                        answer.TextAnswer,
                        answer.IsAnswered,
                        answer.Question.Options.Where(option => option.IsCorrect)
                            .Select(option => (Guid?)option.Id)
                            .SingleOrDefault(),
                        answer.Question.CorrectBoolean,
                        answer.Question.AcceptedAnswers
                            .OrderBy(accepted => accepted.SortOrder)
                            .ThenBy(accepted => accepted.Id)
                            .Select(accepted => accepted.Text)
                            .ToList(),
                        answer.IsCorrect!.Value,
                        answer.AwardedPoints!.Value))
                    .ToList()))
            .SingleAsync(cancellationToken);

    /// <summary>
    /// 保存题型校验后的互斥答案字段和规范化比较键。
    /// </summary>
    private readonly record struct PreparedAnswer(
        Guid? SelectedOptionId,
        bool? BooleanAnswer,
        string? TextAnswer,
        string? NormalizedTextAnswer);
}
