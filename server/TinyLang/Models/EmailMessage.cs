namespace TinyLang.Models;

/// <summary>
/// 描述等待发送的邮件内容、收件人和附件。
/// </summary>
public sealed record EmailMessage
{
    public required List<EmailAddress> To { get; init; }
    public List<EmailAddress> Cc { get; init; } = [];
    public List<EmailAddress> Bcc { get; init; } = [];

    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required bool IsHtml { get; init; }

    public List<EmailAttachment> EmailAttachments { get; init; } = [];
}
