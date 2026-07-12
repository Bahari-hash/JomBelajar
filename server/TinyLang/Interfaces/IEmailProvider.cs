using TinyLang.Models;

namespace TinyLang.Interfaces;

public interface IEmailProvider
{
    Task DeliverEmailAsync(EmailMessageWrapper message, CancellationToken cancellationToken = default);
}
