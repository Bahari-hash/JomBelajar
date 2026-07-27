using Microsoft.Extensions.Hosting;
using TinyLang.Interfaces;

namespace TinyLang.Workers;

/// <summary>
/// 在 Web API 启动阶段验证 ffprobe 和 FFmpeg 可执行程序。
/// </summary>
/// <param name="preflight">媒体工具启动检查实现。</param>
public sealed class VideoToolPreflightWorker(IVideoToolPreflight preflight)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => preflight.ValidateAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
