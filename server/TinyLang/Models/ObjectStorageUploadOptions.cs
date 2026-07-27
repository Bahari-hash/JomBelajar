namespace TinyLang.Models;

/// <summary>
/// 描述上传衍生对象时使用的供应商无关 HTTP metadata。
/// </summary>
/// <param name="ContentType">对象媒体类型。</param>
/// <param name="CacheControl">对象缓存策略。</param>
/// <param name="Metadata">可选的通用自定义 metadata。</param>
public sealed record ObjectStorageUploadOptions(
    string ContentType,
    string CacheControl,
    IReadOnlyDictionary<string, string>? Metadata = null);
