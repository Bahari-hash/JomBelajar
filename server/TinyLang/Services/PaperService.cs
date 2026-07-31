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
/// 实现试卷完整聚合写入、发布校验、历史锁定和安全目录查询。
/// </summary>
public sealed class PaperService : IPaperService
{
    private static readonly string[] SortOrderUniqueIndexes =
    [
        "IX_paper_questions_PaperId_SortOrder",
        "IX_paper_question_options_QuestionId_SortOrder",
        "IX_fill_blank_accepted_answers_QuestionId_SortOrder"
    ];
    private const string AcceptedAnswerUniqueIndex =
        "IX_fill_blank_accepted_answers_QuestionId_NormalizedText";
    private const string AttemptPaperForeignKey =
        "FK_paper_attempts_papers_PaperId";

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaperService> _logger;

    /// <summary>
    /// 使用数据库、约束分类器、时间源和结构化日志创建试卷服务。
    /// </summary>
    public PaperService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider,
        ILogger<PaperService> logger)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> CreateDraftAsync(
        Guid adminId,
        CreatePaperRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new CreatePaperRequestValidator());
        var paper = new Paper
        {
            Title = NormalizeRequired(request.Title),
            Description = NormalizeOptional(request.Description),
            Instructions = NormalizeOptional(request.Instructions),
            LanguageTag = WordTextNormalizer.NormalizeLanguageTag(request.LanguageTag),
            PassingScore = request.PassingScore,
            CreatedById = adminId,
            LastEditorId = adminId
        };
        ApplyNewTarget(paper, request);
        paper.TotalScore = CalculateTotalScore(request.Questions);
        _db.Papers.Add(paper);
        await SavePaperChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created paper draft {PaperId} by administrator {AdminId}",
            paper.Id,
            adminId);
        return await GetAdminByIdAsync(paper.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> UpdateAsync(
        Guid paperId,
        Guid adminId,
        UpdatePaperRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new UpdatePaperRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        if (paper.Status == PaperPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
        }
        if (await HasAttemptsAsync(paperId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.PaperContentLocked);
        }
        if (paper.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw ConflictException.Create(ErrorCodes.PaperConcurrencyConflict);
        }

        ValidateChildOwnership(paper, request);
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        paper.Title = NormalizeRequired(request.Title);
        paper.Description = NormalizeOptional(request.Description);
        paper.Instructions = NormalizeOptional(request.Instructions);
        paper.LanguageTag = WordTextNormalizer.NormalizeLanguageTag(request.LanguageTag);
        paper.PassingScore = request.PassingScore;
        paper.TotalScore = CalculateTotalScore(request.Questions);
        paper.LastEditorId = adminId;
        paper.ConcurrencyStamp = Guid.NewGuid();

        StageExistingChildren(paper, request);
        await SavePaperChangesAsync(cancellationToken);
        ApplyFinalTarget(paper, request);
        await SavePaperChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Updated paper {PaperId} by administrator {AdminId}",
            paperId,
            adminId);
        return await GetAdminByIdAsync(paperId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> PublishAsync(
        Guid paperId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        if (paper.Status == PaperPublicationStatus.Published)
        {
            return await GetAdminByIdAsync(paperId, cancellationToken);
        }
        if (paper.Status is not (PaperPublicationStatus.Draft or
            PaperPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
        }

        EnsurePublishable(paper);
        paper.TotalScore = paper.Questions.Sum(value => value.Points);
        paper.Status = PaperPublicationStatus.Published;
        paper.PublishedAt ??= _timeProvider.GetUtcNow();
        paper.LastEditorId = adminId;
        paper.ConcurrencyStamp = Guid.NewGuid();
        await SavePaperChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published paper {PaperId} by administrator {AdminId}",
            paperId,
            adminId);
        return await GetAdminByIdAsync(paperId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> UnpublishAsync(
        Guid paperId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        if (paper.Status == PaperPublicationStatus.Unpublished)
        {
            return await GetAdminByIdAsync(paperId, cancellationToken);
        }
        if (paper.Status != PaperPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
        }

        paper.Status = PaperPublicationStatus.Unpublished;
        paper.LastEditorId = adminId;
        paper.ConcurrencyStamp = Guid.NewGuid();
        await SavePaperChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Unpublished paper {PaperId} by administrator {AdminId}",
            paperId,
            adminId);
        return await GetAdminByIdAsync(paperId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid paperId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        if (paper.Status == PaperPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.PaperPublishedDeleteConflict);
        }
        if (await HasAttemptsAsync(paperId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.PaperContentLocked);
        }

        _db.Papers.Remove(paper);
        await SavePaperChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted paper {PaperId} by administrator {AdminId}",
            paperId,
            adminId);
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> GetAdminByIdAsync(
        Guid paperId,
        CancellationToken cancellationToken = default)
        => await _db.Papers.AsNoTracking()
            .Where(value => value.Id == paperId)
            .Select(ToAdminResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<AdminPaperListItemResponse>> GetAdminListAsync(
        AdminPaperListRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new AdminPaperListRequestValidator());
        var query = _db.Papers.AsNoTracking();
        if (request.Status is { } status)
        {
            query = query.Where(value => value.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var language = WordTextNormalizer.NormalizeLanguageTag(request.Language);
            query = query.Where(value => value.LanguageTag == language);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new AdminPaperListItemResponse(
                value.Id,
                value.Title,
                value.LanguageTag,
                value.Status,
                value.Questions.Count,
                value.TotalScore,
                value.PassingScore,
                value.PublishedAt,
                value.UpdatedAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<PaperCatalogItemResponse>> GetCatalogAsync(
        PaperCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperCatalogRequestValidator());
        var query = PublishedPapersQuery();
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var language = WordTextNormalizer.NormalizeLanguageTag(request.Language);
            query = query.Where(value => value.LanguageTag == language);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new PaperCatalogItemResponse(
                value.Id,
                value.Title,
                value.Description,
                value.LanguageTag,
                value.Questions.Count,
                value.TotalScore,
                value.PassingScore,
                value.PublishedAt!.Value))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<PaperDetailsResponse> GetDetailsAsync(
        Guid paperId,
        CancellationToken cancellationToken = default)
        => await PublishedPapersQuery()
            .Where(value => value.Id == paperId)
            .Select(value => new PaperDetailsResponse(
                value.Id,
                value.Title,
                value.Description,
                value.Instructions,
                value.LanguageTag,
                value.Questions.Count,
                value.TotalScore,
                value.PassingScore,
                value.PublishedAt!.Value))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);

    /// <summary>
    /// 加载包含全部私有题目、选项和标准答案的 tracked 试卷聚合。
    /// </summary>
    private async Task<Paper> FindPaperForEditAsync(
        Guid paperId,
        CancellationToken cancellationToken)
        => await _db.Papers
            .Include(value => value.Questions)
                .ThenInclude(value => value.Options)
            .Include(value => value.Questions)
                .ThenInclude(value => value.AcceptedAnswers)
            .SingleOrDefaultAsync(value => value.Id == paperId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);

    /// <summary>
    /// 判断试卷是否已经产生任意用户测验记录。
    /// </summary>
    private Task<bool> HasAttemptsAsync(Guid paperId, CancellationToken cancellationToken)
        => _db.PaperAttempts.AsNoTracking().AnyAsync(
            value => value.PaperId == paperId,
            cancellationToken);

    /// <summary>
    /// 验证更新请求中的已有子项均属于当前试卷和对应题目。
    /// </summary>
    private static void ValidateChildOwnership(Paper paper, UpdatePaperRequest request)
    {
        var questions = paper.Questions.ToDictionary(value => value.Id);
        foreach (var input in request.Questions)
        {
            if (input.Id is not { } questionId)
            {
                continue;
            }
            if (!questions.TryGetValue(questionId, out var question))
            {
                throw new RequestValidationException(ErrorCodes.PaperChildIdConflict);
            }

            var optionIds = question.Options.Select(value => value.Id).ToHashSet();
            if (input.Options.Any(value =>
                value.Id is { } optionId && !optionIds.Contains(optionId)))
            {
                throw new RequestValidationException(ErrorCodes.PaperChildIdConflict);
            }
            var answerIds = question.AcceptedAnswers.Select(value => value.Id).ToHashSet();
            if (input.AcceptedAnswers.Any(value =>
                value.Id is { } answerId && !answerIds.Contains(answerId)))
            {
                throw new RequestValidationException(ErrorCodes.PaperChildIdConflict);
            }
        }
    }

    /// <summary>
    /// 将创建请求的全部新题目和答案加入试卷聚合。
    /// </summary>
    private static void ApplyNewTarget(Paper paper, PaperUpsertRequest request)
    {
        foreach (var input in request.Questions)
        {
            paper.Questions.Add(CreateQuestion(paper, input));
        }
    }

    /// <summary>
    /// 删除遗漏子项并把保留子项移入不会触发唯一约束的暂存值。
    /// </summary>
    private void StageExistingChildren(Paper paper, UpdatePaperRequest request)
    {
        var desiredQuestionIds = request.Questions.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
        var existingQuestions = paper.Questions.ToArray();
        var questionStagingSortOrders = CreateStagingSortOrders(
            existingQuestions.Select(value => value.SortOrder),
            request.Questions.Select(value => value.SortOrder),
            existingQuestions.Count(value => desiredQuestionIds.Contains(value.Id)));
        foreach (var question in existingQuestions)
        {
            if (!desiredQuestionIds.Contains(question.Id))
            {
                _db.PaperQuestions.Remove(question);
                paper.Questions.Remove(question);
                continue;
            }

            question.SortOrder = questionStagingSortOrders.Dequeue();
            var input = request.Questions.Single(value => value.Id == question.Id);
            var desiredOptionIds = input.Options.Where(value => value.Id.HasValue)
                .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
            var existingOptions = question.Options.ToArray();
            var optionStagingSortOrders = CreateStagingSortOrders(
                existingOptions.Select(value => value.SortOrder),
                input.Options.Select(value => value.SortOrder),
                existingOptions.Count(value => desiredOptionIds.Contains(value.Id)));
            foreach (var option in existingOptions)
            {
                if (!desiredOptionIds.Contains(option.Id))
                {
                    _db.PaperQuestionOptions.Remove(option);
                    question.Options.Remove(option);
                    continue;
                }
                option.SortOrder = optionStagingSortOrders.Dequeue();
            }

            var desiredAnswerIds = input.AcceptedAnswers.Where(value => value.Id.HasValue)
                .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
            var existingAnswers = question.AcceptedAnswers.ToArray();
            var answerStagingSortOrders = CreateStagingSortOrders(
                existingAnswers.Select(value => value.SortOrder),
                input.AcceptedAnswers.Select(value => value.SortOrder),
                existingAnswers.Count(value => desiredAnswerIds.Contains(value.Id)));
            var unavailableNormalizedTexts = existingAnswers
                .Select(value => value.NormalizedText)
                .Concat(input.AcceptedAnswers.Select(value =>
                    FillBlankAnswerNormalizer.Normalize(
                        value.Text,
                        input.FillBlankCaseSensitive)))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var answer in existingAnswers)
            {
                if (!desiredAnswerIds.Contains(answer.Id))
                {
                    _db.FillBlankAcceptedAnswers.Remove(answer);
                    question.AcceptedAnswers.Remove(answer);
                    continue;
                }
                answer.SortOrder = answerStagingSortOrders.Dequeue();
                answer.NormalizedText = CreateStagingNormalizedText(
                    answer.Id,
                    unavailableNormalizedTexts);
            }
        }
    }

    /// <summary>
    /// 在数据库允许范围内选择同时避开当前值和最终值的唯一暂存顺序。
    /// </summary>
    private static Queue<int> CreateStagingSortOrders(
        IEnumerable<int> currentSortOrders,
        IEnumerable<int> targetSortOrders,
        int count)
    {
        var unavailable = currentSortOrders.Concat(targetSortOrders).ToHashSet();
        var staging = new Queue<int>(Enumerable
            .Range(0, OnlineQuizConstraints.MaxSortOrder + 1)
            .Where(value => !unavailable.Contains(value))
            .Take(count));
        if (staging.Count != count)
        {
            throw ConflictException.Create(ErrorCodes.PaperSortOrderConflict);
        }
        return staging;
    }

    /// <summary>
    /// 创建不与当前值或最终值碰撞的数据库内部标准答案暂存键。
    /// </summary>
    private static string CreateStagingNormalizedText(
        Guid answerId,
        ISet<string> unavailable)
    {
        var candidate = $"__tiny_lang_staging__{answerId:N}";
        while (!unavailable.Add(candidate))
        {
            candidate = $"_{candidate}";
        }
        return candidate;
    }

    /// <summary>
    /// 将完整更新请求应用到保留子项并创建全部新子项。
    /// </summary>
    private void ApplyFinalTarget(Paper paper, UpdatePaperRequest request)
    {
        foreach (var input in request.Questions)
        {
            PaperQuestion question;
            if (input.Id is { } questionId)
            {
                question = paper.Questions.Single(value => value.Id == questionId);
                ApplyQuestionValues(question, input);
            }
            else
            {
                question = CreateQuestion(paper, input);
                paper.Questions.Add(question);
                _db.PaperQuestions.Add(question);
                continue;
            }

            foreach (var optionInput in input.Options)
            {
                if (optionInput.Id is { } optionId)
                {
                    ApplyOptionValues(
                        question.Options.Single(value => value.Id == optionId),
                        optionInput);
                }
                else
                {
                    var option = CreateOption(question, optionInput);
                    question.Options.Add(option);
                    _db.PaperQuestionOptions.Add(option);
                }
            }
            foreach (var answerInput in input.AcceptedAnswers)
            {
                if (answerInput.Id is { } answerId)
                {
                    ApplyAcceptedAnswerValues(
                        question.AcceptedAnswers.Single(value => value.Id == answerId),
                        answerInput,
                        question.FillBlankCaseSensitive);
                }
                else
                {
                    var answer = CreateAcceptedAnswer(question, answerInput);
                    question.AcceptedAnswers.Add(answer);
                    _db.FillBlankAcceptedAnswers.Add(answer);
                }
            }
        }
    }

    /// <summary>
    /// 创建一道新题目及其全部新选项和标准答案。
    /// </summary>
    private static PaperQuestion CreateQuestion(Paper paper, PaperQuestionInput input)
    {
        var question = new PaperQuestion
        {
            PaperId = paper.Id,
            Paper = paper,
            Prompt = string.Empty
        };
        ApplyQuestionValues(question, input);
        foreach (var optionInput in input.Options)
        {
            question.Options.Add(CreateOption(question, optionInput));
        }
        foreach (var answerInput in input.AcceptedAnswers)
        {
            question.AcceptedAnswers.Add(CreateAcceptedAnswer(question, answerInput));
        }
        return question;
    }

    /// <summary>
    /// 将请求中的题型、文本、评分和顺序应用到 tracked 题目。
    /// </summary>
    private static void ApplyQuestionValues(
        PaperQuestion question,
        PaperQuestionInput input)
    {
        question.Type = input.Type;
        question.Prompt = NormalizeRequired(input.Prompt);
        question.Explanation = NormalizeOptional(input.Explanation);
        question.Points = input.Points;
        question.SortOrder = input.SortOrder;
        question.CorrectBoolean = input.CorrectBoolean;
        question.FillBlankCaseSensitive = input.FillBlankCaseSensitive;
    }

    /// <summary>
    /// 创建隶属于指定单选题的新选项。
    /// </summary>
    private static PaperQuestionOption CreateOption(
        PaperQuestion question,
        PaperQuestionOptionInput input)
    {
        var option = new PaperQuestionOption
        {
            QuestionId = question.Id,
            Question = question,
            Text = string.Empty
        };
        ApplyOptionValues(option, input);
        return option;
    }

    /// <summary>
    /// 将选项文本、正确标记和顺序应用到 tracked 选项。
    /// </summary>
    private static void ApplyOptionValues(
        PaperQuestionOption option,
        PaperQuestionOptionInput input)
    {
        option.Text = NormalizeRequired(input.Text);
        option.IsCorrect = input.IsCorrect;
        option.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 创建隶属于指定填空题的新可接受答案。
    /// </summary>
    private static FillBlankAcceptedAnswer CreateAcceptedAnswer(
        PaperQuestion question,
        FillBlankAcceptedAnswerInput input)
    {
        var answer = new FillBlankAcceptedAnswer
        {
            QuestionId = question.Id,
            Question = question,
            Text = string.Empty,
            NormalizedText = string.Empty
        };
        ApplyAcceptedAnswerValues(answer, input, question.FillBlankCaseSensitive);
        return answer;
    }

    /// <summary>
    /// 将展示文本、比较键和顺序应用到 tracked 可接受答案。
    /// </summary>
    private static void ApplyAcceptedAnswerValues(
        FillBlankAcceptedAnswer answer,
        FillBlankAcceptedAnswerInput input,
        bool caseSensitive)
    {
        answer.Text = FillBlankAnswerNormalizer.NormalizeForDisplay(input.Text);
        answer.NormalizedText = FillBlankAnswerNormalizer.Normalize(
            input.Text,
            caseSensitive);
        answer.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 验证试卷聚合满足进入 Published 的全部结构和评分要求。
    /// </summary>
    private static void EnsurePublishable(Paper paper)
    {
        var totalScore = paper.Questions.Sum(value => value.Points);
        if (string.IsNullOrWhiteSpace(paper.Title) ||
            string.IsNullOrWhiteSpace(paper.LanguageTag) ||
            paper.Questions.Count is < 1 or > OnlineQuizConstraints.MaxQuestionCount ||
            paper.Questions.Select(value => value.SortOrder).Distinct().Count() !=
                paper.Questions.Count ||
            totalScore is < 0 or > OnlineQuizConstraints.MaxTotalScore ||
            paper.PassingScore < 0 || paper.PassingScore > totalScore ||
            paper.Questions.Any(question => !IsPublishableQuestion(question)))
        {
            throw ConflictException.Create(ErrorCodes.PaperPublishRequirementsNotMet);
        }
    }

    /// <summary>
    /// 判断一道题目是否满足其题型对应的完整发布要求。
    /// </summary>
    private static bool IsPublishableQuestion(PaperQuestion question)
    {
        if (string.IsNullOrWhiteSpace(question.Prompt) ||
            question.Points is < OnlineQuizConstraints.MinPoints or
                > OnlineQuizConstraints.MaxPoints ||
            question.SortOrder is < 0 or > OnlineQuizConstraints.MaxSortOrder)
        {
            return false;
        }

        return question.Type switch
        {
            PaperQuestionType.SingleChoice =>
                question.Options.Count is >= 2 and <= OnlineQuizConstraints.MaxOptionCount &&
                question.Options.All(value => !string.IsNullOrWhiteSpace(value.Text)) &&
                question.Options.Select(value => value.SortOrder).Distinct().Count() ==
                    question.Options.Count &&
                question.Options.Count(value => value.IsCorrect) == 1 &&
                question.CorrectBoolean is null && !question.FillBlankCaseSensitive &&
                question.AcceptedAnswers.Count == 0,
            PaperQuestionType.TrueFalse =>
                question.CorrectBoolean.HasValue && question.Options.Count == 0 &&
                question.AcceptedAnswers.Count == 0 && !question.FillBlankCaseSensitive,
            PaperQuestionType.FillBlank =>
                question.Options.Count == 0 && question.CorrectBoolean is null &&
                question.AcceptedAnswers.Count is >= 1 and <=
                    OnlineQuizConstraints.MaxAcceptedAnswerCount &&
                question.AcceptedAnswers.All(value =>
                    !string.IsNullOrWhiteSpace(value.Text) &&
                    !string.IsNullOrWhiteSpace(value.NormalizedText)) &&
                question.AcceptedAnswers.Select(value => value.SortOrder)
                    .Distinct().Count() == question.AcceptedAnswers.Count &&
                question.AcceptedAnswers.Select(value => value.NormalizedText)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    question.AcceptedAnswers.Count,
            _ => false
        };
    }

    /// <summary>
    /// 创建可由 EF Core 翻译的管理员完整详情 projection。
    /// </summary>
    private static Expression<Func<Paper, AdminPaperResponse>>
        ToAdminResponseProjection()
        => paper => new AdminPaperResponse(
            paper.Id,
            paper.Title,
            paper.Description,
            paper.Instructions,
            paper.LanguageTag,
            paper.Status,
            paper.PassingScore,
            paper.TotalScore,
            paper.CreatedById,
            paper.LastEditorId,
            paper.PublishedAt,
            paper.ConcurrencyStamp,
            paper.Questions.OrderBy(question => question.SortOrder)
                .ThenBy(question => question.Id)
                .Select(question => new AdminPaperQuestionResponse(
                    question.Id,
                    question.Type,
                    question.Prompt,
                    question.Explanation,
                    question.Points,
                    question.SortOrder,
                    question.CorrectBoolean,
                    question.FillBlankCaseSensitive,
                    question.Options.OrderBy(option => option.SortOrder)
                        .ThenBy(option => option.Id)
                        .Select(option => new AdminPaperQuestionOptionResponse(
                            option.Id,
                            option.Text,
                            option.IsCorrect,
                            option.SortOrder))
                        .ToList(),
                    question.AcceptedAnswers.OrderBy(answer => answer.SortOrder)
                        .ThenBy(answer => answer.Id)
                        .Select(answer => new AdminFillBlankAcceptedAnswerResponse(
                            answer.Id,
                            answer.Text,
                            answer.SortOrder))
                        .ToList()))
                .ToList(),
            paper.CreatedAt,
            paper.UpdatedAt);

    /// <summary>
    /// 创建只包含可开始新测验的已发布试卷查询。
    /// </summary>
    private IQueryable<Paper> PublishedPapersQuery()
        => _db.Papers.AsNoTracking().Where(value =>
            value.Status == PaperPublicationStatus.Published &&
            value.PublishedAt != null);

    /// <summary>
    /// 保存试卷变更并映射排序、标准答案、历史外键和并发冲突。
    /// </summary>
    private async Task SavePaperChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.PaperConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                SortOrderUniqueIndexes))
        {
            throw ConflictException.Create(ErrorCodes.PaperSortOrderConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                AcceptedAnswerUniqueIndex))
        {
            throw ConflictException.Create(ErrorCodes.PaperAcceptedAnswerDuplicate);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                AttemptPaperForeignKey))
        {
            throw ConflictException.Create(ErrorCodes.PaperContentLocked);
        }
    }

    /// <summary>
    /// 计算请求题目分值之和并拒绝越过持久化安全范围的结果。
    /// </summary>
    private static int CalculateTotalScore(
        IReadOnlyCollection<PaperQuestionInput> questions)
    {
        var total = questions.Sum(value => (long)value.Points);
        if (total is < 0 or > OnlineQuizConstraints.MaxTotalScore)
        {
            throw new RequestValidationException(ErrorCodes.PaperPointsInvalid);
        }
        return (int)total;
    }

    /// <summary>
    /// 去除必填文本两端空白。
    /// </summary>
    private static string NormalizeRequired(string value) => value.Trim();

    /// <summary>
    /// 将空白可选文本转换为 null，否则去除两端空白。
    /// </summary>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 创建包含总数和总页数的分页响应。
    /// </summary>
    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(items, page, pageSize, totalCount,
            (totalCount + pageSize - 1) / pageSize);
}
