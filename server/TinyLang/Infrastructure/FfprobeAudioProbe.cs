using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 通过 ffprobe JSON 验证源音频流、时长、采样率、声道和视频流限制。
/// </summary>
public sealed class FfprobeAudioProbe : IAudioProbe
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMediaProcessRunner _processRunner;
    private readonly AudioProcessingSettings _settings;

    /// <summary>
    /// 使用受控进程执行器和源音频限制创建探测器。
    /// </summary>
    public FfprobeAudioProbe(
        IMediaProcessRunner processRunner,
        IOptions<AudioProcessingSettings> options)
    {
        _processRunner = processRunner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<AudioProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        MediaProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(
                _settings.FfprobePath,
                [
                    "-v", "error",
                    "-print_format", "json",
                    "-show_format",
                    "-show_streams",
                    inputPath
                ],
                TimeSpan.FromSeconds(_settings.ProbeTimeoutSeconds),
                cancellationToken);
        }
        catch (MediaProcessException exception)
        {
            throw MapProcessException(exception);
        }
        if (result.ExitCode != 0)
        {
            throw Permanent(AudioProcessingFailureCode.ProbeFailed);
        }

        FfprobeDocument document;
        try
        {
            document = JsonSerializer.Deserialize<FfprobeDocument>(
                result.StandardOutput,
                SerializerOptions) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Permanent(AudioProcessingFailureCode.ProbeFailed);
        }

        var streams = document.Streams ?? [];
        if (streams.Any(stream =>
            string.Equals(stream.CodecType, "video", StringComparison.Ordinal) &&
            stream.Disposition?.AttachedPicture != 1))
        {
            throw Permanent(AudioProcessingFailureCode.VideoStreamPresent);
        }
        var audio = streams.FirstOrDefault(stream =>
            string.Equals(stream.CodecType, "audio", StringComparison.Ordinal))
            ?? throw Permanent(AudioProcessingFailureCode.AudioStreamMissing);
        var duration = ParseDouble(document.Format?.Duration) ??
            ParseDouble(audio.Duration);
        if (duration is null || !double.IsFinite(duration.Value) || duration <= 0)
        {
            throw Permanent(AudioProcessingFailureCode.DurationInvalid);
        }
        if (duration > _settings.MaxSourceDurationSeconds)
        {
            throw Permanent(AudioProcessingFailureCode.SourceDurationExceeded);
        }
        if (!int.TryParse(
            audio.SampleRate,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var sampleRate) || sampleRate <= 0)
        {
            throw Permanent(AudioProcessingFailureCode.SampleRateInvalid);
        }
        if (sampleRate > _settings.MaxSourceSampleRate)
        {
            throw Permanent(AudioProcessingFailureCode.SourceSampleRateExceeded);
        }
        if (audio.Channels <= 0)
        {
            throw Permanent(AudioProcessingFailureCode.ChannelsInvalid);
        }
        if (audio.Channels > _settings.MaxSourceChannels)
        {
            throw Permanent(AudioProcessingFailureCode.SourceChannelsExceeded);
        }

        return new AudioProbeResult(
            duration.Value,
            sampleRate,
            audio.Channels,
            NormalizeMetadata(document.Format?.FormatName?.Split(',')[0], 100),
            NormalizeMetadata(audio.CodecName, 64));
    }

    /// <summary>
    /// 将通用进程失败映射为音频稳定失败码。
    /// </summary>
    private static AudioProcessingException MapProcessException(
        MediaProcessException exception)
        => new(
            exception.FailureKind == MediaProcessFailureKind.TimedOut
                ? AudioProcessingFailureCode.ProcessTimedOut
                : AudioProcessingFailureCode.ProcessStartFailed,
            isTransient: true);

    /// <summary>
    /// 创建不可自动重试的源媒体校验失败。
    /// </summary>
    private static AudioProcessingException Permanent(
        AudioProcessingFailureCode failureCode)
        => new(failureCode, isTransient: false);

    /// <summary>
    /// 使用不受当前区域影响的格式解析有限媒体数值。
    /// </summary>
    private static double? ParseDouble(string? value)
        => double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// 将不可信 ffprobe 文本压缩到数据库字段允许的稳定边界。
    /// </summary>
    private static string NormalizeMetadata(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    /// <summary>
    /// 映射 ffprobe 顶层 streams 和 format JSON。
    /// </summary>
    private sealed record FfprobeDocument(
        [property: JsonPropertyName("streams")] IReadOnlyList<FfprobeStream>? Streams,
        [property: JsonPropertyName("format")] FfprobeFormat? Format);

    /// <summary>
    /// 映射音频探测所需的 ffprobe stream 字段。
    /// </summary>
    private sealed record FfprobeStream(
        [property: JsonPropertyName("codec_type")] string? CodecType,
        [property: JsonPropertyName("codec_name")] string? CodecName,
        [property: JsonPropertyName("sample_rate")] string? SampleRate,
        [property: JsonPropertyName("channels")] int Channels,
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("disposition")] FfprobeDisposition? Disposition);

    /// <summary>
    /// 映射用于区分封面图和普通视频流的 disposition 字段。
    /// </summary>
    private sealed record FfprobeDisposition(
        [property: JsonPropertyName("attached_pic")] int AttachedPicture);

    /// <summary>
    /// 映射 ffprobe format 的时长和容器摘要。
    /// </summary>
    private sealed record FfprobeFormat(
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("format_name")] string? FormatName);
}
