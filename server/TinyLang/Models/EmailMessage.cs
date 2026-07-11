namespace TinyLang.Models;

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
