namespace TinyLang.Services;

/// <summary>
/// 定义媒体上传后台归档、过期终止和遗留 staging 清理边界。
/// </summary>
public interface IMediaUploadMaintenanceService
{
    /// <summary>
    /// 处理一批已完成 Multipart 协议但尚未归档的媒体资源。
    /// </summary>
    /// <param name="cancellationToken">用于停止批处理的令牌。</param>
    /// <returns>成功激活的资源数量。</returns>
    Task<int> FinalizeBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理一批已过期上传及已激活资源遗留的 staging 对象。
    /// </summary>
    /// <param name="cancellationToken">用于停止批处理的令牌。</param>
    /// <returns>完成清理的资源数量。</returns>
    Task<int> CleanupBatchAsync(CancellationToken cancellationToken = default);
}
