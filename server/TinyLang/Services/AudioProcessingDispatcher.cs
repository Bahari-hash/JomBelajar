using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 将数据库中的到期音频任务有界发布到 RabbitMQ 并记录调度时间。
/// </summary>
public sealed class AudioProcessingDispatcher(
    IAudioProcessingService processingService,
    IAudioProcessingQueue processingQueue) : IAudioProcessingDispatcher
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
