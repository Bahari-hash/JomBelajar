using System.ComponentModel.DataAnnotations;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新词条共享的完整目标内容。
/// </summary>
public abstract record WordUpsertRequest
{
    public required string LanguageTag { get; init; }
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
    public required string DefinitionLanguageTag { get; init; }
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
    public required string LanguageTag { get; init; }
    public required string Translation { get; init; }
    public required string TranslationLanguageTag { get; init; }
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
    public string? Language { get; init; }
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
    public string? Language { get; init; }
}

/// <summary>
/// 返回管理员管理词条时所需的例句内容和服务端标识。
/// </summary>
public sealed record AdminExampleSentenceResponse(
    Guid Id,
    string Sentence,
    string LanguageTag,
    string Translation,
    string TranslationLanguageTag,
    Guid? AudioClipId,
    int SortOrder);

/// <summary>
/// 返回管理员管理词条时所需的释义及其例句。
/// </summary>
public sealed record AdminWordSenseResponse(
    Guid Id,
    PartOfSpeech PartOfSpeech,
    string Definition,
    string DefinitionLanguageTag,
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
    string LanguageTag,
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
    string LanguageTag,
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
/// 描述批量录入中的一个新词条，不允许客户端提交任何持久化标识或状态。
/// </summary>
public sealed record BatchWordRowRequest
{
    public required string LanguageTag { get; init; }
    public required string Headword { get; init; }
    public IReadOnlyCollection<BatchWordSenseInput> Senses { get; init; } = [];
    public IReadOnlyCollection<BatchWordPronunciationInput> Pronunciations { get; init; } = [];
}

/// <summary>
/// 描述批量录入中的释义及其完整例句集合。
/// </summary>
public sealed record BatchWordSenseInput
{
    public PartOfSpeech PartOfSpeech { get; init; }
    public required string Definition { get; init; }
    public required string DefinitionLanguageTag { get; init; }
    public string? UsageNote { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyCollection<BatchExampleSentenceInput> Examples { get; init; } = [];
}

/// <summary>
/// 描述批量录入中的双语例句和可选音频引用。
/// </summary>
public sealed record BatchExampleSentenceInput
{
    public required string Sentence { get; init; }
    public required string LanguageTag { get; init; }
    public required string Translation { get; init; }
    public required string TranslationLanguageTag { get; init; }
    public Guid? AudioClipId { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述批量录入中的发音音频关联。
/// </summary>
public sealed record BatchWordPronunciationInput
{
    public Guid AudioClipId { get; init; }
    public string? AccentTag { get; init; }
    public string? Ipa { get; init; }
    public bool IsDefault { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 包装一个有界、按数组位置确定 rowIndex 的批量录入请求。
/// </summary>
public sealed record BatchWordRequest
{
    public IReadOnlyCollection<BatchWordRowRequest> Rows { get; init; } = [];
}

/// <summary>
/// 返回批量校验中一个字段的稳定错误码和客户端消息。
/// </summary>
public sealed record BatchWordFieldErrorResponse(
    string Field,
    IReadOnlyList<ErrorCodes> ErrorCodes,
    IReadOnlyList<string> Messages);

/// <summary>
/// 返回一个批量行的规范化预览及其全部字段错误。
/// </summary>
public sealed record BatchWordRowValidationResponse(
    int RowIndex,
    CreateWordRequest? Normalized,
    IReadOnlyList<BatchWordFieldErrorResponse> Errors);

/// <summary>
/// 返回批量校验结果；该响应不代表任何数据库写入。
/// </summary>
public sealed record BatchWordValidationResponse(
    bool IsValid,
    IReadOnlyList<BatchWordFieldErrorResponse> Errors,
    IReadOnlyList<BatchWordRowValidationResponse> Rows);

/// <summary>
/// 返回成功导入行与服务端生成词条标识的稳定映射。
/// </summary>
public sealed record BatchWordCreatedItemResponse(int RowIndex, Guid WordId);

/// <summary>
/// 返回一次原子批量导入的摘要。
/// </summary>
public sealed record BatchWordImportResponse(
    Guid BatchId,
    int CreatedCount,
    IReadOnlyList<BatchWordCreatedItemResponse> Items);

/// <summary>
/// 返回用户可见的例句纯文本和可选播放音频标识。
/// </summary>
public sealed record ExampleSentenceResponse(
    string Sentence,
    string LanguageTag,
    string Translation,
    string TranslationLanguageTag,
    Guid? AudioClipId,
    int SortOrder);

/// <summary>
/// 返回用户可见的释义及其有序例句。
/// </summary>
public sealed record WordSenseResponse(
    PartOfSpeech PartOfSpeech,
    string Definition,
    string DefinitionLanguageTag,
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
    string LanguageTag,
    string Headword,
    PartOfSpeech PartOfSpeech,
    string Definition,
    string DefinitionLanguageTag,
    WordPronunciationResponse DefaultPronunciation,
    DateTimeOffset PublishedAt);

/// <summary>
/// 返回用户可见的完整已发布词条内容。
/// </summary>
public sealed record WordResponse(
    Guid Id,
    string LanguageTag,
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
    public const int MaxLanguageTagLength = 35;
    public const int MaxSenseCount = 20;
    public const int MaxExampleCount = 20;
    public const int MaxPronunciationCount = 20;
    public const int MaxSortOrder = 10_000;
    public const int MaxBatchRowCount = 100;
    public const int MaxBatchSenseCount = 1_000;
    public const int MaxBatchExampleCount = 5_000;
    public const int MaxBatchPronunciationCount = 1_000;
    public const int MaxBatchTextCharacterCount = 1_000_000;
    public const long MaxBatchRequestBodyBytes = 2 * 1024 * 1024;
}
