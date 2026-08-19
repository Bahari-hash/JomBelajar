using System.Linq;
using System.Net;
using System.Net.Http;
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
/// 验证在线试卷 endpoints 的路由、授权、身份、typed status 和安全 JSON 契约。
/// </summary>
public sealed class OnlineQuizEndpointTests
{
    /// <summary>
    /// 验证管理和用户路由均使用对应的授权策略。
    /// </summary>
    [Fact]
    public void RoutesShouldApplyAdminAndUserPolicies()
    {
        using var app = CreateMetadataApp();
        var routes = GetRoutes(app);
        var adminRoutes = routes.Where(value =>
            value.RoutePattern.RawText!.StartsWith("/api/admin/"));
        var userRoutes = routes.Where(value =>
            !value.RoutePattern.RawText!.StartsWith("/api/admin/"));

        routes.Should().HaveCount(28);
        adminRoutes.Should().HaveCount(15).And.OnlyContain(endpoint => endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(value => value.Policy == AuthorizationPolicies.RequireAdmin));
        userRoutes.Should().HaveCount(13).And.OnlyContain(endpoint => endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(value => value.Policy == AuthorizationPolicies.RequireUser));
    }

    /// <summary>
    /// 验证创建试卷从 principal 读取 admin 并返回正确 Location。
    /// </summary>
    [Fact]
    public async Task CreatePaperShouldUsePrincipalAndReturnCreatedLocation()
    {
        var adminId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        var paperService = new Mock<IPaperService>();
        paperService.Setup(value => value.CreateDraftAsync(
                adminId,
                It.IsAny<CreatePaperRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAdminPaperResponse(paperId));
        await using var app = await CreateHttpAppAsync(
            paperService.Object,
            Mock.Of<IPaperAttemptService>(),
            adminId);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/admin/papers",
            new CreatePaperRequest
            {
                Title = "Quiz",
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/api/admin/papers/{paperId}");
        paperService.Verify(value => value.CreateDraftAsync(
            adminId,
            It.IsAny<CreatePaperRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证发布检查、归档和 DELETE body 转发同一客户端并发标识。
    /// </summary>
    [Fact]
    public async Task AdminMutationsShouldForwardRequiredConcurrencyStamp()
    {
        var adminId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        var request = new PaperMutationRequest { ConcurrencyStamp = stamp };
        var paperService = new Mock<IPaperService>();
        paperService.Setup(value => value.ValidateAsync(
                paperId,
                It.IsAny<PaperMutationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperValidationResponse(true, []));
        paperService.Setup(value => value.ArchiveAsync(
                paperId,
                adminId,
                It.IsAny<PaperMutationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAdminPaperResponse(paperId));
        await using var app = await CreateHttpAppAsync(
            paperService.Object,
            Mock.Of<IPaperAttemptService>(),
            adminId);
        var client = app.GetTestClient();

        var validation = await client.PostAsJsonAsync(
            $"/api/admin/papers/{paperId}/validate",
            request,
            TestContext.Current.CancellationToken);
        var archive = await client.PostAsJsonAsync(
            $"/api/admin/papers/{paperId}/archive",
            request,
            TestContext.Current.CancellationToken);
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/papers/{paperId}")
        {
            Content = JsonContent.Create(request)
        };
        var delete = await client.SendAsync(
            deleteRequest,
            TestContext.Current.CancellationToken);

        validation.StatusCode.Should().Be(HttpStatusCode.OK);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        paperService.Verify(value => value.ValidateAsync(
            paperId,
            It.Is<PaperMutationRequest>(body => body.ConcurrencyStamp == stamp),
            It.IsAny<CancellationToken>()), Times.Once);
        paperService.Verify(value => value.ArchiveAsync(
            paperId,
            adminId,
            It.Is<PaperMutationRequest>(body => body.ConcurrencyStamp == stamp),
            It.IsAny<CancellationToken>()), Times.Once);
        paperService.Verify(value => value.DeleteAsync(
            paperId,
            adminId,
            It.Is<PaperMutationRequest>(body => body.ConcurrencyStamp == stamp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证新建测验返回 201/Location，而恢复活动测验返回 200。
    /// </summary>
    [Fact]
    public async Task StartAttemptShouldDistinguishCreatedAndResumedResults()
    {
        var userId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var attempt = CreateAttemptResponse(attemptId, paperId);
        var attemptService = new Mock<IPaperAttemptService>();
        attemptService.SetupSequence(value => value.StartAsync(
                userId,
                paperId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperAttemptStartOutcome(attempt, WasCreated: true))
            .ReturnsAsync(new PaperAttemptStartOutcome(attempt, WasCreated: false));
        await using var app = await CreateHttpAppAsync(
            Mock.Of<IPaperService>(),
            attemptService.Object,
            userId);

        var created = await app.GetTestClient().PostAsync(
            $"/api/papers/{paperId}/attempts",
            null,
            TestContext.Current.CancellationToken);
        var resumed = await app.GetTestClient().PostAsync(
            $"/api/papers/{paperId}/attempts",
            null,
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().Be($"/api/paper-attempts/{attemptId}");
        resumed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// 验证逐题保存返回 204，提交和结果查询返回 200。
    /// </summary>
    [Fact]
    public async Task AnswerAndSubmitEndpointsShouldUseFixedStatusContracts()
    {
        var userId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var attemptService = new Mock<IPaperAttemptService>();
        attemptService.Setup(value => value.SubmitAsync(
                userId,
                attemptId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResultResponse(attemptId, paperId, questionId));
        attemptService.Setup(value => value.GetResultAsync(
                userId,
                attemptId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResultResponse(attemptId, paperId, questionId));
        await using var app = await CreateHttpAppAsync(
            Mock.Of<IPaperService>(),
            attemptService.Object,
            userId);

        var saved = await app.GetTestClient().PutAsJsonAsync(
            $"/api/paper-attempts/{attemptId}/answers/{questionId}",
            new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
            TestContext.Current.CancellationToken);
        var cleared = await app.GetTestClient().DeleteAsync(
            $"/api/paper-attempts/{attemptId}/answers/{questionId}",
            TestContext.Current.CancellationToken);
        var submitted = await app.GetTestClient().PostAsync(
            $"/api/paper-attempts/{attemptId}/submit",
            null,
            TestContext.Current.CancellationToken);
        var result = await app.GetTestClient().GetAsync(
            $"/api/paper-attempts/{attemptId}/result",
            TestContext.Current.CancellationToken);

        saved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        attemptService.Verify(value => value.SaveAnswerAsync(
            userId,
            attemptId,
            questionId,
            It.Is<SavePaperAttemptAnswerRequest>(request =>
                request.BooleanAnswer == true),
            It.IsAny<CancellationToken>()), Times.Once);
        attemptService.Verify(value => value.ClearAnswerAsync(
            userId,
            attemptId,
            questionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证提交前测验 JSON 不存在正确答案、解析或判分字段。
    /// </summary>
    [Fact]
    public async Task AttemptResponseShouldNotExposeCorrectAnswersOrScores()
    {
        var userId = Guid.NewGuid();
        var paperId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var attemptService = new Mock<IPaperAttemptService>();
        attemptService.Setup(value => value.GetAttemptAsync(
                userId,
                attemptId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAttemptResponse(attemptId, paperId, questionId));
        await using var app = await CreateHttpAppAsync(
            Mock.Of<IPaperService>(),
            attemptService.Object,
            userId);

        var response = await app.GetTestClient().GetAsync(
            $"/api/paper-attempts/{attemptId}",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("savedAnswer");
        json.Should().Contain("paperTotalScore");
        json.Should().Contain("paperPassingScore");
        json.Should().Contain("points");
        json.Should().NotContain("isCorrect");
        json.Should().NotContain("correctBoolean");
        json.Should().NotContain("correctOptionId");
        json.Should().NotContain("acceptedAnswers");
        json.Should().NotContain("explanation");
        json.Should().NotContain("awardedPoints");
        json.Should().NotContain("isPassed");
        json.Should().NotContain("\"score\"");
    }

    /// <summary>
    /// 创建仅用于检查路由 metadata 的应用。
    /// </summary>
    private static WebApplication CreateMetadataApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IPaperService>());
        builder.Services.AddSingleton(Mock.Of<IPaperAttemptService>());
        builder.Services.AddSingleton(Mock.Of<IPaperCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IPaperBatchService>());
        builder.Services.AddSingleton(Mock.Of<IWrongQuestionService>());
        var app = builder.Build();
        app.MapGroup("/api").MapOnlineQuizApi();
        return app;
    }

    /// <summary>
    /// 创建支持内存 HTTP 请求、测试身份和两类授权策略的应用。
    /// </summary>
    private static async Task<WebApplication> CreateHttpAppAsync(
        IPaperService paperService,
        IPaperAttemptService attemptService,
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
        builder.Services.AddSingleton(paperService);
        builder.Services.AddSingleton(attemptService);
        builder.Services.AddSingleton(Mock.Of<IPaperCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IPaperBatchService>());
        builder.Services.AddSingleton(Mock.Of<IWrongQuestionService>());
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
        app.MapGroup("/api").MapOnlineQuizApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// 返回应用中的全部在线试卷 route endpoints。
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> GetRoutes(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(value => value.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    /// <summary>
    /// 创建 endpoint mock 使用的最小管理员试卷响应。
    /// </summary>
    private static AdminPaperResponse CreateAdminPaperResponse(Guid paperId)
        => new(
            paperId,
            "Quiz",
            null,
            null,
            [],
            PaperPublicationStatus.Draft,
            60,
            0,
            0,
            0,
            new ContentAuditUserResponse(Guid.NewGuid(), null, null),
            new ContentAuditUserResponse(Guid.NewGuid(), null, null),
            null,
            null,
            Guid.NewGuid(),
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    /// <summary>
    /// 创建 endpoint mock 使用的安全测验恢复响应。
    /// </summary>
    private static UserPaperAttemptResponse CreateAttemptResponse(
        Guid attemptId,
        Guid paperId,
        Guid? questionId = null)
        => new(
            attemptId,
            paperId,
            1,
            PaperAttemptStatus.InProgress,
            "Quiz",
            null,
            null,
            questionId.HasValue ? 1 : 0,
            1,
            1,
            DateTimeOffset.UtcNow,
            null,
            questionId is { } id
                ?
                [
                    new UserPaperAttemptQuestionResponse(
                        id,
                        PaperQuestionType.TrueFalse,
                        "Prompt",
                        1,
                        0,
                        [],
                        new UserPaperAttemptSavedAnswerResponse(
                            null,
                            true,
                            null,
                            DateTimeOffset.UtcNow))
                ]
                : []);

    /// <summary>
    /// 创建 endpoint mock 使用的最小已提交结果。
    /// </summary>
    private static PaperAttemptResultResponse CreateResultResponse(
        Guid attemptId,
        Guid paperId,
        Guid questionId)
        => new(
            attemptId,
            paperId,
            1,
            "Quiz",
            1,
            1,
            1,
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new PaperAttemptQuestionResultResponse(
                    questionId,
                    PaperQuestionType.TrueFalse,
                    "Prompt",
                    "Explanation",
                    1,
                    0,
                    [],
                    null,
                    true,
                    null,
                    true,
                    null,
                    true,
                    [],
                    true,
                    1)
            ]);
}
