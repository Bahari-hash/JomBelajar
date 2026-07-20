using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class TokenBlacklist(IDistributedCache cache) : ITokenBlacklist
{
    private static string Key(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"auth:blacklist:{Convert.ToHexString(hash)}";
    }

    public Task AddAsync(string token, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return cache.SetStringAsync(Key(token), "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        }, cancellationToken);
    }

    public async Task<bool> ContainsAsync(string token, CancellationToken cancellationToken = default)
        => await cache.GetStringAsync(Key(token), cancellationToken) is not null;
}
