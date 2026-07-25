using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Models;

namespace TinyLang.Services;

/// <summary>
/// 定义媒体上传预签名和上传确认的业务契约。
/// </summary>
public interface IMediaResourceService
{
    /// <summary>
    /// 校验上传元数据，创建待确认资源并生成对象存储上传地址。
    /// </summary>
    /// <param name="uploaderId">上传用户标识。</param>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="size">文件大小，单位为字节。</param>
    /// <param name="contentType">文件媒体类型。</param>
    /// <param name="module">资源所属业务模块。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>待确认资源标识、对象名称和预签名上传地址。</returns>
    Task<MediaResourcePresignResult> CreatePendingResourceAndPresignAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建受服务端控制的媒体资源和 S3-compatible Multipart Upload 会话。
    /// </summary>
    /// <param name="uploaderId">上传用户标识。</param>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="size">文件总大小，单位为字节。</param>
    /// <param name="contentType">文件媒体类型。</param>
    /// <param name="module">资源所属业务模块。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>不包含 provider upload ID 的应用会话信息。</returns>
    Task<MultipartUploadCreateResult> CreateMultipartUploadAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为属于当前用户且仍有效的会话批量签发 part 上传地址。
    /// </summary>
    /// <param name="sessionId">应用上传会话标识。</param>
    /// <param name="uploaderId">执行操作的用户标识。</param>
    /// <param name="partNumbers">需要签名的唯一 part 编号。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>每个 part 的地址、长度和失效时间。</returns>
    Task<IReadOnlyList<MultipartPartPresignResult>> PresignMultipartPartsAsync(
        Guid sessionId,
        Guid uploaderId,
        IReadOnlyCollection<int> partNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询当前用户会话及 provider 已接收的 parts，用于断点续传。
    /// </summary>
    /// <param name="sessionId">应用上传会话标识。</param>
    /// <param name="uploaderId">查询用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>不泄漏 provider upload ID 的会话状态。</returns>
    Task<MultipartUploadStatusResult> GetMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成 provider Multipart Upload 并将资源提交给后台归档。
    /// </summary>
    /// <param name="sessionId">应用上传会话标识。</param>
    /// <param name="uploaderId">执行操作的用户标识。</param>
    /// <param name="parts">客户端从各 part 上传响应收集的 ETag。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>进入归档或已完成状态的会话信息。</returns>
    Task<MultipartUploadStatusResult> CompleteMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        IReadOnlyCollection<ObjectStorageUploadedPart> parts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等终止当前用户尚未完成的 Multipart Upload。
    /// </summary>
    /// <param name="sessionId">应用上传会话标识。</param>
    /// <param name="uploaderId">执行操作的用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示终止和持久化操作的任务。</returns>
    Task AbortMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证简单上传对象并激活资源；Multipart 归档未完成时不会同步复制大对象。
    /// </summary>
    /// <param name="resourceId">待确认媒体资源标识。</param>
    /// <param name="uploaderId">执行确认的上传用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>已激活的媒体资源。</returns>
    Task<MediaResource> ConfirmAsync(
        Guid resourceId,
        Guid uploaderId,
        CancellationToken cancellationToken = default);
}
