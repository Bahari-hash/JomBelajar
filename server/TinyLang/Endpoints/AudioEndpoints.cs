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
/// 定义编辑者音频管理和登录用户播放授权 HTTP endpoints。
/// </summary>
public static class AudioEndpoints
{
    /// <summary>
    /// 注册音频管理和播放路由及其授权、限流 metadata。
    /// </summary>
    public static RouteGroupBuilder MapAudioApi(this RouteGroupBuilder endpoints)
    {
        var editor = endpoints.MapGroup("/editor/audio")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editor.MapPost("", CreateAudioAsync);
        editor.MapGet("", GetEditorAudioAsync);
        editor.MapGet("/{id:guid}", GetEditorAudioByIdAsync);
        editor.MapPut("/{id:guid}", UpdateAudioAsync);
        editor.MapPost("/{id:guid}/publish", PublishAudioAsync);
        editor.MapPost("/{id:guid}/unpublish", UnpublishAudioAsync);
        editor.MapPost("/{id:guid}/retry", RetryAudioAsync);

        endpoints.MapGroup("/audio")
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .MapPost("/{id:guid}/playback", GetPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.AudioPlaybackLimit);
        return endpoints;
    }

    /// <summary>
    /// 创建音频草稿和首个持久化处理任务。
    /// </summary>
    public static async Task<Created<EditorAudioClipResponse>> CreateAudioAsync(
        CreateAudioClipRequest request,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
    {
        var response = await audioService.CreateAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/editor/audio/{response.Id}", response);
    }

    /// <summary>
    /// 返回当前编辑者的音频管理分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<EditorAudioClipListItemResponse>>>
        GetEditorAudioAsync(
            [AsParameters] EditorAudioClipListRequest request,
            ClaimsPrincipal principal,
            IAudioClipService audioService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.GetEditorListAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 返回当前编辑者拥有的音频管理详情。
    /// </summary>
    public static async Task<Ok<EditorAudioClipResponse>> GetEditorAudioByIdAsync(
        Guid id,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.GetEditorByIdAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 更新当前编辑者音频的展示元数据。
    /// </summary>
    public static async Task<Ok<EditorAudioClipResponse>> UpdateAudioAsync(
        Guid id,
        UpdateAudioClipRequest request,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.UpdateAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 幂等发布一个处理就绪的音频。
    /// </summary>
    public static async Task<Ok<EditorAudioClipResponse>> PublishAudioAsync(
        Guid id,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.PublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 幂等下架一个已经发布的音频。
    /// </summary>
    public static async Task<Ok<EditorAudioClipResponse>> UnpublishAudioAsync(
        Guid id,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.UnpublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 为失败音频创建新的不可变输出版本任务。
    /// </summary>
    public static async Task<Ok<EditorAudioClipResponse>> RetryAudioAsync(
        Guid id,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.RetryAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 返回短期 MP3 地址并禁止播放授权响应缓存。
    /// </summary>
    public static async Task<Ok<AudioPlaybackResponse>> GetPlaybackAsync(
        Guid id,
        HttpContext httpContext,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
    {
        var response = await audioService.GetPlaybackAsync(id, cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(response);
    }
}
