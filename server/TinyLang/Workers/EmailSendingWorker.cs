using MassTransit;
using Microsoft.Extensions.Logging;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Workers;

/// <summary>
/// 消费队列中的邮件消息并交给底层邮件 provider 投递。
/// </summary>
/// <param name="emailProvider">邮件投递 provider。</param>
/// <param name="logger">消费和投递日志记录器。</param>
public sealed class EmailSendingWorker(
    IEmailProvider emailProvider, ILogger<EmailSendingWorker> logger)
    : IConsumer<EmailMessageWrapper>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<EmailMessageWrapper> context)
    {
        var messageWrapper = context.Message;
        logger.LogInformation("MQ has received the message. Sending....");

        await emailProvider.DeliverEmailAsync(messageWrapper, context.CancellationToken);
        logger.LogInformation("MQ email sending task completed.");
    }
}
