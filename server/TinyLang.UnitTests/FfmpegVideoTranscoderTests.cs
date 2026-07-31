using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 FFmpeg 结构化参数、三档输出和 master 相对 URI。
/// </summary>
public sealed class FfmpegVideoTranscoderTests
{
    [Fact]
    public async Task FullHdTranscodeShouldUseExactInputArgumentAndRelativeMasterUris()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tiny-lang-ffmpeg-test-{Guid.NewGuid():N}");
        try
        {
            var settings = new VideoProcessingSettings();
            var runner = new RecordingProcessRunner();
            var transcoder = new FfmpegVideoTranscoder(
                runner,
                new VideoRenditionPlanner(Options.Create(settings)),
                Options.Create(settings));
            var inputPath = Path.Combine(root, "source name;not-shell.mp4");
            var outputPath = Path.Combine(root, "output");

            var result = await transcoder.TranscodeAsync(
                inputPath,
                outputPath,
                new MediaProbeResult(60, 1920, 1080, "mp4", "h264", "aac"),
                TestContext.Current.CancellationToken);

            result.Renditions.Select(value => value.Plan.TargetHeight)
                .Should().Equal(480, 720, 1080);
            runner.Invocations.Should().HaveCount(4);
            runner.Invocations.Should().OnlyContain(value =>
                value.Executable == "ffmpeg" && value.Arguments.Contains(inputPath));
            var master = await File.ReadAllTextAsync(
                result.MasterPlaylistPath,
                TestContext.Current.CancellationToken);
            master.Should().Contain("480p/index.m3u8");
            master.Should().Contain("720p/index.m3u8");
            master.Should().Contain("1080p/index.m3u8");
            master.Should().NotContain(outputPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// 记录结构化参数并创建转码器期望验证的最小输出文件。
    /// </summary>
    private sealed class RecordingProcessRunner : IMediaProcessRunner
    {
        public List<(string Executable, IReadOnlyList<string> Arguments)> Invocations { get; } = [];

        /// <inheritdoc />
        public Task<MediaProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((executable, arguments));
            var outputPath = arguments[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "generated");
            if (arguments.Contains("-hls_segment_filename"))
            {
                File.WriteAllText(
                    Path.Combine(Path.GetDirectoryName(outputPath)!, "segment_000001.ts"),
                    "segment");
            }
            return Task.FromResult(new MediaProcessResult(0, string.Empty, string.Empty));
        }
    }
}
