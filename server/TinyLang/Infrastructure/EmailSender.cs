using MassTransit;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

public sealed class EmailSender(IEmailProvider emailProvider, IPublishEndpoint publishEndpoint) : IEmailSender
{
    public Task EnqueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var messageWrapper = new SendEmailMessage
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreateTime = DateTimeOffset.UtcNow
        };
        return publishEndpoint.Publish(messageWrapper, cancellationToken);
    }

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var messageWrapper = new SendEmailMessage
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreateTime = DateTimeOffset.UtcNow
        };
        return emailProvider.DeliverEmailAsync(messageWrapper, cancellationToken);
    }
}
