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
    [Fact]
    public async Task RotationShouldBeAppliedBeforeDimensionValidation()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1080,
                  "height": 1980, "side_data_list": [{ "rotation": -90 }] },
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

        result.DisplayWidth.Should().Be(1980);
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
    public async Task RotatedDisplayAbove1980By1080ShouldBeRejected()
    {
        var runner = CreateRunner("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1081,
                  "height": 1981, "tags": { "rotate": "90" } },
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
