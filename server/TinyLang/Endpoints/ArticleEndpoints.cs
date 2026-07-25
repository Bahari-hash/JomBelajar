using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class ArticleEndpoints
{
    public static RouteGroupBuilder MapArticlesApi(this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/articles", GetPublicArticlesAsync);

        endpoints.MapGet("/articles/{id:guid}", GetPublicArticleAsync);

        endpoints.MapGet("/article-categories", GetPublicArticleCategoriesAsync);

        var editorGroup = endpoints.MapGroup("/editor")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editorGroup.MapPost("/articles", CreateArticleDraftAsync);

        editorGroup.MapGet("/articles", GetEditorArticlesAsync);

        editorGroup.MapGet("/articles/{id:guid}", GetEditorArticleAsync);

        editorGroup.MapPut("/articles/{id:guid}", UpdateArticleAsync);

        editorGroup.MapPost("/articles/{id:guid}/publish", PublishArticleAsync);

        editorGroup.MapPost("/articles/{id:guid}/unpublish", UnpublishArticleAsync);

        editorGroup.MapDelete("/articles/{id:guid}", ArchiveArticleAsync);

        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("/article-categories", GetEditorArticleCategoriesAsync);

        adminGroup.MapPost("/article-categories", CreateArticleCategoryAsync);

        adminGroup.MapPut("/article-categories/{id:guid}", UpdateArticleCategoryAsync);

        adminGroup.MapDelete("/article-categories/{id:guid}", DeleteArticleCategoryAsync);

        return endpoints;
    }

    public static async Task<Ok<PagedResponse<ArticleListItemResponse>>> GetPublicArticlesAsync(
        [AsParameters] ArticleListRequest request,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetPublicListAsync(request, cancellationToken));

    public static async Task<Ok<ArticleResponse>> GetPublicArticleAsync(
        Guid id,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetPublicByIdAsync(id, cancellationToken));

    public static async Task<Created<ArticleResponse>> CreateArticleDraftAsync(
        CreateArticleRequest request,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
    {
        var response = await articleService.CreateDraftAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Created($"/api/editor/articles/{response.Id}", response);
    }

    public static async Task<Ok<PagedResponse<ArticleListItemResponse>>> GetEditorArticlesAsync(
        [AsParameters] ArticleListRequest request,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetEditorListAsync(request, cancellationToken));

    public static async Task<Ok<ArticleResponse>> GetEditorArticleAsync(
        Guid id,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetEditorByIdAsync(id, cancellationToken));

    public static async Task<Ok<ArticleResponse>> UpdateArticleAsync(
        Guid id,
        UpdateArticleRequest request,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.UpdateAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    public static async Task<Ok<ArticleResponse>> PublishArticleAsync(
        Guid id,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.PublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    public static async Task<Ok<ArticleResponse>> UnpublishArticleAsync(
        Guid id,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.UnpublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    public static async Task<NoContent> ArchiveArticleAsync(
        Guid id,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
    {
        await articleService.ArchiveAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<PagedResponse<ArticleCategoryResponse>>> GetPublicArticleCategoriesAsync(
        [AsParameters] ArticleCategoryListRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetPublicListAsync(request, cancellationToken));

    public static async Task<Ok<PagedResponse<ArticleCategoryResponse>>> GetEditorArticleCategoriesAsync(
        [AsParameters] AdminArticleCategoryListRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetAdminListAsync(request, cancellationToken));

    public static async Task<Created<ArticleCategoryResponse>> CreateArticleCategoryAsync(
        CreateArticleCategoryRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/admin/article-categories/{response.Id}", response);
    }

    public static async Task<Ok<ArticleCategoryResponse>> UpdateArticleCategoryAsync(
        Guid id,
        UpdateArticleCategoryRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.UpdateAsync(id, request, cancellationToken));

    public static async Task<NoContent> DeleteArticleCategoryAsync(
        Guid id,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
