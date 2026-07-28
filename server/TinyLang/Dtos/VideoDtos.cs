using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述使用已激活课程视频资源创建视频的请求。
/// </summary>
public sealed record CreateVideoRequest
{
    public Guid SourceMediaResourceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string OriginalLanguage { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
}

/// <summary>
/// 描述视频允许编辑者修改的展示元数据。
/// </summary>
public sealed record UpdateVideoRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string OriginalLanguage { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
}

/// <summary>
/// 描述编辑者视频列表的分页、关键词和状态筛选。
/// </summary>
public sealed record EditorVideoListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public VideoProcessingStatus? ProcessingStatus { get; init; }
    public VideoPublicationStatus? PublicationStatus { get; init; }
    public Guid? CategoryId { get; init; }
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
/// 描述编辑者为视频关联 WebVTT 上传资源的请求。
/// </summary>
public sealed record AddVideoSubtitleRequest
{
    public Guid MediaResourceId { get; init; }
    public required string LanguageTag { get; init; }
    public required string DisplayName { get; init; }
    public bool IsDefault { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述当前登录用户上报的播放位置。
/// </summary>
public sealed record UpdateVideoProgressRequest
{
    public double PositionSeconds { get; init; }
}

/// <summary>
/// 返回编辑者可见的视频处理和发布状态摘要。
/// </summary>
public sealed record EditorVideoListItemResponse(
    Guid Id,
    string Title,
    string OriginalLanguage,
    VideoProcessingStatus ProcessingStatus,
    VideoPublicationStatus PublicationStatus,
    double? DurationSeconds,
    string? FailureCode,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<EditorVideoCategorySummaryResponse> Categories);

/// <summary>
/// 返回普通用户可见的视频分类摘要。
/// </summary>
public sealed record VideoCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug);

/// <summary>
/// 返回编辑者管理视频关联的分类摘要及启用状态。
/// </summary>
public sealed record EditorVideoCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive);

/// <summary>
/// 返回视频分类的管理字段和关联视频数量。
/// </summary>
public sealed record VideoCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int VideoCount);

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
/// 返回编辑者管理字幕所需的稳定字段。
/// </summary>
public sealed record EditorVideoSubtitleResponse(
    Guid Id,
    Guid MediaResourceId,
    string LanguageTag,
    string DisplayName,
    bool IsDefault,
    int SortOrder);

/// <summary>
/// 返回编辑者管理视频所需的完整业务状态，不包含存储凭据。
/// </summary>
public sealed record EditorVideoResponse(
    Guid Id,
    Guid SourceMediaResourceId,
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
    IReadOnlyList<VideoRenditionResponse> Renditions,
    IReadOnlyList<EditorVideoSubtitleResponse> Subtitles,
    IReadOnlyList<EditorVideoCategorySummaryResponse> Categories,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回登录用户视频目录中的展示摘要。
/// </summary>
public sealed record VideoCatalogItemResponse(
    Guid Id,
    string Title,
    string? Description,
    string OriginalLanguage,
    double DurationSeconds,
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
    DateTimeOffset PublishedAt,
    IReadOnlyList<VideoCategorySummaryResponse> Categories);

/// <summary>
/// 返回播放器使用的一条带临时授权字幕地址。
/// </summary>
public sealed record VideoPlaybackSubtitleResponse(
    string LanguageTag,
    string DisplayName,
    bool IsDefault,
    string Url);

/// <summary>
/// 返回短期播放地址、poster、字幕和当前用户续播位置。
/// </summary>
public sealed record VideoPlaybackResponse(
    string MasterPlaylistUrl,
    string? PosterUrl,
    DateTimeOffset ExpiresAt,
    double DurationSeconds,
    double PositionSeconds,
    bool IsCompleted,
    IReadOnlyList<VideoPlaybackSubtitleResponse> Subtitles);
