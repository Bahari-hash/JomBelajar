using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义管理员视频管理、登录目录、播放授权和进度用例。
/// </summary>
public interface IVideoService
{
    /// <summary>
    /// 使用当前管理员的 Active CourseVideo 资源创建视频和首个处理任务。
    /// </summary>
    Task<AdminVideoResponse> CreateAsync(
        Guid adminId,
        CreateVideoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回所有管理员协作维护的视频管理分页列表。
    /// </summary>
    Task<PagedResponse<AdminVideoListItemResponse>> GetAdminListAsync(
        AdminVideoListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回全局视频管理详情。
    /// </summary>
    Task<AdminVideoResponse> GetAdminByIdAsync(
        Guid videoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为管理员返回任意发布状态下已转码视频的短期预览地址。
    /// </summary>
    Task<VideoPlaybackResponse> GetAdminPlaybackAsync(
        Guid videoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新当前管理员视频的展示元数据。
    /// </summary>
    Task<AdminVideoResponse> UpdateAsync(
        Guid videoId,
        Guid adminId,
        UpdateVideoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等发布一个已经处理就绪的视频。
    /// </summary>
    Task<AdminVideoResponse> PublishAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架当前管理员已经发布的视频。
    /// </summary>
    Task<AdminVideoResponse> UnpublishAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为失败视频创建新输出版本和持久化任务。
    /// </summary>
    Task<AdminVideoResponse> RetryAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 软归档一个未发布且没有活动处理任务的视频。
    /// </summary>
    Task<AdminVideoResponse> ArchiveAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
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
    /// 为登录用户返回短期 HLS、poster 地址和续播位置。
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
