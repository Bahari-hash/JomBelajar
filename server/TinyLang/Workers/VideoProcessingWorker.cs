using MassTransit;
using Microsoft.Extensions.Logging;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.Workers;

/// <summary>
/// 消费 RabbitMQ 视频任务消息，原子领取数据库租约后执行媒体处理。
/// </summary>
/// <param name="processingService">视频任务领取和处理服务。</param>
/// <param name="logger">消费状态日志记录器。</param>
public sealed class VideoProcessingWorker(
    IVideoProcessingService processingService,
    ILogger<VideoProcessingWorker> logger)
    : IConsumer<VideoProcessingRequested>
{
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

        await processingService.ProcessClaimedAsync(
            message.JobId,
            message.Id,
            context.CancellationToken);
    }
}
