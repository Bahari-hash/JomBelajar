namespace TinyLang.Interfaces;

/// <summary>
/// 定义 access token 和 refresh token 撤销状态的缓存契约。
/// </summary>
public interface ITokenBlacklist
{
    /// <summary>
    /// 将 access token 标识加入黑名单并保留至令牌过期。
    /// </summary>
    /// <param name="tokenId">JWT token 标识。</param>
    /// <param name="expiresAt">令牌的绝对过期时间。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    Task AddAccessTokenAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断 access token 标识是否已被列入黑名单。
    /// </summary>
    /// <param name="tokenId">JWT token 标识。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>令牌已被撤销时返回 <see langword="true"/>。</returns>
    Task<bool> ContainsAccessTokenAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 refresh token 加入黑名单并保留至令牌过期。
    /// </summary>
    /// <param name="token">refresh token 明文。</param>
    /// <param name="expiresAt">令牌的绝对过期时间。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    Task AddRefreshTokenAsync(string token, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
