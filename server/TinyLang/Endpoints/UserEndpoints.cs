using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Exceptions;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUsersApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users");

        group.MapPut("/me/profile", async (
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(principal);
            var response = await userService.UpdateProfileAsync(userId, request, cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/{id:guid}/ban", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var operatorId = GetUserId(principal);
            await userService.BanAsync(operatorId, id, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPost("/{id:guid}/unban", async (
            Guid id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            await userService.UnbanAsync(id, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPost("/{id:guid}/role", async (
            Guid id,
            UpdateRoleRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var response = await userService.UpdateRoleAsync(id, request, cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtClaimNamesExtension.UserId);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw UnauthorizedException.Create(ErrorCodes.TokenInvalid);
    }
}
