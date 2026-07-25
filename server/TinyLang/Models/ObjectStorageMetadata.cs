namespace TinyLang.Models;

/// <summary>
/// 表示从对象存储读取的对象元数据。
/// </summary>
/// <param name="Size">对象大小，单位为字节。</param>
/// <param name="ContentType">对象的媒体类型；存储端未返回时为 <see langword="null"/>。</param>
public sealed record ObjectStorageMetadata(long Size, string? ContentType);
