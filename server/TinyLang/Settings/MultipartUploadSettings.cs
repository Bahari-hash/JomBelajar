using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述 Multipart Upload、用户配额、后台归档和过期清理参数。
/// </summary>
public sealed record MultipartUploadSettings
{
    public const string SectionName = "MultipartUploadSettings";

    [Range(5, 512)]
    public int ThresholdMB { get; init; } = 64;

    [Range(5, 512)]
    public int PartSizeMB { get; init; } = 16;

    [Range(1, 10000)]
    public int MaxPartCount { get; init; } = 10000;

    [Range(1, 100)]
    public int PartPresignBatchLimit { get; init; } = 20;

    [Range(5, 10080)]
    public int SessionTtlMinutes { get; init; } = 1440;

    [Range(5, 3600)]
    public int CleanupIntervalSeconds { get; init; } = 60;

    [Range(1, 500)]
    public int CleanupBatchSize { get; init; } = 50;

    [Range(30, 3600)]
    public int FinalizationLeaseSeconds { get; init; } = 300;

    [Range(1, 100)]
    public int FinalizationMaxAttempts { get; init; } = 10;

    [Range(1, 1440)]
    public int RetryMaxDelayMinutes { get; init; } = 60;

    [Range(1, 1000)]
    public int MaxIncompleteUploadCountPerUser { get; init; } = 10;

    [Range(1, int.MaxValue)]
    public int MaxIncompleteUploadMBPerUser { get; init; } = 2048;
}
