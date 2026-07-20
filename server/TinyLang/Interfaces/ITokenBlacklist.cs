namespace TinyLang.Interfaces;

public interface ITokenBlacklist
{
    Task AddAsync(string token, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<bool> ContainsAsync(string token, CancellationToken cancellationToken = default);
}
