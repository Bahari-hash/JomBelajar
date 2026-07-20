using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register/token", async (
            RegisterTokenRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.SendRegisterTokenAsync(request.Email, cancellationToken);
            return Results.Ok();
        });

        group.MapPost("/change-email/token", async (
            SendChangeEmailTokenRequest request,
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.SendChangeEmailTokenAsync(
                EndpointIdentity.GetUserId(principal), request.NewEmail, cancellationToken);
            return Results.Ok();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/reset-password/token", async (
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.SendResetPasswordTokenAsync(
                EndpointIdentity.GetUserId(principal), cancellationToken);
            return Results.Ok();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/delete-me/token", async (
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.SendDeleteAccountTokenAsync(
                EndpointIdentity.GetUserId(principal), cancellationToken);
            return Results.Ok();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.RegisterAsync(request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.LoginAsync(
                request,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IAuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.RefreshAsync(
                request,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/logout", async (
            LogoutRequest request,
            IAuthService authService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = EndpointIdentity.GetUserId(principal);
            var accessToken = httpContext.Request.Headers.Authorization
                .ToString()
                .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
            await authService.LogoutAsync(userId, accessToken, request.RefreshToken, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/users/{userId:guid}/revoke", async (
            Guid userId,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.RevokeUserAsync(userId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        return endpoints;
    }
}
