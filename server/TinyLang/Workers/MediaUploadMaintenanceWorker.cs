using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 周期创建作用域并触发有界的媒体归档和上传清理批处理。
/// </summary>
public sealed class MediaUploadMaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MultipartUploadSettings _settings;
    private readonly ILogger<MediaUploadMaintenanceWorker> _logger;

    /// <summary>
    /// 使用作用域工厂和轮询配置创建后台维护 worker。
    /// </summary>
    public MediaUploadMaintenanceWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MultipartUploadSettings> options,
        ILogger<MediaUploadMaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_settings.CleanupIntervalSeconds));
        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// 在独立依赖注入作用域中执行一次归档和清理周期。
    /// </summary>
    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var maintenance = scope.ServiceProvider
                .GetRequiredService<IMediaUploadMaintenanceService>();
            await maintenance.FinalizeBatchAsync(cancellationToken);
            await maintenance.CleanupBatchAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Media upload maintenance cycle failed with {FailureType}",
                exception.GetType().Name);
        }
    }
}
