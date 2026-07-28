namespace TinyLang.Models;

/// <summary>
/// 描述 ffprobe 验证后的源音频媒体属性。
/// </summary>
public sealed record AudioProbeResult(
    double DurationSeconds,
    int SampleRate,
    int Channels,
    string ContainerFormat,
    string Codec);

/// <summary>
/// 描述 FFmpeg 生成的单个本地 MP3 输出。
/// </summary>
public sealed record AudioTranscodeResult(string OutputPath);
