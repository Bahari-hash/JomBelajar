namespace TinyLang.Services;

/// <summary>
/// 定义音频任务的 RabbitMQ 调度、原子领取和媒体处理业务边界。
/// </summary>
public interface IAudioProcessingService
{
    /// <summary>
    /// 确定性查询一批需要发布到消息队列的到期任务。
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDispatchableJobIdsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在消息成功发布后记录调度时间以抑制短期重复发布。
    /// </summary>
    Task MarkDispatchedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为当前 RabbitMQ 消息原子领取指定任务和有效租约。
    /// </summary>
    Task<bool> TryClaimAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理由当前 worker 持有有效租约的任务并收敛状态。
    /// </summary>
    Task ProcessClaimedAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default);
}
