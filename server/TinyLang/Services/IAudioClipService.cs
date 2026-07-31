using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义管理员音频管理和登录用户播放授权用例。
/// </summary>
public interface IAudioClipService
{
    /// <summary>
    /// 使用当前管理员的 Active Audio 资源创建音频和首个处理任务。
    /// </summary>
    Task<AdminAudioClipResponse> CreateAsync(
        Guid adminId,
        CreateAudioClipRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回所有管理员协作维护的音频管理分页列表。
    /// </summary>
    Task<PagedResponse<AdminAudioClipListItemResponse>> GetAdminListAsync(
        AdminAudioClipListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回全局音频管理详情。
    /// </summary>
    Task<AdminAudioClipResponse> GetAdminByIdAsync(
        Guid audioClipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新当前管理员音频的展示元数据。
    /// </summary>
    Task<AdminAudioClipResponse> UpdateAsync(
        Guid audioClipId,
        Guid adminId,
        UpdateAudioClipRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等发布一个已经处理就绪的音频。
    /// </summary>
    Task<AdminAudioClipResponse> PublishAsync(
        Guid audioClipId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架当前管理员已经发布的音频。
    /// </summary>
    Task<AdminAudioClipResponse> UnpublishAsync(
        Guid audioClipId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为失败音频创建新输出版本和持久化任务。
    /// </summary>
    Task<AdminAudioClipResponse> RetryAsync(
        Guid audioClipId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为登录用户返回已发布音频的短期 MP3 地址。
    /// </summary>
    Task<AudioPlaybackResponse> GetPlaybackAsync(
        Guid audioClipId,
        CancellationToken cancellationToken = default);
}
