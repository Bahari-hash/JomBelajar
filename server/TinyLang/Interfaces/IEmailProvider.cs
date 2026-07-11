using TinyLang.Models;

namespace TinyLang.Interfaces;

public interface IEmailProvider
{
    Task DeliverEmailAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
}
