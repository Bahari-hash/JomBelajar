namespace TinyLang.Interfaces;

/// <summary>
/// 定义向视频处理消息队列发布持久化任务标识的边界。
/// </summary>
public interface IVideoProcessingQueue
{
    /// <summary>
    /// 将指定视频任务发布到后台处理队列。
    /// </summary>
    /// <param name="jobId">持久化视频处理任务标识。</param>
    /// <param name="cancellationToken">用于取消消息发布的令牌。</param>
    /// <returns>表示消息已被 broker 接受的任务。</returns>
    Task EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
