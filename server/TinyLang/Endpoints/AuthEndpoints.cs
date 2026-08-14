using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Exceptions;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义注册、登录、令牌生命周期和认证验证码的 HTTP endpoints。
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// 注册认证路由及其授权和限流要求。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
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

        group.MapPost("/forgot-password-token", SendForgotPasswordTokenAsync)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        group.MapPut("/forgot-password", ResetForgottenPasswordAsync)
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

    /// <summary>
    /// 向请求邮箱发送注册验证码。
    /// </summary>
    /// <param name="request">注册验证码申请。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<Ok> SendRegisterTokenAsync(
        RegisterTokenRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendRegisterTokenAsync(request.Email, cancellationToken);
        return TypedResults.Ok();
    }

    /// <summary>
    /// 向当前用户的新邮箱发送换绑验证码。
    /// </summary>
    /// <param name="request">新邮箱。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
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

    /// <summary>
    /// 向当前用户邮箱发送密码重置验证码。
    /// </summary>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<Ok> SendResetPasswordTokenAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendResetPasswordTokenAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Ok();
    }

    /// <summary>
    /// 为有效账户发送匿名密码找回验证码，并对未知邮箱保持相同响应。
    /// </summary>
    /// <param name="request">待找回账户的邮箱。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>不暴露邮箱是否存在的成功结果。</returns>
    public static async Task<Ok> SendForgotPasswordTokenAsync(
        ForgotPasswordTokenRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendForgotPasswordTokenAsync(
            request.Email,
            cancellationToken);
        return TypedResults.Ok();
    }

    /// <summary>
    /// 使用邮箱验证码匿名重置账户密码。
    /// </summary>
    /// <param name="request">邮箱、新密码和验证码。</param>
    /// <param name="securityService">账户安全业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> ResetForgottenPasswordAsync(
        ForgotPasswordRequest request,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        await securityService.ResetForgottenPasswordAsync(
            request,
            cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 向当前用户邮箱发送账户删除验证码。
    /// </summary>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<Ok> SendDeleteAccountTokenAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.SendDeleteAccountTokenAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Ok();
    }

    /// <summary>
    /// 验证注册请求并创建用户账户。
    /// </summary>
    /// <param name="request">邮箱、密码和注册验证码。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>新建用户的成功响应。</returns>
    public static async Task<Ok<UserResponse>> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return TypedResults.Ok(response);
    }

    /// <summary>
    /// 使用邮箱密码登录，并从 HTTP 上下文记录客户端信息。
    /// </summary>
    /// <param name="request">登录凭据。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="httpContext">当前 HTTP 上下文。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>新签发令牌和用户信息的成功响应。</returns>
    public static async Task<Ok<AuthSessionResponse>> LoginAsync(
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
        SetRefreshTokenCookie(httpContext, response.RefreshToken);
        return TypedResults.Ok(new AuthSessionResponse(
            response.Token,
            response.ExpiresIn,
            response.User));
    }

    /// <summary>
    /// 轮换 refresh token，并从 HTTP 上下文更新客户端信息。
    /// </summary>
    /// <param name="request">当前 refresh token。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="httpContext">当前 HTTP 上下文。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>轮换后令牌和用户信息的成功响应。</returns>
    public static async Task<Ok<AuthSessionResponse>> RefreshAsync(
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await authService.RefreshAsync(
            ResolveRefreshToken(httpContext),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        SetRefreshTokenCookie(httpContext, response.RefreshToken);
        return TypedResults.Ok(new AuthSessionResponse(
            response.Token,
            response.ExpiresIn,
            response.User));
    }

    /// <summary>
    /// 从当前 principal 读取 access token 身份并退出当前会话。
    /// </summary>
    /// <param name="request">当前 refresh token。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> LogoutAsync(
        IAuthService authService,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var accessToken = EndpointIdentity.GetAccessTokenIdentity(principal);
        await authService.LogoutAsync(
            accessToken.UserId,
            accessToken.TokenId,
            accessToken.ExpiresAt,
            ResolveRefreshToken(httpContext),
            cancellationToken);
        httpContext.Response.Cookies.Delete(RefreshTokenCookieName);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 由管理员撤销目标用户的全部会话。
    /// </summary>
    /// <param name="userId">目标用户标识。</param>
    /// <param name="authService">认证业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> RevokeUserAsync(
        Guid userId,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.RevokeUserAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }

    private const string RefreshTokenCookieName = "tiny-lang.refresh";

    private static string ResolveRefreshToken(HttpContext httpContext)
    {
        var cookieToken = httpContext.Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(cookieToken))
        {
            throw UnauthorizedException.Create(ErrorCodes.RefreshTokenInvalid);
        }
        return cookieToken;
    }

    private static void SetRefreshTokenCookie(
        HttpContext httpContext,
        string refreshToken)
    {
        var jwtSettings = httpContext.RequestServices
            .GetRequiredService<IOptions<JwtSettings>>()
            .Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(
            jwtSettings.RefreshTokenExpMinutes);
        httpContext.Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth",
                Expires = expiresAt,
                IsEssential = true
            });
    }
}
