using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class TokenBlacklist(IDistributedCache cache) : ITokenBlacklist
{
    private static string AccessTokenKey(string tokenId)
    {
        return $"auth:blacklist:access:{Hash(tokenId)}";
    }

    private static string RefreshTokenKey(string token)
        => $"auth:blacklist:refresh:{Hash(token)}";

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public Task AddAccessTokenAsync(
        string tokenId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
        => AddAsync(AccessTokenKey(tokenId), expiresAt, cancellationToken);

    public Task AddRefreshTokenAsync(
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
        => AddAsync(RefreshTokenKey(token), expiresAt, cancellationToken);

    public async Task<bool> ContainsAccessTokenAsync(
        string tokenId,
        CancellationToken cancellationToken = default)
        => await cache.GetStringAsync(AccessTokenKey(tokenId), cancellationToken) is not null;

    private Task AddAsync(
        string key,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        }, cancellationToken);
    }
}
