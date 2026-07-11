using TinyLang.Models;

namespace TinyLang.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    Task EnqueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
