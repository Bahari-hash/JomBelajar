using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频 ffprobe JSON 解析、视频流排除和源媒体上限。
/// </summary>
public sealed class FfprobeAudioProbeTests
{
    /// <summary>
    /// 验证音频流和 attached picture 可共同存在并返回真实属性。
    /// </summary>
    [Fact]
    public async Task AudioWithAttachedPictureShouldBeAccepted()
    {
        var probe = CreateProbe("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "mjpeg",
                  "disposition": { "attached_pic": 1 } },
                { "codec_type": "audio", "codec_name": "mp3",
                  "sample_rate": "44100", "channels": 2 }
              ],
              "format": { "duration": "12.5", "format_name": "mp3" }
            }
            """);

        var result = await probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        result.DurationSeconds.Should().Be(12.5);
        result.SampleRate.Should().Be(44100);
        result.Channels.Should().Be(2);
        result.Codec.Should().Be("mp3");
    }

    /// <summary>
    /// 验证普通视频 stream 使音频源永久失败。
    /// </summary>
    [Fact]
    public async Task OrdinaryVideoStreamShouldBeRejected()
    {
        var probe = CreateProbe("""
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264" },
                { "codec_type": "audio", "codec_name": "aac",
                  "sample_rate": "48000", "channels": 2 }
              ],
              "format": { "duration": "20", "format_name": "mp4" }
            }
            """);

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<AudioProcessingException>();
        exception.Which.FailureCode.Should().Be(
            AudioProcessingFailureCode.VideoStreamPresent);
        exception.Which.IsTransient.Should().BeFalse();
    }

    /// <summary>
    /// 验证缺少音频 stream 时使用独立稳定失败码。
    /// </summary>
    [Fact]
    public async Task MissingAudioStreamShouldBeRejected()
    {
        var probe = CreateProbe("""
            { "streams": [], "format": { "duration": "20", "format_name": "wav" } }
            """);

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<AudioProcessingException>();
        exception.Which.FailureCode.Should().Be(
            AudioProcessingFailureCode.AudioStreamMissing);
    }

    /// <summary>
    /// 验证无法解析的 ffprobe JSON 和非有限时长均被永久拒绝。
    /// </summary>
    [Theory]
    [InlineData("not-json", AudioProcessingFailureCode.ProbeFailed)]
    [InlineData("{\"streams\":[{\"codec_type\":\"audio\",\"codec_name\":\"mp3\",\"sample_rate\":\"44100\",\"channels\":1}],\"format\":{\"duration\":\"NaN\"}}", AudioProcessingFailureCode.DurationInvalid)]
    public async Task InvalidProbeDocumentShouldBeRejected(
        string json,
        AudioProcessingFailureCode failureCode)
    {
        var probe = CreateProbe(json);

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<AudioProcessingException>();
        exception.Which.FailureCode.Should().Be(failureCode);
        exception.Which.IsTransient.Should().BeFalse();
    }

    /// <summary>
    /// 验证时长、采样率和声道配置上限均在真实探测后执行。
    /// </summary>
    [Theory]
    [InlineData("601", "44100", 2, AudioProcessingFailureCode.SourceDurationExceeded)]
    [InlineData("60", "192001", 2, AudioProcessingFailureCode.SourceSampleRateExceeded)]
    [InlineData("60", "44100", 9, AudioProcessingFailureCode.SourceChannelsExceeded)]
    public async Task SourceLimitsShouldBeEnforced(
        string duration,
        string sampleRate,
        int channels,
        AudioProcessingFailureCode failureCode)
    {
        var probe = CreateProbe($$"""
            {
              "streams": [
                { "codec_type": "audio", "codec_name": "pcm_s16le",
                  "sample_rate": "{{sampleRate}}", "channels": {{channels}} }
              ],
              "format": { "duration": "{{duration}}", "format_name": "wav" }
            }
            """);

        var action = () => probe.ProbeAsync(
            "source.media",
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<AudioProcessingException>();
        exception.Which.FailureCode.Should().Be(failureCode);
    }

    /// <summary>
    /// 使用固定 JSON 输出创建音频探测器。
    /// </summary>
    private static FfprobeAudioProbe CreateProbe(string json)
    {
        var runner = new Mock<IMediaProcessRunner>();
        runner.Setup(value => value.RunAsync(
                "ffprobe",
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProcessResult(0, json, string.Empty));
        return new FfprobeAudioProbe(
            runner.Object,
            Options.Create(new AudioProcessingSettings()));
    }
}
