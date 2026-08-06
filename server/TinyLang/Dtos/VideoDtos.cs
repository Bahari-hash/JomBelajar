using System.ComponentModel.DataAnnotations;
using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述使用已激活课程视频资源创建视频的请求。
/// </summary>
public sealed record CreateVideoRequest
{
    public Guid SourceMediaResourceId { get; init; }
    public Guid? CoverMediaResourceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string OriginalLanguage { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
}

/// <summary>
/// 描述视频允许管理员修改的展示元数据。
/// </summary>
public sealed record UpdateVideoRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string OriginalLanguage { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
    public VideoCoverAction CoverAction { get; init; } = VideoCoverAction.Keep;
    public Guid? CoverMediaResourceId { get; init; }
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述发布、下架、重试和归档视频所需的并发前置条件。
/// </summary>
public sealed record VideoMutationRequest
{
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述管理员视频列表的分页、关键词和状态筛选。
/// </summary>
public sealed record AdminVideoListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public VideoProcessingStatus? ProcessingStatus { get; init; }
    public VideoPublicationStatus? PublicationStatus { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? CreatedById { get; init; }
}

/// <summary>
/// 描述登录用户视频目录的分页和关键词筛选。
/// </summary>
public sealed record VideoCatalogRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public Guid? CategoryId { get; init; }
}

/// <summary>
/// 描述登录用户可见视频分类列表的分页和关键词条件。
/// </summary>
public record VideoCategoryListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
}

/// <summary>
/// 描述管理员视频分类列表及是否包含停用分类的条件。
/// </summary>
public sealed record AdminVideoCategoryListRequest : VideoCategoryListRequest
{
    public bool IncludeInactive { get; init; }
}

/// <summary>
/// 描述管理员创建视频分类的请求。
/// </summary>
public sealed record CreateVideoCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// 描述管理员更新视频分类内容和启用状态的请求。
/// </summary>
public sealed record UpdateVideoCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// 描述当前登录用户上报的播放位置。
/// </summary>
public sealed record UpdateVideoProgressRequest
{
    public double PositionSeconds { get; init; }
}

/// <summary>
/// 返回管理员可见的视频处理和发布状态摘要。
/// </summary>
public sealed record AdminVideoListItemResponse(
    Guid Id,
    string Title,
    string OriginalLanguage,
    VideoProcessingStatus ProcessingStatus,
    VideoPublicationStatus PublicationStatus,
    double? DurationSeconds,
    string? FailureCode,
    IReadOnlyList<AdminVideoCategorySummaryResponse> Categories,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    Guid ConcurrencyStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    VideoProcessingJobSummaryResponse? LatestJob);

/// <summary>
/// 返回已确认视频封面的非敏感摘要。
/// </summary>
public sealed record VideoCoverSummaryResponse(
    Guid Id,
    string OriginalName,
    string Url);

/// <summary>
/// 返回内容管理审计所需的最小用户资料。
/// </summary>
public sealed record ContentAuditUserResponse(
    Guid Id,
    string? Nickname,
    string? AvatarUrl);

/// <summary>
/// 返回不含租约、对象路径或内部异常的视频处理任务摘要。
/// </summary>
public sealed record VideoProcessingJobSummaryResponse(
    Guid Id,
    VideoProcessingJobStatus Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode);

/// <summary>
/// 返回普通用户可见的视频分类摘要。
/// </summary>
public sealed record VideoCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug);

/// <summary>
/// 返回视频页面展示所需的发布用户摘要。
/// </summary>
/// <param name="Id">用户标识。</param>
/// <param name="Nickname">用户昵称。</param>
/// <param name="AvatarUrl">用户头像地址。</param>
public sealed record VideoUserSummaryResponse(
    Guid Id,
    string? Nickname,
    string? AvatarUrl);

/// <summary>
/// 返回管理员管理视频关联的分类摘要及启用状态。
/// </summary>
public sealed record AdminVideoCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive);

/// <summary>
/// 返回视频分类的管理字段、关联视频数量和创建时间。
/// </summary>
public sealed record VideoCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int VideoCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// 返回显式清空视频分类关联的结果。
/// </summary>
public sealed record ClearVideoCategoryResponse(
    Guid CategoryId,
    int RemovedVideoCount);

/// <summary>
/// 返回一个实际生成的视频清晰度。
/// </summary>
public sealed record VideoRenditionResponse(
    int TargetHeight,
    int Width,
    int Height,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    string Codecs);

/// <summary>
/// 返回管理员管理视频所需的完整业务状态，不包含存储凭据。
/// </summary>
public sealed record AdminVideoResponse(
    Guid Id,
    Guid SourceMediaResourceId,
    VideoCoverSummaryResponse? Cover,
    string Title,
    string? Description,
    string OriginalLanguage,
    VideoProcessingStatus ProcessingStatus,
    VideoPublicationStatus PublicationStatus,
    double? DurationSeconds,
    int? DisplayWidth,
    int? DisplayHeight,
    string? ContainerFormat,
    string? VideoCodec,
    string? AudioCodec,
    string? FailureCode,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<VideoRenditionResponse> Renditions,
    IReadOnlyList<AdminVideoCategorySummaryResponse> Categories,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    Guid ConcurrencyStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    VideoProcessingJobSummaryResponse? LatestJob);

/// <summary>
/// 返回登录用户视频目录中的展示摘要。
/// </summary>
public sealed record VideoCatalogItemResponse(
    Guid Id,
    string Title,
    string? Description,
    string OriginalLanguage,
    double DurationSeconds,
    VideoUserSummaryResponse Author,
    string? CoverUrl,
    DateTimeOffset PublishedAt,
    IReadOnlyList<VideoCategorySummaryResponse> Categories);

/// <summary>
/// 返回登录用户可见的已发布视频详情。
/// </summary>
public sealed record VideoDetailsResponse(
    Guid Id,
    string Title,
    string? Description,
    string OriginalLanguage,
    double DurationSeconds,
    int DisplayWidth,
    int DisplayHeight,
    VideoUserSummaryResponse Author,
    string? CoverUrl,
    DateTimeOffset PublishedAt,
    IReadOnlyList<VideoCategorySummaryResponse> Categories);

/// <summary>
/// 返回短期播放地址、poster 和当前用户续播位置。
/// </summary>
public sealed record VideoPlaybackResponse(
    string MasterPlaylistUrl,
    string? PosterUrl,
    DateTimeOffset? ExpiresAt,
    double DurationSeconds,
    double PositionSeconds,
    bool IsCompleted);
