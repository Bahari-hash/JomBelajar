namespace TinyLang.Models;

/// <summary>
/// 描述待上传媒体资源及其对象存储预签名地址。
/// </summary>
/// <param name="ResourceId">待确认媒体资源的标识。</param>
/// <param name="PresignedUrl">客户端用于上传对象的临时地址。</param>
/// <param name="ObjectName">对象存储中的临时对象名称。</param>
public sealed record MediaResourcePresignResult(
    Guid ResourceId,
    string PresignedUrl,
    string ObjectName);
