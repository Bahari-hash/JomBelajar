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
    public async Task<EditorWordResponse> CreateDraftAsync(
        Guid editorId,
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
            CreatedById = editorId,
            LastEditorId = editorId
        };
        ApplyNewTarget(word, request);
        _db.Words.Add(word);
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created word draft {WordId} by editor {EditorId}",
            word.Id,
            editorId);
        return await GetEditorByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorWordResponse> UpdateAsync(
        Guid wordId,
        Guid editorId,
        UpdateWordRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetCollections(request, allowExistingIds: true);
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        if (word.Status == WordPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }
        if (word.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw ConflictException.Create(ErrorCodes.WordConcurrencyConflict);
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
        word.LastEditorId = editorId;
        word.ConcurrencyStamp = Guid.NewGuid();
        StageExistingChildren(word, request);
        await SaveWordChangesAsync(cancellationToken);

        ApplyFinalTarget(word, request);
        await SaveWordChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Updated word {WordId} by editor {EditorId}",
            word.Id,
            editorId);
        return await GetEditorByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorWordResponse> PublishAsync(
        Guid wordId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        if (word.Status == WordPublicationStatus.Published)
        {
            return await GetEditorByIdAsync(wordId, cancellationToken);
        }
        if (word.Status is not (WordPublicationStatus.Draft or WordPublicationStatus.Unpublished))
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }

        EnsurePublishableContent(word);
        await ValidateStoredAudioAsync(word, cancellationToken);
        word.Status = WordPublicationStatus.Published;
        word.PublishedAt ??= _timeProvider.GetUtcNow();
        word.LastEditorId = editorId;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published word {WordId} by editor {EditorId}",
            word.Id,
            editorId);
        return await GetEditorByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorWordResponse> UnpublishAsync(
        Guid wordId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        if (word.Status == WordPublicationStatus.Unpublished)
        {
            return await GetEditorByIdAsync(wordId, cancellationToken);
        }
        if (word.Status != WordPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordStatusConflict);
        }

        word.Status = WordPublicationStatus.Unpublished;
        word.LastEditorId = editorId;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Unpublished word {WordId} by editor {EditorId}",
            word.Id,
            editorId);
        return await GetEditorByIdAsync(word.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid wordId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        if (word.Status == WordPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.WordPublishedDeleteConflict);
        }

        _db.Words.Remove(word);
        await SaveWordChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted word {WordId} by editor {EditorId}",
            wordId,
            editorId);
    }

    /// <inheritdoc />
    public async Task<EditorWordResponse> GetEditorByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default)
        => await _db.Words.AsNoTracking()
            .Where(value => value.Id == wordId)
            .Select(ToEditorResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<EditorWordListItemResponse>> GetEditorListAsync(
        EditorWordListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Words.AsNoTracking();
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
            var keyword = WordTextNormalizer.CreateHeadwordComparisonKey(request.Keyword);
            query = query.Where(value => value.NormalizedHeadword.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new EditorWordListItemResponse(
                value.Id,
                value.LanguageTag,
                value.Headword,
                value.Status,
                value.Senses.Count,
                value.Pronunciations.Count,
                value.PublishedAt,
                value.UpdatedAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<WordResponse> GetUserByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default)
        => await ApplyUserVisibility(_db.Words.AsNoTracking())
            .Where(value => value.Id == wordId)
            .Select(ToUserResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<WordListItemResponse>> GetUserListAsync(
        WordListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyUserVisibility(_db.Words.AsNoTracking());
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
    /// 应用普通用户词条状态和全部关联音频实时可用性条件。
    /// </summary>
    private static IQueryable<Word> ApplyUserVisibility(IQueryable<Word> query)
        => query.Where(word =>
            word.Status == WordPublicationStatus.Published &&
            word.PublishedAt != null &&
            word.Senses.Any() &&
            word.Pronunciations.Any() &&
            word.Pronunciations.Count(value => value.IsDefault) == 1 &&
            word.Pronunciations.All(pronunciation =>
                pronunciation.AudioClip != null &&
                pronunciation.AudioClip.ProcessingStatus == AudioProcessingStatus.Ready &&
                pronunciation.AudioClip.PublicationStatus == AudioPublicationStatus.Published &&
                pronunciation.AudioClip.Kind == AudioClipKind.WordPronunciation &&
                (pronunciation.AudioClip.LanguageTag == word.LanguageTag ||
                 pronunciation.AudioClip.LanguageTag.StartsWith(word.LanguageTag + "-") ||
                 word.LanguageTag.StartsWith(pronunciation.AudioClip.LanguageTag + "-"))) &&
            word.Senses.SelectMany(sense => sense.Examples).All(example =>
                example.AudioClipId == null ||
                (example.AudioClip != null &&
                 example.AudioClip.ProcessingStatus == AudioProcessingStatus.Ready &&
                 example.AudioClip.PublicationStatus == AudioPublicationStatus.Published &&
                 example.AudioClip.Kind == AudioClipKind.ExampleSentence &&
                 (example.AudioClip.LanguageTag == example.LanguageTag ||
                  example.AudioClip.LanguageTag.StartsWith(example.LanguageTag + "-") ||
                  example.LanguageTag.StartsWith(example.AudioClip.LanguageTag + "-")))));

    /// <summary>
    /// 创建可由 EF Core 翻译的编辑者详情 projection。
    /// </summary>
    private static Expression<Func<Word, EditorWordResponse>> ToEditorResponseProjection()
        => word => new EditorWordResponse(
            word.Id,
            word.LanguageTag,
            word.Headword,
            word.Status,
            word.CreatedById,
            word.LastEditorId,
            word.PublishedAt,
            word.ConcurrencyStamp,
            word.Senses.OrderBy(sense => sense.SortOrder)
                .ThenBy(sense => sense.Id)
                .Select(sense => new EditorWordSenseResponse(
                    sense.Id,
                    sense.PartOfSpeech,
                    sense.Definition,
                    sense.DefinitionLanguageTag,
                    sense.UsageNote,
                    sense.SortOrder,
                    sense.Examples.OrderBy(example => example.SortOrder)
                        .ThenBy(example => example.Id)
                        .Select(example => new EditorExampleSentenceResponse(
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
                .Select(pronunciation => new EditorWordPronunciationResponse(
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
}
