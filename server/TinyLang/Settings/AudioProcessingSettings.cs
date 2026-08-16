using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述短音频探测、MP3 输出和持久化 worker 的安全运行参数。
/// </summary>
public sealed record AudioProcessingSettings
{
    public const string SectionName = "AudioProcessingSettings";

    [Required]
    public string FfprobePath { get; init; } = "ffprobe";

    [Required]
    public string FfmpegPath { get; init; } = "ffmpeg";

    [Range(1, 3600)]
    public int MaxSourceDurationSeconds { get; init; } = 600;

    [Range(8000, 384000)]
    public int MaxSourceSampleRate { get; init; } = 192000;

    [Range(1, 32)]
    public int MaxSourceChannels { get; init; } = 8;

    [Range(8000, 192000)]
    public int OutputSampleRate { get; init; } = 44100;

    [Range(1, 8)]
    public int OutputChannels { get; init; } = 1;

    [Range(32, 320)]
    public int OutputBitrateKbps { get; init; } = 96;

    [Range(-70, -5)]
    public double LoudnessTargetLufs { get; init; } = -16;

    [Range(-10, 0)]
    public double TruePeakDb { get; init; } = -1.5;

    [Range(1, 20)]
    public double LoudnessRange { get; init; } = 11;

    [Range(5, 600)]
    public int ProbeTimeoutSeconds { get; init; } = 60;

    [Range(30, 7200)]
    public int TranscodeTimeoutSeconds { get; init; } = 1800;

    [Range(1, 300)]
    public int PollingIntervalSeconds { get; init; } = 5;

    [Range(1, 3600)]
    public int DispatchThrottleSeconds { get; init; } = 30;

    [Range(60, 86400)]
    public int LeaseSeconds { get; init; } = 2400;

    [Range(1, 3600)]
    public int HeartbeatIntervalSeconds { get; init; } = 60;

    [Range(1, 100)]
    public int BatchSize { get; init; } = 8;

    [Range(1, 16)]
    public int MaxConcurrency { get; init; } = 2;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 3;

    [Range(1, 1440)]
    public int RetryMaxDelayMinutes { get; init; } = 60;

    [Required]
    public string TemporaryDirectory { get; init; } = "audio-processing-temp";

    [Range(1, 102400)]
    public int MinimumFreeDiskMB { get; init; } = 256;
}
