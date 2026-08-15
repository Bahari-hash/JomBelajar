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
/// 定义管理员视频管理和登录用户目录、播放、进度 HTTP endpoints。
/// </summary>
public static class VideoEndpoints
{
    /// <summary>
    /// 注册视频管理和登录播放路由及其授权、限流 metadata。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapVideosApi(this RouteGroupBuilder endpoints)
    {
        var userGroup = endpoints.MapGroup("/")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
            
        userGroup.MapGet("/videos", GetCatalogAsync);
        userGroup.MapGet("/videos/{id:guid}", GetDetailsAsync);
        userGroup.MapPost("/videos/{id:guid}/playback", GetPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.VideoPlaybackLimit);
        userGroup.MapPut("/videos/{id:guid}/progress", UpdateProgressAsync)
            .RequireRateLimiting(RateLimitPolicies.VideoProgressLimit);

        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapPost("/videos", CreateVideoAsync);
        adminGroup.MapGet("/videos", GetAdminVideosAsync);
        adminGroup.MapGet("/videos/{id:guid}", GetAdminVideoAsync);
        adminGroup.MapPut("/videos/{id:guid}", UpdateVideoAsync);
        adminGroup.MapPost("/videos/{id:guid}/publish", PublishVideoAsync);
        adminGroup.MapPost("/videos/{id:guid}/unpublish", UnpublishVideoAsync);
        adminGroup.MapPost("/videos/{id:guid}/retry", RetryVideoAsync);
        adminGroup.MapPost("/videos/{id:guid}/archive", ArchiveVideoAsync);
        adminGroup.MapPost("/videos/{id:guid}/playback", GetAdminPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.VideoPlaybackLimit);

        return endpoints;
    }

    /// <summary>
    /// 创建视频草稿和首个持久化处理任务。
    /// </summary>
    public static async Task<Created<AdminVideoResponse>> CreateVideoAsync(
        CreateVideoRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        var response = await videoService.CreateAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/admin/videos/{response.Id}", response);
    }

    /// <summary>
    /// 返回所有管理员协作维护的视频管理分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<AdminVideoListItemResponse>>> GetAdminVideosAsync(
        [AsParameters] AdminVideoListRequest request,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetAdminListAsync(
            request, cancellationToken));

    /// <summary>
    /// 返回全局视频管理详情。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> GetAdminVideoAsync(
        Guid id,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetAdminByIdAsync(
            id, cancellationToken));

    /// <summary>
    /// 更新当前管理员视频的展示元数据。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> UpdateVideoAsync(
        Guid id,
        UpdateVideoRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.UpdateAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 幂等发布一个处理就绪的视频。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> PublishVideoAsync(
        Guid id,
        VideoMutationRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.PublishAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 幂等下架一个已经发布的视频。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> UnpublishVideoAsync(
        Guid id,
        VideoMutationRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.UnpublishAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 为失败视频创建新的不可变输出版本任务。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> RetryVideoAsync(
        Guid id,
        VideoMutationRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.RetryAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 软归档一个未发布且没有活动处理任务的视频。
    /// </summary>
    public static async Task<Ok<AdminVideoResponse>> ArchiveVideoAsync(
        Guid id,
        VideoMutationRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.ArchiveAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 返回任意发布状态下已转码视频的管理员短期预览地址。
    /// </summary>
    public static async Task<Ok<VideoPlaybackResponse>> GetAdminPlaybackAsync(
        Guid id,
        HttpContext httpContext,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        var response = await videoService.GetAdminPlaybackAsync(id, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(response);
    }

    /// <summary>
    /// 返回登录用户可见的已发布视频目录。
    /// </summary>
    public static async Task<Ok<PagedResponse<VideoCatalogItemResponse>>> GetCatalogAsync(
        [AsParameters] VideoCatalogRequest request,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetCatalogAsync(request, cancellationToken));

    /// <summary>
    /// 返回登录用户可见的已发布视频详情。
    /// </summary>
    public static async Task<Ok<VideoDetailsResponse>> GetDetailsAsync(
        Guid id,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetDetailsAsync(id, cancellationToken));

    /// <summary>
    /// 返回当前用户的短期播放地址并禁止授权响应缓存。
    /// </summary>
    public static async Task<Ok<VideoPlaybackResponse>> GetPlaybackAsync(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        var response = await videoService.GetPlaybackAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(response);
    }

    /// <summary>
    /// 原子更新当前登录用户的服务端视频进度。
    /// </summary>
    public static async Task<NoContent> UpdateProgressAsync(
        Guid id,
        UpdateVideoProgressRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        await videoService.UpdateProgressAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.NoContent();
    }
}
