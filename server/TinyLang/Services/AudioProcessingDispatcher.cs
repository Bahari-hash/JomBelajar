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
        var jobs = await processingService.GetDispatchableJobsAsync(
            cancellationToken);
        var dispatched = 0;
        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processingQueue.EnqueueAsync(
                job.JobId,
                job.AudioResourceId,
                job.OutputVersion,
                cancellationToken);
            await processingService.MarkDispatchedAsync(job.JobId, cancellationToken);
            dispatched++;
        }
        return dispatched;
    }
}
