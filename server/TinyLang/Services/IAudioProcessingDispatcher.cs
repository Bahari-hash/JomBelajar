namespace TinyLang.Services;

/// <summary>
/// 定义将到期 PostgreSQL 音频任务发布到 RabbitMQ 的单轮调度用例。
/// </summary>
public interface IAudioProcessingDispatcher
{
    /// <summary>
    /// 查询并发布一批到期的音频处理任务。
    /// </summary>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
