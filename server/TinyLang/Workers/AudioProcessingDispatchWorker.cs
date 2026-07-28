using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 周期创建作用域，将到期 PostgreSQL 音频任务发布到 RabbitMQ。
/// </summary>
public sealed class AudioProcessingDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioProcessingSettings _settings;
    private readonly ILogger<AudioProcessingDispatchWorker> _logger;

    /// <summary>
    /// 使用作用域工厂、音频配置和日志记录器创建调度后台服务。
    /// </summary>
    public AudioProcessingDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AudioProcessingSettings> options,
        ILogger<AudioProcessingDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_settings.PollingIntervalSeconds));
        do
        {
            await DispatchCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// 在独立依赖注入作用域内执行一轮有界消息发布。
    /// </summary>
    private async Task DispatchCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider
                .GetRequiredService<IAudioProcessingDispatcher>();
            var count = await dispatcher.DispatchDueAsync(cancellationToken);
            if (count > 0)
            {
                _logger.LogInformation(
                    "Dispatched {AudioJobCount} audio processing jobs",
                    count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Audio processing dispatch failed with {FailureType}",
                exception.GetType().Name);
        }
    }
}
