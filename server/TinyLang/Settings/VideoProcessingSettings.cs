using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述 ffprobe、FFmpeg、HLS 输出和持久化 worker 的安全运行参数。
/// </summary>
public sealed record VideoProcessingSettings
{
    public const string SectionName = "VideoProcessingSettings";

    [Required]
    public string FfprobePath { get; init; } = "ffprobe";

    [Required]
    public string FfmpegPath { get; init; } = "ffmpeg";

    [Range(1, 8192)]
    public int MaxSourceWidth { get; init; } = 1980;

    [Range(1, 8192)]
    public int MaxSourceHeight { get; init; } = 1080;

    [Range(5, 600)]
    public int ProbeTimeoutSeconds { get; init; } = 60;

    [Range(60, 86400)]
    public int TranscodeTimeoutSeconds { get; init; } = 7200;

    [Range(1, 300)]
    public int PollingIntervalSeconds { get; init; } = 5;

    [Range(1, 3600)]
    public int DispatchThrottleSeconds { get; init; } = 30;

    [Range(60, 86400)]
    public int LeaseSeconds { get; init; } = 10800;

    [Range(1, 100)]
    public int BatchSize { get; init; } = 4;

    [Range(1, 16)]
    public int MaxConcurrency { get; init; } = 1;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 3;

    [Range(1, 1440)]
    public int RetryMaxDelayMinutes { get; init; } = 60;

    [Required]
    public string TemporaryDirectory { get; init; } = "video-processing-temp";

    [Range(1, 102400)]
    public int MinimumFreeDiskMB { get; init; } = 2048;

    [Range(100, 50000)]
    public int VideoBitrate480Kbps { get; init; } = 1200;

    [Range(100, 50000)]
    public int VideoBitrate720Kbps { get; init; } = 2500;

    [Range(100, 50000)]
    public int VideoBitrate1080Kbps { get; init; } = 4500;

    [Range(32, 512)]
    public int AudioBitrateKbps { get; init; } = 128;

    [Range(2, 15)]
    public int HlsSegmentSeconds { get; init; } = 6;

    [Range(1, 100)]
    public int PosterJpegQuality { get; init; } = 85;

    [Range(1024, 1048576)]
    public int MaxDiagnosticOutputCharacters { get; init; } = 32768;
}
