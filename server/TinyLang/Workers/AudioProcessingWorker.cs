using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 消费 RabbitMQ 音频任务消息，原子领取数据库租约后执行媒体处理。
/// </summary>
public sealed class AudioProcessingWorker(
    IAudioProcessingService processingService,
    IServiceScopeFactory scopeFactory,
    IOptions<AudioProcessingSettings> options,
    TimeProvider timeProvider,
    ILogger<AudioProcessingWorker> logger)
    : IConsumer<AudioProcessingRequested>
{
    private readonly AudioProcessingSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AudioProcessingRequested> context)
    {
        var message = context.Message;
        if (!await processingService.TryClaimAsync(
            message.JobId,
            message.AudioResourceId,
            message.OutputVersion,
            message.Id,
            context.CancellationToken))
        {
            logger.LogDebug(
                "Ignored stale or duplicate audio processing message for audio resource {AudioResourceId}, output {OutputVersion}, job {JobId}",
                message.AudioResourceId,
                message.OutputVersion,
                message.JobId);
            return;
        }
        using var leaseLostCancellation = new CancellationTokenSource();
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            leaseLostCancellation.Token);
        var heartbeatTask = MonitorLeaseAsync(
            message,
            leaseLostCancellation,
            processingCancellation.Token);
        try
        {
            await processingService.ProcessClaimedAsync(
                message.JobId,
                message.AudioResourceId,
                message.OutputVersion,
                message.Id,
                processingCancellation.Token);
        }
        catch (OperationCanceledException) when (
            leaseLostCancellation.IsCancellationRequested &&
            !context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Stopped stale audio processing worker after lease loss for job {JobId}",
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

    private async Task MonitorLeaseAsync(
        AudioProcessingRequested message,
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
                    .GetRequiredService<IAudioProcessingService>();
                renewed = await leaseService.RenewLeaseAsync(
                    message.JobId,
                    message.AudioResourceId,
                    message.OutputVersion,
                    message.Id,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Audio lease renewal failed for job {JobId} with {FailureType}",
                    message.JobId,
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
