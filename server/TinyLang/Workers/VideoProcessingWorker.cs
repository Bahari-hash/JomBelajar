using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 消费 RabbitMQ 视频任务消息，原子领取数据库租约后执行媒体处理。
/// </summary>
/// <param name="processingService">视频任务领取和处理服务。</param>
/// <param name="scopeFactory">为每次 heartbeat 创建独立处理服务作用域的工厂。</param>
/// <param name="options">视频租约和 heartbeat 配置。</param>
/// <param name="timeProvider">heartbeat 延迟使用的系统时钟。</param>
/// <param name="logger">消费状态日志记录器。</param>
public sealed class VideoProcessingWorker(
    IVideoProcessingService processingService,
    IServiceScopeFactory scopeFactory,
    IOptions<VideoProcessingSettings> options,
    TimeProvider timeProvider,
    ILogger<VideoProcessingWorker> logger)
    : IConsumer<VideoProcessingRequested>
{
    private readonly VideoProcessingSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<VideoProcessingRequested> context)
    {
        var message = context.Message;
        if (!await processingService.TryClaimAsync(
            message.JobId,
            message.Id,
            context.CancellationToken))
        {
            logger.LogDebug(
                "Ignored stale or duplicate video processing message for job {JobId}",
                message.JobId);
            return;
        }

        using var leaseLostCancellation = new CancellationTokenSource();
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            leaseLostCancellation.Token);
        var heartbeatTask = MonitorLeaseAsync(
            message.JobId,
            message.Id,
            leaseLostCancellation,
            processingCancellation.Token);
        try
        {
            await processingService.ProcessClaimedAsync(
                message.JobId,
                message.Id,
                processingCancellation.Token);
        }
        catch (OperationCanceledException) when (
            leaseLostCancellation.IsCancellationRequested &&
            !context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Stopped stale video processing worker after lease loss for job {JobId}",
                message.JobId);
        }
        finally
        {
            await processingCancellation.CancelAsync();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException) when (
                processingCancellation.IsCancellationRequested)
            {
            }
        }
    }

    /// <summary>
    /// 周期创建独立作用域续租，续租失败时取消当前媒体处理链路。
    /// </summary>
    private async Task MonitorLeaseAsync(
        Guid jobId,
        Guid workerId,
        CancellationTokenSource leaseLostCancellation,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(_settings.HeartbeatIntervalSeconds),
                timeProvider,
                cancellationToken);

            bool renewed;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var leaseService = scope.ServiceProvider
                    .GetRequiredService<IVideoProcessingService>();
                renewed = await leaseService.RenewLeaseAsync(
                    jobId,
                    workerId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Video lease renewal failed for job {JobId} with {FailureType}",
                    jobId,
                    exception.GetType().Name);
                renewed = false;
            }

            if (!renewed)
            {
                await leaseLostCancellation.CancelAsync();
                return;
            }
        }
    }
}
