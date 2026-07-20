using System.Security.Claims;
using TinyLang.Constants;
using TinyLang.Exceptions;

namespace TinyLang.Endpoints;

internal static class EndpointIdentity
{
    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtClaimNamesExtension.UserId);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
    }
}
