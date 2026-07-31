namespace TinyLang.Models;

/// <summary>
/// 返回媒体对象访问地址及可选的 UTC 失效时间。
/// </summary>
/// <param name="Url">外部访问地址。</param>
/// <param name="ExpiresAt">地址失效时间。</param>
public sealed record VideoDeliveryUrl(string Url, DateTimeOffset? ExpiresAt);
