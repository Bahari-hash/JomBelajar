using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义媒体上传预签名和上传确认的 HTTP endpoints。
/// </summary>
public static class UploadEndpoints
{
    /// <summary>
    /// 注册头像、编辑者媒体预签名及资源确认路由。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapUploadsApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/uploads");

        group.MapPost("/users/avatar/presign", CreateAvatarPresignAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);

        group.MapPost("/editor/media/presign", CreateEditorMediaPresignAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireEditor)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);

        group.MapPut("/resources/{id:guid}/confirm", ConfirmUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        return endpoints;
    }

    /// <summary>
    /// 为当前用户创建头像上传资源和预签名地址。
    /// </summary>
    /// <param name="request">头像文件元数据。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="mediaResourceService">媒体资源业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>资源标识、对象名称和预签名地址。</returns>
    public static async Task<Ok<PresignResponse>> CreateAvatarPresignAsync(
        AvatarPresignRequest request,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var response = await mediaResourceService.CreatePendingResourceAndPresignAsync(
            EndpointIdentity.GetUserId(principal),
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            ResourceModule.Avatar,
            cancellationToken);
        return TypedResults.Ok(new PresignResponse(
            response.ResourceId,
            response.PresignedUrl,
            response.ObjectName));
    }

    /// <summary>
    /// 为当前编辑者创建指定模块的媒体上传资源和预签名地址。
    /// </summary>
    /// <param name="request">媒体模块和文件元数据。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="mediaResourceService">媒体资源业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>资源标识、对象名称和预签名地址。</returns>
    public static async Task<Ok<PresignResponse>> CreateEditorMediaPresignAsync(
        EditorMediaPresignRequest request,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var response = await mediaResourceService.CreatePendingResourceAndPresignAsync(
            EndpointIdentity.GetUserId(principal),
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            request.Module,
            cancellationToken);
        return TypedResults.Ok(new PresignResponse(
            response.ResourceId,
            response.PresignedUrl,
            response.ObjectName));
    }

    /// <summary>
    /// 确认当前用户已完成指定媒体资源的对象上传。
    /// </summary>
    /// <param name="id">待确认媒体资源标识。</param>
    /// <param name="principal">当前已认证用户。</param>
    /// <param name="mediaResourceService">媒体资源业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>激活后的媒体资源响应。</returns>
    public static async Task<Ok<MediaResourceResponse>> ConfirmUploadAsync(
        Guid id,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var resource = await mediaResourceService.ConfirmAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.Ok(ToResponse(resource));
    }

    /// <summary>
    /// 将媒体资源实体投影为 HTTP 响应模型。
    /// </summary>
    /// <param name="resource">媒体资源实体。</param>
    /// <returns>媒体资源响应。</returns>
    private static MediaResourceResponse ToResponse(MediaResource resource)
        => new(
            resource.Id,
            resource.UploaderId,
            resource.ObjectName,
            resource.OriginalName,
            resource.Module,
            resource.Status,
            resource.Size,
            resource.Extension,
            resource.ContentType,
            resource.Url,
            resource.CreatedAt);
}
