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
    public AudioClipKind Kind { get; init; }
}

/// <summary>
/// 描述管理员允许修改的音频展示元数据。
/// </summary>
public sealed record UpdateAudioClipRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public AudioClipKind Kind { get; init; }
}

/// <summary>
/// 描述管理员音频列表的分页、关键词和状态筛选。
/// </summary>
public sealed record AdminAudioClipListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public AudioProcessingStatus? ProcessingStatus { get; init; }
    public AudioPublicationStatus? PublicationStatus { get; init; }
    public AudioClipKind? Kind { get; init; }
}

/// <summary>
/// 返回管理员音频列表中的处理和发布状态摘要。
/// </summary>
public sealed record AdminAudioClipListItemResponse(
    Guid Id,
    string Title,
    AudioClipKind Kind,
    AudioProcessingStatus ProcessingStatus,
    AudioPublicationStatus PublicationStatus,
    double? DurationSeconds,
    string? FailureCode,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回管理员管理音频所需的完整业务状态，不包含内部对象路径。
/// </summary>
public sealed record AdminAudioClipResponse(
    Guid Id,
    Guid SourceMediaResourceId,
    string Title,
    string? Description,
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
/// 返回登录用户播放短音频所需的交付地址和安全元数据。
/// </summary>
public sealed record AudioPlaybackResponse(
    string Url,
    DateTimeOffset? ExpiresAt,
    double DurationSeconds,
    AudioClipKind AudioClipKind);

/// <summary>
/// 描述管理员初始化音频资源上传时提交的文件元数据。
/// </summary>
public sealed record InitializeAudioUploadRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// 返回音频资源初始化后的媒体标识和应用层上传信息。
/// </summary>
public sealed record AudioUploadInitializationResponse(
    Guid AudioResourceId,
    Guid MediaResourceId,
    string? PresignedUrl,
    Guid? MultipartSessionId,
    int? PartSize,
    int? PartCount,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// 描述管理员重命名音频资源的请求。
/// </summary>
public sealed record RenameAudioResourceRequest
{
    public required string Name { get; init; }
}

/// <summary>
/// 描述管理员音频资源列表查询条件。
/// </summary>
public sealed record AdminAudioResourceListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public AudioResourceStatus? Status { get; init; }
}

/// <summary>
/// 返回管理员音频资源列表项。
/// </summary>
public sealed record AdminAudioResourceListItemResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回管理员音频资源详情，不暴露对象存储路径。
/// </summary>
public sealed record AdminAudioResourceResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    int? SampleRate,
    int? Channels,
    string? ContainerFormat,
    string? SourceCodec,
    string? LastFailureCode,
    Guid? CurrentOutputVersion,
    Guid ConcurrencyStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回登录用户播放就绪音频所需的短期交付地址。
/// </summary>
public sealed record AudioResourcePlaybackResponse(
    string Url,
    DateTimeOffset? ExpiresAt,
    double DurationSeconds);
