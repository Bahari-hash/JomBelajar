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
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证单词背诵 endpoints 的路由、授权、身份和 typed HTTP 契约。
/// </summary>
public sealed class WordStudyEndpointTests
{
    /// <summary>
    /// 验证今日背诵和基础背诵路由均统一使用 RequireUser 策略。
    /// </summary>
    [Fact]
    public void RoutesShouldRequireAuthenticatedUser()
    {
        using var app = CreateMetadataApp();
        var routes = GetRoutes(app);

        routes.Should().HaveCount(8);
        routes.Should().OnlyContain(endpoint => endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(value => value.Policy == AuthorizationPolicies.RequireUser));
    }

    [Fact]
    public async Task TodayRoutesShouldUseAuthenticatedUserId()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var service = new Mock<IWordStudyService>();
        service.Setup(value => value.GetTodayAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WordStudyTodayResponse(
                DateTimeOffset.Parse("2026-07-29T00:00:00Z"),
                20,
                WordStudyTodayState.NotStarted,
                null));
        service.Setup(value => value.StartTodayAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionResponse(sessionId));
        await using var app = await CreateHttpAppAsync(service.Object, userId);

        var today = await app.GetTestClient().GetAsync(
            "/api/word-study/today",
            TestContext.Current.CancellationToken);
        var start = await app.GetTestClient().PostAsync(
            "/api/word-study/today/start",
            null,
            TestContext.Current.CancellationToken);

        today.StatusCode.Should().Be(HttpStatusCode.OK);
        start.StatusCode.Should().Be(HttpStatusCode.Created);
        service.Verify(value => value.GetTodayAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(value => value.StartTodayAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证创建从 principal 读取用户并返回会话详情 Location。
    /// </summary>
    [Fact]
    public async Task CreateShouldUseAuthenticatedUserAndReturnLocation()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var service = new Mock<IWordStudyService>();
        service.Setup(value => value.CreateSessionAsync(
                userId,
                It.IsAny<CreateWordStudySessionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionResponse(sessionId));
        await using var app = await CreateHttpAppAsync(service.Object, userId);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/word-study/sessions",
            new CreateWordStudySessionRequest { WordCount = 10 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(
            $"/api/word-study/sessions/{sessionId}");
        service.Verify(value => value.CreateSessionAsync(
            userId,
            It.Is<CreateWordStudySessionRequest>(request =>
                request.WordCount == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证不存在 Active session 和没有下一项时返回 204。
    /// </summary>
    [Fact]
    public async Task EmptyActiveAndNextShouldReturnNoContent()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var service = new Mock<IWordStudyService>();
        service.Setup(value => value.GetActiveSessionAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WordStudySessionResponse?)null);
        service.Setup(value => value.GetNextItemAsync(
                userId,
                sessionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WordStudyNextItemResponse?)null);
        await using var app = await CreateHttpAppAsync(service.Object, userId);

        var active = await app.GetTestClient().GetAsync(
            "/api/word-study/sessions/active",
            TestContext.Current.CancellationToken);
        var next = await app.GetTestClient().GetAsync(
            $"/api/word-study/sessions/{sessionId}/next",
            TestContext.Current.CancellationToken);

        active.StatusCode.Should().Be(HttpStatusCode.NoContent);
        next.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// 验证提交结果和放弃会话只传递路由标识及认证用户身份。
    /// </summary>
    [Fact]
    public async Task ResultAndAbandonShouldUseRouteAndPrincipalIds()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new Mock<IWordStudyService>();
        service.Setup(value => value.SubmitResultAsync(
                userId,
                sessionId,
                itemId,
                It.IsAny<SubmitWordStudyResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionResponse(sessionId));
        service.Setup(value => value.AbandonSessionAsync(
                userId,
                sessionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionResponse(
                sessionId,
                WordStudySessionStatus.Abandoned));
        await using var app = await CreateHttpAppAsync(service.Object, userId);

        var result = await app.GetTestClient().PostAsJsonAsync(
            $"/api/word-study/sessions/{sessionId}/items/{itemId}/result",
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Remembered
            },
            TestContext.Current.CancellationToken);
        var abandon = await app.GetTestClient().PostAsync(
            $"/api/word-study/sessions/{sessionId}/abandon",
            null,
            TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        abandon.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Verify(value => value.SubmitResultAsync(
            userId,
            sessionId,
            itemId,
            It.Is<SubmitWordStudyResultRequest>(request =>
                request.Result == WordStudyResult.Remembered),
            It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(value => value.AbandonSessionAsync(
            userId,
            sessionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证 next JSON 只包含安全词条内容和 AudioClipId。
    /// </summary>
    [Fact]
    public async Task NextResponseShouldNotExposeInternalAudioFields()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var audioClipId = Guid.NewGuid();
        var service = new Mock<IWordStudyService>();
        service.Setup(value => value.GetNextItemAsync(
                userId,
                sessionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WordStudyNextItemResponse(
                sessionId,
                Guid.NewGuid(),
                0,
                1,
                Guid.NewGuid(),
                "hello",
                "en",
                [],
                [new WordPronunciationResponse(audioClipId, null, null, true, 0)]));
        await using var app = await CreateHttpAppAsync(service.Object, userId);

        var response = await app.GetTestClient().GetAsync(
            $"/api/word-study/sessions/{sessionId}/next",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("audioClipId");
        json.Should().NotContain("ownerId");
        json.Should().NotContain("objectName");
        json.Should().NotContain("outputObjectName");
        json.Should().NotContain("failureCode");
        json.Should().NotContain("url");
    }

    /// <summary>
    /// 创建仅用于检查路由 metadata 的应用。
    /// </summary>
    private static WebApplication CreateMetadataApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IWordStudyService>());
        var app = builder.Build();
        app.MapGroup("/api").MapWordStudyApi();
        return app;
    }

    /// <summary>
    /// 创建支持内存 HTTP 请求和测试授权策略的应用。
    /// </summary>
    private static async Task<WebApplication> CreateHttpAppAsync(
        IWordStudyService studyService,
        Guid userId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true)));
        builder.Services.AddSingleton(studyService);
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
        app.MapGroup("/api").MapWordStudyApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// 返回应用中的全部背诵 route endpoints。
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> GetRoutes(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(value => value.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    /// <summary>
    /// 创建 endpoint mock 使用的最小会话响应。
    /// </summary>
    private static WordStudySessionResponse CreateSessionResponse(
        Guid sessionId,
        WordStudySessionStatus status = WordStudySessionStatus.Active)
        => new(
            sessionId,
            20,
            1,
            false,
            WordStudySelectionMode.Sequential,
            null,
            status,
            status == WordStudySessionStatus.Active ? 0 : 1,
            0,
            0,
            status == WordStudySessionStatus.Completed ? 1 : 0,
            DateTimeOffset.UtcNow,
            status == WordStudySessionStatus.Completed
                ? DateTimeOffset.UtcNow
                : null,
            status == WordStudySessionStatus.Abandoned
                ? DateTimeOffset.UtcNow
                : null);
}
