using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用结构化 FFmpeg 参数生成三档以内的 VOD HLS 和 JPEG poster。
/// </summary>
public sealed class FfmpegVideoTranscoder : IVideoTranscoder
{
    private readonly IMediaProcessRunner _processRunner;
    private readonly VideoRenditionPlanner _planner;
    private readonly VideoProcessingSettings _settings;

    /// <summary>
    /// 使用进程执行器、rendition 规划器和编码配置创建转码器。
    /// </summary>
    public FfmpegVideoTranscoder(
        IMediaProcessRunner processRunner,
        VideoRenditionPlanner planner,
        IOptions<VideoProcessingSettings> options)
    {
        _processRunner = processRunner;
        _planner = planner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<VideoTranscodeResult> TranscodeAsync(
        string inputPath,
        string outputDirectory,
        MediaProbeResult probeResult,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var plans = _planner.CreatePlan(
            probeResult.DisplayWidth,
            probeResult.DisplayHeight);
        var generated = new List<GeneratedVideoRendition>(plans.Count);
        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var renditionDirectory = Path.Combine(outputDirectory, plan.Label);
            Directory.CreateDirectory(renditionDirectory);
            var playlistPath = Path.Combine(renditionDirectory, "index.m3u8");
            var segmentPath = Path.Combine(renditionDirectory, "segment_%06d.ts");
            var result = await RunProcessAsync(
                _settings.FfmpegPath,
                BuildRenditionArguments(inputPath, playlistPath, segmentPath, plan),
                cancellationToken);
            EnsureSuccessful(result);
            if (!File.Exists(playlistPath))
            {
                throw new VideoProcessingException(
                    VideoProcessingFailureCode.OutputValidationFailed,
                    isTransient: true);
            }
            await NormalizeVariantPlaylistAsync(playlistPath, cancellationToken);
            generated.Add(new GeneratedVideoRendition(plan, playlistPath));
        }

        var posterPath = Path.Combine(outputDirectory, "poster.jpg");
        var posterResult = await RunProcessAsync(
            _settings.FfmpegPath,
            BuildPosterArguments(inputPath, posterPath, probeResult.DurationSeconds),
            cancellationToken);
        EnsureSuccessful(posterResult);
        if (!File.Exists(posterPath))
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.OutputValidationFailed,
                isTransient: true);
        }

        var masterPath = Path.Combine(outputDirectory, "master.m3u8");
        await File.WriteAllTextAsync(
            masterPath,
            BuildMasterPlaylist(generated),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return new VideoTranscodeResult(masterPath, posterPath, generated);
    }

    /// <summary>
    /// 执行 FFmpeg 并将通用进程失败映射为视频稳定失败码。
    /// </summary>
    private async Task<MediaProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _processRunner.RunAsync(
                executable,
                arguments,
                TimeSpan.FromSeconds(_settings.TranscodeTimeoutSeconds),
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
    }

    /// <summary>
    /// 构建单条 HLS rendition 的无 shell FFmpeg 参数。
    /// </summary>
    private IReadOnlyList<string> BuildRenditionArguments(
        string inputPath,
        string playlistPath,
        string segmentPath,
        VideoRenditionPlan plan)
    {
        var maximumBitrate = (int)Math.Ceiling(plan.VideoBitrateKbps * 1.07);
        var level = plan.Height >= 1080 ? "4.0" : "3.1";
        return
        [
            "-hide_banner", "-nostdin", "-y",
            "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a:0",
            "-vf", $"scale={plan.Width}:{plan.Height}",
            "-c:v", "libx264", "-preset", "medium",
            "-profile:v", "high", "-level:v", level,
            "-b:v", $"{plan.VideoBitrateKbps}k",
            "-maxrate", $"{maximumBitrate}k",
            "-bufsize", $"{plan.VideoBitrateKbps * 2}k",
            "-force_key_frames",
            $"expr:gte(t,n_forced*{_settings.HlsSegmentSeconds})",
            "-sc_threshold", "0",
            "-c:a", "aac", "-b:a", $"{plan.AudioBitrateKbps}k", "-ac", "2",
            "-f", "hls",
            "-hls_time", _settings.HlsSegmentSeconds.ToString(CultureInfo.InvariantCulture),
            "-hls_playlist_type", "vod",
            "-hls_flags", "independent_segments",
            "-hls_segment_filename", segmentPath,
            playlistPath
        ];
    }

    /// <summary>
    /// 构建对短视频安全的 JPEG poster FFmpeg 参数。
    /// </summary>
    private IReadOnlyList<string> BuildPosterArguments(
        string inputPath,
        string posterPath,
        double durationSeconds)
    {
        var screenshotSeconds = Math.Max(0, Math.Min(5, durationSeconds * 0.1));
        var jpegScale = Math.Clamp(
            31 - (int)Math.Round(_settings.PosterJpegQuality * 29d / 100d),
            2,
            31);
        return
        [
            "-hide_banner", "-nostdin", "-y",
            "-ss", screenshotSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", inputPath,
            "-map", "0:v:0", "-frames:v", "1",
            "-q:v", jpegScale.ToString(CultureInfo.InvariantCulture),
            posterPath
        ];
    }

    /// <summary>
    /// 根据实际生成的 rendition 构建只含相对 URI 的 master playlist。
    /// </summary>
    private static string BuildMasterPlaylist(
        IReadOnlyCollection<GeneratedVideoRendition> renditions)
    {
        var builder = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-INDEPENDENT-SEGMENTS\n");
        foreach (var rendition in renditions.OrderBy(value => value.Plan.Height))
        {
            var averageBandwidth =
                (rendition.Plan.VideoBitrateKbps + rendition.Plan.AudioBitrateKbps) * 1000;
            var peakBandwidth = (int)Math.Ceiling(averageBandwidth * 1.1);
            builder.Append("#EXT-X-STREAM-INF:BANDWIDTH=")
                .Append(peakBandwidth)
                .Append(",AVERAGE-BANDWIDTH=")
                .Append(averageBandwidth)
                .Append(",RESOLUTION=")
                .Append(rendition.Plan.Width)
                .Append('x')
                .Append(rendition.Plan.Height)
                .Append(",CODECS=\"")
                .Append(rendition.Plan.Codecs)
                .Append("\"\n")
                .Append(rendition.Plan.Label)
                .Append("/index.m3u8\n");
        }
        return builder.ToString();
    }

    /// <summary>
    /// 将 FFmpeg 生成的 segment URI 验证并规范化为同目录相对文件名。
    /// </summary>
    private static async Task NormalizeVariantPlaylistAsync(
        string playlistPath,
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(playlistPath, cancellationToken);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                throw new VideoProcessingException(
                    VideoProcessingFailureCode.OutputValidationFailed,
                    isTransient: false);
            }
            var normalized = line.Replace('\\', '/');
            var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
            if (fileName.Length == 0 || fileName is "." or "..")
            {
                throw new VideoProcessingException(
                    VideoProcessingFailureCode.OutputValidationFailed,
                    isTransient: false);
            }
            lines[index] = fileName;
        }
        await File.WriteAllLinesAsync(
            playlistPath,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    /// <summary>
    /// 将 FFmpeg 非零退出转换为不暴露 stderr 的稳定失败分类。
    /// </summary>
    private static void EnsureSuccessful(MediaProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.TranscodeFailed,
                isTransient: false);
        }
    }
}
