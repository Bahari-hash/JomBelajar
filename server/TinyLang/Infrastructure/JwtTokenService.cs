using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TinyLang.Constants;
using TinyLang.Entities;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用配置的对称密钥创建 JWT，并生成和散列 refresh token。
/// </summary>
/// <param name="options">JWT 签名和有效期配置。</param>
public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenExpMinutes).ToUnixTimeSeconds());
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtClaimNamesExtension.UserId, user.Id.ToString()),
                new Claim(JwtClaimNamesExtension.TokenId, Guid.NewGuid().ToString("N")),
                new Claim(JwtClaimNamesExtension.Name, user.Email),
                new Claim(JwtClaimNamesExtension.Role, user.Role.ToString()),
                new Claim(JwtClaimNamesExtension.TokenVersion, user.TokenVersion.ToString())
            ]),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Expires = expiresAt.UtcDateTime,
            NotBefore = DateTime.UtcNow,
            SigningCredentials = credentials
        };
        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expiresAt);
    }

    /// <inheritdoc />
    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <inheritdoc />
    public string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
