using MassTransit;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

/// <summary>
/// 为邮件附加消息元数据，并选择消息队列或直接 provider 投递。
/// </summary>
/// <param name="emailProvider">直接邮件投递 provider。</param>
/// <param name="publishEndpoint">MassTransit 消息发布端点。</param>
public sealed class EmailSender(IEmailProvider emailProvider, IPublishEndpoint publishEndpoint) : IEmailSender
{
    /// <inheritdoc />
    public Task EnqueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var messageWrapper = new EmailMessageWrapper
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreateTime = DateTimeOffset.UtcNow
        };
        return publishEndpoint.Publish(messageWrapper, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var messageWrapper = new EmailMessageWrapper
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreateTime = DateTimeOffset.UtcNow
        };
        return emailProvider.DeliverEmailAsync(messageWrapper, cancellationToken);
    }
}
