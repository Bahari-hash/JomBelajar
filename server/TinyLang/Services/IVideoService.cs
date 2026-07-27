using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义编辑者视频管理、字幕、登录目录、播放授权和进度用例。
/// </summary>
public interface IVideoService
{
    /// <summary>
    /// 使用当前编辑者的 Active CourseVideo 资源创建视频和首个处理任务。
    /// </summary>
    Task<EditorVideoResponse> CreateAsync(
        Guid editorId,
        CreateVideoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回当前编辑者拥有的视频管理分页列表。
    /// </summary>
    Task<PagedResponse<EditorVideoListItemResponse>> GetEditorListAsync(
        Guid editorId,
        EditorVideoListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回当前编辑者拥有的视频管理详情。
    /// </summary>
    Task<EditorVideoResponse> GetEditorByIdAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新当前编辑者视频的展示元数据。
    /// </summary>
    Task<EditorVideoResponse> UpdateAsync(
        Guid videoId,
        Guid editorId,
        UpdateVideoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等发布一个已经处理就绪的视频。
    /// </summary>
    Task<EditorVideoResponse> PublishAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架当前编辑者已经发布的视频。
    /// </summary>
    Task<EditorVideoResponse> UnpublishAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为失败视频创建新输出版本和持久化任务。
    /// </summary>
    Task<EditorVideoResponse> RetryAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验并关联当前编辑者上传的 Active WebVTT 字幕。
    /// </summary>
    Task<EditorVideoSubtitleResponse> AddSubtitleAsync(
        Guid videoId,
        Guid editorId,
        AddVideoSubtitleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除当前编辑者视频的一条字幕关联并幂等删除其对象。
    /// </summary>
    Task RemoveSubtitleAsync(
        Guid videoId,
        Guid subtitleId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回登录用户可见的已发布且就绪视频目录。
    /// </summary>
    Task<PagedResponse<VideoCatalogItemResponse>> GetCatalogAsync(
        VideoCatalogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回登录用户可见的已发布且就绪视频详情。
    /// </summary>
    Task<VideoDetailsResponse> GetDetailsAsync(
        Guid videoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为登录用户返回短期 HLS、poster、字幕地址和续播位置。
    /// </summary>
    Task<VideoPlaybackResponse> GetPlaybackAsync(
        Guid videoId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子更新登录用户的播放位置和服务端完成状态。
    /// </summary>
    Task UpdateProgressAsync(
        Guid videoId,
        Guid userId,
        UpdateVideoProgressRequest request,
        CancellationToken cancellationToken = default);
}
