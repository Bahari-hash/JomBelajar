using TinyLang.Entities;

namespace TinyLang.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
