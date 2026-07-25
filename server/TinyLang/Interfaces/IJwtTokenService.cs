using TinyLang.Entities;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义 access token 和 refresh token 的创建与散列契约。
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// 为指定用户创建带身份、角色和 token version 的 JWT access token。
    /// </summary>
    /// <param name="user">令牌所属用户。</param>
    /// <returns>序列化令牌及其绝对过期时间。</returns>
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);

    /// <summary>
    /// 创建具有足够随机性的 refresh token 明文。
    /// </summary>
    /// <returns>可返回客户端的 refresh token。</returns>
    string CreateRefreshToken();

    /// <summary>
    /// 为持久化或查找 refresh token 生成不可逆摘要。
    /// </summary>
    /// <param name="token">refresh token 明文。</param>
    /// <returns>令牌的稳定摘要。</returns>
    string HashRefreshToken(string token);
}
