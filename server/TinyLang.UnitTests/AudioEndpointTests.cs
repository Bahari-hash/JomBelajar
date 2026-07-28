using System.Linq;
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
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频 endpoints 的路由、授权和独立播放限流 metadata。
/// </summary>
public sealed class AudioEndpointTests
{
    /// <summary>
    /// 验证编辑者管理路由和普通播放路由使用各自授权策略。
    /// </summary>
    [Fact]
    public async Task EditorAndPlaybackRoutesShouldUseExpectedPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var endpoint in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith(
                "/api/editor/audio",
                StringComparison.Ordinal)))
        {
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value =>
                    value.Policy == AuthorizationPolicies.RequireEditor);
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

    /// <summary>
    /// 验证播放授权处理器明确禁止客户端和共享缓存保存响应。
    /// </summary>
    [Fact]
    public async Task PlaybackResponseShouldUsePrivateNoStore()
    {
        var audioService = new Mock<IAudioClipService>();
        audioService.Setup(value => value.GetPlaybackAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioPlaybackResponse(
                "https://media.example/audio.mp3",
                DateTimeOffset.UtcNow.AddMinutes(5),
                2,
                "en",
                AudioClipKind.Other));
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
        builder.Services.AddSingleton(Mock.Of<IAudioClipService>());
        var app = builder.Build();
        app.MapGroup("/api").MapAudioApi();
        return app;
    }

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
