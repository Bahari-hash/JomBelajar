using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Models;
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
        var userGroup = endpoints.MapGroup("/")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        userGroup.MapPost("/audio/{id:guid}/playback", GetPlaybackAsync)
            .RequireRateLimiting(RateLimitPolicies.AudioPlaybackLimit);

        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapPost("/audio", CreateAudioAsync);
        adminGroup.MapGet("/audio", GetAdminAudioAsync);
        adminGroup.MapGet("/audio/{id:guid}", GetAdminAudioByIdAsync);
        adminGroup.MapPut("/audio/{id:guid}", UpdateAudioAsync);
        adminGroup.MapPost("/audio/{id:guid}/publish", PublishAudioAsync);
        adminGroup.MapPost("/audio/{id:guid}/unpublish", UnpublishAudioAsync);
        adminGroup.MapPost("/audio/{id:guid}/retry", RetryAudioAsync);
        adminGroup.MapPost("/audio/uploads/simple", InitializeSimpleUploadAsync)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);
        adminGroup.MapPost("/audio/uploads/multipart", InitializeMultipartUploadAsync)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);
        adminGroup.MapPut("/audio/{id:guid}/upload/confirm", ConfirmAudioUploadAsync);
        adminGroup.MapPost("/audio/multipart/{sessionId:guid}/parts/presign", PresignAudioMultipartPartsAsync)
            .RequireRateLimiting(RateLimitPolicies.UploadPresignLimit);
        adminGroup.MapGet("/audio/multipart/{sessionId:guid}", GetAudioMultipartUploadAsync);
        adminGroup.MapPost("/audio/multipart/{sessionId:guid}/complete", CompleteAudioMultipartUploadAsync)
            .RequireRateLimiting(RateLimitPolicies.UploadCommandLimit);
        adminGroup.MapDelete("/audio/multipart/{sessionId:guid}", AbortAudioMultipartUploadAsync)
            .RequireRateLimiting(RateLimitPolicies.UploadCommandLimit);

        return endpoints;
    }

    public static async Task<Created<AudioUploadInitializationResponse>> InitializeSimpleUploadAsync(
        InitializeAudioUploadRequest request,
        ClaimsPrincipal principal,
        IAudioResourceService audioService,
        CancellationToken cancellationToken)
    {
        var response = await audioService.InitializeSimpleUploadAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Created($"/api/admin/audio/{response.AudioResourceId}", response);
    }

    public static async Task<Created<AudioUploadInitializationResponse>> InitializeMultipartUploadAsync(
        InitializeAudioUploadRequest request,
        ClaimsPrincipal principal,
        IAudioResourceService audioService,
        CancellationToken cancellationToken)
    {
        var response = await audioService.InitializeMultipartUploadAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Created($"/api/admin/audio/{response.AudioResourceId}", response);
    }

    public static async Task<NoContent> ConfirmAudioUploadAsync(
        Guid id,
        ClaimsPrincipal principal,
        IAudioResourceService audioService,
        CancellationToken cancellationToken)
    {
        await audioService.ConfirmUploadAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<IReadOnlyList<MultipartPartPresignResponse>>> PresignAudioMultipartPartsAsync(
        Guid sessionId,
        MultipartPartPresignRequest request,
        ClaimsPrincipal principal,
        IAudioResourceService audioResourceService,
        CancellationToken cancellationToken)
    {
        var results = await audioResourceService.PresignMultipartPartsAsync(
            sessionId, EndpointIdentity.GetUserId(principal), request.PartNumbers, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<MultipartPartPresignResponse>>(
            results.Select(value => new MultipartPartPresignResponse(
                value.PartNumber, value.PresignedUrl, value.ContentLength, value.ExpiresAt)).ToArray());
    }

    public static async Task<Ok<MultipartUploadStatusResponse>> GetAudioMultipartUploadAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IAudioResourceService audioResourceService,
        CancellationToken cancellationToken)
    {
        var result = await audioResourceService.GetMultipartUploadAsync(
            sessionId, EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Ok(ToMultipartResponse(result));
    }

    public static async Task<Accepted<MultipartUploadStatusResponse>> CompleteAudioMultipartUploadAsync(
        Guid sessionId,
        CompleteMultipartUploadRequest request,
        ClaimsPrincipal principal,
        IAudioResourceService audioResourceService,
        CancellationToken cancellationToken)
    {
        var result = await audioResourceService.CompleteMultipartUploadAsync(
            sessionId,
            EndpointIdentity.GetUserId(principal),
            request.Parts.Select(value => new ObjectStorageUploadedPart(value.PartNumber, value.ETag)).ToArray(),
            cancellationToken);
        return TypedResults.Accepted($"/api/admin/audio/multipart/{sessionId}", ToMultipartResponse(result));
    }

    public static async Task<NoContent> AbortAudioMultipartUploadAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IAudioResourceService audioResourceService,
        CancellationToken cancellationToken)
    {
        await audioResourceService.AbortMultipartUploadAsync(
            sessionId, EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.NoContent();
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

    private static MultipartUploadStatusResponse ToMultipartResponse(
        MultipartUploadStatusResult result)
        => new(
            result.ResourceId,
            result.SessionId,
            result.Status,
            result.PartSize,
            result.PartCount,
            result.ExpiresAt,
            result.UploadedParts.Select(value => new UploadedMultipartPartResponse(
                value.PartNumber,
                value.ETag,
                value.Size)).ToArray());
}
