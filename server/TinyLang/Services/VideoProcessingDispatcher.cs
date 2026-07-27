using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 将数据库中的到期视频任务有界发布到 RabbitMQ，并记录成功调度时间。
/// </summary>
/// <param name="processingService">视频任务查询和调度状态服务。</param>
/// <param name="processingQueue">视频处理消息队列。</param>
public sealed class VideoProcessingDispatcher(
    IVideoProcessingService processingService,
    IVideoProcessingQueue processingQueue) : IVideoProcessingDispatcher
{
    /// <inheritdoc />
    public async Task<int> DispatchDueAsync(
        CancellationToken cancellationToken = default)
    {
        var jobIds = await processingService.GetDispatchableJobIdsAsync(
            cancellationToken);
        var dispatched = 0;
        foreach (var jobId in jobIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processingQueue.EnqueueAsync(jobId, cancellationToken);
            await processingService.MarkDispatchedAsync(jobId, cancellationToken);
            dispatched++;
        }
        return dispatched;
    }
}
