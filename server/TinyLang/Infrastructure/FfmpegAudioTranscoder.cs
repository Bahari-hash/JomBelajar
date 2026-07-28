using System.Globalization;
using System.IO;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用结构化 FFmpeg 参数生成固定采样率、码率和响度的单声道 MP3。
/// </summary>
public sealed class FfmpegAudioTranscoder : IAudioTranscoder
{
    private readonly IMediaProcessRunner _processRunner;
    private readonly AudioProcessingSettings _settings;

    /// <summary>
    /// 使用进程执行器和音频编码配置创建转码器。
    /// </summary>
    public FfmpegAudioTranscoder(
        IMediaProcessRunner processRunner,
        IOptions<AudioProcessingSettings> options)
    {
        _processRunner = processRunner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<AudioTranscodeResult> TranscodeAsync(
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "audio.mp3");
        MediaProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(
                _settings.FfmpegPath,
                BuildArguments(inputPath, outputPath),
                TimeSpan.FromSeconds(_settings.TranscodeTimeoutSeconds),
                cancellationToken);
        }
        catch (MediaProcessException exception)
        {
            throw new AudioProcessingException(
                exception.FailureKind == MediaProcessFailureKind.TimedOut
                    ? AudioProcessingFailureCode.ProcessTimedOut
                    : AudioProcessingFailureCode.ProcessStartFailed,
                isTransient: true);
        }
        if (result.ExitCode != 0)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.TranscodeFailed,
                isTransient: false);
        }
        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.OutputValidationFailed,
                isTransient: true);
        }
        return new AudioTranscodeResult(outputPath);
    }

    /// <summary>
    /// 构建不经过 shell 且不包含静音裁剪的固定 MP3 编码参数。
    /// </summary>
    private IReadOnlyList<string> BuildArguments(string inputPath, string outputPath)
        =>
        [
            "-hide_banner", "-nostdin", "-y",
            "-i", inputPath,
            "-map", "0:a:0", "-vn",
            "-ac", _settings.OutputChannels.ToString(CultureInfo.InvariantCulture),
            "-ar", _settings.OutputSampleRate.ToString(CultureInfo.InvariantCulture),
            "-c:a", "libmp3lame",
            "-b:a", $"{_settings.OutputBitrateKbps}k",
            "-af", $"loudnorm=I={Format(_settings.LoudnessTargetLufs)}:" +
                $"TP={Format(_settings.TruePeakDb)}:" +
                $"LRA={Format(_settings.LoudnessRange)}",
            outputPath
        ];

    /// <summary>
    /// 以不受服务器区域影响的紧凑格式输出 FFmpeg 数值参数。
    /// </summary>
    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
