namespace TinyLang.Models;

/// <summary>
/// 表示请求 RabbitMQ consumer 处理一个持久化音频任务的消息。
/// </summary>
public sealed record AudioProcessingRequested
{
    public required Guid Id { get; init; }
    public required Guid JobId { get; init; }
    public required Guid AudioResourceId { get; init; }
    public required Guid OutputVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 表示从数据库读取并准备发布的不可变音频任务标识。
/// </summary>
public sealed record AudioProcessingDispatchItem(
    Guid JobId,
    Guid AudioResourceId,
    Guid OutputVersion);
