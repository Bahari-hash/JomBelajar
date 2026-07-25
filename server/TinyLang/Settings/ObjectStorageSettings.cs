using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述 S3-compatible 对象存储的连接、桶和公开访问配置。
/// </summary>
public sealed record ObjectStorageSettings
{
    public const string SectionName = "ObjectStorageSettings";

    public string? ServiceUrl { get; init; }

    [Required]
    public required string Region { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    [Required]
    public required string Bucket { get; init; }

    public bool ForcePathStyle { get; init; }

    [Required, Url]
    public required string PublicBaseUrl { get; init; }

    [Range(60, 604800)]
    public int PresignedUrlExpirySeconds { get; init; } = 900;
}
