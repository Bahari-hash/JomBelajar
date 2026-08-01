using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条 endpoints 的路由、授权、身份传递和 typed HTTP 契约。
/// </summary>
public sealed class WordEndpointTests
{
    /// <summary>
    /// 验证全部管理路由使用 RequireAdmin，用户查询使用 RequireUser。
    /// </summary>
    [Fact]
    public async Task RoutesShouldUseExpectedAuthorizationPolicies()
    {
        await using var app = CreateMetadataApp();
        var routes = GetRoutes(app);

        routes.Where(value => value.RoutePattern.RawText!.StartsWith(
                "/api/admin/words",
                StringComparison.Ordinal))
            .Should().OnlyContain(endpoint => endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Any(value => value.Policy == AuthorizationPolicies.RequireAdmin));
        routes.Where(value => value.RoutePattern.RawText!.StartsWith(
                "/api/words",
                StringComparison.Ordinal))
            .Should().OnlyContain(endpoint => endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Any(value => value.Policy == AuthorizationPolicies.RequireUser));
        routes.Should().HaveCount(12);
    }

    /// <summary>
    /// 验证创建使用 authenticated user ID 并返回正确的 201 Location。
    /// </summary>
    [Fact]
    public async Task CreateShouldUseAuthenticatedAdminAndReturnLocation()
    {
        var adminId = Guid.NewGuid();
        var wordId = Guid.NewGuid();
        var service = new Mock<IWordService>();
        service.Setup(value => value.CreateDraftAsync(
                adminId,
                It.IsAny<CreateWordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAdminResponse(wordId, adminId));
        await using var app = await CreateHttpAppAsync(service.Object, adminId);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/admin/words",
            new CreateWordRequest { Headword = "hello", LanguageTag = "en" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/api/admin/words/{wordId}");
        service.Verify(value => value.CreateDraftAsync(
            adminId,
            It.Is<CreateWordRequest>(request => request.Headword == "hello"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证用户列表绑定分页、关键词和语言并返回 200。
    /// </summary>
    [Fact]
    public async Task UserListShouldBindFiltersAndReturnOk()
    {
        var service = new Mock<IWordService>();
        service.Setup(value => value.GetUserListAsync(
                It.IsAny<WordListRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<WordListItemResponse>([], 2, 5, 0, 0));
        await using var app = await CreateHttpAppAsync(service.Object, Guid.NewGuid());

        var response = await app.GetTestClient().GetAsync(
            "/api/words?page=2&pageSize=5&keyword=hello&language=en",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Verify(value => value.GetUserListAsync(
            It.Is<WordListRequest>(request =>
                request.Page == 2 &&
                request.PageSize == 5 &&
                request.Keyword == "hello" &&
                request.Language == "en"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证删除 endpoint 返回 204 并传递当前管理员身份。
    /// </summary>
    [Fact]
    public async Task DeleteShouldReturnNoContent()
    {
        var adminId = Guid.NewGuid();
        var wordId = Guid.NewGuid();
        var service = new Mock<IWordService>();
        await using var app = await CreateHttpAppAsync(service.Object, adminId);

        var concurrencyStamp = Guid.NewGuid();
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/words/{wordId}")
        {
            Content = JsonContent.Create(new WordMutationRequest
            {
                ConcurrencyStamp = concurrencyStamp
            })
        };
        var response = await app.GetTestClient().SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        service.Verify(value => value.DeleteAsync(
            wordId,
            adminId,
            It.Is<WordMutationRequest>(value =>
                value.ConcurrencyStamp == concurrencyStamp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证批量校验和导入路由返回 typed 200 并传递当前管理员身份。
    /// </summary>
    [Fact]
    public async Task BatchEndpointsShouldUseAuthenticatedAdmin()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<IWordService>();
        service.Setup(value => value.ValidateBatchAsync(
                It.IsAny<BatchWordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchWordValidationResponse(true, [], []));
        service.Setup(value => value.ImportBatchAsync(
                adminId,
                It.IsAny<BatchWordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchWordImportResponse(Guid.NewGuid(), 0, []));
        await using var app = await CreateHttpAppAsync(service.Object, adminId);
        var request = new BatchWordRequest { Rows = [] };

        var validateResponse = await app.GetTestClient().PostAsJsonAsync(
            "/api/admin/words/batch/validate",
            request,
            TestContext.Current.CancellationToken);
        var importResponse = await app.GetTestClient().PostAsJsonAsync(
            "/api/admin/words/batch",
            request,
            TestContext.Current.CancellationToken);

        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        service.Verify(value => value.ValidateBatchAsync(
            It.IsAny<BatchWordRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(value => value.ImportBatchAsync(
            adminId,
            It.IsAny<BatchWordRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 创建仅用于检查路由 metadata 的应用。
    /// </summary>
    private static WebApplication CreateMetadataApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IWordService>());
        var app = builder.Build();
        app.MapGroup("/api").MapWordsApi();
        return app;
    }

    /// <summary>
    /// 创建支持内存 HTTP 请求和测试授权策略的应用。
    /// </summary>
    private static async Task<WebApplication> CreateHttpAppAsync(
        IWordService wordService,
        Guid userId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(wordService);
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
        app.MapGroup("/api").MapWordsApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// 返回应用中的全部词条 route endpoints。
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> GetRoutes(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(value => value.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    /// <summary>
    /// 创建 endpoint mock 使用的最小管理员词条响应。
    /// </summary>
    private static AdminWordResponse CreateAdminResponse(Guid wordId, Guid adminId)
        => new(
            wordId,
            "en",
            "hello",
            WordPublicationStatus.Draft,
            new ContentAuditUserResponse(adminId, null, null),
            new ContentAuditUserResponse(adminId, null, null),
            null,
            null,
            Guid.NewGuid(),
            [],
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
