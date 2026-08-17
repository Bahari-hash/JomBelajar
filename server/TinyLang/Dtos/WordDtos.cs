using System.ComponentModel.DataAnnotations;
using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新词条共享的完整目标内容。
/// </summary>
public abstract record WordUpsertRequest
{
    public required string Headword { get; init; }
    public Guid? AudioResourceId { get; init; }
    public IReadOnlyCollection<WordSenseInput> Senses { get; init; } = [];
}

/// <summary>
/// 描述创建一个立即有效词条的请求。
/// </summary>
public sealed record CreateWordRequest : WordUpsertRequest;

/// <summary>
/// 描述以完整目标集合和乐观并发标识更新词条的请求。
/// </summary>
public sealed record UpdateWordRequest : WordUpsertRequest
{
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述硬删除词条所需的并发前置条件。
/// </summary>
public sealed record DeleteWordRequest
{
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述一个待新增或更新的词条释义及其完整例句集合。
/// </summary>
public sealed record WordSenseInput
{
    public Guid? Id { get; init; }
    public PartOfSpeech PartOfSpeech { get; init; }
    public required string Definition { get; init; }
    public string? UsageNote { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyCollection<ExampleSentenceInput> Examples { get; init; } = [];
}

/// <summary>
/// 描述一个待新增或更新的释义例句。
/// </summary>
public sealed record ExampleSentenceInput
{
    public Guid? Id { get; init; }
    public Guid? AudioResourceId { get; init; }
    public required string Sentence { get; init; }
    public required string Translation { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述管理员词条列表的筛选和分页条件。
/// </summary>
public sealed record AdminWordListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public PartOfSpeech? PartOfSpeech { get; init; }
    public string? Definition { get; init; }
}

/// <summary>
/// 描述登录用户词条列表的筛选和分页条件。
/// </summary>
public sealed record WordListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
}

/// <summary>
/// 返回管理员编辑例句时所需的共享音频摘要。
/// </summary>
public sealed record AdminExampleSentenceAudioResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode);

/// <summary>
/// 返回管理员管理词条时所需的例句内容和服务端标识。
/// </summary>
public sealed record AdminExampleSentenceResponse(
    Guid Id,
    string Sentence,
    string Translation,
    AdminExampleSentenceAudioResponse? Audio,
    int SortOrder);

/// <summary>
/// 返回管理员管理词条时所需的释义及其例句。
/// </summary>
public sealed record AdminWordSenseResponse(
    Guid Id,
    PartOfSpeech PartOfSpeech,
    string Definition,
    string? UsageNote,
    int SortOrder,
    IReadOnlyList<AdminExampleSentenceResponse> Examples);

/// <summary>
/// 返回管理员编辑词条时所需的共享音频摘要。
/// </summary>
public sealed record AdminWordAudioResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode);

/// <summary>
/// 返回词条的完整编辑状态和嵌套内容。
/// </summary>
public sealed record AdminWordResponse(
    Guid Id,
    string Headword,
    AdminWordAudioResponse? Audio,
    Guid ConcurrencyStamp,
    IReadOnlyList<AdminWordSenseResponse> Senses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回管理员词条列表中的管理摘要。
/// </summary>
public sealed record AdminWordListItemResponse(
    Guid Id,
    string Headword,
    PartOfSpeech? PrimaryPartOfSpeech,
    string? PrimaryDefinition,
    int SenseCount,
    int ExampleCount,
    bool HasAudio,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyStamp);

/// <summary>
/// 返回用户可见的例句纯文本。
/// </summary>
public sealed record ExampleSentenceResponse(
    string Sentence,
    string Translation,
    Guid? AudioResourceId,
    int SortOrder);

/// <summary>
/// 返回用户可见的释义及其有序例句。
/// </summary>
public sealed record WordSenseResponse(
    PartOfSpeech PartOfSpeech,
    string Definition,
    string? UsageNote,
    int SortOrder,
    IReadOnlyList<ExampleSentenceResponse> Examples);

/// <summary>
/// 返回用户词条列表中的首要释义和可选共享音频。
/// </summary>
public sealed record WordListItemResponse(
    Guid Id,
    string Headword,
    PartOfSpeech PartOfSpeech,
    string Definition,
    Guid? AudioResourceId,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回用户可见的完整词条内容。
/// </summary>
public sealed record WordResponse(
    Guid Id,
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 定义词条请求和持久化模型共享的有界限制。
/// </summary>
public static class WordConstraints
{
    public const int MaxHeadwordLength = 200;
    public const int MaxTextLength = 2000;
    public const int MaxUsageNoteLength = 1000;
    public const int MaxSenseCount = 20;
    public const int MaxExampleCount = 20;
    public const int MaxSortOrder = 10_000;
    public const int MaxBatchWordCount = 1_000;
    public const int MaxBatchSenseCount = 10_000;
    public const int MaxBatchExampleCount = 50_000;
    public const int MaxBatchTextCharacterCount = 10_000_000;
    public const long MaxBatchRequestBodyBytes = 20L * 1024 * 1024;
}
