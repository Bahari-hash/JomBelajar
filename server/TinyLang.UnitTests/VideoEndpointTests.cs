using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频 endpoints 的路由、授权和独立限流 metadata。
/// </summary>
public sealed class VideoEndpointTests
{
    [Fact]
    public async Task AdminAndUserRoutesShouldUseExpectedPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var endpoint in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith("/api/admin/videos", StringComparison.Ordinal)))
        {
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireAdmin);
        }
        foreach (var endpoint in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith("/api/videos", StringComparison.Ordinal)))
        {
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireUser);
        }
        routes.Should().NotContain(value =>
            value.RoutePattern.RawText!.Contains("anonymous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PlaybackAndProgressShouldUseIndependentRateLimits()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        GetRoute(routes, "/api/videos/{id:guid}/playback", "POST")
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(RateLimitPolicies.VideoPlaybackLimit);
        GetRoute(routes, "/api/videos/{id:guid}/progress", "PUT")
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(RateLimitPolicies.VideoProgressLimit);
    }

    [Fact]
    public async Task SubtitleRoutesShouldNotBeRegistered()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        routes.Should().NotContain(value =>
            value.RoutePattern.RawText!.Contains("subtitles", StringComparison.Ordinal));
        GetRoute(routes, "/api/admin/videos/{id:guid}/archive", "POST")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireAdmin);
    }

    [Fact]
    public async Task VideoCategoryRoutesShouldUseUserAndAdminPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        GetRoute(routes, "/api/video-categories", "GET")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireUser);
        foreach (var route in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith("/api/admin/video-categories", StringComparison.Ordinal)))
        {
            route.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireAdmin);
        }
        GetRoute(routes, "/api/admin/video-categories/{id:guid}/videos", "DELETE")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireAdmin);
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IVideoService>());
        builder.Services.AddSingleton(Mock.Of<IVideoCategoryService>());
        var app = builder.Build();
        app.MapGroup("/api")
            .MapVideoCategoriesApi()
            .MapVideosApi();
        return app;
    }

    private static IReadOnlyList<RouteEndpoint> GetRoutes(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(value => value.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    private static RouteEndpoint GetRoute(
        IReadOnlyCollection<RouteEndpoint> routes,
        string pattern,
        string method)
        => routes.Single(value =>
            value.RoutePattern.RawText == pattern &&
            value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
}
