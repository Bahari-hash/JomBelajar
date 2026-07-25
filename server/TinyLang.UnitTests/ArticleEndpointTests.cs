using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

public sealed class ArticleEndpointTests
{
    private static readonly string[] PublicRoutes =
    [
        "/api/articles",
        "/api/articles/{id:guid}",
        "/api/article-categories"
    ];

    private static readonly string[] EditorRoutes =
    [
        "/api/editor/articles",
        "/api/editor/articles/{id:guid}",
        "/api/editor/articles/{id:guid}/publish",
        "/api/editor/articles/{id:guid}/unpublish",
    ];

    private static readonly string[] AdminRoutes =
    [
        "/api/admin/article-categories",
        "/api/admin/article-categories/{id:guid}"
    ];

    [Fact]
    public async Task RoutesShouldUseTheExpectedAuthorizationPolicies()
    {
        await using var app = CreateMetadataApp();
        var routes = GetRouteEndpoints(app);

        foreach (var pattern in PublicRoutes)
        {
            var matching = routes.Where(x => x.RoutePattern.RawText == pattern).ToArray();
            matching.Should().NotBeEmpty("available routes: {0}", string.Join(", ", routes.Select(x => x.RoutePattern.RawText)));
            matching.Should().OnlyContain(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() == null);
        }
        foreach (var pattern in EditorRoutes)
        {
            var matching = routes.Where(x => x.RoutePattern.RawText == pattern).ToArray();
            matching.Should().NotBeEmpty("available routes: {0}", string.Join(", ", routes.Select(x => x.RoutePattern.RawText)));
            matching.Should().OnlyContain(endpoint => endpoint.Metadata
                    .GetOrderedMetadata<IAuthorizeData>()
                    .Any(data => data.Policy == AuthorizationPolicies.RequireEditor));
        }
        foreach (var pattern in AdminRoutes)
        {
            var matching = routes.Where(x => x.RoutePattern.RawText == pattern).ToArray();
            matching.Should().NotBeEmpty("available routes: {0}", string.Join(", ", routes.Select(x => x.RoutePattern.RawText)));
            matching.Should().OnlyContain(endpoint => endpoint.Metadata
                    .GetOrderedMetadata<IAuthorizeData>()
                    .Any(data => data.Policy == AuthorizationPolicies.RequireAdmin));
        }
    }

    [Fact]
    public async Task PublicListShouldBindQueryParametersAndReturnOk()
    {
        var categoryId = Guid.NewGuid();
        var articleService = new Mock<IArticleService>();
        articleService.Setup(x => x.GetPublicListAsync(
                It.IsAny<ArticleListRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ArticleListItemResponse>([], 2, 5, 0, 0));
        await using var app = await CreateHttpAppAsync(articleService.Object);

        var response = await app.GetTestClient().GetAsync(
            $"/api/articles?page=2&pageSize=5&categoryId={categoryId}&keyword=grammar",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        articleService.Verify(x => x.GetPublicListAsync(
            It.Is<ArticleListRequest>(request =>
                request.Page == 2 &&
                request.PageSize == 5 &&
                request.CategoryId == categoryId &&
                request.Keyword == "grammar" &&
                request.Status == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateShouldUseAuthenticatedUserAndReturnCreatedLocation()
    {
        var userId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var responseBody = CreateArticleResponse(articleId, userId);
        var articleService = new Mock<IArticleService>();
        articleService.Setup(x => x.CreateDraftAsync(
                userId,
                It.IsAny<CreateArticleRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseBody);
        await using var app = await CreateHttpAppAsync(articleService.Object, userId: userId);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/editor/articles",
            new CreateArticleRequest { Title = "Article", ContentHtml = "<p>Body</p>" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/api/editor/articles/{articleId}");
        articleService.Verify(x => x.CreateDraftAsync(
            userId,
            It.Is<CreateArticleRequest>(request => request.Title == "Article"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveShouldReturnNoContent()
    {
        var userId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var articleService = new Mock<IArticleService>();
        await using var app = await CreateHttpAppAsync(articleService.Object, userId: userId);

        var response = await app.GetTestClient().DeleteAsync(
            $"/api/editor/articles/{articleId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        articleService.Verify(x => x.ArchiveAsync(
            articleId,
            userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCategoryShouldReturnCreatedLocation()
    {
        var categoryId = Guid.NewGuid();
        var categoryService = new Mock<IArticleCategoryService>();
        categoryService.Setup(x => x.CreateAsync(
                It.IsAny<CreateArticleCategoryRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArticleCategoryResponse(
                categoryId, "Grammar", "grammar", null, true, 0));
        await using var app = await CreateHttpAppAsync(
            Mock.Of<IArticleService>(), categoryService.Object);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/admin/article-categories",
            new CreateArticleCategoryRequest { Name = "Grammar", Slug = "grammar" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/api/admin/article-categories/{categoryId}");
    }

    private static WebApplication CreateMetadataApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IArticleService>());
        builder.Services.AddSingleton(Mock.Of<IArticleCategoryService>());
        var app = builder.Build();
        app.MapGroup("/api").MapArticlesApi();
        return app;
    }

    private static async Task<WebApplication> CreateHttpAppAsync(
        IArticleService articleService,
        IArticleCategoryService? categoryService = null,
        Guid? userId = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireEditor, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(articleService);
        builder.Services.AddSingleton(categoryService ?? Mock.Of<IArticleCategoryService>());
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimNamesExtension.UserId, (userId ?? Guid.NewGuid()).ToString())
            ], "Test"));
            await next();
        });
        app.UseAuthorization();
        app.MapGroup("/api").MapArticlesApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static IReadOnlyList<RouteEndpoint> GetRouteEndpoints(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    private static ArticleResponse CreateArticleResponse(Guid articleId, Guid userId)
        => new(
            articleId,
            "Article",
            null,
            "<p>Body</p>",
            ArticleStatus.Draft,
            [],
            new ArticleUserSummaryResponse(userId, "Editor", null),
            new ArticleUserSummaryResponse(userId, "Editor", null),
            null,
            null,
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
