using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Models;
using TinyLang.Policies;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class UploadEndpointTests
{
    [Fact]
    public async Task MultipartRoutesShouldUseExpectedAuthorizationPolicies()
    {
        await using var app = CreateMetadataApp();
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        GetRoute(routes, "/api/uploads/admin/media/multipart", "POST")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        GetRoute(routes, "/api/uploads/admin/media/capabilities", "GET")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        foreach (var (pattern, method) in new[]
        {
            ("/api/uploads/admin/multipart/{sessionId:guid}/parts/presign", "POST"),
            ("/api/uploads/admin/multipart/{sessionId:guid}", "GET"),
            ("/api/uploads/admin/multipart/{sessionId:guid}/complete", "POST"),
            ("/api/uploads/admin/multipart/{sessionId:guid}", "DELETE")
        })
        {
            GetRoute(routes, pattern, method)
                .Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        }

        routes.Should().NotContain(endpoint =>
            endpoint.RoutePattern.RawText!.StartsWith(
                "/api/uploads/multipart",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task MultipartCreateCompleteAndAbortShouldReturnTypedSemantics()
    {
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var service = new Mock<IMediaResourceService>();
        service.Setup(x => x.CreateMultipartUploadAsync(
                userId,
                "course.mp4",
                ".mp4",
                64L * 1024 * 1024,
                "video/mp4",
                ResourceModule.CourseVideo,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MultipartUploadCreateResult(
                resourceId, sessionId, 16L * 1024 * 1024, 4, expiresAt));
        service.Setup(x => x.CompleteMultipartUploadAsync(
                sessionId,
                userId,
                It.IsAny<IReadOnlyCollection<ObjectStorageUploadedPart>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MultipartUploadStatusResult(
                resourceId,
                sessionId,
                MultipartUploadStatus.Finalizing,
                16L * 1024 * 1024,
                4,
                expiresAt,
                []));
        await using var app = await CreateHttpAppAsync(service.Object, userId);
        var client = app.GetTestClient();

        var created = await client.PostAsJsonAsync(
            "/api/uploads/admin/media/multipart",
            new MultipartUploadRequest
            {
                OriginalName = "course.mp4",
                Extension = ".mp4",
                ContentType = "video/mp4",
                Size = 64L * 1024 * 1024,
                Module = ResourceModule.CourseVideo
            },
            TestContext.Current.CancellationToken);
        var completed = await client.PostAsJsonAsync(
            $"/api/uploads/admin/multipart/{sessionId}/complete",
            new CompleteMultipartUploadRequest
            {
                Parts =
                [
                    new(1, "etag-1"),
                    new(2, "etag-2"),
                    new(3, "etag-3"),
                    new(4, "etag-4")
                ]
            },
            TestContext.Current.CancellationToken);
        var aborted = await client.DeleteAsync(
            $"/api/uploads/admin/multipart/{sessionId}",
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().Be($"/api/uploads/admin/multipart/{sessionId}");
        completed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        completed.Headers.Location.Should().Be($"/api/uploads/admin/multipart/{sessionId}");
        aborted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        service.Verify(x => x.AbortMultipartUploadAsync(
            sessionId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CapabilityShouldReturnOrderedPolicyAndMultipartLimitsWithoutProviderData()
    {
        await using var app = await CreateHttpAppAsync(
            Mock.Of<IMediaResourceService>(),
            Guid.NewGuid());

        var response = await app.GetTestClient().GetAsync(
            "/api/uploads/admin/media/capabilities?module=CourseVideo",
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("maxSizeBytes").GetInt64()
            .Should().Be(1024L * 1024 * 1024);
        json.RootElement.GetProperty("multipartThresholdBytes").GetInt64()
            .Should().Be(64L * 1024 * 1024);
        json.RootElement.GetProperty("partSizeBytes").GetInt64()
            .Should().Be(16L * 1024 * 1024);
        json.RootElement.GetProperty("maxPartCount").GetInt32().Should().Be(10000);
        json.RootElement.GetProperty("partPresignBatchLimit").GetInt32().Should().Be(20);
        json.RootElement.GetProperty("allowedTypes")
            .EnumerateArray()
            .Select(value => value.GetProperty("extension").GetString())
            .Should().Equal(".mp4", ".ogg", ".webm");
        json.RootElement.TryGetProperty("bucket", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("objectName", out _).Should().BeFalse();
    }

    private static WebApplication CreateMetadataApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        app.MapGroup("/api").MapUploadsApi();
        return app;
    }

    private static async Task<WebApplication> CreateHttpAppAsync(
        IMediaResourceService service,
        Guid userId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(new MediaUploadPolicy(
            Options.Create(TestUploadSettings.Create())));
        builder.Services.AddSingleton<IOptions<MultipartUploadSettings>>(
            Options.Create(TestMultipartUploadSettings.Create()));
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimNamesExtension.UserId, userId.ToString())
            ], "Test"));
            await next();
        });
        app.UseAuthorization();
        app.MapGroup("/api").MapUploadsApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static RouteEndpoint GetRoute(
        IEnumerable<RouteEndpoint> routes,
        string pattern,
        string method)
        => routes.Single(endpoint =>
            endpoint.RoutePattern.RawText == pattern &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
}
