using MassTransit;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用项目共享的 MassTransit bus 发布音频处理请求。
/// </summary>
public sealed class MassTransitAudioProcessingQueue(
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider) : IAudioProcessingQueue
{
    /// <inheritdoc />
    public Task EnqueueAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var message = new AudioProcessingRequested
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            CreatedAt = timeProvider.GetUtcNow()
        };
        return publishEndpoint.Publish(message, cancellationToken);
    }
}
