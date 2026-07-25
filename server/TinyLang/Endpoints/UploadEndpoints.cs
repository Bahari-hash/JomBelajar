using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义简单 PUT、Multipart Upload、上传确认和终止的 HTTP endpoints。
/// </summary>
public static class UploadEndpoints
{
    /// <summary>
    /// 注册简单上传、Multipart Upload 和资源确认路由。
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

        group.MapPost("/editor/media/multipart", CreateMultipartUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireEditor)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);

        group.MapPost("/multipart/{sessionId:guid}/parts/presign", PresignMultipartPartsAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);

        group.MapGet("/multipart/{sessionId:guid}", GetMultipartUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/multipart/{sessionId:guid}/complete", CompleteMultipartUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.UploadCommandLimit);

        group.MapDelete("/multipart/{sessionId:guid}", AbortMultipartUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .RequireRateLimiting(RateLimitPolicies.UploadCommandLimit);

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
    /// 为当前编辑者创建大文件 Multipart Upload 会话。
    /// </summary>
    public static async Task<Created<MultipartUploadCreateResponse>> CreateMultipartUploadAsync(
        MultipartUploadRequest request,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var result = await mediaResourceService.CreateMultipartUploadAsync(
            EndpointIdentity.GetUserId(principal),
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            request.Module,
            cancellationToken);
        var response = new MultipartUploadCreateResponse(
            result.ResourceId,
            result.SessionId,
            result.PartSize,
            result.PartCount,
            result.ExpiresAt);
        return TypedResults.Created(
            $"/api/uploads/multipart/{result.SessionId}",
            response);
    }

    /// <summary>
    /// 为当前用户会话批量签发受限的 part 上传地址。
    /// </summary>
    public static async Task<Ok<IReadOnlyList<MultipartPartPresignResponse>>>
        PresignMultipartPartsAsync(
            Guid sessionId,
            MultipartPartPresignRequest request,
            ClaimsPrincipal principal,
            IMediaResourceService mediaResourceService,
            CancellationToken cancellationToken)
    {
        var results = await mediaResourceService.PresignMultipartPartsAsync(
            sessionId,
            EndpointIdentity.GetUserId(principal),
            request.PartNumbers,
            cancellationToken);
        return TypedResults.Ok<IReadOnlyList<MultipartPartPresignResponse>>(
            results.Select(result => new MultipartPartPresignResponse(
                result.PartNumber,
                result.PresignedUrl,
                result.ContentLength,
                result.ExpiresAt)).ToArray());
    }

    /// <summary>
    /// 返回当前用户 Multipart Upload 的恢复状态和已上传 parts。
    /// </summary>
    public static async Task<Ok<MultipartUploadStatusResponse>> GetMultipartUploadAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var result = await mediaResourceService.GetMultipartUploadAsync(
            sessionId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.Ok(ToResponse(result));
    }

    /// <summary>
    /// 完成 provider Multipart Upload，并返回后台归档进度地址。
    /// </summary>
    public static async Task<Accepted<MultipartUploadStatusResponse>> CompleteMultipartUploadAsync(
        Guid sessionId,
        CompleteMultipartUploadRequest request,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        var result = await mediaResourceService.CompleteMultipartUploadAsync(
            sessionId,
            EndpointIdentity.GetUserId(principal),
            request.Parts.Select(part => new ObjectStorageUploadedPart(
                part.PartNumber,
                part.ETag)).ToArray(),
            cancellationToken);
        return TypedResults.Accepted(
            $"/api/uploads/multipart/{sessionId}",
            ToResponse(result));
    }

    /// <summary>
    /// 幂等终止当前用户尚未完成的 Multipart Upload。
    /// </summary>
    public static async Task<NoContent> AbortMultipartUploadAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IMediaResourceService mediaResourceService,
        CancellationToken cancellationToken)
    {
        await mediaResourceService.AbortMultipartUploadAsync(
            sessionId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.NoContent();
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

    /// <summary>
    /// 将应用会话结果投影为不含 provider 内部标识的 HTTP 响应。
    /// </summary>
    private static MultipartUploadStatusResponse ToResponse(
        MultipartUploadStatusResult result)
        => new(
            result.ResourceId,
            result.SessionId,
            result.Status,
            result.PartSize,
            result.PartCount,
            result.ExpiresAt,
            result.UploadedParts.Select(part => new UploadedMultipartPartResponse(
                part.PartNumber,
                part.ETag,
                part.Size)).ToArray());
}
