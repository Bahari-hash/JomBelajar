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
        "IX_fill_blank_accepted_answers_QuestionId_SortOrder",
        "IX_paper_dictation_blanks_QuestionId_SortOrder"
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
            PassingScorePercentage = request.PassingScorePercentage,
            CreatedById = adminId,
            LastEditorId = adminId
        };
        await ValidateAudioTargetsAsync(request.Questions, cancellationToken);
        await ApplyCategoryTargetAsync(paper, request.CategoryIds, cancellationToken);
        ApplyNewTarget(paper, request);
        paper.TotalScore = CalculateTotalScore(request.Questions);
        paper.PassingScore = CalculatePassingScore(
            paper.TotalScore,
            paper.PassingScorePercentage);
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
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
        if (paper.Status is PaperPublicationStatus.Published or
            PaperPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
        }
        if (await HasAttemptsAsync(paperId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.PaperContentLocked);
        }
        ValidateChildOwnership(paper, request);
        await ValidateAudioTargetsAsync(request.Questions, cancellationToken);
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        paper.Title = NormalizeRequired(request.Title);
        paper.Description = NormalizeOptional(request.Description);
        paper.Instructions = NormalizeOptional(request.Instructions);
        paper.PassingScorePercentage = request.PassingScorePercentage;
        paper.TotalScore = CalculateTotalScore(request.Questions);
        paper.PassingScore = CalculatePassingScore(
            paper.TotalScore,
            paper.PassingScorePercentage);
        paper.LastEditorId = adminId;
        paper.ConcurrencyStamp = Guid.NewGuid();

        StageExistingChildren(paper, request);
        await SavePaperChangesAsync(cancellationToken);
        await ApplyCategoryTargetAsync(paper, request.CategoryIds, cancellationToken);
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
    public async Task<PaperValidationResponse> ValidateAsync(
        Guid paperId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperMutationRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
        if (paper.Status is not (PaperPublicationStatus.Draft or
            PaperPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.PaperValidationStateConflict);
        }

        var issues = BuildPublishValidationIssues(paper);
        return new PaperValidationResponse(issues.Count == 0, issues);
    }

    /// <inheritdoc />
    public async Task<AdminPaperResponse> PublishAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperMutationRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
        if (paper.Status == PaperPublicationStatus.Published)
        {
            return await GetAdminByIdAsync(paperId, cancellationToken);
        }
        if (paper.Status is not (PaperPublicationStatus.Draft or
            PaperPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
        }

        if (BuildPublishValidationIssues(paper).Count > 0)
        {
            throw ConflictException.Create(ErrorCodes.PaperPublishRequirementsNotMet);
        }
        paper.TotalScore = paper.Questions.Sum(value => value.Points);
        paper.PassingScore = CalculatePassingScore(
            paper.TotalScore,
            paper.PassingScorePercentage);
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
        PaperMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperMutationRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
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
    public async Task<AdminPaperResponse> ArchiveAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperMutationRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
        if (paper.Status is not (PaperPublicationStatus.Draft or
            PaperPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.PaperArchiveConflict);
        }

        paper.Status = PaperPublicationStatus.Archived;
        paper.ArchivedAt = _timeProvider.GetUtcNow();
        paper.LastEditorId = adminId;
        paper.ConcurrencyStamp = Guid.NewGuid();
        await SavePaperChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Archived paper {PaperId} by administrator {AdminId}",
            paperId,
            adminId);
        return await GetAdminByIdAsync(paperId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperMutationRequestValidator());
        var paper = await FindPaperForEditAsync(paperId, cancellationToken);
        EnsureExpectedStamp(paper, request.ConcurrencyStamp);
        if (paper.Status == PaperPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.PaperPublishedDeleteConflict);
        }
        if (paper.Status == PaperPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.PaperStatusConflict);
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
    {
        var response = await _db.Papers.AsNoTracking()
            .Where(value => value.Id == paperId)
            .Select(ToAdminResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);
        var auditUsers = await LoadAuditUsersAsync(
            [response.CreatedBy.Id, response.LastEditor.Id],
            cancellationToken);
        return EnrichAuditUsers(response, auditUsers);
    }

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
        else
        {
            query = query.Where(value => value.Status != PaperPublicationStatus.Archived);
        }
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(value => value.CategoryAssignments.Any(
                assignment => assignment.PaperCategoryId == categoryId));
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var projectedItems = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new AdminPaperListItemResponse(
                value.Id,
                value.Title,
                value.CategoryAssignments.OrderBy(x => x.PaperCategory.Name)
                    .Select(x => new PaperCategorySummaryResponse(
                        x.PaperCategoryId, x.PaperCategory.Name, x.PaperCategory.Slug))
                    .ToList(),
                value.Status,
                value.Questions.Count,
                value.TotalScore,
                value.PassingScore,
                value.Attempts.Count,
                new ContentAuditUserResponse(value.CreatedById, null, null),
                new ContentAuditUserResponse(value.LastEditorId, null, null),
                value.PublishedAt,
                value.ArchivedAt,
                value.CreatedAt,
                value.UpdatedAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        var auditUserIds = projectedItems.SelectMany(value => new[]
        {
            value.CreatedBy.Id,
            value.LastEditor.Id
        }).Distinct().ToArray();
        var auditUsers = await LoadAuditUsersAsync(auditUserIds, cancellationToken);
        var items = projectedItems.Select(value => value with
        {
            CreatedBy = GetAuditUser(value.CreatedBy.Id, auditUsers),
            LastEditor = GetAuditUser(value.LastEditor.Id, auditUsers)
        }).ToArray();
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<PaperCatalogItemResponse>> GetCatalogAsync(
        PaperCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new PaperCatalogRequestValidator());
        var query = PublishedPapersQuery();
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(value => value.CategoryAssignments.Any(
                assignment => assignment.PaperCategoryId == categoryId));
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
                value.CategoryAssignments.OrderBy(x => x.PaperCategory.Name)
                    .Select(x => new PaperCategorySummaryResponse(
                        x.PaperCategoryId, x.PaperCategory.Name, x.PaperCategory.Slug))
                    .ToList(),
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
                value.CategoryAssignments.OrderBy(x => x.PaperCategory.Name)
                    .Select(x => new PaperCategorySummaryResponse(
                        x.PaperCategoryId, x.PaperCategory.Name, x.PaperCategory.Slug))
                    .ToList(),
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
            .Include(value => value.CategoryAssignments)
                .ThenInclude(value => value.PaperCategory)
            .Include(value => value.Questions)
                .ThenInclude(value => value.Options)
            .Include(value => value.Questions)
                .ThenInclude(value => value.AcceptedAnswers)
            .Include(value => value.Questions)
                .ThenInclude(value => value.DictationBlanks)
            .Include(value => value.Questions)
                .ThenInclude(value => value.AudioResource)
            .SingleOrDefaultAsync(value => value.Id == paperId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperNotFound);

    private async Task ApplyCategoryTargetAsync(
        Paper paper,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds.Distinct().ToArray();
        var categories = await _db.PaperCategories
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (categories.Count != ids.Length)
        {
            throw NotFoundException.Create(ErrorCodes.PaperCategoryNotFound);
        }
        if (categories.Any(x => !x.IsActive))
        {
            throw ConflictException.Create(ErrorCodes.PaperCategoryInactive);
        }
        foreach (var assignment in paper.CategoryAssignments.ToArray())
        {
            _db.PaperCategoryAssignments.Remove(assignment);
            paper.CategoryAssignments.Remove(assignment);
        }
        foreach (var category in categories)
        {
            paper.CategoryAssignments.Add(new PaperCategoryAssignment
            {
                PaperId = paper.Id,
                Paper = paper,
                PaperCategoryId = category.Id,
                PaperCategory = category
            });
        }
    }

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
            var blankIds = question.DictationBlanks.Select(value => value.Id).ToHashSet();
            if (input.DictationBlanks.Any(value =>
                value.Id is { } blankId && !blankIds.Contains(blankId)))
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

            var desiredBlankIds = input.DictationBlanks.Where(value => value.Id.HasValue)
                .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
            var existingBlanks = question.DictationBlanks.ToArray();
            var blankStagingSortOrders = CreateStagingSortOrders(
                existingBlanks.Select(value => value.SortOrder),
                input.DictationBlanks.Select(value => value.SortOrder),
                existingBlanks.Count(value => desiredBlankIds.Contains(value.Id)));
            foreach (var blank in existingBlanks)
            {
                if (!desiredBlankIds.Contains(blank.Id))
                {
                    _db.PaperDictationBlanks.Remove(blank);
                    question.DictationBlanks.Remove(blank);
                    continue;
                }
                blank.SortOrder = blankStagingSortOrders.Dequeue();
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
            foreach (var blankInput in input.DictationBlanks)
            {
                if (blankInput.Id is { } blankId)
                {
                    ApplyDictationBlankValues(
                        question.DictationBlanks.Single(value => value.Id == blankId),
                        blankInput);
                }
                else
                {
                    var blank = CreateDictationBlank(question, blankInput);
                    question.DictationBlanks.Add(blank);
                    _db.PaperDictationBlanks.Add(blank);
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
        foreach (var blankInput in input.DictationBlanks)
        {
            question.DictationBlanks.Add(CreateDictationBlank(question, blankInput));
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
        question.AudioResourceId = input.AudioResourceId;
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

    private static PaperDictationBlank CreateDictationBlank(
        PaperQuestion question,
        PaperDictationBlankInput input)
    {
        var blank = new PaperDictationBlank
        {
            QuestionId = question.Id,
            Question = question,
            Answer = string.Empty,
            NormalizedAnswer = string.Empty
        };
        ApplyDictationBlankValues(blank, input);
        return blank;
    }

    private static void ApplyDictationBlankValues(
        PaperDictationBlank blank,
        PaperDictationBlankInput input)
    {
        var answer = input.Answer.Trim();
        blank.Answer = answer;
        blank.NormalizedAnswer = answer.ToUpperInvariant();
        blank.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 构造发布和只读检查共享的稳定字段问题集合。
    /// </summary>
    private static IReadOnlyList<PaperValidationIssueResponse>
        BuildPublishValidationIssues(Paper paper)
    {
        var issues = new List<PaperValidationIssueResponse>();
        void Add(
            string field,
            ErrorCodes errorCode,
            Guid? questionId = null,
            Guid? childId = null)
        {
            if (!issues.Any(value => value.Field == field &&
                value.ErrorCode == errorCode && value.QuestionId == questionId &&
                value.ChildId == childId))
            {
                issues.Add(new PaperValidationIssueResponse(
                    field,
                    errorCode,
                    errorCode.GetMessage(),
                    questionId,
                    childId));
            }
        }

        if (string.IsNullOrWhiteSpace(paper.Title))
        {
            Add("title", ErrorCodes.PaperTitleRequired);
        }
        else if (paper.Title.Length > OnlineQuizConstraints.MaxTitleLength)
        {
            Add("title", ErrorCodes.PaperTitleLengthLimit);
        }
        var questions = paper.Questions.OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id).ToArray();
        if (questions.Length is < 1 or > OnlineQuizConstraints.MaxQuestionCount)
        {
            Add("questions", ErrorCodes.PaperQuestionCollectionInvalid);
        }
        var duplicateQuestionSortOrders = questions.GroupBy(value => value.SortOrder)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var totalScore = questions.Sum(value => (long)value.Points);
        if (totalScore is < 0 or > OnlineQuizConstraints.MaxTotalScore)
        {
            Add("questions", ErrorCodes.PaperPointsInvalid);
        }
        if (paper.PassingScorePercentage is < 1 or > 100)
        {
            Add("passingScorePercentage", ErrorCodes.PaperPassingScoreInvalid);
        }

        for (var questionIndex = 0; questionIndex < questions.Length; questionIndex++)
        {
            var question = questions[questionIndex];
            var field = $"questions[{questionIndex}]";
            if (string.IsNullOrWhiteSpace(question.Prompt))
            {
                Add($"{field}.prompt", ErrorCodes.PaperQuestionPromptRequired, question.Id);
            }
            else if (question.Prompt.Length > OnlineQuizConstraints.MaxPromptLength)
            {
                Add($"{field}.prompt", ErrorCodes.PaperQuestionPromptLengthLimit, question.Id);
            }
            if (question.Points is < OnlineQuizConstraints.MinPoints or
                > OnlineQuizConstraints.MaxPoints)
            {
                Add($"{field}.points", ErrorCodes.PaperPointsInvalid, question.Id);
            }
            if (question.SortOrder is < 0 or > OnlineQuizConstraints.MaxSortOrder)
            {
                Add($"{field}.sortOrder", ErrorCodes.PaperSortOrderInvalid, question.Id);
            }
            else if (duplicateQuestionSortOrders.Contains(question.SortOrder))
            {
                Add($"{field}.sortOrder", ErrorCodes.PaperSortOrderConflict, question.Id);
            }

            switch (question.Type)
            {
                case PaperQuestionType.SingleChoice:
                    AddSingleChoiceIssues(question, questionIndex, Add);
                    break;
                case PaperQuestionType.TrueFalse:
                    AddTrueFalseIssues(question, questionIndex, Add);
                    break;
                case PaperQuestionType.FillBlank:
                    AddFillBlankIssues(question, questionIndex, Add);
                    break;
                case PaperQuestionType.Dictation:
                    AddDictationIssues(question, questionIndex, Add);
                    break;
                default:
                    Add($"{field}.type", ErrorCodes.PaperQuestionTypeInvalid, question.Id);
                    break;
            }
        }

        return issues;
    }

    private async Task ValidateAudioTargetsAsync(
        IReadOnlyCollection<PaperQuestionInput> questions,
        CancellationToken cancellationToken)
    {
        var ids = questions.Where(value => value.Type == PaperQuestionType.Dictation)
            .Select(value => value.AudioResourceId)
            .Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault())
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return;
        }
        var audio = await _db.AudioResources.AsNoTracking()
            .Where(value => ids.Contains(value.Id))
            .Select(value => new { value.Id, value.Status })
            .ToListAsync(cancellationToken);
        if (audio.Count != ids.Length)
        {
            throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        }
        if (audio.Any(value => value.Status != AudioResourceStatus.Ready))
        {
            throw ConflictException.Create(ErrorCodes.AudioNotReady);
        }
    }

    private static void AddDictationIssues(
        PaperQuestion question,
        int questionIndex,
        Action<string, ErrorCodes, Guid?, Guid?> add)
    {
        var field = $"questions[{questionIndex}]";
        if (question.AudioResourceId is null)
        {
            add($"{field}.audioResourceId", ErrorCodes.AudioNotFound, question.Id, null);
        }
        else if (question.AudioResource?.Status != AudioResourceStatus.Ready)
        {
            add($"{field}.audioResourceId", ErrorCodes.AudioNotReady, question.Id, null);
        }
        if (question.Options.Count > 0 || question.AcceptedAnswers.Count > 0 ||
            question.CorrectBoolean.HasValue || question.FillBlankCaseSensitive)
        {
            add(field, ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
        var blanks = question.DictationBlanks.OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id).ToArray();
        if (blanks.Length is < 1 or > OnlineQuizConstraints.MaxDictationBlankCount)
        {
            add($"{field}.dictationBlanks", ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
        var duplicateSortOrders = blanks.GroupBy(value => value.SortOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        for (var index = 0; index < blanks.Length; index++)
        {
            var blank = blanks[index];
            var blankField = $"{field}.dictationBlanks[{index}]";
            if (string.IsNullOrWhiteSpace(blank.Answer) ||
                string.IsNullOrWhiteSpace(blank.NormalizedAnswer))
            {
                add($"{blankField}.answer", ErrorCodes.PaperAcceptedAnswerRequired, question.Id, blank.Id);
            }
            else if (blank.Answer.Length > OnlineQuizConstraints.MaxAnswerTextLength ||
                blank.NormalizedAnswer.Length > OnlineQuizConstraints.MaxAnswerTextLength)
            {
                add($"{blankField}.answer", ErrorCodes.PaperAnswerTextLengthLimit, question.Id, blank.Id);
            }
            if (blank.SortOrder is < 0 or > OnlineQuizConstraints.MaxSortOrder)
            {
                add($"{blankField}.sortOrder", ErrorCodes.PaperSortOrderInvalid, question.Id, blank.Id);
            }
            else if (duplicateSortOrders.Contains(blank.SortOrder))
            {
                add($"{blankField}.sortOrder", ErrorCodes.PaperSortOrderConflict, question.Id, blank.Id);
            }
        }
    }

    /// <summary>
    /// 将单选题发布问题追加到共享问题集合。
    /// </summary>
    private static void AddSingleChoiceIssues(
        PaperQuestion question,
        int questionIndex,
        Action<string, ErrorCodes, Guid?, Guid?> add)
    {
        var field = $"questions[{questionIndex}]";
        var options = question.Options.OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id).ToArray();
        if (options.Length is < 2 or > OnlineQuizConstraints.MaxOptionCount ||
            options.Count(value => value.IsCorrect) != 1)
        {
            add($"{field}.options", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        AddOptionIssues(question.Id, questionIndex, options, add);
        if (question.CorrectBoolean.HasValue)
        {
            add($"{field}.correctBoolean", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.FillBlankCaseSensitive)
        {
            add($"{field}.fillBlankCaseSensitive",
                ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
        if (question.AcceptedAnswers.Count > 0)
        {
            add($"{field}.acceptedAnswers", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.AudioResourceId.HasValue || question.DictationBlanks.Count > 0)
        {
            add(field, ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
    }

    /// <summary>
    /// 将判断题发布问题追加到共享问题集合。
    /// </summary>
    private static void AddTrueFalseIssues(
        PaperQuestion question,
        int questionIndex,
        Action<string, ErrorCodes, Guid?, Guid?> add)
    {
        var field = $"questions[{questionIndex}]";
        if (!question.CorrectBoolean.HasValue)
        {
            add($"{field}.correctBoolean", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.Options.Count > 0)
        {
            add($"{field}.options", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.AcceptedAnswers.Count > 0)
        {
            add($"{field}.acceptedAnswers", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.FillBlankCaseSensitive)
        {
            add($"{field}.fillBlankCaseSensitive",
                ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
        if (question.AudioResourceId.HasValue || question.DictationBlanks.Count > 0)
        {
            add(field, ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }
    }

    /// <summary>
    /// 将填空题发布问题追加到共享问题集合。
    /// </summary>
    private static void AddFillBlankIssues(
        PaperQuestion question,
        int questionIndex,
        Action<string, ErrorCodes, Guid?, Guid?> add)
    {
        var field = $"questions[{questionIndex}]";
        if (question.Options.Count > 0)
        {
            add($"{field}.options", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.CorrectBoolean.HasValue)
        {
            add($"{field}.correctBoolean", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        if (question.AudioResourceId.HasValue || question.DictationBlanks.Count > 0)
        {
            add(field, ErrorCodes.PaperQuestionShapeInvalid, question.Id, null);
        }

        var answers = question.AcceptedAnswers.OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id).ToArray();
        if (answers.Length is < 1 or > OnlineQuizConstraints.MaxAcceptedAnswerCount)
        {
            add($"{field}.acceptedAnswers", ErrorCodes.PaperQuestionShapeInvalid,
                question.Id, null);
        }
        var duplicateSortOrders = answers.GroupBy(value => value.SortOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var duplicateNormalized = answers.GroupBy(
                value => value.NormalizedText,
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        for (var answerIndex = 0; answerIndex < answers.Length; answerIndex++)
        {
            var answer = answers[answerIndex];
            var answerField = $"{field}.acceptedAnswers[{answerIndex}]";
            if (string.IsNullOrWhiteSpace(answer.Text) ||
                string.IsNullOrWhiteSpace(answer.NormalizedText))
            {
                add($"{answerField}.text", ErrorCodes.PaperAcceptedAnswerRequired,
                    question.Id, answer.Id);
            }
            else if (answer.Text.Length > OnlineQuizConstraints.MaxAnswerTextLength ||
                answer.NormalizedText.Length > OnlineQuizConstraints.MaxAnswerTextLength)
            {
                add($"{answerField}.text", ErrorCodes.PaperAnswerTextLengthLimit,
                    question.Id, answer.Id);
            }
            if (answer.SortOrder is < 0 or > OnlineQuizConstraints.MaxSortOrder)
            {
                add($"{answerField}.sortOrder", ErrorCodes.PaperSortOrderInvalid,
                    question.Id, answer.Id);
            }
            else if (duplicateSortOrders.Contains(answer.SortOrder))
            {
                add($"{answerField}.sortOrder", ErrorCodes.PaperSortOrderConflict,
                    question.Id, answer.Id);
            }
            if (duplicateNormalized.Contains(answer.NormalizedText))
            {
                add($"{answerField}.text", ErrorCodes.PaperAcceptedAnswerDuplicate,
                    question.Id, answer.Id);
            }
        }
    }

    /// <summary>
    /// 将单选题选项的文本和排序问题追加到共享问题集合。
    /// </summary>
    private static void AddOptionIssues(
        Guid questionId,
        int questionIndex,
        IReadOnlyList<PaperQuestionOption> options,
        Action<string, ErrorCodes, Guid?, Guid?> add)
    {
        var duplicateSortOrders = options.GroupBy(value => value.SortOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var option = options[optionIndex];
            var field = $"questions[{questionIndex}].options[{optionIndex}]";
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                add($"{field}.text", ErrorCodes.PaperOptionTextRequired,
                    questionId, option.Id);
            }
            else if (option.Text.Length > OnlineQuizConstraints.MaxOptionTextLength)
            {
                add($"{field}.text", ErrorCodes.PaperOptionTextLengthLimit,
                    questionId, option.Id);
            }
            if (option.SortOrder is < 0 or > OnlineQuizConstraints.MaxSortOrder)
            {
                add($"{field}.sortOrder", ErrorCodes.PaperSortOrderInvalid,
                    questionId, option.Id);
            }
            else if (duplicateSortOrders.Contains(option.SortOrder))
            {
                add($"{field}.sortOrder", ErrorCodes.PaperSortOrderConflict,
                    questionId, option.Id);
            }
        }
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
            paper.CategoryAssignments.OrderBy(x => x.PaperCategory.Name)
                .Select(x => new PaperCategorySummaryResponse(
                    x.PaperCategoryId, x.PaperCategory.Name, x.PaperCategory.Slug))
                .ToList(),
            paper.Status,
            paper.PassingScorePercentage,
            paper.PassingScore,
            paper.TotalScore,
            paper.Attempts.Count,
            new ContentAuditUserResponse(paper.CreatedById, null, null),
            new ContentAuditUserResponse(paper.LastEditorId, null, null),
            paper.PublishedAt,
            paper.ArchivedAt,
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
                        .ToList(),
                    question.AudioResourceId,
                    question.DictationBlanks.OrderBy(blank => blank.SortOrder)
                        .ThenBy(blank => blank.Id)
                        .Select(blank => new AdminPaperDictationBlankResponse(
                            blank.Id,
                            blank.Answer,
                            blank.SortOrder))
                        .ToList()))
                .ToList(),
            paper.CreatedAt,
            paper.UpdatedAt);

    /// <summary>
    /// 一次性加载管理响应所需的最小审计用户资料。
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ContentAuditUserResponse>>
        LoadAuditUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        => await _db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new ContentAuditUserResponse(
                user.Id,
                user.Nickname,
                user.AvatarUrl))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

    /// <summary>
    /// 将详情 projection 中的审计标识替换为安全用户摘要。
    /// </summary>
    private static AdminPaperResponse EnrichAuditUsers(
        AdminPaperResponse response,
        IReadOnlyDictionary<Guid, ContentAuditUserResponse> users)
        => response with
        {
            CreatedBy = GetAuditUser(response.CreatedBy.Id, users),
            LastEditor = GetAuditUser(response.LastEditor.Id, users)
        };

    /// <summary>
    /// 返回审计用户摘要；异常孤立数据仅保留稳定标识。
    /// </summary>
    private static ContentAuditUserResponse GetAuditUser(
        Guid userId,
        IReadOnlyDictionary<Guid, ContentAuditUserResponse> users)
        => users.TryGetValue(userId, out var user)
            ? user
            : new ContentAuditUserResponse(userId, null, null);

    /// <summary>
    /// 在任何幂等、状态或历史判断前校验客户端看到的试卷版本。
    /// </summary>
    private static void EnsureExpectedStamp(Paper paper, Guid expectedStamp)
    {
        if (paper.ConcurrencyStamp != expectedStamp)
        {
            throw ConflictException.Create(ErrorCodes.PaperConcurrencyConflict);
        }
    }

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
    /// 根据试卷总分和及格百分比计算向上取整的及格分。
    /// </summary>
    private static int CalculatePassingScore(
        int totalScore,
        int passingScorePercentage)
        => (int)Math.Ceiling(totalScore * passingScorePercentage / 100m);

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
