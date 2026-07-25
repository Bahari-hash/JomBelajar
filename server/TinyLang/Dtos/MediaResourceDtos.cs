using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述申请头像上传预签名地址时提交的文件元数据。
/// </summary>
public sealed record AvatarPresignRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// 描述编辑者申请业务媒体上传预签名地址时提交的文件元数据。
/// </summary>
public sealed record EditorMediaPresignRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public ResourceModule Module { get; init; }
}

/// <summary>
/// 描述编辑者创建 Multipart Upload 会话时提交的媒体元数据。
/// </summary>
public sealed record MultipartUploadRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public ResourceModule Module { get; init; }
}

/// <summary>
/// 描述一次批量 part 预签名请求。
/// </summary>
public sealed record MultipartPartPresignRequest
{
    public required IReadOnlyList<int> PartNumbers { get; init; }
}

/// <summary>
/// 描述客户端完成一个 part 后取得的 ETag。
/// </summary>
/// <param name="PartNumber">从 1 开始的 part 编号。</param>
/// <param name="ETag">provider 在上传响应中返回的 ETag。</param>
public sealed record CompletedMultipartPartRequest(int PartNumber, string ETag);

/// <summary>
/// 描述提交给 Multipart complete 操作的完整 part 列表。
/// </summary>
public sealed record CompleteMultipartUploadRequest
{
    public required IReadOnlyList<CompletedMultipartPartRequest> Parts { get; init; }
}

/// <summary>
/// 返回待上传资源及其对象存储预签名地址。
/// </summary>
/// <param name="ResourceId">待确认媒体资源的标识。</param>
/// <param name="PresignedUrl">客户端上传对象的临时地址。</param>
/// <param name="ObjectName">对象存储中的临时对象名称。</param>
public sealed record PresignResponse(
    Guid ResourceId,
    string PresignedUrl,
    string ObjectName);

/// <summary>
/// 返回新建 Multipart Upload 会话的应用级布局。
/// </summary>
public sealed record MultipartUploadCreateResponse(
    Guid ResourceId,
    Guid SessionId,
    long PartSize,
    int PartCount,
    DateTimeOffset ExpiresAt);

/// <summary>
/// 返回一个 part 的预签名地址及客户端必须发送的长度。
/// </summary>
public sealed record MultipartPartPresignResponse(
    int PartNumber,
    string PresignedUrl,
    long ContentLength,
    DateTimeOffset ExpiresAt);

/// <summary>
/// 返回 provider 已接收的 part 摘要。
/// </summary>
public sealed record UploadedMultipartPartResponse(
    int PartNumber,
    string ETag,
    long? Size);

/// <summary>
/// 返回 Multipart Upload 会话状态，不暴露 provider upload ID。
/// </summary>
public sealed record MultipartUploadStatusResponse(
    Guid ResourceId,
    Guid SessionId,
    MultipartUploadStatus Status,
    long PartSize,
    int PartCount,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<UploadedMultipartPartResponse> UploadedParts);

/// <summary>
/// 返回媒体资源的持久化状态和公开访问信息。
/// </summary>
/// <param name="Id">媒体资源标识。</param>
/// <param name="UploaderId">上传用户标识。</param>
/// <param name="ObjectName">对象存储中的对象名称。</param>
/// <param name="OriginalName">客户端提交的原始文件名。</param>
/// <param name="Module">资源所属业务模块。</param>
/// <param name="Status">资源上传确认状态。</param>
/// <param name="Size">申报的文件大小，单位为字节。</param>
/// <param name="Extension">规范化后的扩展名。</param>
/// <param name="ContentType">规范化后的媒体类型。</param>
/// <param name="Url">确认上传后的公开地址。</param>
/// <param name="CreatedAt">资源记录创建时间。</param>
public sealed record MediaResourceResponse(
    Guid Id,
    Guid UploaderId,
    string ObjectName,
    string OriginalName,
    ResourceModule Module,
    ResourceStatus Status,
    long Size,
    string Extension,
    string ContentType,
    string? Url,
    DateTimeOffset CreatedAt);
