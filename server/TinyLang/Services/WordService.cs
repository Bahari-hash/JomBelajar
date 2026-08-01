using System.Linq.Expressions;
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
/// 实现词条聚合写入、音频关联校验、发布状态和安全用户查询规则。
/// </summary>
public sealed class WordService : IWordService
{
    private const string HeadwordUniqueIndex =
        "IX_words_LanguageTag_NormalizedHeadword";
    private const string PronunciationAudioUniqueIndex =
        "IX_word_pronunciations_WordId_AudioClipId";
    private const string DefaultPronunciationUniqueIndex =
        "IX_word_pronunciations_WordId";
    private static readonly string[] SortOrderUniqueIndexes =
    [
        "IX_word_senses_WordId_SortOrder",
        "IX_example_sentences_WordSenseId_SortOrder",
        "IX_word_pronunciations_WordId_SortOrder"
    ];
    private static readonly string[] StudyHistoryForeignKeys =
    [
        "FK_user_word_progress_words_WordId",
        "FK_word_study_session_items_words_WordId"
    ];

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WordService> _logger;

    /// <summary>
    /// 使用数据库、约束异常分类器、时间源和结构化日志创建词条服务。
    /// </summary>
    public WordService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider,
        ILogger<WordService> logger)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> CreateDraftAsync(
        Guid adminId,
        CreateWordRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetCollections(request, allowExistingIds: false);
        var identity = NormalizeIdentity(request.Headword, request.LanguageTag);
        await EnsureHeadwordUniqueAsync(
            identity.LanguageTag,
            identity.NormalizedHeadword,
            excludedWordId: null,
            cancellationToken);
        await ValidateRequestedAudioAsync(
            identity.LanguageTag,
            request.Senses,
            request.Pronunciations,
            cancellationToken);

        var word = new Word
        {
            LanguageTag = identity.LanguageTag,
            Headword = identity.Headword,
            NormalizedHeadword = identity.NormalizedHeadword,
            CreatedById = adminId,
            LastEditorId = adminId
        };
        ApplyNewTarget(word, request);
        _db.Words.Add(word);
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created word draft {WordId} by administrator {AdminId}",
            word.Id,
            adminId);
        return await GetAdminByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> UpdateAsync(
        Guid wordId,
        Guid adminId,
        UpdateWordRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetCollections(request, allowExistingIds: true);
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);
        if (word.Status is WordPublicationStatus.Published or
            WordPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }

        ValidateChildOwnership(word, request);
        var identity = NormalizeIdentity(request.Headword, request.LanguageTag);
        await EnsureHeadwordUniqueAsync(
            identity.LanguageTag,
            identity.NormalizedHeadword,
            word.Id,
            cancellationToken);
        await ValidateRequestedAudioAsync(
            identity.LanguageTag,
            request.Senses,
            request.Pronunciations,
            cancellationToken);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        word.LanguageTag = identity.LanguageTag;
        word.Headword = identity.Headword;
        word.NormalizedHeadword = identity.NormalizedHeadword;
        word.LastEditorId = adminId;
        word.ConcurrencyStamp = Guid.NewGuid();
        StageExistingChildren(word, request);
        await SaveWordChangesAsync(cancellationToken);

        ApplyFinalTarget(word, request);
        await SaveWordChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Updated word {WordId} by administrator {AdminId}",
            word.Id,
            adminId);
        return await GetAdminByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> PublishAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);
        if (word.Status == WordPublicationStatus.Published)
        {
            return await GetAdminByIdAsync(wordId, cancellationToken);
        }
        if (word.Status is not (WordPublicationStatus.Draft or WordPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }

        EnsurePublishableContent(word);
        await ValidateStoredAudioAsync(word, cancellationToken);
        word.Status = WordPublicationStatus.Published;
        word.PublishedAt ??= _timeProvider.GetUtcNow();
        word.LastEditorId = adminId;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published word {WordId} by administrator {AdminId}",
            word.Id,
            adminId);
        return await GetAdminByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> UnpublishAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);
        if (word.Status == WordPublicationStatus.Unpublished)
        {
            return await GetAdminByIdAsync(wordId, cancellationToken);
        }
        if (word.Status != WordPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }

        word.Status = WordPublicationStatus.Unpublished;
        word.LastEditorId = adminId;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Unpublished word {WordId} by administrator {AdminId}",
            word.Id,
            adminId);
        return await GetAdminByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> ArchiveAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);
        if (word.Status is not (WordPublicationStatus.Draft or
            WordPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.WordArchiveConflict);
        }

        word.Status = WordPublicationStatus.Archived;
        word.ArchivedAt = _timeProvider.GetUtcNow();
        word.LastEditorId = adminId;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Archived word {WordId} by administrator {AdminId}",
            word.Id,
            adminId);
        return await GetAdminByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);
        if (word.Status == WordPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordPublishedDeleteConflict);
        }
        if (word.Status == WordPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }
        if (await _db.UserWordProgress.AsNoTracking().AnyAsync(
                value => value.WordId == wordId,
                cancellationToken) ||
            await _db.WordStudySessionItems.AsNoTracking().AnyAsync(
                value => value.WordId == wordId,
                cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.WordHasStudyHistory);
        }

        _db.Words.Remove(word);
        await SaveWordChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted word {WordId} by administrator {AdminId}",
            wordId,
            adminId);
    }

    /// <inheritdoc />
    public async Task<BatchWordValidationResponse> ValidateBatchAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken = default)
        => (await BuildBatchValidationAsync(request, cancellationToken)).Response;

    /// <inheritdoc />
    public async Task<BatchWordImportResponse> ImportBatchAsync(
        Guid adminId,
        BatchWordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await BuildBatchValidationAsync(request, cancellationToken);
        if (!validation.Response.IsValid)
        {
            throw new RequestValidationException(
                ErrorCodes.WordBatchValidationFailed,
                ToBatchErrorDictionary(validation.Response));
        }

        var words = validation.Rows.Select(row =>
        {
            var word = new Word
            {
                LanguageTag = row.Identity.LanguageTag,
                Headword = row.Identity.Headword,
                NormalizedHeadword = row.Identity.NormalizedHeadword,
                CreatedById = adminId,
                LastEditorId = adminId
            };
            ApplyNewTarget(word, row.Request);
            return (row.RowIndex, Word: word);
        }).ToArray();

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        _db.Words.AddRange(words.Select(value => value.Word));
        await SaveWordChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var batchId = Guid.NewGuid();
        _logger.LogInformation(
            "Imported word batch {BatchId} with {WordCount} drafts by administrator {AdminId}",
            batchId,
            words.Length,
            adminId);
        return new BatchWordImportResponse(
            batchId,
            words.Length,
            words.Select(value => new BatchWordCreatedItemResponse(
                value.RowIndex,
                value.Word.Id)).ToArray());
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> GetAdminByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default)
    {
        var response = await _db.Words.AsNoTracking()
            .Where(value => value.Id == wordId)
            .Select(ToAdminResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);
        var users = await LoadAuditUsersAsync(
            [response.CreatedBy.Id, response.LastEditor.Id],
            cancellationToken);
        return EnrichAuditUsers(response, users);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<AdminWordListItemResponse>> GetAdminListAsync(
        AdminWordListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Words.AsNoTracking();
        if (request.Status is { } status)
        {
            query = query.Where(value => value.Status == status);
        }
        else
        {
            query = query.Where(value => value.Status != WordPublicationStatus.Archived);
        }
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var language = WordTextNormalizer.NormalizeLanguageTag(request.Language);
            query = query.Where(value => value.LanguageTag == language);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = WordTextNormalizer.CreateHeadwordComparisonKey(request.Keyword);
            query = query.Where(value => value.NormalizedHeadword.Contains(keyword));
        }
        if (request.PartOfSpeech is { } partOfSpeech)
        {
            query = query.Where(value => value.Senses.Any(sense =>
                sense.PartOfSpeech == partOfSpeech));
        }
        if (!string.IsNullOrWhiteSpace(request.Definition))
        {
            var definition = request.Definition.Trim().ToUpperInvariant();
            query = query.Where(value => value.Senses.Any(sense =>
                sense.Definition.ToUpper().Contains(definition)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new AdminWordListItemResponse(
                value.Id,
                value.LanguageTag,
                value.Headword,
                value.Status,
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => (PartOfSpeech?)sense.PartOfSpeech)
                    .FirstOrDefault(),
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.Definition)
                    .FirstOrDefault(),
                value.Senses.Count,
                value.Senses.SelectMany(sense => sense.Examples).Count(),
                value.Pronunciations.Count,
                new ContentAuditUserResponse(value.CreatedById, null, null),
                new ContentAuditUserResponse(value.LastEditorId, null, null),
                value.PublishedAt,
                value.ArchivedAt,
                value.CreatedAt,
                value.UpdatedAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        var auditUserIds = items.SelectMany(value => new[]
            {
                value.CreatedBy.Id,
                value.LastEditor.Id
            })
            .Distinct()
            .ToArray();
        var auditUsers = await LoadAuditUsersAsync(auditUserIds, cancellationToken);
        var enrichedItems = items.Select(value => value with
        {
            CreatedBy = GetAuditUser(value.CreatedBy.Id, auditUsers),
            LastEditor = GetAuditUser(value.LastEditor.Id, auditUsers)
        }).ToArray();
        return CreatePage(enrichedItems, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<WordResponse> GetUserByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default)
        => await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
            .Where(value => value.Id == wordId)
            .Select(ToUserResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<WordListItemResponse>> GetUserListAsync(
        WordListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = WordVisibilityPolicy.Apply(_db.Words.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var language = WordTextNormalizer.NormalizeLanguageTag(request.Language);
            query = query.Where(value => value.LanguageTag == language);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = WordTextNormalizer.CreateHeadwordComparisonKey(request.Keyword);
            query = query.Where(value => value.NormalizedHeadword.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new WordListItemResponse(
                value.Id,
                value.LanguageTag,
                value.Headword,
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.PartOfSpeech)
                    .First(),
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.Definition)
                    .First(),
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.DefinitionLanguageTag)
                    .First(),
                value.Pronunciations.Where(pronunciation => pronunciation.IsDefault)
                    .OrderBy(pronunciation => pronunciation.SortOrder)
                    .ThenBy(pronunciation => pronunciation.Id)
                    .Select(pronunciation => new WordPronunciationResponse(
                        pronunciation.AudioClipId,
                        pronunciation.AccentTag,
                        pronunciation.Ipa,
                        pronunciation.IsDefault,
                        pronunciation.SortOrder))
                    .First(),
                value.PublishedAt!.Value))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <summary>
    /// 加载包含全部私有子项的 tracked 词条聚合。
    /// </summary>
    private async Task<Word> FindWordForEditAsync(
        Guid wordId,
        CancellationToken cancellationToken)
        => await _db.Words
            .Include(value => value.Senses)
                .ThenInclude(value => value.Examples)
            .Include(value => value.Pronunciations)
            .SingleOrDefaultAsync(value => value.Id == wordId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    /// <summary>
    /// 对批量请求执行有界结构、单行规则、重复词头和音频引用校验。
    /// </summary>
    private async Task<BatchValidationResult> BuildBatchValidationAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rows is null || request.Rows.Count is < 1 or
            > WordConstraints.MaxBatchRowCount)
        {
            var error = CreateBatchError("rows", ErrorCodes.WordBatchRowCountInvalid);
            return new BatchValidationResult(
                new BatchWordValidationResponse(false, [error], []),
                []);
        }

        var rows = request.Rows.ToArray();
        if (rows.Any(row => row is null))
        {
            var error = CreateBatchError("rows", ErrorCodes.WordChildCollectionInvalid);
            return new BatchValidationResult(
                new BatchWordValidationResponse(false, [error], []),
                []);
        }

        var totalSenseCount = rows.Sum(row => row.Senses?.Count ?? 0);
        var totalExampleCount = rows.Sum(row => row.Senses?.Sum(sense =>
            sense?.Examples?.Count ?? 0) ?? 0);
        var totalPronunciationCount = rows.Sum(row => row.Pronunciations?.Count ?? 0);
        var topLevelErrors = new List<BatchWordFieldErrorResponse>();
        if (totalSenseCount > WordConstraints.MaxBatchSenseCount ||
            totalExampleCount > WordConstraints.MaxBatchExampleCount ||
            totalPronunciationCount > WordConstraints.MaxBatchPronunciationCount)
        {
            topLevelErrors.Add(CreateBatchError(
                "rows",
                ErrorCodes.WordBatchChildCountLimit));
        }
        if (CountBatchCharacters(rows) > WordConstraints.MaxBatchTextCharacterCount)
        {
            topLevelErrors.Add(CreateBatchError(
                "rows",
                ErrorCodes.WordBatchTextLengthLimit));
        }
        if (topLevelErrors.Count > 0)
        {
            return new BatchValidationResult(
                new BatchWordValidationResponse(false, topLevelErrors, []),
                []);
        }

        var validator = new CreateWordRequestValidator();
        var normalizedRows = new List<BatchNormalizedRow>(rows.Length);
        var errorsByRow = Enumerable.Range(0, rows.Length)
            .ToDictionary(index => index, _ => new List<BatchError>());
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            if (rows[rowIndex].Senses is null ||
                rows[rowIndex].Pronunciations is null ||
                rows[rowIndex].Senses.Any(sense =>
                    sense is null || sense.Examples is null ||
                    sense.Examples.Any(example => example is null)) ||
                rows[rowIndex].Pronunciations.Any(pronunciation =>
                    pronunciation is null))
            {
                AddBatchError(
                    errorsByRow[rowIndex],
                    "content",
                    ErrorCodes.WordChildCollectionInvalid);
                continue;
            }
            var candidate = ToCreateRequest(rows[rowIndex]);
            var result = await validator.ValidateAsync(candidate, cancellationToken);
            foreach (var failure in result.Errors)
            {
                var code = Enum.TryParse<ErrorCodes>(failure.ErrorCode, out var parsed)
                    ? parsed
                    : ErrorCodes.RequestValidationFailed;
                AddBatchError(
                    errorsByRow[rowIndex],
                    ToBatchFieldPath(failure.PropertyName),
                    code);
            }
            if (!result.IsValid)
            {
                continue;
            }

            try
            {
                var identity = NormalizeIdentity(candidate.Headword, candidate.LanguageTag);
                var normalized = NormalizeCreateRequest(candidate, identity);
                normalizedRows.Add(new BatchNormalizedRow(rowIndex, normalized, identity));
            }
            catch (BaseAppException exception)
            {
                AddBatchError(errorsByRow[rowIndex], "headword", exception.ErrorCode);
            }
        }

        if (normalizedRows.Sum(row => CountRequestCharacters(row.Request)) >
            WordConstraints.MaxBatchTextCharacterCount)
        {
            topLevelErrors.Add(CreateBatchError(
                "rows",
                ErrorCodes.WordBatchTextLengthLimit));
        }

        foreach (var duplicateGroup in normalizedRows.GroupBy(row => new
        {
            row.Identity.LanguageTag,
            row.Identity.NormalizedHeadword
        }).Where(group => group.Count() > 1))
        {
            foreach (var row in duplicateGroup)
            {
                AddBatchError(errorsByRow[row.RowIndex], "headword", ErrorCodes.WordDuplicate);
            }
        }

        if (normalizedRows.Count > 0)
        {
            var languages = normalizedRows.Select(row => row.Identity.LanguageTag)
                .Distinct().ToArray();
            var headwords = normalizedRows.Select(row => row.Identity.NormalizedHeadword)
                .Distinct().ToArray();
            var existingKeys = await _db.Words.AsNoTracking()
                .Where(word => languages.Contains(word.LanguageTag) &&
                    headwords.Contains(word.NormalizedHeadword))
                .Select(word => new { word.LanguageTag, word.NormalizedHeadword })
                .ToListAsync(cancellationToken);
            var existingSet = existingKeys.Select(value =>
                    $"{value.LanguageTag}\u001f{value.NormalizedHeadword}")
                .ToHashSet(StringComparer.Ordinal);
            foreach (var row in normalizedRows.Where(row => existingSet.Contains(
                         $"{row.Identity.LanguageTag}\u001f{row.Identity.NormalizedHeadword}")))
            {
                AddBatchError(errorsByRow[row.RowIndex], "headword", ErrorCodes.WordDuplicate);
            }

            await ValidateBatchAudioAsync(normalizedRows, errorsByRow, cancellationToken);
        }

        var rowResponses = rows.Select((_, rowIndex) =>
        {
            var normalized = normalizedRows.SingleOrDefault(row => row.RowIndex == rowIndex);
            return new BatchWordRowValidationResponse(
                rowIndex,
                normalized?.Request,
                ToBatchErrorResponses(errorsByRow[rowIndex]));
        }).ToArray();
        var isValid = topLevelErrors.Count == 0 &&
            rowResponses.All(row => row.Errors.Count == 0);
        return new BatchValidationResult(
            new BatchWordValidationResponse(isValid, topLevelErrors, rowResponses),
            normalizedRows);
    }

    /// <summary>
    /// 一次加载批量请求引用的全部音频并将错误定位到具体嵌套字段。
    /// </summary>
    private async Task ValidateBatchAudioAsync(
        IReadOnlyCollection<BatchNormalizedRow> rows,
        IDictionary<int, List<BatchError>> errorsByRow,
        CancellationToken cancellationToken)
    {
        var audioIds = rows.SelectMany(row => row.Request.Pronunciations
                .Select(value => value.AudioClipId)
                .Concat(row.Request.Senses.SelectMany(value => value.Examples)
                    .Where(value => value.AudioClipId.HasValue)
                    .Select(value => value.AudioClipId.GetValueOrDefault())))
            .Distinct().ToArray();
        var audioById = audioIds.Length == 0
            ? new Dictionary<Guid, AudioClip>()
            : await _db.AudioClips.AsNoTracking()
                .Where(value => audioIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, cancellationToken);

        foreach (var row in rows)
        {
            var pronunciations = row.Request.Pronunciations.ToArray();
            for (var index = 0; index < pronunciations.Length; index++)
            {
                var pronunciation = pronunciations[index];
                var code = GetAudioErrorCode(
                    audioById,
                    pronunciation.AudioClipId,
                    AudioClipKind.WordPronunciation,
                    row.Identity.LanguageTag);
                if (code.HasValue)
                {
                    AddBatchError(
                        errorsByRow[row.RowIndex],
                        $"pronunciations[{index}].audioClipId",
                        code.Value);
                }
            }

            var senses = row.Request.Senses.ToArray();
            for (var senseIndex = 0; senseIndex < senses.Length; senseIndex++)
            {
                var examples = senses[senseIndex].Examples.ToArray();
                for (var exampleIndex = 0; exampleIndex < examples.Length; exampleIndex++)
                {
                    var example = examples[exampleIndex];
                    if (!example.AudioClipId.HasValue)
                    {
                        continue;
                    }
                    var code = GetAudioErrorCode(
                        audioById,
                        example.AudioClipId.Value,
                        AudioClipKind.ExampleSentence,
                        example.LanguageTag);
                    if (code.HasValue)
                    {
                        AddBatchError(
                            errorsByRow[row.RowIndex],
                            $"senses[{senseIndex}].examples[{exampleIndex}].audioClipId",
                            code.Value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 返回一个批量音频引用的首个稳定业务错误码。
    /// </summary>
    private static ErrorCodes? GetAudioErrorCode(
        IReadOnlyDictionary<Guid, AudioClip> audioById,
        Guid audioId,
        AudioClipKind expectedKind,
        string expectedLanguageTag)
    {
        if (!audioById.TryGetValue(audioId, out var audio))
        {
            return ErrorCodes.WordAudioNotFound;
        }
        if (audio.ProcessingStatus != AudioProcessingStatus.Ready ||
            audio.PublicationStatus != AudioPublicationStatus.Published)
        {
            return ErrorCodes.WordAudioUnavailable;
        }
        if (audio.Kind != expectedKind)
        {
            return ErrorCodes.WordAudioKindMismatch;
        }
        return WordTextNormalizer.AreLanguageTagsCompatible(
            audio.LanguageTag,
            expectedLanguageTag)
            ? null
            : ErrorCodes.WordAudioLanguageMismatch;
    }

    /// <summary>
    /// 规范化并验证词头及语言唯一键的持久化形态。
    /// </summary>
    private static WordIdentity NormalizeIdentity(string headword, string languageTag)
    {
        if (string.IsNullOrWhiteSpace(headword))
        {
            throw new RequestValidationException(ErrorCodes.WordHeadwordRequired);
        }
        if (string.IsNullOrWhiteSpace(languageTag) ||
            languageTag.Length > WordConstraints.MaxLanguageTagLength ||
            !MediaValidationPatterns.LanguageTag().IsMatch(languageTag))
        {
            throw new RequestValidationException(ErrorCodes.WordLanguageInvalid);
        }

        var display = WordTextNormalizer.NormalizeHeadwordForDisplay(headword);
        var comparisonKey = WordTextNormalizer.CreateHeadwordComparisonKey(headword);
        if (display.Length > WordConstraints.MaxHeadwordLength ||
            comparisonKey.Length > WordConstraints.MaxHeadwordLength)
        {
            throw new RequestValidationException(ErrorCodes.WordHeadwordLengthLimit);
        }
        return new WordIdentity(
            WordTextNormalizer.NormalizeLanguageTag(languageTag),
            display,
            comparisonKey);
    }

    /// <summary>
    /// 在数据库约束之外提前识别规范化词头冲突。
    /// </summary>
    private async Task EnsureHeadwordUniqueAsync(
        string languageTag,
        string normalizedHeadword,
        Guid? excludedWordId,
        CancellationToken cancellationToken)
    {
        if (await _db.Words.AsNoTracking().AnyAsync(value =>
            value.LanguageTag == languageTag &&
            value.NormalizedHeadword == normalizedHeadword &&
            (!excludedWordId.HasValue || value.Id != excludedWordId.Value),
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.WordDuplicate);
        }
    }

    /// <summary>
    /// 防御性验证完整目标集合的数量、标识、排序和默认发音边界。
    /// </summary>
    private static void ValidateTargetCollections(
        WordUpsertRequest request,
        bool allowExistingIds)
    {
        if (request.Senses is null || request.Pronunciations is null ||
            request.Senses.Count > WordConstraints.MaxSenseCount ||
            request.Pronunciations.Count > WordConstraints.MaxPronunciationCount ||
            request.Senses.Any(value =>
                value is null || value.Examples is null ||
                value.Examples.Count > WordConstraints.MaxExampleCount) ||
            request.Pronunciations.Any(value => value is null))
        {
            throw new RequestValidationException(ErrorCodes.WordChildCollectionInvalid);
        }

        var senseIds = request.Senses.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToArray();
        var exampleIds = request.Senses.SelectMany(value => value.Examples)
            .Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToArray();
        var pronunciationIds = request.Pronunciations.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToArray();
        if (senseIds.Any(value => value == Guid.Empty) ||
            exampleIds.Any(value => value == Guid.Empty) ||
            pronunciationIds.Any(value => value == Guid.Empty) ||
            senseIds.Distinct().Count() != senseIds.Length ||
            exampleIds.Distinct().Count() != exampleIds.Length ||
            pronunciationIds.Distinct().Count() != pronunciationIds.Length)
        {
            throw new RequestValidationException(ErrorCodes.WordChildIdConflict);
        }
        if (!allowExistingIds &&
            (senseIds.Length > 0 || exampleIds.Length > 0 || pronunciationIds.Length > 0))
        {
            throw new RequestValidationException(ErrorCodes.WordChildIdInvalid);
        }
        if (allowExistingIds && request.Senses.Any(value =>
            value.Id is null && value.Examples.Any(example => example.Id.HasValue)))
        {
            throw new RequestValidationException(ErrorCodes.WordChildIdInvalid);
        }

        if (request.Senses.Any(value => !Enum.IsDefined(value.PartOfSpeech)))
        {
            throw new RequestValidationException(ErrorCodes.WordPartOfSpeechInvalid);
        }
        if (request.Senses.Any(value =>
                !IsValidSortOrder(value.SortOrder) ||
                value.Examples.Any(example => !IsValidSortOrder(example.SortOrder))) ||
            request.Pronunciations.Any(pronunciation =>
                !IsValidSortOrder(pronunciation.SortOrder)))
        {
            throw new RequestValidationException(ErrorCodes.WordSortOrderInvalid);
        }
        if (request.Senses.Any(value =>
                value.Examples.Select(example => example.SortOrder).Distinct().Count() !=
                    value.Examples.Count) ||
            request.Senses.Select(value => value.SortOrder).Distinct().Count() !=
                request.Senses.Count ||
            request.Pronunciations.Select(value => value.SortOrder).Distinct().Count() !=
                request.Pronunciations.Count)
        {
            throw new RequestValidationException(ErrorCodes.WordSortOrderConflict);
        }
        if (request.Pronunciations.Any(value => value.AudioClipId == Guid.Empty))
        {
            throw new RequestValidationException(ErrorCodes.WordPronunciationAudioInvalid);
        }
        if (request.Pronunciations.Select(value => value.AudioClipId).Distinct().Count() !=
            request.Pronunciations.Count)
        {
            throw new RequestValidationException(ErrorCodes.WordPronunciationAudioDuplicate);
        }
        if (request.Pronunciations.Count(value => value.IsDefault) > 1)
        {
            throw new RequestValidationException(ErrorCodes.WordDefaultPronunciationConflict);
        }
    }

    /// <summary>
    /// 确保请求中的已有子项属于当前词条和对应父释义。
    /// </summary>
    private static void ValidateChildOwnership(Word word, UpdateWordRequest request)
    {
        var senses = word.Senses.ToDictionary(value => value.Id);
        foreach (var input in request.Senses)
        {
            if (input.Id is not { } senseId)
            {
                continue;
            }
            if (!senses.TryGetValue(senseId, out var sense))
            {
                throw new RequestValidationException(ErrorCodes.WordChildIdConflict);
            }

            var exampleIds = sense.Examples.Select(value => value.Id).ToHashSet();
            if (input.Examples.Any(value =>
                value.Id is { } exampleId && !exampleIds.Contains(exampleId)))
            {
                throw new RequestValidationException(ErrorCodes.WordChildIdConflict);
            }
        }

        var pronunciationIds = word.Pronunciations.Select(value => value.Id).ToHashSet();
        if (request.Pronunciations.Any(value =>
            value.Id is { } pronunciationId && !pronunciationIds.Contains(pronunciationId)))
        {
            throw new RequestValidationException(ErrorCodes.WordChildIdConflict);
        }
    }

    /// <summary>
    /// 批量加载并校验请求目标集合中的所有音频关联。
    /// </summary>
    private async Task ValidateRequestedAudioAsync(
        string wordLanguageTag,
        IReadOnlyCollection<WordSenseInput> senses,
        IReadOnlyCollection<WordPronunciationInput> pronunciations,
        CancellationToken cancellationToken)
    {
        var audioIds = pronunciations.Select(value => value.AudioClipId)
            .Concat(senses.SelectMany(value => value.Examples)
                .Where(value => value.AudioClipId.HasValue)
                .Select(value => value.AudioClipId.GetValueOrDefault()))
            .Distinct()
            .ToArray();
        var audioById = await LoadAudioAsync(audioIds, cancellationToken);

        foreach (var pronunciation in pronunciations)
        {
            ValidateAudio(
                audioById[pronunciation.AudioClipId],
                AudioClipKind.WordPronunciation,
                wordLanguageTag);
        }
        foreach (var example in senses.SelectMany(value => value.Examples)
                     .Where(value => value.AudioClipId.HasValue))
        {
            ValidateAudio(
                audioById[example.AudioClipId!.Value],
                AudioClipKind.ExampleSentence,
                example.LanguageTag);
        }
    }

    /// <summary>
    /// 批量校验待发布聚合中已经持久化的所有音频关联。
    /// </summary>
    private async Task ValidateStoredAudioAsync(
        Word word,
        CancellationToken cancellationToken)
    {
        var audioIds = word.Pronunciations.Select(value => value.AudioClipId)
            .Concat(word.Senses.SelectMany(value => value.Examples)
                .Where(value => value.AudioClipId.HasValue)
                .Select(value => value.AudioClipId.GetValueOrDefault()))
            .Distinct()
            .ToArray();
        var audioById = await LoadAudioAsync(audioIds, cancellationToken);
        foreach (var pronunciation in word.Pronunciations)
        {
            ValidateAudio(
                audioById[pronunciation.AudioClipId],
                AudioClipKind.WordPronunciation,
                word.LanguageTag);
        }
        foreach (var example in word.Senses.SelectMany(value => value.Examples)
                     .Where(value => value.AudioClipId.HasValue))
        {
            ValidateAudio(
                audioById[example.AudioClipId!.Value],
                AudioClipKind.ExampleSentence,
                example.LanguageTag);
        }
    }

    /// <summary>
    /// 一次性加载请求引用的全部音频，并统一处理缺失标识。
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, AudioClip>> LoadAudioAsync(
        IReadOnlyCollection<Guid> audioIds,
        CancellationToken cancellationToken)
    {
        if (audioIds.Count == 0)
        {
            return new Dictionary<Guid, AudioClip>();
        }

        var audio = await _db.AudioClips.AsNoTracking()
            .Where(value => audioIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        if (audio.Count != audioIds.Count)
        {
            throw NotFoundException.Create(ErrorCodes.WordAudioNotFound);
        }
        return audio;
    }

    /// <summary>
    /// 校验单个音频的状态、用途和语言兼容性。
    /// </summary>
    private static void ValidateAudio(
        AudioClip audioClip,
        AudioClipKind expectedKind,
        string expectedLanguageTag)
    {
        if (audioClip.ProcessingStatus != AudioProcessingStatus.Ready ||
            audioClip.PublicationStatus != AudioPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordAudioUnavailable);
        }
        if (audioClip.Kind != expectedKind)
        {
            throw ConflictException.Create(ErrorCodes.WordAudioKindMismatch);
        }
        if (!WordTextNormalizer.AreLanguageTagsCompatible(
            audioClip.LanguageTag,
            expectedLanguageTag))
        {
            throw ConflictException.Create(ErrorCodes.WordAudioLanguageMismatch);
        }
    }

    /// <summary>
    /// 将创建请求的全部子项作为新实体加入词条。
    /// </summary>
    private static void ApplyNewTarget(Word word, WordUpsertRequest request)
    {
        foreach (var senseInput in request.Senses)
        {
            word.Senses.Add(CreateSense(word, senseInput));
        }
        foreach (var pronunciationInput in request.Pronunciations)
        {
            word.Pronunciations.Add(CreatePronunciation(word, pronunciationInput));
        }
    }

    /// <summary>
    /// 删除目标集合遗漏的子项，并将保留项移到无冲突的临时排序区。
    /// </summary>
    private void StageExistingChildren(Word word, UpdateWordRequest request)
    {
        var desiredSenseIds = request.Senses.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
        var senseIndex = 0;
        foreach (var sense in word.Senses.ToArray())
        {
            if (!desiredSenseIds.Contains(sense.Id))
            {
                _db.WordSenses.Remove(sense);
                word.Senses.Remove(sense);
                continue;
            }

            sense.SortOrder = int.MinValue + senseIndex++;
            var senseInput = request.Senses.Single(value => value.Id == sense.Id);
            var desiredExampleIds = senseInput.Examples.Where(value => value.Id.HasValue)
                .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
            var exampleIndex = 0;
            foreach (var example in sense.Examples.ToArray())
            {
                if (!desiredExampleIds.Contains(example.Id))
                {
                    _db.ExampleSentences.Remove(example);
                    sense.Examples.Remove(example);
                    continue;
                }
                example.SortOrder = int.MinValue + exampleIndex++;
            }
        }

        var desiredPronunciationIds = request.Pronunciations.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToHashSet();
        var pronunciationIndex = 0;
        foreach (var pronunciation in word.Pronunciations.ToArray())
        {
            if (!desiredPronunciationIds.Contains(pronunciation.Id))
            {
                _db.WordPronunciations.Remove(pronunciation);
                word.Pronunciations.Remove(pronunciation);
                continue;
            }
            pronunciation.SortOrder = int.MinValue + pronunciationIndex++;
            pronunciation.IsDefault = false;
        }
    }

    /// <summary>
    /// 将保留项更新到最终值，并加入所有无标识的新子项。
    /// </summary>
    private void ApplyFinalTarget(Word word, UpdateWordRequest request)
    {
        foreach (var senseInput in request.Senses)
        {
            WordSense sense;
            if (senseInput.Id is { } senseId)
            {
                sense = word.Senses.Single(value => value.Id == senseId);
                ApplySenseValues(sense, senseInput);
            }
            else
            {
                sense = CreateSense(word, senseInput);
                word.Senses.Add(sense);
                _db.WordSenses.Add(sense);
            }

            foreach (var exampleInput in senseInput.Examples)
            {
                if (exampleInput.Id is { } exampleId)
                {
                    ApplyExampleValues(
                        sense.Examples.Single(value => value.Id == exampleId),
                        exampleInput);
                }
                else if (senseInput.Id is not null)
                {
                    var example = CreateExample(sense, exampleInput);
                    sense.Examples.Add(example);
                    _db.ExampleSentences.Add(example);
                }
            }
        }

        foreach (var pronunciationInput in request.Pronunciations)
        {
            if (pronunciationInput.Id is { } pronunciationId)
            {
                ApplyPronunciationValues(
                    word.Pronunciations.Single(value => value.Id == pronunciationId),
                    pronunciationInput);
            }
            else
            {
                var pronunciation = CreatePronunciation(word, pronunciationInput);
                word.Pronunciations.Add(pronunciation);
                _db.WordPronunciations.Add(pronunciation);
            }
        }
    }

    /// <summary>
    /// 创建一个释义及其全部新例句。
    /// </summary>
    private static WordSense CreateSense(Word word, WordSenseInput input)
    {
        var sense = new WordSense
        {
            WordId = word.Id,
            Word = word,
            Definition = string.Empty,
            DefinitionLanguageTag = string.Empty
        };
        ApplySenseValues(sense, input);
        foreach (var exampleInput in input.Examples)
        {
            sense.Examples.Add(CreateExample(sense, exampleInput));
        }
        return sense;
    }

    /// <summary>
    /// 将请求中的释义字段应用到 tracked 实体。
    /// </summary>
    private static void ApplySenseValues(WordSense sense, WordSenseInput input)
    {
        sense.PartOfSpeech = input.PartOfSpeech;
        sense.Definition = WordTextNormalizer.NormalizeRequiredText(input.Definition);
        sense.DefinitionLanguageTag = WordTextNormalizer.NormalizeLanguageTag(
            input.DefinitionLanguageTag);
        sense.UsageNote = WordTextNormalizer.NormalizeOptionalText(input.UsageNote);
        sense.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 创建一个隶属于指定释义的新例句。
    /// </summary>
    private static ExampleSentence CreateExample(
        WordSense sense,
        ExampleSentenceInput input)
    {
        var example = new ExampleSentence
        {
            WordSenseId = sense.Id,
            WordSense = sense,
            Sentence = string.Empty,
            LanguageTag = string.Empty,
            Translation = string.Empty,
            TranslationLanguageTag = string.Empty
        };
        ApplyExampleValues(example, input);
        return example;
    }

    /// <summary>
    /// 将请求中的例句字段应用到 tracked 实体。
    /// </summary>
    private static void ApplyExampleValues(
        ExampleSentence example,
        ExampleSentenceInput input)
    {
        example.Sentence = WordTextNormalizer.NormalizeRequiredText(input.Sentence);
        example.LanguageTag = WordTextNormalizer.NormalizeLanguageTag(input.LanguageTag);
        example.Translation = WordTextNormalizer.NormalizeRequiredText(input.Translation);
        example.TranslationLanguageTag = WordTextNormalizer.NormalizeLanguageTag(
            input.TranslationLanguageTag);
        example.AudioClipId = input.AudioClipId;
        example.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 创建一个隶属于指定词条的新发音关联。
    /// </summary>
    private static WordPronunciation CreatePronunciation(
        Word word,
        WordPronunciationInput input)
    {
        var pronunciation = new WordPronunciation
        {
            WordId = word.Id,
            Word = word,
            AudioClipId = input.AudioClipId
        };
        ApplyPronunciationValues(pronunciation, input);
        return pronunciation;
    }

    /// <summary>
    /// 将请求中的发音字段应用到 tracked 实体。
    /// </summary>
    private static void ApplyPronunciationValues(
        WordPronunciation pronunciation,
        WordPronunciationInput input)
    {
        pronunciation.AudioClipId = input.AudioClipId;
        pronunciation.AccentTag = WordTextNormalizer.NormalizeOptionalText(input.AccentTag);
        pronunciation.Ipa = WordTextNormalizer.NormalizeOptionalText(input.Ipa);
        pronunciation.IsDefault = input.IsDefault;
        pronunciation.SortOrder = input.SortOrder;
    }

    /// <summary>
    /// 验证词条聚合满足从草稿进入 Published 的全部内容要求。
    /// </summary>
    private static void EnsurePublishableContent(Word word)
    {
        if (string.IsNullOrWhiteSpace(word.Headword) ||
            string.IsNullOrWhiteSpace(word.LanguageTag) ||
            word.Senses.Count == 0 ||
            word.Senses.Any(value =>
                !Enum.IsDefined(value.PartOfSpeech) ||
                string.IsNullOrWhiteSpace(value.Definition) ||
                string.IsNullOrWhiteSpace(value.DefinitionLanguageTag) ||
                value.Examples.Count == 0 ||
                value.Examples.Any(example =>
                    string.IsNullOrWhiteSpace(example.Sentence) ||
                    string.IsNullOrWhiteSpace(example.LanguageTag) ||
                    string.IsNullOrWhiteSpace(example.Translation) ||
                    string.IsNullOrWhiteSpace(example.TranslationLanguageTag))) ||
            word.Pronunciations.Count == 0 ||
            word.Pronunciations.Count(value => value.IsDefault) != 1)
        {
            throw ConflictException.Create(ErrorCodes.WordPublishRequirementsNotMet);
        }
    }

    /// <summary>
    /// 创建可由 EF Core 翻译的管理员详情 projection。
    /// </summary>
    private static Expression<Func<Word, AdminWordResponse>> ToAdminResponseProjection()
        => word => new AdminWordResponse(
            word.Id,
            word.LanguageTag,
            word.Headword,
            word.Status,
            new ContentAuditUserResponse(word.CreatedById, null, null),
            new ContentAuditUserResponse(word.LastEditorId, null, null),
            word.PublishedAt,
            word.ArchivedAt,
            word.ConcurrencyStamp,
            word.Senses.OrderBy(sense => sense.SortOrder)
                .ThenBy(sense => sense.Id)
                .Select(sense => new AdminWordSenseResponse(
                    sense.Id,
                    sense.PartOfSpeech,
                    sense.Definition,
                    sense.DefinitionLanguageTag,
                    sense.UsageNote,
                    sense.SortOrder,
                    sense.Examples.OrderBy(example => example.SortOrder)
                        .ThenBy(example => example.Id)
                        .Select(example => new AdminExampleSentenceResponse(
                            example.Id,
                            example.Sentence,
                            example.LanguageTag,
                            example.Translation,
                            example.TranslationLanguageTag,
                            example.AudioClipId,
                            example.SortOrder))
                        .ToList()))
                .ToList(),
            word.Pronunciations.OrderBy(pronunciation => pronunciation.SortOrder)
                .ThenBy(pronunciation => pronunciation.Id)
                .Select(pronunciation => new AdminWordPronunciationResponse(
                    pronunciation.Id,
                    pronunciation.AudioClipId,
                    pronunciation.AccentTag,
                    pronunciation.Ipa,
                    pronunciation.IsDefault,
                    pronunciation.SortOrder))
                .ToList(),
            word.CreatedAt,
            word.UpdatedAt);

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
    private static AdminWordResponse EnrichAuditUsers(
        AdminWordResponse response,
        IReadOnlyDictionary<Guid, ContentAuditUserResponse> users)
        => response with
        {
            CreatedBy = GetAuditUser(response.CreatedBy.Id, users),
            LastEditor = GetAuditUser(response.LastEditor.Id, users)
        };

    /// <summary>
    /// 返回审计用户摘要；历史测试或异常孤立数据仅保留稳定标识。
    /// </summary>
    private static ContentAuditUserResponse GetAuditUser(
        Guid userId,
        IReadOnlyDictionary<Guid, ContentAuditUserResponse> users)
        => users.TryGetValue(userId, out var user)
            ? user
            : new ContentAuditUserResponse(userId, null, null);

    /// <summary>
    /// 在任何幂等或状态判断之前验证客户端看到的并发版本。
    /// </summary>
    private static void EnsureExpectedStamp(Word word, Guid expectedStamp)
    {
        if (word.ConcurrencyStamp != expectedStamp)
        {
            throw ConflictException.Create(ErrorCodes.WordConcurrencyConflict);
        }
    }

    /// <summary>
    /// 创建可由 EF Core 翻译的用户详情 projection。
    /// </summary>
    private static Expression<Func<Word, WordResponse>> ToUserResponseProjection()
        => word => new WordResponse(
            word.Id,
            word.LanguageTag,
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
            word.Pronunciations.OrderBy(pronunciation => pronunciation.SortOrder)
                .ThenBy(pronunciation => pronunciation.Id)
                .Select(pronunciation => new WordPronunciationResponse(
                    pronunciation.AudioClipId,
                    pronunciation.AccentTag,
                    pronunciation.Ipa,
                    pronunciation.IsDefault,
                    pronunciation.SortOrder))
                .ToList(),
            word.PublishedAt!.Value);

    /// <summary>
    /// 保存词条变更并映射预期唯一约束和乐观并发冲突。
    /// </summary>
    private async Task SaveWordChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(
                "Word concurrency conflict affected entity types {EntityTypes}",
                string.Join(",", exception.Entries.Select(value => value.Metadata.Name)));
            throw ConflictException.Create(ErrorCodes.WordConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                HeadwordUniqueIndex))
        {
            throw ConflictException.Create(ErrorCodes.WordDuplicate);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                PronunciationAudioUniqueIndex))
        {
            throw ConflictException.Create(ErrorCodes.WordPronunciationAudioDuplicate);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                DefaultPronunciationUniqueIndex))
        {
            throw ConflictException.Create(ErrorCodes.WordDefaultPronunciationConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                SortOrderUniqueIndexes))
        {
            throw ConflictException.Create(ErrorCodes.WordSortOrderConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                StudyHistoryForeignKeys))
        {
            throw ConflictException.Create(ErrorCodes.WordHasStudyHistory);
        }
    }

    /// <summary>
    /// 将不含标识的批量行转换为单条创建规则可校验的请求。
    /// </summary>
    private static CreateWordRequest ToCreateRequest(BatchWordRowRequest row)
        => new()
        {
            LanguageTag = row.LanguageTag,
            Headword = row.Headword,
            Senses = row.Senses.Select(sense => new WordSenseInput
            {
                PartOfSpeech = sense.PartOfSpeech,
                Definition = sense.Definition,
                DefinitionLanguageTag = sense.DefinitionLanguageTag,
                UsageNote = sense.UsageNote,
                SortOrder = sense.SortOrder,
                Examples = sense.Examples.Select(example => new ExampleSentenceInput
                {
                    Sentence = example.Sentence,
                    LanguageTag = example.LanguageTag,
                    Translation = example.Translation,
                    TranslationLanguageTag = example.TranslationLanguageTag,
                    AudioClipId = example.AudioClipId,
                    SortOrder = example.SortOrder
                }).ToArray()
            }).ToArray(),
            Pronunciations = row.Pronunciations.Select(pronunciation =>
                new WordPronunciationInput
                {
                    AudioClipId = pronunciation.AudioClipId,
                    AccentTag = pronunciation.AccentTag,
                    Ipa = pronunciation.Ipa,
                    IsDefault = pronunciation.IsDefault,
                    SortOrder = pronunciation.SortOrder
                }).ToArray()
        };

    /// <summary>
    /// 生成与最终持久化完全一致的批量规范化预览。
    /// </summary>
    private static CreateWordRequest NormalizeCreateRequest(
        CreateWordRequest request,
        WordIdentity identity)
        => new()
        {
            LanguageTag = identity.LanguageTag,
            Headword = identity.Headword,
            Senses = request.Senses.Select(sense => sense with
            {
                Definition = WordTextNormalizer.NormalizeRequiredText(sense.Definition),
                DefinitionLanguageTag = WordTextNormalizer.NormalizeLanguageTag(
                    sense.DefinitionLanguageTag),
                UsageNote = WordTextNormalizer.NormalizeOptionalText(sense.UsageNote),
                Examples = sense.Examples.Select(example => example with
                {
                    Sentence = WordTextNormalizer.NormalizeRequiredText(example.Sentence),
                    LanguageTag = WordTextNormalizer.NormalizeLanguageTag(
                        example.LanguageTag),
                    Translation = WordTextNormalizer.NormalizeRequiredText(
                        example.Translation),
                    TranslationLanguageTag = WordTextNormalizer.NormalizeLanguageTag(
                        example.TranslationLanguageTag)
                }).ToArray()
            }).ToArray(),
            Pronunciations = request.Pronunciations.Select(pronunciation =>
                pronunciation with
                {
                    AccentTag = WordTextNormalizer.NormalizeOptionalText(
                        pronunciation.AccentTag),
                    Ipa = WordTextNormalizer.NormalizeOptionalText(pronunciation.Ipa)
                }).ToArray()
        };

    /// <summary>
    /// 统计批量原始请求中所有会持久化的文本字符数。
    /// </summary>
    private static int CountBatchCharacters(IEnumerable<BatchWordRowRequest> rows)
        => rows.Sum(row =>
            TextLength(row.LanguageTag) +
            TextLength(row.Headword) +
            (row.Senses?.Sum(sense =>
                TextLength(sense?.Definition) +
                TextLength(sense?.DefinitionLanguageTag) +
                TextLength(sense?.UsageNote) +
                (sense?.Examples?.Sum(example =>
                    TextLength(example?.Sentence) +
                    TextLength(example?.LanguageTag) +
                    TextLength(example?.Translation) +
                    TextLength(example?.TranslationLanguageTag)) ?? 0)) ?? 0) +
            (row.Pronunciations?.Sum(pronunciation =>
                TextLength(pronunciation?.AccentTag) +
                TextLength(pronunciation?.Ipa)) ?? 0));

    /// <summary>
    /// 统计规范化单条请求中所有会持久化的文本字符数。
    /// </summary>
    private static int CountRequestCharacters(CreateWordRequest request)
        => TextLength(request.LanguageTag) +
            TextLength(request.Headword) +
            request.Senses.Sum(sense =>
                TextLength(sense.Definition) +
                TextLength(sense.DefinitionLanguageTag) +
                TextLength(sense.UsageNote) +
                sense.Examples.Sum(example =>
                    TextLength(example.Sentence) +
                    TextLength(example.LanguageTag) +
                    TextLength(example.Translation) +
                    TextLength(example.TranslationLanguageTag))) +
            request.Pronunciations.Sum(pronunciation =>
                TextLength(pronunciation.AccentTag) + TextLength(pronunciation.Ipa));

    /// <summary>
    /// 返回可空字符串的安全字符数。
    /// </summary>
    private static int TextLength(string? value) => value?.Length ?? 0;

    /// <summary>
    /// 将 FluentValidation 属性路径规范化为 JSON camelCase 字段路径。
    /// </summary>
    private static string ToBatchFieldPath(string propertyName)
        => string.Join(
            ".",
            propertyName.Split('.').Select(segment => segment.Length == 0
                ? segment
                : char.ToLowerInvariant(segment[0]) + segment[1..]));

    /// <summary>
    /// 向行错误集合加入一个不重复的字段错误。
    /// </summary>
    private static void AddBatchError(
        ICollection<BatchError> errors,
        string field,
        ErrorCodes errorCode)
    {
        if (!errors.Any(error => error.Field == field && error.ErrorCode == errorCode))
        {
            errors.Add(new BatchError(field, errorCode));
        }
    }

    /// <summary>
    /// 将内部字段错误按字段分组为公开响应。
    /// </summary>
    private static IReadOnlyList<BatchWordFieldErrorResponse> ToBatchErrorResponses(
        IEnumerable<BatchError> errors)
        => errors.GroupBy(error => error.Field, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new BatchWordFieldErrorResponse(
                group.Key,
                group.Select(error => error.ErrorCode).Distinct().ToArray(),
                group.Select(error => error.ErrorCode.GetMessage()).Distinct().ToArray()))
            .ToArray();

    /// <summary>
    /// 创建只有一个稳定错误码的公开批量字段错误。
    /// </summary>
    private static BatchWordFieldErrorResponse CreateBatchError(
        string field,
        ErrorCodes errorCode)
        => new(field, [errorCode], [errorCode.GetMessage()]);

    /// <summary>
    /// 将批量验证响应转换为 Problem Details 的稳定字段错误字典。
    /// </summary>
    private static IDictionary<string, string[]> ToBatchErrorDictionary(
        BatchWordValidationResponse response)
    {
        var errors = response.Errors.Select(error => (error.Field, error.Messages))
            .Concat(response.Rows.SelectMany(row => row.Errors.Select(error =>
                ($"rows[{row.RowIndex}].{error.Field}", error.Messages))));
        return errors.GroupBy(error => error.Item1, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(error => error.Messages).Distinct().ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// 判断排序值是否位于请求允许范围。
    /// </summary>
    private static bool IsValidSortOrder(int value)
        => value is >= 0 and <= WordConstraints.MaxSortOrder;

    /// <summary>
    /// 创建包含总页数的词条分页响应。
    /// </summary>
    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(
            items,
            page,
            pageSize,
            totalCount,
            (totalCount + pageSize - 1) / pageSize);

    /// <summary>
    /// 保存规范化后的词条唯一身份字段。
    /// </summary>
    private readonly record struct WordIdentity(
        string LanguageTag,
        string Headword,
        string NormalizedHeadword);

    /// <summary>
    /// 保存一个已规范化批量行及其规范化唯一身份。
    /// </summary>
    private sealed record BatchNormalizedRow(
        int RowIndex,
        CreateWordRequest Request,
        WordIdentity Identity);

    /// <summary>
    /// 保存批量校验的公开响应和可用于原子写入的规范化行。
    /// </summary>
    private sealed record BatchValidationResult(
        BatchWordValidationResponse Response,
        IReadOnlyList<BatchNormalizedRow> Rows);

    /// <summary>
    /// 表示批量校验内部的一个字段错误。
    /// </summary>
    private sealed record BatchError(string Field, ErrorCodes ErrorCode);
}
