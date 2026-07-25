using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using TinyLang.Constants;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 Redis TTL 保存 access token 和 refresh token 的撤销标记。
/// </summary>
/// <param name="redisConnection">Redis 连接复用器。</param>
public sealed class TokenBlacklist(IConnectionMultiplexer redisConnection) : ITokenBlacklist
{
    /// <summary>
    /// 构建不暴露原始 token ID 的 access token 黑名单键。
    /// </summary>
    /// <param name="tokenId">JWT token 标识。</param>
    /// <returns>带应用命名空间的 Redis 键。</returns>
    private static string AccessTokenKey(string tokenId)
    {
        return CacheKeys.BuildRedisKey($"auth:blacklist:access:{Hash(tokenId)}");
    }

    /// <summary>
    /// 构建不暴露 refresh token 明文的黑名单键。
    /// </summary>
    /// <param name="token">refresh token 明文。</param>
    /// <returns>带应用命名空间的 Redis 键。</returns>
    private static string RefreshTokenKey(string token)
        => CacheKeys.BuildRedisKey($"auth:blacklist:refresh:{Hash(token)}");

    /// <summary>
    /// 计算用于 Redis 键的 SHA-256 十六进制摘要。
    /// </summary>
    /// <param name="value">待摘要的敏感值。</param>
    /// <returns>大写十六进制摘要。</returns>
    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <inheritdoc />
    public Task AddAccessTokenAsync(
        string tokenId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
        => AddAsync(AccessTokenKey(tokenId), expiresAt, cancellationToken);

    /// <inheritdoc />
    public Task AddRefreshTokenAsync(
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
        => AddAsync(RefreshTokenKey(token), expiresAt, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ContainsAccessTokenAsync(
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        return await database.KeyExistsAsync(AccessTokenKey(tokenId))
            .WaitAsync(cancellationToken);
    }

    /// <summary>
    /// 将撤销标记写入 Redis，并以令牌剩余有效期作为 TTL。
    /// </summary>
    /// <param name="key">黑名单 Redis 键。</param>
    /// <param name="expiresAt">令牌绝对过期时间。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>表示异步写入操作的任务。</returns>
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
