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
/// 定义管理员音频管理和登录用户播放授权 HTTP endpoints。
/// </summary>
public static class AudioEndpoints
{
    /// <summary>
    /// 注册音频管理和播放路由及其授权、限流 metadata。
    /// </summary>
    public static RouteGroupBuilder MapAudioApi(this RouteGroupBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/audio")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        admin.MapPost("", CreateAudioAsync);
        admin.MapGet("", GetAdminAudioAsync);
        admin.MapGet("/{id:guid}", GetAdminAudioByIdAsync);
        admin.MapPut("/{id:guid}", UpdateAudioAsync);
        admin.MapPost("/{id:guid}/publish", PublishAudioAsync);
        admin.MapPost("/{id:guid}/unpublish", UnpublishAudioAsync);
        admin.MapPost("/{id:guid}/retry", RetryAudioAsync);

        endpoints.MapGroup("/audio")
            .RequireAuthorization(AuthorizationPolicies.RequireUser)
            .MapPost("/{id:guid}/playback", GetPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.AudioPlaybackLimit);
        return endpoints;
    }

    /// <summary>
    /// 创建音频草稿和首个持久化处理任务。
    /// </summary>
    public static async Task<Created<AdminAudioClipResponse>> CreateAudioAsync(
        CreateAudioClipRequest request,
        ClaimsPrincipal principal,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
    {
        var response = await audioService.CreateAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/admin/audio/{response.Id}", response);
    }

    /// <summary>
    /// 返回当前管理员的音频管理分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<AdminAudioClipListItemResponse>>>
        GetAdminAudioAsync(
            [AsParameters] AdminAudioClipListRequest request,
            IAudioClipService audioService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.GetAdminListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 返回当前管理员拥有的音频管理详情。
    /// </summary>
    public static async Task<Ok<AdminAudioClipResponse>> GetAdminAudioByIdAsync(
        Guid id,
        IAudioClipService audioService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await audioService.GetAdminByIdAsync(
            id,
            cancellationToken));

    /// <summary>
    /// 更新当前管理员音频的展示元数据。
    /// </summary>
    public static async Task<Ok<AdminAudioClipResponse>> UpdateAudioAsync(
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
    public static async Task<Ok<AdminAudioClipResponse>> PublishAudioAsync(
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
    public static async Task<Ok<AdminAudioClipResponse>> UnpublishAudioAsync(
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
    public static async Task<Ok<AdminAudioClipResponse>> RetryAudioAsync(
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
