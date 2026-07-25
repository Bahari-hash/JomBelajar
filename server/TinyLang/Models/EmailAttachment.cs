namespace TinyLang.Models;

/// <summary>
/// 表示以内存内容发送的邮件附件。
/// </summary>
public sealed record EmailAttachment
{
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
}
