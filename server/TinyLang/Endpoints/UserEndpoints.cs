using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUsersApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPut("/me/profile", UpdateProfileAsync);

        var adminGroup = endpoints.MapGroup("/admin/users")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapPost("/{id:guid}/ban", BanUserAsync);

        adminGroup.MapPost("/{id:guid}/unban", UnbanUserAsync);

        adminGroup.MapPost("/{id:guid}/role", UpdateUserRoleAsync);

        return endpoints;
    }

    public static async Task<Ok<UserProfileResponse>> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var response = await userService.UpdateProfileAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Ok(response);
    }

    public static async Task<NoContent> BanUserAsync(
        Guid id,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        await userService.BanAsync(
            EndpointIdentity.GetUserId(principal), id, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<NoContent> UnbanUserAsync(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        await userService.UnbanAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<UserRoleResponse>> UpdateUserRoleAsync(
        Guid id,
        UpdateRoleRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var response = await userService.UpdateRoleAsync(id, request, cancellationToken);
        return TypedResults.Ok(response);
    }
}
