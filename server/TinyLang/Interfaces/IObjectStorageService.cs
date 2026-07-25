namespace TinyLang.Interfaces;

using TinyLang.Models;

/// <summary>
/// 定义媒体对象上传、查询、移动和公开寻址的存储契约。
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// 为指定对象和文件元数据创建临时 PUT 上传地址。
    /// </summary>
    /// <param name="objectName">目标对象名称。</param>
    /// <param name="contentType">客户端上传时必须使用的媒体类型。</param>
    /// <param name="size">客户端上传时必须使用的对象大小。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>对象存储预签名上传地址。</returns>
    Task<string> PresignPutObjectAsync(
        string objectName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取指定对象的大小和媒体类型。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>对象元数据；对象不存在时返回 <see langword="null"/>。</returns>
    Task<ObjectStorageMetadata?> GetObjectMetadataAsync(
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将对象复制到新名称并删除源对象。
    /// </summary>
    /// <param name="sourceObjectName">源对象名称。</param>
    /// <param name="destinationObjectName">目标对象名称。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>表示异步移动操作的任务。</returns>
    Task MoveObjectAsync(
        string sourceObjectName,
        string destinationObjectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据公开基础地址构建对象的外部访问 URL。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <returns>对象的公开 URL。</returns>
    string GetPublicUrl(string objectName);
}
