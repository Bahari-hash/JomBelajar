using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 通过 ffprobe JSON 探测真实媒体流并校验显示尺寸有效性、音视频流和时长。
/// </summary>
public sealed class FfprobeMediaProbe : IMediaProbe
{
    private const long MaxRationalComponent = 1_000_000;
    private const double AspectRatioTolerance = 0.01;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMediaProcessRunner _processRunner;
    private readonly VideoProcessingSettings _settings;

    /// <summary>
    /// 使用受控进程执行器创建媒体探测器。
    /// </summary>
    public FfprobeMediaProbe(
        IMediaProcessRunner processRunner,
        IOptions<VideoProcessingSettings> options)
    {
        _processRunner = processRunner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<MediaProbeResult> ProbeAsync(
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
            throw new VideoProcessingException(
                exception.FailureKind == MediaProcessFailureKind.TimedOut
                    ? VideoProcessingFailureCode.ProcessTimedOut
                    : VideoProcessingFailureCode.ProcessStartFailed,
                isTransient: true);
        }
        if (result.ExitCode != 0)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.ProbeFailed,
                isTransient: false);
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
            throw new VideoProcessingException(
                VideoProcessingFailureCode.ProbeFailed,
                isTransient: false);
        }

        var streams = document.Streams ?? [];
        var video = streams.FirstOrDefault(stream =>
            stream.CodecType == "video" &&
            stream.Disposition?.AttachedPicture != 1 &&
            stream.Width > 0 &&
            stream.Height > 0)
            ?? throw new VideoProcessingException(
                VideoProcessingFailureCode.VideoStreamMissing,
                isTransient: false);
        var audio = streams.FirstOrDefault(stream => stream.CodecType == "audio")
            ?? throw new VideoProcessingException(
                VideoProcessingFailureCode.AudioStreamMissing,
                isTransient: false);
        var duration = ParseDuration(document.Format?.Duration) ??
            ParseDuration(video.Duration);
        if (duration is null || !double.IsFinite(duration.Value) || duration <= 0)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.DurationInvalid,
                isTransient: false);
        }

        var displayDimensions = GetDisplayDimensions(video);
        var rotation = GetRotation(video);
        var swapsDimensions = Math.Abs(rotation) % 180 == 90;
        var displayWidthValue = swapsDimensions
            ? displayDimensions.Height
            : displayDimensions.Width;
        var displayHeightValue = swapsDimensions
            ? displayDimensions.Width
            : displayDimensions.Height;
        if (!double.IsFinite(displayWidthValue) ||
            !double.IsFinite(displayHeightValue) ||
            displayWidthValue <= 0 ||
            displayHeightValue <= 0 ||
            displayWidthValue > int.MaxValue ||
            displayHeightValue > int.MaxValue)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.DisplayDimensionsInvalid,
                isTransient: false);
        }
        var displayWidth = (int)Math.Ceiling(displayWidthValue);
        var displayHeight = (int)Math.Ceiling(displayHeightValue);
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.DisplayDimensionsInvalid,
                isTransient: false);
        }
        return new MediaProbeResult(
            duration.Value,
            displayWidth,
            displayHeight,
            document.Format?.FormatName?.Split(',')[0] ?? "unknown",
            video.CodecName ?? "unknown",
            audio.CodecName ?? "unknown");
    }

    /// <summary>
    /// 从 format 或 stream duration 文本解析有限秒数。
    /// </summary>
    private static double? ParseDuration(string? value)
        => double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var duration)
            ? duration
            : null;

    /// <summary>
    /// 使用 SAR 计算显示尺寸，并用 DAR 校验或补足缺失的像素宽高比。
    /// </summary>
    private static (double Width, double Height) GetDisplayDimensions(FfprobeStream stream)
    {
        var sampleAspectRatio = ParseRational(stream.SampleAspectRatio);
        var displayAspectRatio = ParseRational(stream.DisplayAspectRatio);
        var width = (double)stream.Width;
        var height = (double)stream.Height;

        if (sampleAspectRatio is { } sar)
        {
            width *= sar;
            if (displayAspectRatio is { } dar)
            {
                var calculatedDar = width / height;
                var relativeDifference = Math.Abs(calculatedDar - dar) / dar;
                if (!double.IsFinite(relativeDifference) ||
                    relativeDifference > AspectRatioTolerance)
                {
                    throw new VideoProcessingException(
                        VideoProcessingFailureCode.DisplayDimensionsInvalid,
                        isTransient: false);
                }
            }
        }
        else if (displayAspectRatio is { } dar)
        {
            width = height * dar;
        }

        return (width, height);
    }

    /// <summary>
    /// 解析 ffprobe 的有界正 rational；N/A、零值和畸形输入视为缺失。
    /// </summary>
    private static double? ParseRational(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = value.Split([':', '/'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator) ||
            numerator <= 0 ||
            denominator <= 0 ||
            numerator > MaxRationalComponent ||
            denominator > MaxRationalComponent)
        {
            return null;
        }

        var result = (double)numerator / denominator;
        return double.IsFinite(result) && result > 0 ? result : null;
    }

    /// <summary>
    /// 优先读取 side data rotation，并兼容常见 rotate tag。
    /// </summary>
    private static int GetRotation(FfprobeStream stream)
    {
        var sideDataRotation = (stream.SideDataList ?? [])
            .Select(value => value.Rotation)
            .FirstOrDefault(value => value is not null);
        if (sideDataRotation is not null)
        {
            return sideDataRotation.Value;
        }
        return stream.Tags?.TryGetValue("rotate", out var tag) == true &&
            int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rotation)
            ? rotation
            : 0;
    }

    /// <summary>
    /// 映射 ffprobe 顶层 streams 和 format JSON。
    /// </summary>
    private sealed record FfprobeDocument(
        [property: JsonPropertyName("streams")] IReadOnlyList<FfprobeStream>? Streams,
        [property: JsonPropertyName("format")] FfprobeFormat? Format);

    /// <summary>
    /// 映射媒体验证所需的单个 ffprobe stream 字段。
    /// </summary>
    private sealed record FfprobeStream(
        [property: JsonPropertyName("codec_type")] string? CodecType,
        [property: JsonPropertyName("codec_name")] string? CodecName,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height,
        [property: JsonPropertyName("sample_aspect_ratio")] string? SampleAspectRatio,
        [property: JsonPropertyName("display_aspect_ratio")] string? DisplayAspectRatio,
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("tags")] IReadOnlyDictionary<string, string>? Tags,
        [property: JsonPropertyName("disposition")] FfprobeDisposition? Disposition,
        [property: JsonPropertyName("side_data_list")] IReadOnlyList<FfprobeSideData>? SideDataList);

    /// <summary>
    /// 映射用于排除音频封面图 stream 的 disposition 字段。
    /// </summary>
    private sealed record FfprobeDisposition(
        [property: JsonPropertyName("attached_pic")] int AttachedPicture);

    /// <summary>
    /// 映射 ffprobe side data 中的旋转角度。
    /// </summary>
    private sealed record FfprobeSideData(
        [property: JsonPropertyName("rotation")] int? Rotation);

    /// <summary>
    /// 映射 ffprobe format 的时长和容器摘要。
    /// </summary>
    private sealed record FfprobeFormat(
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("format_name")] string? FormatName);
}
