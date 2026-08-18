using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述创建一次固定范围单词背诵会话的选择参数。
/// </summary>
public sealed record CreateWordStudySessionRequest
{
    public int WordCount { get; init; } = WordStudyConstraints.DefaultWordCount;
    public bool IncludePreviouslyStudied { get; init; }
    public WordStudySelectionMode SelectionMode { get; init; } =
        WordStudySelectionMode.Sequential;
}

/// <summary>
/// 描述用户对一个待处理会话项提交的最终背诵结果。
/// </summary>
public sealed record SubmitWordStudyResultRequest
{
    public WordStudyResult? Result { get; init; }
}

/// <summary>
/// 返回背诵会话配置、生命周期和各类处理数量的当前摘要。
/// </summary>
public sealed record WordStudySessionResponse(
    Guid Id,
    int RequestedCount,
    int ActualCount,
    bool IncludePreviouslyStudied,
    WordStudySelectionMode SelectionMode,
    WordStudySessionStatus Status,
    int CompletedCount,
    int RememberedCount,
    int ForgottenCount,
    int SkippedCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? AbandonedAt);

/// <summary>
/// 表示当前 UTC 日期的背诵状态。
/// </summary>
public enum WordStudyTodayState
{
    NotStarted,
    Active,
    Completed
}

/// <summary>
/// 返回当前 UTC 日期的背诵摘要。
/// </summary>
public sealed record WordStudyTodayResponse(
    DateTimeOffset StudyDateUtc,
    int DailyWordStudyCount,
    WordStudyTodayState State,
    WordStudySessionResponse? Session);

/// <summary>
/// 返回当前会话中下一个待背诵项及其实时安全词条内容。
/// </summary>
public sealed record WordStudyNextItemResponse(
    Guid SessionId,
    Guid ItemId,
    int Position,
    int ActualCount,
    Guid WordId,
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId);

/// <summary>
/// 返回会话项目当前仍可见的词头、释义、例句和发音内容。
/// </summary>
public sealed record WordStudySessionItemContentResponse(
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId);

/// <summary>
/// 返回固定会话项目的顺序、结果状态和可选实时词条内容。
/// </summary>
public sealed record WordStudySessionItemResponse(
    Guid ItemId,
    Guid WordId,
    int Position,
    WordStudySessionItemStatus Status,
    bool ContentAvailable,
    WordStudySessionItemContentResponse? Content);

/// <summary>
/// 定义基础单词背诵请求使用的数量上限。
/// </summary>
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
    WordStudyCurrentItemResponse? CurrentItem);

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
    Guid? AudioResourceId);

public sealed record WordSpellingSenseResponse(
    PartOfSpeech PartOfSpeech,
    string Definition,
    string? UsageNote,
    int SortOrder);

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
    WordSpellingResult? SpellingResult,
    WordStudySessionStateResponse Session);

public sealed record SubmitWordMemorizationRequest(
    WordMemorizationResult? Result,
    Guid ItemConcurrencyStamp);

public sealed record SubmitWordSpellingRequest
{
    public required string Answer { get; init; }
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
