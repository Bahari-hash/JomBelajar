using TinyLang.Entities;

namespace TinyLang.Services;

public interface IUserSessionService
{
    Task InvalidateAllAsync(User user, CancellationToken cancellationToken = default);
}
