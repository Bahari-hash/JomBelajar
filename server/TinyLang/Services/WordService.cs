using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 实现立即有效的词条聚合写入、共享音频关联和登录用户查询规则。
/// </summary>
public sealed class WordService : IWordService
{
    private const string HeadwordUniqueIndex =
        "IX_words_NormalizedHeadword";
    private const string AudioResourceForeignKey =
        "FK_words_audio_resources_AudioResourceId";
    private const string ExampleAudioResourceForeignKey =
        "FK_example_sentences_audio_resources_AudioResourceId";
    private static readonly string[] SortOrderUniqueIndexes =
    [
        "IX_word_senses_WordId_SortOrder",
        "IX_example_sentences_WordSenseId_SortOrder"
    ];

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly ILogger<WordService> _logger;

    public WordService(
        IApplicationDbContext db,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        ILogger<WordService> logger)
    {
        _db = db;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> CreateAsync(
        Guid adminId,
        CreateWordRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetCollections(request, allowExistingIds: false);
        await EnsureAudioExistsAsync(request.AudioResourceId, cancellationToken);
        await EnsureExampleAudiosExistAsync(request, cancellationToken);
        var word = WordAggregateBuilder.Create(request);
        await EnsureHeadwordUniqueAsync(
            word.NormalizedHeadword,
            excludedWordId: null,
            cancellationToken);
        _db.Words.Add(word);
        await SaveWordChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created word {WordId} by administrator {AdminId}",
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
        ValidateChildOwnership(word, request);
        await EnsureAudioExistsAsync(request.AudioResourceId, cancellationToken);
        await EnsureExampleAudiosExistAsync(request, cancellationToken);
        var identity = WordAggregateBuilder.NormalizeIdentity(request.Headword);
        await EnsureHeadwordUniqueAsync(
            identity.NormalizedHeadword,
            word.Id,
            cancellationToken);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        word.Headword = identity.Headword;
        word.NormalizedHeadword = identity.NormalizedHeadword;
        word.AudioResourceId = request.AudioResourceId;
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
    public async Task DeleteAsync(
        Guid wordId,
        Guid adminId,
        DeleteWordRequest request,
        CancellationToken cancellationToken = default)
    {
        var word = await FindWordForEditAsync(wordId, cancellationToken);
        EnsureExpectedStamp(word, request.ConcurrencyStamp);

        word.IsDeleted = true;
        word.ConcurrencyStamp = Guid.NewGuid();
        await SaveWordChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted word {WordId} by administrator {AdminId}",
            wordId,
            adminId);
    }

    /// <inheritdoc />
    public async Task<AdminWordResponse> GetAdminByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default)
        => await _db.Words.AsNoTracking()
            .Where(value => value.Id == wordId && !value.IsDeleted)
            .Select(ToAdminResponseProjection())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<AdminWordListItemResponse>> GetAdminListAsync(
        AdminWordListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Words.AsNoTracking().Where(value => !value.IsDeleted);
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
                value.Headword,
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => (Entities.Enums.PartOfSpeech?)sense.PartOfSpeech)
                    .FirstOrDefault(),
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.Definition)
                    .FirstOrDefault(),
                value.Senses.Count,
                value.Senses.SelectMany(sense => sense.Examples).Count(),
                value.AudioResourceId != null,
                value.CreatedAt,
                value.UpdatedAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
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
            .Select(value => new WordListItemResponse(
                value.Id,
                value.Headword,
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.PartOfSpeech)
                    .First(),
                value.Senses.OrderBy(sense => sense.SortOrder)
                    .ThenBy(sense => sense.Id)
                    .Select(sense => sense.Definition)
                    .First(),
                value.AudioResourceId,
                value.UpdatedAt))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    private async Task<Word> FindWordForEditAsync(
        Guid wordId,
        CancellationToken cancellationToken)
        => await _db.Words
            .Include(value => value.Senses)
                .ThenInclude(value => value.Examples)
            .SingleOrDefaultAsync(value => value.Id == wordId && !value.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordNotFound);

    private async Task EnsureAudioExistsAsync(
        Guid? audioResourceId,
        CancellationToken cancellationToken)
    {
        if (audioResourceId is null)
        {
            return;
        }
        if (audioResourceId == Guid.Empty ||
            !await _db.AudioResources.AsNoTracking().AnyAsync(
                value => value.Id == audioResourceId.Value,
                cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.WordAudioInvalid);
        }
    }

    private async Task EnsureExampleAudiosExistAsync(
        WordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var audioResourceIds = request.Senses
            .SelectMany(value => value.Examples)
            .Where(value => value.AudioResourceId.HasValue)
            .Select(value => value.AudioResourceId.GetValueOrDefault())
            .Distinct()
            .ToArray();
        if (audioResourceIds.Length == 0)
        {
            return;
        }
        if (audioResourceIds.Contains(Guid.Empty))
        {
            throw NotFoundException.Create(ErrorCodes.WordExampleAudioInvalid);
        }

        var existingAudioResourceIds = await _db.AudioResources.AsNoTracking()
            .Where(value => audioResourceIds.Contains(value.Id))
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        if (existingAudioResourceIds.Length != audioResourceIds.Length)
        {
            throw NotFoundException.Create(ErrorCodes.WordExampleAudioInvalid);
        }
    }

    private async Task EnsureHeadwordUniqueAsync(
        string normalizedHeadword,
        Guid? excludedWordId,
        CancellationToken cancellationToken)
    {
        if (await _db.Words.AsNoTracking().AnyAsync(value =>
            !value.IsDeleted &&
            value.NormalizedHeadword == normalizedHeadword &&
            (!excludedWordId.HasValue || value.Id != excludedWordId.Value),
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.WordDuplicate);
        }
    }

    private static void ValidateTargetCollections(
        WordUpsertRequest request,
        bool allowExistingIds)
    {
        if (request.Senses is null || request.Senses.Count == 0 ||
            request.Senses.Count > WordConstraints.MaxSenseCount ||
            request.Senses.Any(value =>
                value is null || value.Examples is null ||
                value.Examples.Count > WordConstraints.MaxExampleCount))
        {
            throw new RequestValidationException(
                request.Senses?.Count == 0
                    ? ErrorCodes.WordSenseRequired
                    : ErrorCodes.WordChildCollectionInvalid);
        }

        var senseIds = request.Senses.Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToArray();
        var exampleIds = request.Senses.SelectMany(value => value.Examples)
            .Where(value => value.Id.HasValue)
            .Select(value => value.Id.GetValueOrDefault()).ToArray();
        if (senseIds.Any(value => value == Guid.Empty) ||
            exampleIds.Any(value => value == Guid.Empty) ||
            senseIds.Distinct().Count() != senseIds.Length ||
            exampleIds.Distinct().Count() != exampleIds.Length)
        {
            throw new RequestValidationException(ErrorCodes.WordChildIdConflict);
        }
        if (!allowExistingIds && (senseIds.Length > 0 || exampleIds.Length > 0))
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
            value.Examples.Any(example => !IsValidSortOrder(example.SortOrder))))
        {
            throw new RequestValidationException(ErrorCodes.WordSortOrderInvalid);
        }
        if (request.Senses.Any(value =>
                value.Examples.Select(example => example.SortOrder).Distinct().Count() !=
                    value.Examples.Count) ||
            request.Senses.Select(value => value.SortOrder).Distinct().Count() !=
                request.Senses.Count)
        {
            throw new RequestValidationException(ErrorCodes.WordSortOrderConflict);
        }
    }

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
    }

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
    }

    private void ApplyFinalTarget(Word word, UpdateWordRequest request)
    {
        foreach (var senseInput in request.Senses)
        {
            WordSense sense;
            if (senseInput.Id is { } senseId)
            {
                sense = word.Senses.Single(value => value.Id == senseId);
                WordAggregateBuilder.ApplySenseValues(sense, senseInput);
            }
            else
            {
                sense = WordAggregateBuilder.CreateSense(word, senseInput);
                word.Senses.Add(sense);
                _db.WordSenses.Add(sense);
            }

            foreach (var exampleInput in senseInput.Examples)
            {
                if (exampleInput.Id is { } exampleId)
                {
                    WordAggregateBuilder.ApplyExampleValues(
                        sense.Examples.Single(value => value.Id == exampleId),
                        exampleInput);
                }
                else if (senseInput.Id is not null)
                {
                    var example = WordAggregateBuilder.CreateExample(
                        sense,
                        exampleInput);
                    sense.Examples.Add(example);
                    _db.ExampleSentences.Add(example);
                }
            }
        }
    }

    private static Expression<Func<Word, AdminWordResponse>>
        ToAdminResponseProjection()
        => word => new AdminWordResponse(
            word.Id,
            word.Headword,
            word.AudioResource == null
                ? null
                : new AdminWordAudioResponse(
                    word.AudioResource.Id,
                    word.AudioResource.Name,
                    word.AudioResource.Status,
                    word.AudioResource.DurationSeconds,
                    word.AudioResource.LastFailureCode),
            word.ConcurrencyStamp,
            word.Senses.OrderBy(sense => sense.SortOrder)
                .ThenBy(sense => sense.Id)
                .Select(sense => new AdminWordSenseResponse(
                    sense.Id,
                    sense.PartOfSpeech,
                    sense.Definition,
                    sense.UsageNote,
                    sense.SortOrder,
                    sense.Examples.OrderBy(example => example.SortOrder)
                        .ThenBy(example => example.Id)
                        .Select(example => new AdminExampleSentenceResponse(
                            example.Id,
                            example.Sentence,
                            example.Translation,
                            example.AudioResource == null
                                ? null
                                : new AdminExampleSentenceAudioResponse(
                                    example.AudioResource.Id,
                                    example.AudioResource.Name,
                                    example.AudioResource.Status,
                                    example.AudioResource.DurationSeconds,
                                    example.AudioResource.LastFailureCode),
                            example.SortOrder))
                        .ToList()))
                .ToList(),
            word.CreatedAt,
            word.UpdatedAt);

    private static void EnsureExpectedStamp(Word word, Guid expectedStamp)
    {
        if (word.ConcurrencyStamp != expectedStamp)
        {
            throw ConflictException.Create(ErrorCodes.WordConcurrencyConflict);
        }
    }

    private static Expression<Func<Word, WordResponse>> ToUserResponseProjection()
        => word => new WordResponse(
            word.Id,
            word.Headword,
            word.Senses.OrderBy(sense => sense.SortOrder)
                .ThenBy(sense => sense.Id)
                .Select(sense => new WordSenseResponse(
                    sense.PartOfSpeech,
                    sense.Definition,
                    sense.UsageNote,
                    sense.SortOrder,
                    sense.Examples.OrderBy(example => example.SortOrder)
                        .ThenBy(example => example.Id)
                        .Select(example => new ExampleSentenceResponse(
                            example.Sentence,
                            example.Translation,
                            example.AudioResourceId,
                            example.SortOrder))
                        .ToList()))
                .ToList(),
            word.AudioResourceId,
            word.UpdatedAt);

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
                SortOrderUniqueIndexes))
        {
            throw ConflictException.Create(ErrorCodes.WordSortOrderConflict);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                AudioResourceForeignKey))
        {
            throw NotFoundException.Create(ErrorCodes.WordAudioInvalid);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                ExampleAudioResourceForeignKey))
        {
            throw NotFoundException.Create(ErrorCodes.WordExampleAudioInvalid);
        }
    }

    private static bool IsValidSortOrder(int value)
        => value is >= 0 and <= WordConstraints.MaxSortOrder;

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
}
