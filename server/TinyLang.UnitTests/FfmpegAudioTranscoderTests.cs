using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 MP3 转码使用结构化固定参数且不裁剪静音。
/// </summary>
public sealed class FfmpegAudioTranscoderTests
{
    /// <summary>
    /// 验证路径作为单独参数传递并应用固定 codec、采样率、声道、码率和响度。
    /// </summary>
    [Fact]
    public async Task TranscodeShouldUseFixedMp3ProfileWithoutSilenceTrim()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tiny-lang-audio-ffmpeg-{Guid.NewGuid():N}");
        try
        {
            var runner = new RecordingProcessRunner();
            var transcoder = new FfmpegAudioTranscoder(
                runner,
                Options.Create(new AudioProcessingSettings()));
            var inputPath = Path.Combine(root, "source name;not-shell.wav");

            var result = await transcoder.TranscodeAsync(
                inputPath,
                Path.Combine(root, "output"),
                TestContext.Current.CancellationToken);

            runner.Arguments.Should().ContainInOrder(
                "-i", inputPath,
                "-map", "0:a:0",
                "-ac", "1",
                "-ar", "44100",
                "-c:a", "libmp3lame",
                "-b:a", "96k",
                "-af", "loudnorm=I=-16:TP=-1.5:LRA=11");
            runner.Arguments.Should().NotContain(value =>
                value.Contains("silence", StringComparison.OrdinalIgnoreCase));
            result.OutputPath.Should().EndWith("audio.mp3");
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
    /// 验证 FFmpeg 非零退出转换为不可自动重试的转码失败。
    /// </summary>
    [Fact]
    public async Task NonZeroExitShouldBePermanentTranscodeFailure()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"tiny-lang-audio-failed-{Guid.NewGuid():N}");
        var runner = new Mock<IMediaProcessRunner>();
        runner.Setup(value => value.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProcessResult(1, string.Empty, "bounded error"));
        var transcoder = new FfmpegAudioTranscoder(
            runner.Object,
            Options.Create(new AudioProcessingSettings()));

        try
        {
            var action = () => transcoder.TranscodeAsync(
                "source.wav",
                root,
                TestContext.Current.CancellationToken);

            var exception = await action.Should().ThrowAsync<AudioProcessingException>();
            exception.Which.FailureCode.Should().Be(AudioProcessingFailureCode.TranscodeFailed);
            exception.Which.IsTransient.Should().BeFalse();
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
    /// 验证进程成功但未生成文件时不会返回可发布输出。
    /// </summary>
    [Fact]
    public async Task MissingOutputShouldBeTransientValidationFailure()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"tiny-lang-audio-missing-{Guid.NewGuid():N}");
        var runner = new Mock<IMediaProcessRunner>();
        runner.Setup(value => value.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProcessResult(0, string.Empty, string.Empty));
        var transcoder = new FfmpegAudioTranscoder(
            runner.Object,
            Options.Create(new AudioProcessingSettings()));

        try
        {
            var action = () => transcoder.TranscodeAsync(
                "source.wav",
                root,
                TestContext.Current.CancellationToken);

            var exception = await action.Should().ThrowAsync<AudioProcessingException>();
            exception.Which.FailureCode.Should().Be(
                AudioProcessingFailureCode.OutputValidationFailed);
            exception.Which.IsTransient.Should().BeTrue();
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
    /// 记录 FFmpeg 参数并创建转码器要求的非空输出。
    /// </summary>
    private sealed class RecordingProcessRunner : IMediaProcessRunner
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        /// <inheritdoc />
        public Task<MediaProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Arguments = arguments;
            Directory.CreateDirectory(Path.GetDirectoryName(arguments[^1])!);
            File.WriteAllText(arguments[^1], "mp3");
            return Task.FromResult(new MediaProcessResult(0, string.Empty, string.Empty));
        }
    }
}
