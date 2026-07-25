using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using TinyLang.Constants;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class TokenBlacklist(IConnectionMultiplexer redisConnection) : ITokenBlacklist
{
    private static string AccessTokenKey(string tokenId)
    {
        return CacheKeys.BuildRedisKey($"auth:blacklist:access:{Hash(tokenId)}");
    }

    private static string RefreshTokenKey(string token)
        => CacheKeys.BuildRedisKey($"auth:blacklist:refresh:{Hash(token)}");

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
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        return await database.KeyExistsAsync(AccessTokenKey(tokenId))
            .WaitAsync(cancellationToken);
    }

    private async Task AddAsync(
        string key,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        await database.StringSetAsync(key, "1", lifetime)
            .WaitAsync(cancellationToken);
    }
}
