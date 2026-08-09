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

        group.MapGet("/me", GetCurrentProfileAsync);

        group.MapPut("/me/profile", UpdateProfileAsync);
        group.MapGet("/me/word-study-settings", GetWordStudySettingsAsync);
        group.MapPut("/me/word-study-settings", UpdateWordStudySettingsAsync);

        group.MapGet("/{id:guid}/profile", GetPublicProfileAsync);

        var adminGroup = endpoints.MapGroup("/admin/users")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("", GetAdminUsersAsync);

        adminGroup.MapGet("/{id:guid}", GetAdminUserAsync);

        adminGroup.MapPost("/{id:guid}/ban", BanUserAsync);

        adminGroup.MapPost("/{id:guid}/unban", UnbanUserAsync);

        adminGroup.MapPost("/{id:guid}/role", UpdateUserRoleAsync);

        return endpoints;
    }

    /// <summary>
    /// 获取当前已认证用户的私有账户资料。
    /// </summary>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>当前用户资料成功响应。</returns>
    public static async Task<Ok<CurrentUserProfileResponse>> GetCurrentProfileAsync(
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.GetCurrentProfileAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 更新已认证用户的公开资料。
    /// </summary>
    /// <param name="request">资料更新请求。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的用户资料响应。</returns>
    public static async Task<Ok<CurrentUserProfileResponse>> UpdateProfileAsync(
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
    /// 获取当前用户每日自动背诵数量设置。
    /// </summary>
    public static async Task<Ok<WordStudySettingsResponse>> GetWordStudySettingsAsync(
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.GetWordStudySettingsAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 更新当前用户每日自动背诵数量设置。
    /// </summary>
    public static async Task<Ok<WordStudySettingsResponse>> UpdateWordStudySettingsAsync(
        UpdateWordStudySettingsRequest request,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.UpdateWordStudySettingsAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 获取指定有效用户的最小公开资料。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>公开用户资料成功响应。</returns>
    public static async Task<Ok<PublicUserProfileResponse>> GetPublicProfileAsync(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.GetPublicProfileAsync(id, cancellationToken));

    /// <summary>
    /// 获取管理员可见的用户分页列表。
    /// </summary>
    /// <param name="request">分页与筛选条件。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>用户分页成功响应。</returns>
    public static async Task<Ok<PagedResponse<AdminUserListItemResponse>>> GetAdminUsersAsync(
        [AsParameters] AdminUserListRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.GetAdminListAsync(request, cancellationToken));

    /// <summary>
    /// 获取管理员可见的单个用户详情。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>用户管理详情成功响应。</returns>
    public static async Task<Ok<AdminUserDetailResponse>> GetAdminUserAsync(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await userService.GetAdminByIdAsync(id, cancellationToken));

    /// <summary>
    /// 以当前管理员身份封禁目标用户。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="request">包含必填封禁原因的请求。</param>
    /// <param name="principal">当前已认证管理员。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> BanUserAsync(
        Guid id,
        BanUserRequest request,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        await userService.BanAsync(
            EndpointIdentity.GetUserId(principal), id, request, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 解除目标用户的封禁状态。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="principal">当前已认证管理员。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> UnbanUserAsync(
        Guid id,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        await userService.UnbanAsync(
            EndpointIdentity.GetUserId(principal), id, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 更新目标用户的授权角色。
    /// </summary>
    /// <param name="id">目标用户标识。</param>
    /// <param name="request">角色更新请求。</param>
    /// <param name="principal">当前已认证管理员。</param>
    /// <param name="userService">用户业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的角色响应。</returns>
    public static async Task<Ok<UserRoleResponse>> UpdateUserRoleAsync(
        Guid id,
        UpdateRoleRequest request,
        ClaimsPrincipal principal,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var response = await userService.UpdateRoleAsync(
            EndpointIdentity.GetUserId(principal), id, request, cancellationToken);
        return TypedResults.Ok(response);
    }
}
