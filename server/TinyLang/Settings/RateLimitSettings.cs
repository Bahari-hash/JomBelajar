using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述上传、音视频播放/进度和全局请求的限流参数。
/// </summary>
public sealed record RateLimitSettings
{
    public const string SectionName = "RateLimitSettings";

    [Range(1, int.MaxValue)]
    public int UploadPresignTokenLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadPresignTokensPerPeriod { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadPresignReplenishmentPeriodSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandTokenLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandTokensPerPeriod { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandReplenishmentPeriodSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int VideoPlaybackTokenLimit { get; init; } = 30;

    [Range(1, int.MaxValue)]
    public int VideoPlaybackTokensPerPeriod { get; init; } = 30;

    [Range(1, int.MaxValue)]
    public int VideoPlaybackReplenishmentPeriodSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int AudioPlaybackTokenLimit { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int AudioPlaybackTokensPerPeriod { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int AudioPlaybackReplenishmentPeriodSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int VideoProgressTokenLimit { get; init; } = 120;

    [Range(1, int.MaxValue)]
    public int VideoProgressTokensPerPeriod { get; init; } = 120;

    [Range(1, int.MaxValue)]
    public int VideoProgressReplenishmentPeriodSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int GlobalFallbackPermitLimit { get; init; }

    [Range(0, int.MaxValue)]
    public int GlobalFallbackQueueLimit { get; init; }
}
