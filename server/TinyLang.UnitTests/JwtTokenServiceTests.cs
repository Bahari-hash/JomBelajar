using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TinyLang.Constants;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() => new(Options.Create(new JwtSettings
    {
        JwtSecret = "a-test-secret-that-is-at-least-32-characters-long",
        Issuer = "TinyLang.Tests",
        Audience = "TinyLang.Tests.Client",
        AccessTokenExpMinutes = 30,
        RefreshTokenExpMinutes = 1440
    }));

    [Fact]
    public void ShouldCreateAccessTokenWithRequiredClaims()
    {
        var user = new User
        {
            Username = "learner@example.com",
            Email = "learner@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            TokenVersion = 3
        };

        var result = CreateService().CreateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        token.Claims.Single(x => x.Type == JwtClaimNamesExtension.UserId).Value.Should().Be(user.Id.ToString());
        token.Claims.Single(x => x.Type == JwtClaimNamesExtension.TokenId).Value.Should().NotBeNullOrWhiteSpace();
        token.Claims.Single(x => x.Type == JwtClaimNamesExtension.Role).Value.Should().Be(UserRole.User.ToString());
        token.Claims.Single(x => x.Type == JwtClaimNamesExtension.TokenVersion).Value.Should().Be("3");
        DateTimeOffset.FromUnixTimeSeconds(long.Parse(
                token.Claims.Single(x => x.Type == JwtClaimNamesExtension.Expiration).Value))
            .Should().Be(result.ExpiresAt);
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ShouldCreateUniqueAccessTokenIds()
    {
        var user = new User
        {
            Username = "learner@example.com",
            Email = "learner@example.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };
        var service = CreateService();

        var first = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user).Token);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user).Token);

        first.Claims.Single(x => x.Type == JwtClaimNamesExtension.TokenId).Value.Should().NotBe(
            second.Claims.Single(x => x.Type == JwtClaimNamesExtension.TokenId).Value);
    }

    [Fact]
    public void ShouldCreateUniqueRefreshTokens()
    {
        var service = CreateService();

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        first.Should().NotBeNullOrWhiteSpace().And.NotBe(second);
        service.HashRefreshToken(first).Should().NotBe(service.HashRefreshToken(second));
    }
}
