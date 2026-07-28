namespace TinyLang.Interfaces;

/// <summary>
/// 定义向音频处理消息队列发布持久化任务标识的边界。
/// </summary>
public interface IAudioProcessingQueue
{
    /// <summary>
    /// 将指定音频任务发布到后台处理队列。
    /// </summary>
    Task EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
