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
    public async Task EditorAndUserRoutesShouldUseExpectedPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRoutes(app);

        foreach (var endpoint in routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith("/api/editor/videos", StringComparison.Ordinal)))
        {
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(value => value.Policy == AuthorizationPolicies.RequireEditor);
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

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IVideoService>());
        var app = builder.Build();
        app.MapGroup("/api").MapVideosApi();
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
