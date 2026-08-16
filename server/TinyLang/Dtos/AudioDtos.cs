using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

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
