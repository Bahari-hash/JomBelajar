using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述使用已激活音频资源创建音频资产的请求。
/// </summary>
public sealed record CreateAudioClipRequest
{
    public Guid SourceMediaResourceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string LanguageTag { get; init; }
    public AudioClipKind Kind { get; init; }
}

/// <summary>
/// 描述编辑者允许修改的音频展示元数据。
/// </summary>
public sealed record UpdateAudioClipRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string LanguageTag { get; init; }
    public AudioClipKind Kind { get; init; }
}

/// <summary>
/// 描述编辑者音频列表的分页、关键词和状态筛选。
/// </summary>
public sealed record EditorAudioClipListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public AudioProcessingStatus? ProcessingStatus { get; init; }
    public AudioPublicationStatus? PublicationStatus { get; init; }
    public AudioClipKind? Kind { get; init; }
}

/// <summary>
/// 返回编辑者音频列表中的处理和发布状态摘要。
/// </summary>
public sealed record EditorAudioClipListItemResponse(
    Guid Id,
    string Title,
    string LanguageTag,
    AudioClipKind Kind,
    AudioProcessingStatus ProcessingStatus,
    AudioPublicationStatus PublicationStatus,
    double? DurationSeconds,
    string? FailureCode,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回编辑者管理音频所需的完整业务状态，不包含内部对象路径。
/// </summary>
public sealed record EditorAudioClipResponse(
    Guid Id,
    Guid SourceMediaResourceId,
    string Title,
    string? Description,
    string LanguageTag,
    AudioClipKind Kind,
    AudioProcessingStatus ProcessingStatus,
    AudioPublicationStatus PublicationStatus,
    double? DurationSeconds,
    int? SampleRate,
    int? Channels,
    string? ContainerFormat,
    string? SourceCodec,
    string? FailureCode,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回登录用户播放短音频所需的短期地址和安全元数据。
/// </summary>
public sealed record AudioPlaybackResponse(
    string Url,
    DateTimeOffset ExpiresAt,
    double DurationSeconds,
    string LanguageTag,
    AudioClipKind AudioClipKind);
