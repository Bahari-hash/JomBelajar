using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class RateLimitEndpointTests
{
    [Theory]
    [InlineData("/api/auth/register-token")]
    [InlineData("/api/auth/change-email-token")]
    [InlineData("/api/auth/reset-password-token")]
    [InlineData("/api/auth/forgot-password-token")]
    [InlineData("/api/auth/delete-account-token")]
    [InlineData("/api/auth/forgot-password")]
    public async Task VerificationCodeEndpointsShouldNotUseIpRateLimit(string routePattern)
    {
        await using var app = CreateApp();

        GetRateLimitPolicy(app, routePattern).Should().BeNull();
    }

    [Theory]
    [InlineData("/api/auth/forgot-password-token")]
    [InlineData("/api/auth/forgot-password")]
    public async Task ForgotPasswordEndpointsShouldRemainAnonymous(string routePattern)
    {
        await using var app = CreateApp();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(value => value.RoutePattern.RawText == routePattern);

        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/uploads/users/avatar/presign")]
    [InlineData("/api/uploads/admin/media/presign")]
    [InlineData("/api/uploads/admin/media/multipart")]
    [InlineData("/api/uploads/admin/multipart/{sessionId:guid}/parts/presign")]
    public async Task PresignEndpointsShouldUseUploadPresignLimit(string routePattern)
    {
        await using var app = CreateApp();

        GetRateLimitPolicy(app, routePattern).Should().Be(RateLimitPolicies.UploadPresignLimit);
    }

    [Fact]
    public async Task ConfirmUploadEndpointShouldNotUseUploadPresignLimit()
    {
        await using var app = CreateApp();

        GetRateLimitPolicy(app, "/api/uploads/resources/{id:guid}/confirm").Should().BeNull();
    }

    [Theory]
    [InlineData("/api/uploads/admin/multipart/{sessionId:guid}/complete")]
    [InlineData("/api/uploads/admin/multipart/{sessionId:guid}")]
    public async Task MultipartCommandsShouldUseUploadCommandLimit(string routePattern)
    {
        await using var app = CreateApp();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(value =>
                value.RoutePattern.RawText == routePattern &&
                value.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                    .Any(method => method is "POST" or "DELETE"));
        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(RateLimitPolicies.UploadCommandLimit);
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IAuthService>(Mock.Of<IAuthService>());
        builder.Services.AddSingleton<IAccountSecurityService>(
            Mock.Of<IAccountSecurityService>());
        builder.Services.AddSingleton<IMediaResourceService>(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        var endpoints = app.MapGroup("/api");
        endpoints.MapAuthApi();
        endpoints.MapUploadsApi();
        return app;
    }

    private static string? GetRateLimitPolicy(WebApplication app, string routePattern)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == routePattern)
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
}
