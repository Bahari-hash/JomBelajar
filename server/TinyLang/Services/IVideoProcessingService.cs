namespace TinyLang.Services;

/// <summary>
/// 定义视频任务的 RabbitMQ 调度、原子领取和媒体处理业务边界。
/// </summary>
public interface IVideoProcessingService
{
    /// <summary>
    /// 确定性查询一批需要发布到消息队列的到期任务。
    /// </summary>
    /// <param name="cancellationToken">用于停止查询的令牌。</param>
    /// <returns>按到期时间和创建顺序排列的任务标识。</returns>
    Task<IReadOnlyList<Guid>> GetDispatchableJobIdsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在消息成功发布后记录调度时间，以抑制短时间内的重复发布。
    /// </summary>
    /// <param name="jobId">已经发布的视频处理任务标识。</param>
    /// <param name="cancellationToken">用于停止持久化的令牌。</param>
    /// <returns>表示调度时间已经记录或任务状态已经改变的任务。</returns>
    Task MarkDispatchedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为当前 RabbitMQ 消息原子领取指定任务和有效租约。
    /// </summary>
    /// <param name="jobId">消息指定的视频处理任务标识。</param>
    /// <param name="workerId">当前消息投递的租约 owner 标识。</param>
    /// <param name="cancellationToken">用于停止领取的令牌。</param>
    /// <returns>当前消息是否成功取得任务处理权。</returns>
    Task<bool> TryClaimAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理一个由当前 worker 持有有效租约的任务并收敛到成功或重试状态。
    /// </summary>
    /// <param name="jobId">已领取的视频处理任务标识。</param>
    /// <param name="workerId">必须与任务租约一致的 worker 标识。</param>
    /// <param name="cancellationToken">用于停止下载、进程、上传和数据库操作的令牌。</param>
    /// <returns>表示任务完成或失败状态已持久化的任务。</returns>
    Task ProcessClaimedAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default);
}
