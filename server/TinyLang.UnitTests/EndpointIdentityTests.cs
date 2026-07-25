using System.Security.Claims;
using FluentAssertions;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

public sealed class EndpointIdentityTests
{
    [Fact]
    public void ShouldReadAccessTokenIdentityFromClaims()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        var principal = CreatePrincipal(userId.ToString(), tokenId, expiresAt.ToUnixTimeSeconds().ToString());

        var identity = EndpointIdentity.GetAccessTokenIdentity(principal);

        identity.UserId.Should().Be(userId);
        identity.TokenId.Should().Be(tokenId);
        identity.ExpiresAt.Should().Be(expiresAt);
    }

    [Theory]
    [InlineData(null, "token-id", "100")]
    [InlineData("not-a-guid", "token-id", "100")]
    [InlineData("00000000-0000-0000-0000-000000000001", null, "100")]
    [InlineData("00000000-0000-0000-0000-000000000001", "token-id", null)]
    [InlineData("00000000-0000-0000-0000-000000000001", "token-id", "not-a-number")]
    [InlineData("00000000-0000-0000-0000-000000000001", "token-id", "9223372036854775807")]
    public void ShouldRejectMissingOrInvalidAccessTokenClaims(
        string? userId,
        string? tokenId,
        string? expiration)
    {
        var principal = CreatePrincipal(userId, tokenId, expiration);

        var action = () => EndpointIdentity.GetAccessTokenIdentity(principal);

        action.Should().Throw<UnauthorizedException>();
    }

    private static ClaimsPrincipal CreatePrincipal(
        string? userId,
        string? tokenId,
        string? expiration)
    {
        var claims = new List<Claim>();
        AddClaim(claims, JwtClaimNamesExtension.UserId, userId);
        AddClaim(claims, JwtClaimNamesExtension.TokenId, tokenId);
        AddClaim(claims, JwtClaimNamesExtension.Expiration, expiration);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static void AddClaim(ICollection<Claim> claims, string type, string? value)
    {
        if (value is not null)
        {
            claims.Add(new Claim(type, value));
        }
    }
}
