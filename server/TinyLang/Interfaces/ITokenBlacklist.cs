namespace TinyLang.Interfaces;

public interface ITokenBlacklist
{
    Task AddAccessTokenAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<bool> ContainsAccessTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(string token, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
