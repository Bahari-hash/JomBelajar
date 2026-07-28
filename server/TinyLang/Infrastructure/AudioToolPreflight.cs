using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 在 worker 启动时验证 ffprobe、FFmpeg 和 libmp3lame encoder 可用性。
/// </summary>
public sealed class AudioToolPreflight : IAudioToolPreflight
{
    private readonly IMediaProcessRunner _processRunner;
    private readonly AudioProcessingSettings _settings;

    /// <summary>
    /// 使用进程执行器和音频工具配置创建启动检查器。
    /// </summary>
    public AudioToolPreflight(
        IMediaProcessRunner processRunner,
        IOptions<AudioProcessingSettings> options)
    {
        _processRunner = processRunner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await RunRequiredAsync(_settings.FfprobePath, ["-version"], cancellationToken);
        var encoders = await RunRequiredAsync(
            _settings.FfmpegPath,
            ["-hide_banner", "-encoders"],
            cancellationToken);
        var output = $"{encoders.StandardOutput}\n{encoders.StandardError}";
        if (!output.Contains("libmp3lame", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The configured FFmpeg does not provide the required libmp3lame encoder.");
        }
    }

    /// <summary>
    /// 运行启动检查命令并将底层失败转换为启动时配置错误。
    /// </summary>
    private async Task<MediaProcessResult> RunRequiredAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                executable,
                arguments,
                TimeSpan.FromSeconds(15),
                cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "A configured audio processing tool failed its startup check.");
            }
            return result;
        }
        catch (MediaProcessException exception)
        {
            throw new InvalidOperationException(
                "A configured audio processing tool could not be started.",
                exception);
        }
    }
}
