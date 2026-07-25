namespace TinyLang.Models;

/// <summary>
/// 为消息队列中的邮件附加消息标识和创建时间。
/// </summary>
public sealed record EmailMessageWrapper
{
    public required Guid Id { get; init; }
    public required EmailMessage Message { get; init; }
    public required DateTimeOffset CreateTime { get; init; }
}
