using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 breaking Admin 路由的认证边界及旧 Editor 路由退役行为。
/// </summary>
public sealed class AdminRouteAuthorizationTests
{
    [Theory]
    [InlineData("GET", "/api/admin/articles")]
    [InlineData("GET", "/api/admin/words")]
    [InlineData("GET", "/api/admin/papers")]
    [InlineData("GET", "/api/admin/videos")]
    [InlineData("GET", "/api/admin/audio")]
    [InlineData("POST", "/api/admin/audio/uploads/simple")]
    [InlineData("POST", "/api/admin/audio/uploads/multipart")]
    [InlineData("POST", "/api/uploads/admin/media/presign")]
    [InlineData("POST", "/api/uploads/admin/media/multipart")]
    [InlineData("GET", "/api/uploads/admin/media/capabilities?module=CourseVideo")]
    public async Task AdminRoutesShouldReturnUnauthorizedWithoutAuthentication(
        string method,
        string path)
    {
        await using var app = await CreateAppAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await app.GetTestClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/admin/articles")]
    [InlineData("GET", "/api/admin/words")]
    [InlineData("GET", "/api/admin/papers")]
    [InlineData("GET", "/api/admin/videos")]
    [InlineData("GET", "/api/admin/audio")]
    [InlineData("POST", "/api/admin/audio/uploads/simple")]
    [InlineData("POST", "/api/admin/audio/uploads/multipart")]
    [InlineData("POST", "/api/uploads/admin/media/presign")]
    [InlineData("POST", "/api/uploads/admin/media/multipart")]
    [InlineData("GET", "/api/uploads/admin/media/capabilities?module=CourseVideo")]
    public async Task AdminRoutesShouldReturnForbiddenForUserRole(
        string method,
        string path)
    {
        await using var app = await CreateAppAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestRoleAuthenticationHandler.RoleHeader, "User");
        var response = await app.GetTestClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET", "/api/editor/articles")]
    [InlineData("GET", "/api/editor/words")]
    [InlineData("GET", "/api/editor/papers")]
    [InlineData("GET", "/api/editor/videos")]
    [InlineData("GET", "/api/editor/audio")]
    [InlineData("POST", "/api/uploads/editor/media/presign")]
    [InlineData("POST", "/api/uploads/editor/media/multipart")]
    public async Task RemovedEditorRoutesShouldReturnNotFound(
        string method,
        string path)
    {
        await using var app = await CreateAppAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestRoleAuthenticationHandler.RoleHeader, "Admin");
        var response = await app.GetTestClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestRoleAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>(
                TestRoleAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorizationPolicy();
        builder.Services.AddSingleton(Mock.Of<IArticleService>());
        builder.Services.AddSingleton(Mock.Of<IArticleCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IWordService>());
        builder.Services.AddSingleton(Mock.Of<IPaperService>());
        builder.Services.AddSingleton(Mock.Of<IPaperAttemptService>());
        builder.Services.AddSingleton(Mock.Of<IVideoService>());
        builder.Services.AddSingleton(Mock.Of<IAudioClipService>());
        builder.Services.AddSingleton(Mock.Of<IAudioResourceService>());
        builder.Services.AddSingleton(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGroup("/api")
            .MapUploadsApi()
            .MapArticlesApi()
            .MapVideosApi()
            .MapAudioApi()
            .MapWordsApi()
            .MapOnlineQuizApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}

/// <summary>
/// 将测试 header 中的原始角色值投影为已认证 principal。
/// </summary>
public sealed class TestRoleAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    public const string SchemeName = "TestRole";
    public const string RoleHeader = "X-Test-Role";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtClaimNamesExtension.UserId, Guid.NewGuid().ToString()),
            new Claim(JwtClaimNamesExtension.Role, role.ToString())
        ], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
