using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频 endpoints 的路由、授权和独立播放限流 metadata。
/// </summary>
public sealed class AudioEndpointTests
{
    /// <summary>
    /// 验证管理员管理路由和普通播放路由使用各自授权策略。
    /// </summary>
    [Fact]
    public async Task AdminAndPlaybackRoutesShouldUseExpectedPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var endpoint in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith(
                "/api/admin/audio",
                StringComparison.Ordinal)))
        {
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value =>
                    value.Policy == AuthorizationPolicies.RequireAdmin);
        }
        GetRoute(routes, "/api/audio/{id:guid}/playback", "POST")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireUser);
    }

    /// <summary>
    /// 验证音频播放使用独立于视频的限流策略。
    /// </summary>
    [Fact]
    public async Task PlaybackShouldUseIndependentAudioRateLimit()
    {
        await using var app = CreateApp();

        GetRoute(GetRoutes(app), "/api/audio/{id:guid}/playback", "POST")
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(RateLimitPolicies.AudioPlaybackLimit);
    }

    [Fact]
    public async Task DedicatedAudioUploadRoutesShouldRequireAdmin()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var (pattern, method) in new[]
        {
            ("/api/admin/audio/uploads/simple", "POST"),
            ("/api/admin/audio/uploads/multipart", "POST"),
            ("/api/admin/audio/{id:guid}/upload/confirm", "PUT"),
            ("/api/admin/audio/multipart/{sessionId:guid}/parts/presign", "POST"),
            ("/api/admin/audio/multipart/{sessionId:guid}", "GET"),
            ("/api/admin/audio/multipart/{sessionId:guid}/complete", "POST"),
            ("/api/admin/audio/multipart/{sessionId:guid}", "DELETE")
        })
        {
            GetRoute(routes, pattern, method)
                .Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        }
    }

    [Fact]
    public async Task AdminAudioResourceRoutesShouldExposeOnlyCurrentContract()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var (pattern, method) in new[]
        {
            ("/api/admin/audio", "GET"),
            ("/api/admin/audio/{id:guid}", "GET"),
            ("/api/admin/audio/{id:guid}/name", "PATCH"),
            ("/api/admin/audio/{id:guid}/retry-upload", "POST"),
            ("/api/admin/audio/{id:guid}/reprocess", "POST"),
            ("/api/admin/audio/{id:guid}", "DELETE")
        })
        {
            GetRoute(routes, pattern, method)
                .Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        }

        routes.Where(value => value.RoutePattern.RawText == "/api/admin/audio")
            .SelectMany(value => value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            .Should().NotContain("POST");
        routes.Where(value => value.RoutePattern.RawText == "/api/admin/audio/{id:guid}")
            .SelectMany(value => value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            .Should().NotContain("PUT");
        routes.Should().NotContain(value =>
            value.RoutePattern.RawText!.Contains("/publish", StringComparison.Ordinal) ||
            value.RoutePattern.RawText.Contains("/unpublish", StringComparison.Ordinal) ||
            value.RoutePattern.RawText.EndsWith("/retry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SimpleUploadInitializationShouldReturnAudioAndMediaIds()
    {
        var adminId = Guid.NewGuid();
        var audioId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var service = new Mock<IAudioResourceService>();
        service.Setup(value => value.InitializeSimpleUploadAsync(
                adminId,
                It.IsAny<InitializeAudioUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioUploadInitializationResponse(
                audioId,
                mediaId,
                "https://media.example/upload",
                null,
                null,
                null,
                null));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimNamesExtension.UserId, adminId.ToString())
        ], "test"));

        var result = await AudioEndpoints.InitializeSimpleUploadAsync(
            new InitializeAudioUploadRequest
            {
                OriginalName = "audio.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 12
            },
            principal,
            service.Object,
            TestContext.Current.CancellationToken);

        result.Value!.AudioResourceId.Should().Be(audioId);
        result.Value.MediaResourceId.Should().Be(mediaId);
    }

    [Fact]
    public async Task MultipartUploadEndpointsShouldReturnApplicationUploadState()
    {
        var adminId = Guid.NewGuid();
        var audioId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var audioService = new Mock<IAudioResourceService>();
        audioService.Setup(value => value.InitializeMultipartUploadAsync(
                adminId,
                It.IsAny<InitializeAudioUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioUploadInitializationResponse(
                audioId,
                mediaId,
                null,
                sessionId,
                16 * 1024 * 1024,
                4,
                expiresAt));
        audioService.Setup(value => value.PresignMultipartPartsAsync(
                sessionId,
                adminId,
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MultipartPartPresignResult(
                    1, "https://media.example/part-1", 16 * 1024 * 1024, expiresAt)
            ]);
        var status = new MultipartUploadStatusResult(
            mediaId,
            sessionId,
            MultipartUploadStatus.Finalizing,
            16 * 1024 * 1024,
            4,
            expiresAt,
            [new ObjectStorageUploadedPart(1, "etag-1", 16 * 1024 * 1024)]);
        audioService.Setup(value => value.GetMultipartUploadAsync(
                sessionId,
                adminId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        audioService.Setup(value => value.CompleteMultipartUploadAsync(
                sessionId,
                adminId,
                It.IsAny<IReadOnlyCollection<ObjectStorageUploadedPart>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        var principal = CreatePrincipal(adminId);

        var initialized = await AudioEndpoints.InitializeMultipartUploadAsync(
            new InitializeAudioUploadRequest
            {
                OriginalName = "audio.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 64L * 1024 * 1024
            },
            principal,
            audioService.Object,
            TestContext.Current.CancellationToken);
        var presigned = await AudioEndpoints.PresignAudioMultipartPartsAsync(
            sessionId,
            new MultipartPartPresignRequest { PartNumbers = [1] },
            principal,
            audioService.Object,
            TestContext.Current.CancellationToken);
        var queried = await AudioEndpoints.GetAudioMultipartUploadAsync(
            sessionId,
            principal,
            audioService.Object,
            TestContext.Current.CancellationToken);
        var completed = await AudioEndpoints.CompleteAudioMultipartUploadAsync(
            sessionId,
            new CompleteMultipartUploadRequest
            {
                Parts = [new CompletedMultipartPartRequest(1, "etag-1")]
            },
            principal,
            audioService.Object,
            TestContext.Current.CancellationToken);
        var aborted = await AudioEndpoints.AbortAudioMultipartUploadAsync(
            sessionId,
            principal,
            audioService.Object,
            TestContext.Current.CancellationToken);

        initialized.Value!.AudioResourceId.Should().Be(audioId);
        initialized.Value.MediaResourceId.Should().Be(mediaId);
        initialized.Value.MultipartSessionId.Should().Be(sessionId);
        presigned.Value.Should().ContainSingle(value => value.PartNumber == 1);
        queried.Value!.Status.Should().Be(MultipartUploadStatus.Finalizing);
        completed.Location.Should().Be($"/api/admin/audio/multipart/{sessionId}");
        aborted.Should().NotBeNull();
        audioService.Verify(value => value.AbortMultipartUploadAsync(
            sessionId, adminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证播放授权处理器明确禁止客户端和共享缓存保存响应。
    /// </summary>
    [Fact]
    public async Task PlaybackResponseShouldUsePrivateNoStore()
    {
        var audioService = new Mock<IAudioResourceService>();
        audioService.Setup(value => value.GetPlaybackAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioResourcePlaybackResponse(
                "https://media.example/audio.mp3",
                DateTimeOffset.UtcNow.AddMinutes(5),
                2));
        var context = new DefaultHttpContext();

        await AudioEndpoints.GetPlaybackAsync(
            Guid.NewGuid(),
            context,
            audioService.Object,
            TestContext.Current.CancellationToken);

        context.Response.Headers.CacheControl.ToString()
            .Should().Be("private, no-store");
    }

    /// <summary>
    /// 创建仅注册音频路由所需服务的测试应用。
    /// </summary>
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IAudioResourceService>());
        builder.Services.AddSingleton(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        app.MapGroup("/api").MapAudioApi();
        return app;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId)
        => new(new ClaimsIdentity(
        [
            new Claim(JwtClaimNamesExtension.UserId, userId.ToString())
        ], "test"));

    /// <summary>
    /// 返回测试应用中的所有路由 endpoints。
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> GetRoutes(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(value => value.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    /// <summary>
    /// 按 pattern 和 HTTP method 查找唯一音频路由。
    /// </summary>
    private static RouteEndpoint GetRoute(
        IReadOnlyCollection<RouteEndpoint> routes,
        string pattern,
        string method)
        => routes.Single(value =>
            value.RoutePattern.RawText == pattern &&
            value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
}
