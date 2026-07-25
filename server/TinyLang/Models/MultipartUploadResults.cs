using TinyLang.Entities.Enums;

namespace TinyLang.Models;

/// <summary>
/// 返回新建 Multipart Upload 会话的应用级上传布局。
/// </summary>
/// <param name="ResourceId">媒体资源标识。</param>
/// <param name="SessionId">应用维护的上传会话标识。</param>
/// <param name="PartSize">除末片外每个 part 的字节数。</param>
/// <param name="PartCount">完成上传所需的 part 总数。</param>
/// <param name="ExpiresAt">会话失效时间。</param>
public sealed record MultipartUploadCreateResult(
    Guid ResourceId,
    Guid SessionId,
    long PartSize,
    int PartCount,
    DateTimeOffset ExpiresAt);

/// <summary>
/// 返回单个 Multipart part 的受限预签名信息。
/// </summary>
/// <param name="PartNumber">part 编号。</param>
/// <param name="PresignedUrl">上传该 part 的临时地址。</param>
/// <param name="ContentLength">客户端必须上传的字节数。</param>
/// <param name="ExpiresAt">预签名地址失效时间。</param>
public sealed record MultipartPartPresignResult(
    int PartNumber,
    string PresignedUrl,
    long ContentLength,
    DateTimeOffset ExpiresAt);

/// <summary>
/// 返回可用于恢复 Multipart Upload 的应用会话状态和已上传 parts。
/// </summary>
/// <param name="ResourceId">媒体资源标识。</param>
/// <param name="SessionId">应用上传会话标识。</param>
/// <param name="Status">当前会话状态。</param>
/// <param name="PartSize">除末片外每个 part 的字节数。</param>
/// <param name="PartCount">完成上传所需的 part 总数。</param>
/// <param name="ExpiresAt">会话失效时间。</param>
/// <param name="UploadedParts">对象存储中已存在的 parts。</param>
public sealed record MultipartUploadStatusResult(
    Guid ResourceId,
    Guid SessionId,
    MultipartUploadStatus Status,
    long PartSize,
    int PartCount,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ObjectStorageUploadedPart> UploadedParts);
