using System.Globalization;
using System.Security.Claims;
using TinyLang.Constants;
using TinyLang.Exceptions;

namespace TinyLang.Endpoints;

/// <summary>
/// 从已认证 principal 中读取并验证 TinyLang endpoint 所需的令牌身份。
/// </summary>
public static class EndpointIdentity
{
    /// <summary>
    /// 读取当前 access token 中的用户标识。
    /// </summary>
    /// <param name="principal">由 JWT authentication 建立的用户 principal。</param>
    /// <returns>令牌中的用户 GUID。</returns>
    /// <exception cref="UnauthorizedException">用户标识 claim 缺失或格式无效。</exception>
    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtClaimNamesExtension.UserId);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
    }

    /// <summary>
    /// 读取当前 access token 的用户、token 标识和过期时间。
    /// </summary>
    /// <param name="principal">由 JWT authentication 建立的用户 principal。</param>
    /// <returns>验证后的 access token 身份。</returns>
    /// <exception cref="UnauthorizedException">必要 claim 缺失、格式无效或时间超出范围。</exception>
    public static AccessTokenIdentity GetAccessTokenIdentity(ClaimsPrincipal principal)
    {
        var userId = GetUserId(principal);
        var tokenId = principal.FindFirstValue(JwtClaimNamesExtension.TokenId);
        var expirationValue = principal.FindFirstValue(JwtClaimNamesExtension.Expiration);
        if (string.IsNullOrWhiteSpace(tokenId) ||
            !long.TryParse(expirationValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTime))
        {
            throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
        }

        try
        {
            return new AccessTokenIdentity(userId, tokenId, DateTimeOffset.FromUnixTimeSeconds(unixTime));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
        }
    }
}

/// <summary>
/// 表示 endpoint 从 JWT claims 中解析出的 access token 身份。
/// </summary>
/// <param name="UserId">令牌所属用户标识。</param>
/// <param name="TokenId">JWT token 标识。</param>
/// <param name="ExpiresAt">令牌的绝对过期时间。</param>
public readonly record struct AccessTokenIdentity(
    Guid UserId,
    string TokenId,
    DateTimeOffset ExpiresAt);
