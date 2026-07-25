using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Models;
using TinyLang.Services;

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

        GetRoute(routes, "/api/uploads/editor/media/multipart", "POST")
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireEditor);
        foreach (var (pattern, method) in new[]
        {
            ("/api/uploads/multipart/{sessionId:guid}/parts/presign", "POST"),
            ("/api/uploads/multipart/{sessionId:guid}", "GET"),
            ("/api/uploads/multipart/{sessionId:guid}/complete", "POST"),
            ("/api/uploads/multipart/{sessionId:guid}", "DELETE")
        })
        {
            GetRoute(routes, pattern, method)
                .Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireUser);
        }
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
            "/api/uploads/editor/media/multipart",
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
            $"/api/uploads/multipart/{sessionId}/complete",
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
            $"/api/uploads/multipart/{sessionId}",
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().Be($"/api/uploads/multipart/{sessionId}");
        completed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        completed.Headers.Location.Should().Be($"/api/uploads/multipart/{sessionId}");
        aborted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        service.Verify(x => x.AbortMultipartUploadAsync(
            sessionId, userId, It.IsAny<CancellationToken>()), Times.Once);
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
            options.AddPolicy(AuthorizationPolicies.RequireEditor, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(service);
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
