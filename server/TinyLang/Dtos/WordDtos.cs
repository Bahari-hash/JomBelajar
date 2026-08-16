using System.ComponentModel.DataAnnotations;
using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新词条共享的完整目标内容。
/// </summary>
public abstract record WordUpsertRequest
{
    public required string Headword { get; init; }
    public IReadOnlyCollection<WordSenseInput> Senses { get; init; } = [];
    public IReadOnlyCollection<WordPronunciationInput> Pronunciations { get; init; } = [];
}

/// <summary>
/// 描述创建词条草稿及其可选子项的请求。
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
/// 描述发布、下架、归档和硬删除词条所需的并发前置条件。
/// </summary>
public sealed record WordMutationRequest
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
    public required string Sentence { get; init; }
    public required string Translation { get; init; }
    public Guid? AudioClipId { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述一个待新增或更新的词条发音关联。
/// </summary>
public sealed record WordPronunciationInput
{
    public Guid? Id { get; init; }
    public Guid AudioClipId { get; init; }
    public string? AccentTag { get; init; }
    public string? Ipa { get; init; }
    public bool IsDefault { get; init; }
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
    public WordPublicationStatus? Status { get; init; }
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
/// 返回管理员管理词条时所需的例句内容和服务端标识。
/// </summary>
public sealed record AdminExampleSentenceResponse(
    Guid Id,
    string Sentence,
    string Translation,
    Guid? AudioClipId,
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
/// 返回管理员管理词条时所需的发音关联。
/// </summary>
public sealed record AdminWordPronunciationResponse(
    Guid Id,
    Guid AudioClipId,
    string? AccentTag,
    string? Ipa,
    bool IsDefault,
    int SortOrder);

/// <summary>
/// 返回词条的完整编辑状态、审计信息和嵌套内容。
/// </summary>
public sealed record AdminWordResponse(
    Guid Id,
    string Headword,
    WordPublicationStatus Status,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyStamp,
    IReadOnlyList<AdminWordSenseResponse> Senses,
    IReadOnlyList<AdminWordPronunciationResponse> Pronunciations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回管理员词条列表中的管理摘要。
/// </summary>
public sealed record AdminWordListItemResponse(
    Guid Id,
    string Headword,
    WordPublicationStatus Status,
    PartOfSpeech? PrimaryPartOfSpeech,
    string? PrimaryDefinition,
    int SenseCount,
    int ExampleCount,
    int PronunciationCount,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyStamp);

/// <summary>
/// 返回用户可见的例句纯文本和可选播放音频标识。
/// </summary>
public sealed record ExampleSentenceResponse(
    string Sentence,
    string Translation,
    Guid? AudioClipId,
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
/// 返回用户可见的发音元数据和播放音频标识。
/// </summary>
public sealed record WordPronunciationResponse(
    Guid AudioClipId,
    string? AccentTag,
    string? Ipa,
    bool IsDefault,
    int SortOrder);

/// <summary>
/// 返回用户词条列表中的首要释义和默认发音摘要。
/// </summary>
public sealed record WordListItemResponse(
    Guid Id,
    string Headword,
    PartOfSpeech PartOfSpeech,
    string Definition,
    WordPronunciationResponse DefaultPronunciation,
    DateTimeOffset PublishedAt);

/// <summary>
/// 返回用户可见的完整已发布词条内容。
/// </summary>
public sealed record WordResponse(
    Guid Id,
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    IReadOnlyList<WordPronunciationResponse> Pronunciations,
    DateTimeOffset PublishedAt);

/// <summary>
/// 定义词条请求和持久化模型共享的有界限制。
/// </summary>
public static class WordConstraints
{
    public const int MaxHeadwordLength = 200;
    public const int MaxTextLength = 2000;
    public const int MaxUsageNoteLength = 1000;
    public const int MaxAccentTagLength = 100;
    public const int MaxIpaLength = 200;
    public const int MaxSenseCount = 20;
    public const int MaxExampleCount = 20;
    public const int MaxPronunciationCount = 20;
    public const int MaxSortOrder = 10_000;
}
