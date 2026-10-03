using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

public static class WordStudyConstraints
{
    public const int DefaultWordCount = 20;
    public const int MinWordCount = 1;
    public const int MaxWordCount = 100;
    public const int DefaultReviewCount = 50;
    public const int MinReviewCount = 1;
    public const int MaxReviewCount = 200;
}

public sealed record WordStudySessionStateResponse(
    Guid Id,
    WordStudySessionType SessionType,
    WordStudyPhase Phase,
    WordStudySessionStatus Status,
    int ActualCount,
    int CompletedCount,
    int MemorizationPassedCount,
    int SpellingPassedCount,
    int ExcludedCount,
    int SkippedCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    WordStudyCurrentItemResponse? CurrentItem,
    DateTimeOffset? NextAvailableAt = null,
    int NewCount = 0, int LearningCount = 0, int ReviewCount = 0,
    WordStudyGroupSummary? Summary = null);

public sealed record WordStudyCurrentItemResponse(
    WordStudyPhase Phase,
    Guid ItemId,
    Guid WordId,
    Guid ItemConcurrencyStamp,
    bool IsFavorite,
    WordMemorizationContentResponse? Memorization,
    WordSpellingPromptResponse? Spelling);

public sealed record WordMemorizationContentResponse(
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId,
    IReadOnlyList<WordRatingPreviewResponse>? RatingPreviews = null);

public sealed record WordSpellingSenseResponse(
    PartOfSpeech PartOfSpeech,
    string Definition);

public sealed record WordSpellingPromptResponse(
    IReadOnlyList<WordSpellingSenseResponse> Senses);

public sealed record WordStudyCompletedItemResponse(
    Guid ItemId,
    Guid WordId,
    int Position,
    WordStudySessionItemStatus Status,
    string? Headword,
    bool HadMemorizationFailure,
    bool HadSpellingFailure);

public sealed record WordStudyCommandResponse(
    WordSpellingOutcomeResponse? SpellingOutcome,
    WordStudySessionStateResponse Session);

public sealed record WordSpellingOutcomeResponse(
    WordSpellingResult Result,
    string CorrectAnswer);

public sealed record SubmitWordMemorizationRequest(
    WordMemorizationResult? Result,
    Guid ItemConcurrencyStamp);

public sealed record SubmitWordSpellingRequest
{
    public required string Answer { get; init; }
    public bool Skip { get; init; }
    public Guid ItemConcurrencyStamp { get; init; }
}

public sealed record ExcludeWordFromReviewRequest(Guid ItemConcurrencyStamp);

public sealed record WordLearningOverviewResponse(
    int TotalLearnedCount,
    int TodayLearnedCount,
    bool HasMoreWords,
    WordStudySessionStateResponse? ActiveSession);

public sealed record WordReviewOverviewResponse(
    int DueCount,
    int OverdueCount,
    WordStudySessionStateResponse? ActiveSession);

public sealed record WordStudyTodayReviewItemResponse(
    Guid WordId,
    string Headword,
    WordStudyActivityType ActivityType,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId,
    bool IsFavorite);

public sealed record WordStudyTodayReviewResponse(
    DateOnly StudyDateUtc,
    IReadOnlyList<WordStudyTodayReviewItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record WordStudyCheckInResponse(
    DateOnly StudyDateUtc,
    DateTimeOffset CheckedInAtUtc);

public sealed record WordStudyCheckInCalendarResponse(
    int Year,
    int Month,
    IReadOnlyList<WordStudyCheckInResponse> CheckedInDates,
    int CurrentStreak,
    int LongestStreak,
    int TotalCheckInDays);

public sealed record WordRatingPreviewResponse(WordMemorizationResult Rating, DateTimeOffset DueAt, long IntervalSeconds);

public sealed record WordStudySummaryChoice(bool SkipSpelling);
public sealed record WordStudyGroupSummary(int WordCount, int RatingCount, int AgainCount, int HardCount, int GoodCount, int EasyCount);
