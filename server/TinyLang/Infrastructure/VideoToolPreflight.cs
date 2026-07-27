using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 通过结构化 version 参数在 worker 启动时验证 ffprobe 和 FFmpeg。
/// </summary>
public sealed class VideoToolPreflight : IVideoToolPreflight
{
    private readonly IMediaProcessRunner _processRunner;
    private readonly VideoProcessingSettings _settings;

    /// <summary>
    /// 使用进程执行器和工具路径创建启动检查器。
    /// </summary>
    public VideoToolPreflight(
        IMediaProcessRunner processRunner,
        IOptions<VideoProcessingSettings> options)
    {
        _processRunner = processRunner;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await ValidateToolAsync(_settings.FfprobePath, cancellationToken);
        await ValidateToolAsync(_settings.FfmpegPath, cancellationToken);
    }

    /// <summary>
    /// 验证单个工具的 version 命令成功退出。
    /// </summary>
    private async Task ValidateToolAsync(
        string executable,
        CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            executable,
            ["-version"],
            TimeSpan.FromSeconds(15),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "A configured video processing tool failed its startup check.");
        }
    }
}
