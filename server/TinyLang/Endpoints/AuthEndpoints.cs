using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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

        group.MapPost("/register-token", SendRegisterTokenAsync)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        group.MapPost("/change-email-token", SendChangeEmailTokenAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        group.MapPost("/reset-password-token", SendResetPasswordTokenAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        group.MapPost("/delete-account-token", SendDeleteAccountTokenAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        group.MapPost("/register", RegisterAsync);

        group.MapPost("/login", LoginAsync);

        group.MapPost("/refresh", RefreshAsync);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/admin/users/{userId:guid}/revoke", RevokeUserAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        return endpoints;
    }

    public static async Task<Ok> SendRegisterTokenAsync(
        RegisterTokenRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendRegisterTokenAsync(request.Email, cancellationToken);
        return TypedResults.Ok();
    }

    public static async Task<Ok> SendChangeEmailTokenAsync(
        SendChangeEmailTokenRequest request,
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendChangeEmailTokenAsync(
            EndpointIdentity.GetUserId(principal), request.NewEmail, cancellationToken);
        return TypedResults.Ok();
    }

    public static async Task<Ok> SendResetPasswordTokenAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendResetPasswordTokenAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Ok();
    }

    public static async Task<Ok> SendDeleteAccountTokenAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendDeleteAccountTokenAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Ok();
    }

    public static async Task<Ok<UserResponse>> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return TypedResults.Ok(response);
    }

    public static async Task<Ok<AuthTokenResponse>> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(
            request,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return TypedResults.Ok(response);
    }

    public static async Task<Ok<AuthTokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await authService.RefreshAsync(
            request,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return TypedResults.Ok(response);
    }

    public static async Task<NoContent> LogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var accessToken = EndpointIdentity.GetAccessTokenIdentity(principal);
        await authService.LogoutAsync(
            accessToken.UserId,
            accessToken.TokenId,
            accessToken.ExpiresAt,
            request.RefreshToken,
            cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<NoContent> RevokeUserAsync(
        Guid userId,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.RevokeUserAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }
}
