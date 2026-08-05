using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义当前用户密码、邮箱和账户删除的安全 endpoints。
/// </summary>
public static class SecurityEndpoints
{
    /// <summary>
    /// 注册要求普通用户授权的账户安全路由。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapSecurityApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPut("/me/reset-password", ResetPasswordAsync);

        group.MapPut("/me/change-email", ChangeEmailAsync);

        group.MapDelete("/me/delete-account", DeleteAccountAsync)
            .RequireRateLimiting(RateLimitPolicies.StrictCodeLimit);

        return endpoints;
    }

    /// <summary>
    /// 使用验证码重置当前用户密码。
    /// </summary>
    /// <param name="request">密码重置请求。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="securityService">账户安全业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> ResetPasswordAsync(
        ResetPasswordRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        await securityService.ResetPasswordAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 使用验证码换绑当前用户邮箱。
    /// </summary>
    /// <param name="request">邮箱换绑请求。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="securityService">账户安全业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>邮箱更新后的成功响应。</returns>
    public static async Task<Ok<ChangeEmailResponse>> ChangeEmailAsync(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        var response = await securityService.ChangeEmailAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Ok(response);
    }

    /// <summary>
    /// 使用验证码软删除当前用户账户。
    /// </summary>
    /// <param name="request">账户删除请求。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="securityService">账户安全业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        await securityService.DeleteAccountAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.NoContent();
    }
}
