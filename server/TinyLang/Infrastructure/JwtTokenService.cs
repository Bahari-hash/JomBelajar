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

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

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
                new Claim(JwtClaimNamesExtension.Name, user.Username),
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

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
