using MassTransit;
using Microsoft.Extensions.Logging;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.Workers;

/// <summary>
/// 消费 RabbitMQ 音频任务消息，原子领取数据库租约后执行媒体处理。
/// </summary>
public sealed class AudioProcessingWorker(
    IAudioProcessingService processingService,
    ILogger<AudioProcessingWorker> logger)
    : IConsumer<AudioProcessingRequested>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AudioProcessingRequested> context)
    {
        var message = context.Message;
        if (!await processingService.TryClaimAsync(
            message.JobId,
            message.Id,
            context.CancellationToken))
        {
            logger.LogDebug(
                "Ignored stale or duplicate audio processing message for job {JobId}",
                message.JobId);
            return;
        }
        await processingService.ProcessClaimedAsync(
            message.JobId,
            message.Id,
            context.CancellationToken);
    }
}
