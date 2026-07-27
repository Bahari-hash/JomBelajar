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
/// 定义编辑者视频管理和登录用户目录、播放、进度 HTTP endpoints。
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
        var editor = endpoints.MapGroup("/editor/videos")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editor.MapPost("", CreateVideoAsync);
        editor.MapGet("", GetEditorVideosAsync);
        editor.MapGet("/{id:guid}", GetEditorVideoAsync);
        editor.MapPut("/{id:guid}", UpdateVideoAsync);
        editor.MapPost("/{id:guid}/publish", PublishVideoAsync);
        editor.MapPost("/{id:guid}/unpublish", UnpublishVideoAsync);
        editor.MapPost("/{id:guid}/retry", RetryVideoAsync);
        editor.MapPost("/{id:guid}/subtitles", AddSubtitleAsync);
        editor.MapDelete("/{id:guid}/subtitles/{subtitleId:guid}", RemoveSubtitleAsync);

        var videos = endpoints.MapGroup("/videos")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        videos.MapGet("", GetCatalogAsync);
        videos.MapGet("/{id:guid}", GetDetailsAsync);
        videos.MapPost("/{id:guid}/playback", GetPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.VideoPlaybackLimit);
        videos.MapPut("/{id:guid}/progress", UpdateProgressAsync)
            .RequireRateLimiting(RateLimitPolicies.VideoProgressLimit);
        return endpoints;
    }

    /// <summary>
    /// 创建视频草稿和首个持久化处理任务。
    /// </summary>
    public static async Task<Created<EditorVideoResponse>> CreateVideoAsync(
        CreateVideoRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        var response = await videoService.CreateAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/editor/videos/{response.Id}", response);
    }

    /// <summary>
    /// 返回当前编辑者的视频管理分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<EditorVideoListItemResponse>>> GetEditorVideosAsync(
        [AsParameters] EditorVideoListRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetEditorListAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 返回当前编辑者拥有的视频管理详情。
    /// </summary>
    public static async Task<Ok<EditorVideoResponse>> GetEditorVideoAsync(
        Guid id,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.GetEditorByIdAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 更新当前编辑者视频的展示元数据。
    /// </summary>
    public static async Task<Ok<EditorVideoResponse>> UpdateVideoAsync(
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
    public static async Task<Ok<EditorVideoResponse>> PublishVideoAsync(
        Guid id,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.PublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 幂等下架一个已经发布的视频。
    /// </summary>
    public static async Task<Ok<EditorVideoResponse>> UnpublishVideoAsync(
        Guid id,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.UnpublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 为失败视频创建新的不可变输出版本任务。
    /// </summary>
    public static async Task<Ok<EditorVideoResponse>> RetryVideoAsync(
        Guid id,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await videoService.RetryAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 关联一条经过基础内容验证的 WebVTT 字幕。
    /// </summary>
    public static async Task<Created<EditorVideoSubtitleResponse>> AddSubtitleAsync(
        Guid id,
        AddVideoSubtitleRequest request,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        var response = await videoService.AddSubtitleAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created(
            $"/api/editor/videos/{id}/subtitles/{response.Id}",
            response);
    }

    /// <summary>
    /// 删除一条当前编辑者视频的字幕关联。
    /// </summary>
    public static async Task<NoContent> RemoveSubtitleAsync(
        Guid id,
        Guid subtitleId,
        ClaimsPrincipal principal,
        IVideoService videoService,
        CancellationToken cancellationToken)
    {
        await videoService.RemoveSubtitleAsync(
            id,
            subtitleId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.NoContent();
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
