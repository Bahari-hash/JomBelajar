using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义编辑者音频管理和登录用户播放授权用例。
/// </summary>
public interface IAudioClipService
{
    /// <summary>
    /// 使用当前编辑者的 Active Audio 资源创建音频和首个处理任务。
    /// </summary>
    Task<EditorAudioClipResponse> CreateAsync(
        Guid editorId,
        CreateAudioClipRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回当前编辑者拥有的音频管理分页列表。
    /// </summary>
    Task<PagedResponse<EditorAudioClipListItemResponse>> GetEditorListAsync(
        Guid editorId,
        EditorAudioClipListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回当前编辑者拥有的音频管理详情。
    /// </summary>
    Task<EditorAudioClipResponse> GetEditorByIdAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新当前编辑者音频的展示元数据。
    /// </summary>
    Task<EditorAudioClipResponse> UpdateAsync(
        Guid audioClipId,
        Guid editorId,
        UpdateAudioClipRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等发布一个已经处理就绪的音频。
    /// </summary>
    Task<EditorAudioClipResponse> PublishAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架当前编辑者已经发布的音频。
    /// </summary>
    Task<EditorAudioClipResponse> UnpublishAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为失败音频创建新输出版本和持久化任务。
    /// </summary>
    Task<EditorAudioClipResponse> RetryAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为登录用户返回已发布音频的短期 MP3 地址。
    /// </summary>
    Task<AudioPlaybackResponse> GetPlaybackAsync(
        Guid audioClipId,
        CancellationToken cancellationToken = default);
}
