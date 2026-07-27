namespace TinyLang.Services;

/// <summary>
/// 定义将到期 PostgreSQL 视频任务发布到 RabbitMQ 的单轮调度用例。
/// </summary>
public interface IVideoProcessingDispatcher
{
    /// <summary>
    /// 查询并发布一批到期的视频处理任务。
    /// </summary>
    /// <param name="cancellationToken">用于停止查询和消息发布的令牌。</param>
    /// <returns>本轮成功发布的消息数量。</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
