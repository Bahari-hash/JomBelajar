using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 ffprobe JSON 解析、旋转尺寸和永久失败分类。
/// </summary>
public sealed class FfprobeMediaProbeTests
{
    /// <summary>
    /// 验证通用进程超时仍映射为既有视频稳定失败码。
    /// </summary>
    [Fact]
    public async Task MediaProcessTimeoutShouldMapToVideoFailure()
    {
        var runner = new Mock<IMediaProcessRunner>();
        runner.Setup(value => value.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MediaProcessException(MediaProcessFailureKind.TimedOut));
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(VideoProcessingFailureCode.ProcessTimedOut);
        exception.Which.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task RotationShouldBeAppliedBeforeDimensionValidation()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1080,
                  "height": 1920, "side_data_list": [{ "rotation": -90 }] },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "60.5", "format_name": "mov,mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var result = await probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        result.DisplayWidth.Should().Be(1920);
        result.DisplayHeight.Should().Be(1080);
        result.DurationSeconds.Should().Be(60.5);
        result.ContainerFormat.Should().Be("mov");
    }

    [Fact]
    public async Task MissingAudioShouldBePermanentFailure()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1280,
                  "height": 720 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(VideoProcessingFailureCode.AudioStreamMissing);
        exception.Which.IsTransient.Should().BeFalse();
    }

    [Fact]
    public async Task AttachedPictureShouldNotCountAsUsableVideoStream()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "mjpeg", "width": 1200,
                  "height": 1200, "disposition": { "attached_pic": 1 } },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "120", "format_name": "mp3" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(VideoProcessingFailureCode.VideoStreamMissing);
    }

    [Fact]
    public async Task RotatedDisplayAbove1920By1080ShouldBeRejected()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1081,
                  "height": 1921, "tags": { "rotate": "90" } },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(
            VideoProcessingFailureCode.SourceResolutionExceeded);
    }

    [Fact]
    public async Task AnamorphicSarShouldProduceDisplayDimensionsWithoutUpscaling()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1440,
                  "height": 1080, "sample_aspect_ratio": "4:3",
                  "display_aspect_ratio": "16:9" },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var result = await probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        result.DisplayWidth.Should().Be(1920);
        result.DisplayHeight.Should().Be(1080);
    }

    [Fact]
    public async Task DarShouldProvideFallbackWhenSarIsUnavailable()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1440,
                  "height": 1080, "sample_aspect_ratio": "N/A",
                  "display_aspect_ratio": "16:9" },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var result = await probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        result.DisplayWidth.Should().Be(1920);
        result.DisplayHeight.Should().Be(1080);
    }

    [Fact]
    public async Task FractionalDisplayWidthAboveLimitShouldRoundUpAndBeRejected()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1441,
                  "height": 1080, "sample_aspect_ratio": "4:3" },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(
            VideoProcessingFailureCode.SourceResolutionExceeded);
    }

    [Fact]
    public async Task InconsistentSarAndDarShouldBeRejected()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1440,
                  "height": 1080, "sample_aspect_ratio": "4:3",
                  "display_aspect_ratio": "4:3" },
                { "codec_type": "audio", "codec_name": "aac", "width": 0, "height": 0 }
              ],
              "format": { "duration": "12", "format_name": "mp4" }
            }
            """);
        var probe = new FfprobeMediaProbe(
            runner.Object,
            Options.Create(new VideoProcessingSettings()));

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<VideoProcessingException>();
        exception.Which.FailureCode.Should().Be(
            VideoProcessingFailureCode.DisplayDimensionsInvalid);
    }

    private static Mock<IMediaProcessRunner> CreateRunner(string json)
    {
        var runner = new Mock<IMediaProcessRunner>();
        runner.Setup(value => value.RunAsync(
                "ffprobe",
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProcessResult(0, json, string.Empty));
        return runner;
    }
}
