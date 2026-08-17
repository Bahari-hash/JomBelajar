using System.ComponentModel.DataAnnotations;
using TinyLang.Exceptions;

namespace TinyLang.Dtos;

/// <summary>
/// Describes one word batch request.
/// </summary>
public sealed record BatchWordRequest
{
    public IReadOnlyCollection<BatchWordRowRequest> Words { get; init; } = [];
}

/// <summary>
/// Describes one word row in a batch request.
/// </summary>
public sealed record BatchWordRowRequest
{
    public string? Headword { get; init; }
    public string? AudioFileName { get; init; }
    public IReadOnlyCollection<BatchWordSenseInput> Senses { get; init; } = [];
}

/// <summary>
/// Describes one sense in a batch word row.
/// </summary>
public sealed record BatchWordSenseInput
{
    [AllowedValues(
        "Noun",
        "Verb",
        "Adjective",
        "Adverb",
        "Pronoun",
        "Determiner",
        "Preposition",
        "Conjunction",
        "Interjection",
        "Numeral",
        "Particle",
        "Other")]
    public string? PartOfSpeech { get; init; }

    public string? Definition { get; init; }
    public string? UsageNote { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyCollection<BatchExampleSentenceInput> Examples { get; init; } = [];
}

/// <summary>
/// Describes one example sentence in a batch word sense.
/// </summary>
public sealed record BatchExampleSentenceInput
{
    public string? Sentence { get; init; }
    public string? Translation { get; init; }
    public string? AudioFileName { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// Summarizes a validated word batch.
/// </summary>
public sealed record BatchWordSummaryResponse(
    int WordCount,
    int SenseCount,
    int ExampleCount,
    int WordAudioReferenceCount,
    int ExampleAudioReferenceCount,
    int MatchedAudioReferenceCount);

/// <summary>
/// Summarizes validation results for one word row.
/// </summary>
public sealed record BatchWordRowValidationResponse(
    int RowNumber,
    string? Headword,
    string? NormalizedHeadword,
    string? WordAudioName,
    int SenseCount,
    int ExampleCount,
    int AudioReferenceCount,
    int MatchedAudioCount);

/// <summary>
/// Describes one word batch validation failure.
/// </summary>
public sealed record BatchWordValidationErrorResponse(
    int? RowNumber,
    string Field,
    ErrorCodes ErrorCode,
    string Message);

/// <summary>
/// Returns the complete validation result for one word batch.
/// </summary>
public sealed record BatchWordValidationResponse(
    bool IsValid,
    BatchWordSummaryResponse Summary,
    IReadOnlyList<BatchWordRowValidationResponse> Rows,
    IReadOnlyList<BatchWordValidationErrorResponse> Errors);

/// <summary>
/// Identifies one word created from a batch row.
/// </summary>
public sealed record BatchWordCreatedItemResponse(int RowNumber, Guid WordId);

/// <summary>
/// Returns the words created by one batch import.
/// </summary>
public sealed record BatchWordImportResponse(
    int CreatedCount,
    IReadOnlyList<BatchWordCreatedItemResponse> Items);
