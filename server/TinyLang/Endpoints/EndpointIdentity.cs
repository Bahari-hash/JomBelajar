using System.Globalization;
using System.Security.Claims;
using TinyLang.Constants;
using TinyLang.Exceptions;

namespace TinyLang.Endpoints;

public static class EndpointIdentity
{
    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtClaimNamesExtension.UserId);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
    }

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

public readonly record struct AccessTokenIdentity(
    Guid UserId,
    string TokenId,
    DateTimeOffset ExpiresAt);
