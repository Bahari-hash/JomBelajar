namespace TinyLang.Models;

/// <summary>
/// 表示对象存储中一个已上传 Multipart part 的完成凭据。
/// </summary>
/// <param name="PartNumber">从 1 开始的 part 编号。</param>
/// <param name="ETag">对象存储返回的 part ETag。</param>
/// <param name="Size">part 实际大小；客户端完成请求未提供时为 <see langword="null"/>。</param>
public sealed record ObjectStorageUploadedPart(int PartNumber, string ETag, long? Size = null);
