using Microsoft.Extensions.Hosting;
using TinyLang.Interfaces;

namespace TinyLang.Workers;

/// <summary>
/// 在 Web API 启动阶段验证音频处理工具和 MP3 encoder。
/// </summary>
public sealed class AudioToolPreflightWorker(IAudioToolPreflight preflight)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => preflight.ValidateAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
