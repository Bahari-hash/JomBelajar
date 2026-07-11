using MassTransit;
using Microsoft.Extensions.Logging;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Workers;

public sealed class EmailSendingWorker(
    IEmailProvider emailProvider, ILogger<EmailSendingWorker> logger)
    : IConsumer<SendEmailMessage>
{
    public async Task Consume(ConsumeContext<SendEmailMessage> context)
    {
        var messageWrapper = context.Message;
        logger.LogInformation("MQ has received the message. Sending....");

        await emailProvider.DeliverEmailAsync(messageWrapper, context.CancellationToken);
        logger.LogInformation("MQ email sending task completed.");
    }
}
