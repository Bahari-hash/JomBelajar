using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述视频对象使用开发直连或 CDN HMAC 路径签名的交付配置。
/// </summary>
public sealed record VideoDeliverySettings
{
    public const string SectionName = "VideoDeliverySettings";

    [Required]
    public string Mode { get; init; } = "DirectDevelopment";

    [Url]
    public string? CdnBaseUrl { get; init; }

    [MaxLength(64)]
    public string? KeyId { get; init; }

    public string? SigningSecret { get; init; }

    [Range(60, 3600)]
    public int TokenTtlSeconds { get; init; } = 300;
}
