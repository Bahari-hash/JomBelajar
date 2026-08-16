using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义独立音频资源的管理员维护和登录用户播放契约。
/// </summary>
public interface IAudioResourceService
{
    Task<AudioUploadInitializationResponse> InitializeSimpleUploadAsync(
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<AudioUploadInitializationResponse> InitializeMultipartUploadAsync(
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default);

    Task ConfirmUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<AdminAudioResourceListItemResponse>> GetAdminListAsync(
        AdminAudioResourceListRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminAudioResourceResponse> GetAdminByIdAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default);

    Task<AdminAudioResourceResponse> RenameAsync(
        Guid audioResourceId,
        Guid adminId,
        RenameAudioResourceRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminAudioResourceResponse> RetryUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminAudioResourceResponse> ReprocessAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default);

    Task<AudioResourcePlaybackResponse> GetPlaybackAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default);
}
