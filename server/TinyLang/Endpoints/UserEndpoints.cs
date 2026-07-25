using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义用户资料和管理员用户管理的 HTTP endpoints。
/// </summary>
public static class UserEndpoints
{
    /// <summary>
    /// 注册当前用户资料路由和管理员用户管理路由。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
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

    /// <summary>
    /// 更新已认证用户的公开资料。
    /// </summary>
    /// <param name="request">资料更新请求。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的用户资料响应。</returns>
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

    /// <summary>
    /// 以当前管理员身份封禁目标用户。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="principal">当前已认证管理员。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
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

    /// <summary>
    /// 解除目标用户的封禁状态。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> UnbanUserAsync(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        await userService.UnbanAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 更新目标用户的授权角色。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="request">角色更新请求。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的角色响应。</returns>
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
