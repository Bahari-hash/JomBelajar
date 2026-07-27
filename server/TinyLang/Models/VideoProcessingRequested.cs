namespace TinyLang.Models;

/// <summary>
/// 表示请求 RabbitMQ consumer 处理一个持久化视频任务的消息。
/// </summary>
public sealed record VideoProcessingRequested
{
    public required Guid Id { get; init; }
    public required Guid JobId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
