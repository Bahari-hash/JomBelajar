using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 校验批量词条结构、业务字段、词头唯一性和共享音频引用。
/// </summary>
public sealed class WordBatchService : IWordBatchService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateWordRequest> _wordValidator;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly ILogger<WordBatchService> _logger;

    public WordBatchService(
        IApplicationDbContext db,
        IValidator<CreateWordRequest> wordValidator,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        ILogger<WordBatchService> logger)
    {
        _db = db;
        _wordValidator = wordValidator;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BatchWordValidationResponse> ValidateAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken = default)
        => (await BuildValidationAsync(request, cancellationToken)).Response;

    internal async Task<WordBatchValidationBuildResult> BuildValidationAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken)
    {
        var words = request?.Words;
        if (words is null || words.Count == 0)
        {
            return CreateLimitResult(
                new BatchWordSummaryResponse(0, 0, 0, 0, 0, 0),
                ErrorCodes.WordBatchRequired);
        }

        var wordCount = AddBounded(0, words.Count, int.MaxValue);
        var senseCount = CountSenses(words);
        var exampleCount = CountExamples(words);
        var textCharacterCount = CountTextCharacters(words);
        var structuralSummary = new BatchWordSummaryResponse(
            ToResponseCount(wordCount),
            ToResponseCount(senseCount),
            ToResponseCount(exampleCount),
            0,
            0,
            0);
        if (wordCount > WordConstraints.MaxBatchWordCount)
        {
            return CreateLimitResult(
                structuralSummary,
                ErrorCodes.WordBatchCountLimit);
        }

        if (senseCount > WordConstraints.MaxBatchSenseCount)
        {
            return CreateLimitResult(
                structuralSummary,
                ErrorCodes.WordBatchChildCountLimit);
        }

        if (exampleCount > WordConstraints.MaxBatchExampleCount)
        {
            return CreateLimitResult(
                structuralSummary,
                ErrorCodes.WordBatchChildCountLimit);
        }

        if (textCharacterCount > WordConstraints.MaxBatchTextCharacterCount)
        {
            return CreateLimitResult(
                structuralSummary,
                ErrorCodes.WordBatchTextLengthLimit);
        }

        var errors = new List<BatchWordValidationErrorResponse>();
        var errorKeys = new HashSet<ValidationErrorKey>();
        var audioReferences = CollectAudioReferences(words);
        var resolvedAudio = await ResolveAudioAsync(
            audioReferences,
            errors,
            errorKeys,
            cancellationToken);
        var audioReferencesByRow = audioReferences
            .GroupBy(value => value.RowIndex)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var preparedRows = new List<WordBatchPreparedRow>(words.Count);
        var previews = new List<BatchWordRowValidationResponse>(words.Count);
        var identities = new WordIdentity?[words.Count];

        var rowIndex = 0;
        foreach (var row in words)
        {
            var createRequest = CreateRequest(
                row,
                rowIndex,
                resolvedAudio,
                errors,
                errorKeys);
            preparedRows.Add(new WordBatchPreparedRow(rowIndex + 1, createRequest));

            var validation = await _wordValidator.ValidateAsync(
                createRequest,
                cancellationToken);
            foreach (var failure in validation.Errors)
            {
                var errorCode = Enum.TryParse<ErrorCodes>(
                    failure.ErrorCode,
                    ignoreCase: false,
                    out var parsedCode)
                    ? parsedCode
                    : ErrorCodes.RequestValidationFailed;
                AddError(
                    errors,
                    errorKeys,
                    rowIndex + 1,
                    ToBatchField(rowIndex, failure.PropertyName),
                    errorCode);
            }

            try
            {
                identities[rowIndex] = WordAggregateBuilder.NormalizeIdentity(
                    createRequest.Headword);
            }
            catch (RequestValidationException exception)
            {
                AddError(
                    errors,
                    errorKeys,
                    rowIndex + 1,
                    $"words[{rowIndex}].headword",
                    exception.ErrorCode);
            }

            var rowReferences = audioReferencesByRow.GetValueOrDefault(rowIndex) ?? [];
            var wordAudio = resolvedAudio.GetValueOrDefault(
                AudioLocation.ForWord(rowIndex));
            previews.Add(new BatchWordRowValidationResponse(
                rowIndex + 1,
                identities[rowIndex]?.Headword ?? row?.Headword,
                identities[rowIndex]?.NormalizedHeadword,
                wordAudio?.Name,
                row?.Senses?.Count ?? 0,
                CountRowExamples(row),
                rowReferences.Length,
                rowReferences.Count(value =>
                    resolvedAudio.ContainsKey(value.Location))));
            rowIndex++;
        }

        AddBatchDuplicateErrors(identities, errors, errorKeys);
        await AddDatabaseDuplicateErrorsAsync(
            identities,
            errors,
            errorKeys,
            cancellationToken);

        var wordAudioReferenceCount = audioReferences.Count(value => value.IsWord);
        var exampleAudioReferenceCount = audioReferences.Count - wordAudioReferenceCount;
        var summary = new BatchWordSummaryResponse(
            ToResponseCount(wordCount),
            ToResponseCount(senseCount),
            ToResponseCount(exampleCount),
            wordAudioReferenceCount,
            exampleAudioReferenceCount,
            resolvedAudio.Count);
        var response = new BatchWordValidationResponse(
            errors.Count == 0,
            summary,
            previews,
            errors);
        return new WordBatchValidationBuildResult(response, preparedRows);
    }

    private static WordBatchValidationBuildResult CreateLimitResult(
        BatchWordSummaryResponse summary,
        ErrorCodes errorCode)
    {
        var error = new BatchWordValidationErrorResponse(
            null,
            "words",
            errorCode,
            errorCode.GetMessage());
        return new WordBatchValidationBuildResult(
            new BatchWordValidationResponse(false, summary, [], [error]),
            []);
    }

    private static long CountSenses(
        IReadOnlyCollection<BatchWordRowRequest> words)
    {
        long count = 0;
        foreach (var row in words)
        {
            count = AddBounded(
                count,
                row?.Senses?.Count ?? 0,
                int.MaxValue);
        }
        return count;
    }

    private static long CountExamples(
        IReadOnlyCollection<BatchWordRowRequest> words)
    {
        long count = 0;
        foreach (var row in words)
        {
            if (row?.Senses is null)
            {
                continue;
            }
            foreach (var sense in row.Senses)
            {
                count = AddBounded(
                    count,
                    sense?.Examples?.Count ?? 0,
                    int.MaxValue);
            }
        }
        return count;
    }

    private static long CountTextCharacters(
        IReadOnlyCollection<BatchWordRowRequest> words)
    {
        long count = 0;
        foreach (var row in words)
        {
            count = AddText(count, row?.Headword);
            count = AddText(count, row?.AudioFileName);
            if (count > WordConstraints.MaxBatchTextCharacterCount)
            {
                return count;
            }
            if (row?.Senses is null)
            {
                continue;
            }
            foreach (var sense in row.Senses)
            {
                count = AddText(count, sense?.PartOfSpeech);
                count = AddText(count, sense?.Definition);
                count = AddText(count, sense?.UsageNote);
                if (sense?.Examples is not null)
                {
                    foreach (var example in sense.Examples)
                    {
                        count = AddText(count, example?.Sentence);
                        count = AddText(count, example?.Translation);
                        count = AddText(count, example?.AudioFileName);
                        if (count > WordConstraints.MaxBatchTextCharacterCount)
                        {
                            return count;
                        }
                    }
                }
                if (count > WordConstraints.MaxBatchTextCharacterCount)
                {
                    return count;
                }
            }
        }
        return count;
    }

    private static long AddText(long current, string? value)
        => AddBounded(
            current,
            value?.Length ?? 0,
            WordConstraints.MaxBatchTextCharacterCount);

    private static long AddBounded(long current, int addition, int limit)
    {
        if (current > limit)
        {
            return (long)limit + 1;
        }
        var remaining = (long)limit - current;
        return addition > remaining ? (long)limit + 1 : current + addition;
    }

    private static int ToResponseCount(long count)
        => count > int.MaxValue ? int.MaxValue : (int)count;

    private static List<AudioReference> CollectAudioReferences(
        IReadOnlyCollection<BatchWordRowRequest> words)
    {
        var references = new List<AudioReference>();
        var rowIndex = 0;
        foreach (var row in words)
        {
            AddAudioReference(
                references,
                row?.AudioFileName,
                AudioLocation.ForWord(rowIndex),
                rowIndex,
                $"words[{rowIndex}].audioFileName",
                isWord: true);
            if (row?.Senses is not null)
            {
                var senseIndex = 0;
                foreach (var sense in row.Senses)
                {
                    if (sense?.Examples is not null)
                    {
                        var exampleIndex = 0;
                        foreach (var example in sense.Examples)
                        {
                            AddAudioReference(
                                references,
                                example?.AudioFileName,
                                AudioLocation.ForExample(
                                    rowIndex,
                                    senseIndex,
                                    exampleIndex),
                                rowIndex,
                                $"words[{rowIndex}].senses[{senseIndex}].examples[{exampleIndex}].audioFileName",
                                isWord: false);
                            exampleIndex++;
                        }
                    }
                    senseIndex++;
                }
            }
            rowIndex++;
        }
        return references;
    }

    private static void AddAudioReference(
        ICollection<AudioReference> references,
        string? name,
        AudioLocation location,
        int rowIndex,
        string field,
        bool isWord)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        references.Add(new AudioReference(
            location,
            rowIndex,
            field,
            AudioResource.NormalizeName(name),
            isWord));
    }

    private async Task<Dictionary<AudioLocation, AudioLookup>> ResolveAudioAsync(
        IReadOnlyCollection<AudioReference> references,
        List<BatchWordValidationErrorResponse> errors,
        HashSet<ValidationErrorKey> errorKeys,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<AudioLocation, AudioLookup>();
        if (references.Count == 0)
        {
            return resolved;
        }

        var normalizedNames = references
            .Select(value => value.NormalizedName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var resources = await _db.AudioResources.AsNoTracking()
            .Where(value => normalizedNames.Contains(value.NormalizedName))
            .Select(value => new AudioLookup
            {
                Id = value.Id,
                Name = value.Name,
                NormalizedName = value.NormalizedName,
                Status = value.Status
            })
            .ToArrayAsync(cancellationToken);
        var byName = resources.ToDictionary(
            value => value.NormalizedName,
            StringComparer.Ordinal);

        foreach (var reference in references)
        {
            if (!byName.TryGetValue(reference.NormalizedName, out var audio))
            {
                AddError(
                    errors,
                    errorKeys,
                    reference.RowIndex + 1,
                    reference.Field,
                    ErrorCodes.WordBatchAudioNotFound);
                continue;
            }
            if (audio.Status == AudioResourceStatus.Failed)
            {
                AddError(
                    errors,
                    errorKeys,
                    reference.RowIndex + 1,
                    reference.Field,
                    ErrorCodes.WordBatchAudioFailed);
                continue;
            }
            resolved[reference.Location] = audio;
        }
        return resolved;
    }

    private static CreateWordRequest CreateRequest(
        BatchWordRowRequest? row,
        int rowIndex,
        IReadOnlyDictionary<AudioLocation, AudioLookup> resolvedAudio,
        List<BatchWordValidationErrorResponse> errors,
        HashSet<ValidationErrorKey> errorKeys)
    {
        var senses = new List<WordSenseInput>();
        if (row is not null && row.Senses is null)
        {
            AddError(
                errors,
                errorKeys,
                rowIndex + 1,
                $"words[{rowIndex}].senses",
                ErrorCodes.WordChildCollectionInvalid);
            senses.Add(CreatePlaceholderSense());
        }
        else if (row?.Senses is not null)
        {
            var senseIndex = 0;
            foreach (var sense in row.Senses)
            {
                if (sense is null)
                {
                    AddError(
                        errors,
                        errorKeys,
                        rowIndex + 1,
                        $"words[{rowIndex}].senses[{senseIndex}]",
                        ErrorCodes.WordChildCollectionInvalid);
                    senses.Add(CreatePlaceholderSense());
                    senseIndex++;
                    continue;
                }

                var examples = new List<ExampleSentenceInput>();
                if (sense.Examples is null)
                {
                    AddError(
                        errors,
                        errorKeys,
                        rowIndex + 1,
                        $"words[{rowIndex}].senses[{senseIndex}].examples",
                        ErrorCodes.WordChildCollectionInvalid);
                }
                else
                {
                    var exampleIndex = 0;
                    foreach (var example in sense.Examples)
                    {
                        if (example is null)
                        {
                            AddError(
                                errors,
                                errorKeys,
                                rowIndex + 1,
                                $"words[{rowIndex}].senses[{senseIndex}].examples[{exampleIndex}]",
                                ErrorCodes.WordChildCollectionInvalid);
                            examples.Add(CreatePlaceholderExample());
                            exampleIndex++;
                            continue;
                        }
                        resolvedAudio.TryGetValue(
                            AudioLocation.ForExample(
                                rowIndex,
                                senseIndex,
                                exampleIndex),
                            out var exampleAudio);
                        examples.Add(new ExampleSentenceInput
                        {
                            AudioResourceId = exampleAudio?.Id,
                            Sentence = example.Sentence ?? string.Empty,
                            Translation = example.Translation ?? string.Empty,
                            SortOrder = example.SortOrder
                        });
                        exampleIndex++;
                    }
                }

                var partOfSpeech = ParsePartOfSpeech(sense.PartOfSpeech);
                senses.Add(new WordSenseInput
                {
                    PartOfSpeech = partOfSpeech,
                    Definition = sense.Definition ?? string.Empty,
                    UsageNote = sense.UsageNote,
                    SortOrder = sense.SortOrder,
                    Examples = examples
                });
                senseIndex++;
            }
        }

        resolvedAudio.TryGetValue(AudioLocation.ForWord(rowIndex), out var wordAudio);
        return new CreateWordRequest
        {
            Headword = row?.Headword ?? string.Empty,
            AudioResourceId = wordAudio?.Id,
            Senses = senses
        };
    }

    private static WordSenseInput CreatePlaceholderSense()
        => new()
        {
            PartOfSpeech = PartOfSpeech.Noun,
            Definition = "placeholder",
            Examples = []
        };

    private static ExampleSentenceInput CreatePlaceholderExample()
        => new()
        {
            Sentence = "placeholder",
            Translation = "placeholder"
        };

    private static PartOfSpeech ParsePartOfSpeech(string? value)
    {
        var parsedSuccessfully = Enum.TryParse(
            value,
            ignoreCase: false,
            out PartOfSpeech parsed);
        if (parsedSuccessfully &&
            Enum.IsDefined(parsed) &&
            string.Equals(value, parsed.ToString(), StringComparison.Ordinal))
        {
            return parsed;
        }
        return (PartOfSpeech)(-1);
    }

    private static int CountRowExamples(BatchWordRowRequest? row)
    {
        if (row?.Senses is null)
        {
            return 0;
        }
        var count = 0;
        foreach (var sense in row.Senses)
        {
            count += sense?.Examples?.Count ?? 0;
        }
        return count;
    }

    private static void AddBatchDuplicateErrors(
        IReadOnlyList<WordIdentity?> identities,
        List<BatchWordValidationErrorResponse> errors,
        HashSet<ValidationErrorKey> errorKeys)
    {
        var duplicateRows = identities
            .Select((identity, index) => (Identity: identity, Index: index))
            .Where(value => value.Identity.HasValue)
            .GroupBy(value => value.Identity!.Value.NormalizedHeadword)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(value => value.Index));
        foreach (var index in duplicateRows)
        {
            AddError(
                errors,
                errorKeys,
                index + 1,
                $"words[{index}].headword",
                ErrorCodes.WordDuplicate);
        }
    }

    private async Task AddDatabaseDuplicateErrorsAsync(
        IReadOnlyList<WordIdentity?> identities,
        List<BatchWordValidationErrorResponse> errors,
        HashSet<ValidationErrorKey> errorKeys,
        CancellationToken cancellationToken)
    {
        var normalizedHeadwords = identities
            .Where(value => value.HasValue)
            .Select(value => value!.Value.NormalizedHeadword)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedHeadwords.Length == 0)
        {
            return;
        }

        var existing = await _db.Words.AsNoTracking()
            .Where(value => normalizedHeadwords.Contains(value.NormalizedHeadword))
            .Select(value => value.NormalizedHeadword)
            .ToHashSetAsync(cancellationToken);
        for (var index = 0; index < identities.Count; index++)
        {
            if (identities[index] is { } identity &&
                existing.Contains(identity.NormalizedHeadword))
            {
                AddError(
                    errors,
                    errorKeys,
                    index + 1,
                    $"words[{index}].headword",
                    ErrorCodes.WordDuplicate);
            }
        }
    }

    private static string ToBatchField(int rowIndex, string propertyName)
    {
        var prefix = $"words[{rowIndex}]";
        if (string.IsNullOrEmpty(propertyName))
        {
            return prefix;
        }
        var segments = propertyName.Split('.');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length > 0)
            {
                segments[index] = char.ToLowerInvariant(segments[index][0]) +
                    segments[index][1..];
            }
        }
        return $"{prefix}.{string.Join('.', segments)}";
    }

    private static void AddError(
        ICollection<BatchWordValidationErrorResponse> errors,
        ISet<ValidationErrorKey> errorKeys,
        int? rowNumber,
        string field,
        ErrorCodes errorCode)
    {
        if (!errorKeys.Add(new ValidationErrorKey(rowNumber, field, errorCode)))
        {
            return;
        }
        errors.Add(new BatchWordValidationErrorResponse(
            rowNumber,
            field,
            errorCode,
            errorCode.GetMessage()));
    }

    private readonly record struct AudioLocation(
        int RowIndex,
        int SenseIndex,
        int ExampleIndex)
    {
        public static AudioLocation ForWord(int rowIndex)
            => new(rowIndex, -1, -1);

        public static AudioLocation ForExample(
            int rowIndex,
            int senseIndex,
            int exampleIndex)
            => new(rowIndex, senseIndex, exampleIndex);
    }

    private sealed record AudioReference(
        AudioLocation Location,
        int RowIndex,
        string Field,
        string NormalizedName,
        bool IsWord);

    private sealed class AudioLookup
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public required string NormalizedName { get; init; }
        public AudioResourceStatus Status { get; init; }
    }

    private readonly record struct ValidationErrorKey(
        int? RowNumber,
        string Field,
        ErrorCodes ErrorCode);
}

internal sealed record WordBatchPreparedRow(
    int RowNumber,
    CreateWordRequest Request);

internal sealed record WordBatchValidationBuildResult(
    BatchWordValidationResponse Response,
    IReadOnlyList<WordBatchPreparedRow> Rows);
