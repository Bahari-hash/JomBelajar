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
