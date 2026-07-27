using MassTransit;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用项目共享的 MassTransit bus 发布视频处理请求。
/// </summary>
/// <param name="publishEndpoint">MassTransit 消息发布端点。</param>
/// <param name="timeProvider">提供可测试的 UTC 消息创建时间。</param>
public sealed class MassTransitVideoProcessingQueue(
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider) : IVideoProcessingQueue
{
    /// <inheritdoc />
    public Task EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var message = new VideoProcessingRequested
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            CreatedAt = timeProvider.GetUtcNow()
        };
        return publishEndpoint.Publish(message, cancellationToken);
    }
}
