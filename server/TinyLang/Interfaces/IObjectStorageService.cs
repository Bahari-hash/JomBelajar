using System.IO;

namespace TinyLang.Interfaces;

using TinyLang.Models;

/// <summary>
/// 定义媒体对象简单上传、Multipart Upload、归档和公开寻址的存储契约。
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
    /// 为指定对象创建在明确时间失效的临时 GET 地址。
    /// </summary>
    /// <param name="objectName">待读取的对象名称。</param>
    /// <param name="expiresAt">地址的 UTC 失效时间。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>对象存储预签名读取地址。</returns>
    Task<string> PresignGetObjectAsync(
        string objectName,
        DateTimeOffset expiresAt,
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
    /// 初始化指定对象的 S3-compatible Multipart Upload。
    /// </summary>
    /// <param name="objectName">由服务端生成的 staging 对象名称。</param>
    /// <param name="contentType">完成后的对象媒体类型。</param>
    /// <param name="cancellationToken">用于取消存储请求的令牌。</param>
    /// <returns>只应由服务端持久化的 provider upload ID。</returns>
    Task<string> CreateMultipartUploadAsync(
        string objectName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为 Multipart Upload 的指定 part 创建预签名 PUT 地址。
    /// </summary>
    /// <param name="objectName">staging 对象名称。</param>
    /// <param name="providerUploadId">服务端保存的 provider upload ID。</param>
    /// <param name="partNumber">从 1 开始的 part 编号。</param>
    /// <param name="contentLength">该 part 必须上传的字节数。</param>
    /// <param name="expiresAt">预签名地址不得超过的失效时间。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>part 上传地址。</returns>
    Task<string> PresignUploadPartAsync(
        string objectName,
        string providerUploadId,
        int partNumber,
        long contentLength,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出 provider 已接收的 Multipart parts，用于断点续传和恢复。
    /// </summary>
    /// <param name="objectName">staging 对象名称。</param>
    /// <param name="providerUploadId">服务端保存的 provider upload ID。</param>
    /// <param name="maxParts">允许返回的最大 part 数。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>按 part number 排序的已上传 parts。</returns>
    Task<IReadOnlyList<ObjectStorageUploadedPart>> ListUploadedPartsAsync(
        string objectName,
        string providerUploadId,
        int maxParts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用经过业务层验证和排序的 ETag 列表完成 Multipart Upload。
    /// </summary>
    /// <param name="objectName">staging 对象名称。</param>
    /// <param name="providerUploadId">服务端保存的 provider upload ID。</param>
    /// <param name="parts">按 part number 升序排列的完成凭据。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>表示 provider 完成操作的任务。</returns>
    Task CompleteMultipartUploadAsync(
        string objectName,
        string providerUploadId,
        IReadOnlyList<ObjectStorageUploadedPart> parts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止尚未完成的 Multipart Upload；provider 会话不存在时按幂等成功处理。
    /// </summary>
    /// <param name="objectName">staging 对象名称。</param>
    /// <param name="providerUploadId">服务端保存的 provider upload ID。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>表示 provider 终止操作的任务。</returns>
    Task AbortMultipartUploadAsync(
        string objectName,
        string providerUploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 staging 对象复制到唯一最终名称，不删除源对象。
    /// </summary>
    /// <param name="sourceObjectName">源对象名称。</param>
    /// <param name="destinationObjectName">最终对象名称。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>表示复制操作的任务。</returns>
    Task CopyObjectAsync(
        string sourceObjectName,
        string destinationObjectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等删除指定对象。
    /// </summary>
    /// <param name="objectName">待删除对象名称。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>表示删除操作的任务。</returns>
    Task DeleteObjectAsync(
        string objectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将指定对象流式复制到调用方提供的可写流。
    /// </summary>
    /// <param name="objectName">待读取的对象名称。</param>
    /// <param name="destination">接收对象内容的可写流。</param>
    /// <param name="cancellationToken">用于取消传输的令牌。</param>
    /// <returns>表示流式下载操作的任务。</returns>
    Task DownloadObjectAsync(
        string objectName,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从可读流上传具有准确长度和通用 HTTP metadata 的衍生对象。
    /// </summary>
    /// <param name="objectName">目标对象名称。</param>
    /// <param name="source">对象内容的可读流。</param>
    /// <param name="length">对象准确字节长度。</param>
    /// <param name="options">媒体类型、缓存策略和可选 metadata。</param>
    /// <param name="cancellationToken">用于取消传输的令牌。</param>
    /// <returns>表示流式上传操作的任务。</returns>
    Task UploadObjectAsync(
        string objectName,
        Stream source,
        long length,
        ObjectStorageUploadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在受控前缀下按对象名排序列出不超过指定上限的对象。
    /// </summary>
    /// <param name="prefix">由服务端生成且以斜杠结尾的对象前缀。</param>
    /// <param name="maxCount">允许返回的最大对象数量。</param>
    /// <param name="cancellationToken">用于取消列举的令牌。</param>
    /// <returns>按名称排序的对象名称。</returns>
    Task<IReadOnlyList<string>> ListObjectNamesAsync(
        string prefix,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等删除一组由服务端验证过的对象名称。
    /// </summary>
    /// <param name="objectNames">待删除对象名称。</param>
    /// <param name="cancellationToken">用于取消删除的令牌。</param>
    /// <returns>表示批量删除操作的任务。</returns>
    Task DeleteObjectsAsync(
        IReadOnlyCollection<string> objectNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据公开基础地址构建对象的外部访问 URL。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <returns>对象的公开 URL。</returns>
    string GetPublicUrl(string objectName);
}
