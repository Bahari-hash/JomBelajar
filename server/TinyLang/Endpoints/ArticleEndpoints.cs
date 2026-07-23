using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class ArticleEndpoints
{
    public static RouteGroupBuilder MapArticlesApi(this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/articles", async (
            [AsParameters] ArticleListRequest request,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.GetPublicListAsync(request, cancellationToken)));

        endpoints.MapGet("/articles/{id:guid}", async (
            Guid id,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.GetPublicByIdAsync(id, cancellationToken)));

        var editorGroup = endpoints.MapGroup("/editor")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editorGroup.MapPost("/articles", async (
            CreateArticleRequest request,
            ClaimsPrincipal principal,
            IArticleService articleService,
            CancellationToken cancellationToken)
            =>
        {
            var response = await articleService.CreateDraftAsync(
                EndpointIdentity.GetUserId(principal), request, cancellationToken);
            return Results.Created($"/api/editor/articles/{response.Id}", response);
        });

        editorGroup.MapGet("/articles", async (
            [AsParameters] ArticleListRequest request,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.GetEditorListAsync(request, cancellationToken)));

        editorGroup.MapGet("/articles/{id:guid}", async (
            Guid id,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.GetEditorByIdAsync(id, cancellationToken)));

        editorGroup.MapPut("/articles/{id:guid}", async (
            Guid id,
            UpdateArticleRequest request,
            ClaimsPrincipal principal,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.UpdateAsync(
                id, EndpointIdentity.GetUserId(principal), request, cancellationToken)));

        editorGroup.MapPost("/articles/{id:guid}/publish", async (
            Guid id,
            ClaimsPrincipal principal,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.PublishAsync(
                id, EndpointIdentity.GetUserId(principal), cancellationToken)));

        editorGroup.MapPost("/articles/{id:guid}/unpublish", async (
            Guid id,
            ClaimsPrincipal principal,
            IArticleService articleService,
            CancellationToken cancellationToken)
            => Results.Ok(await articleService.UnpublishAsync(
                id, EndpointIdentity.GetUserId(principal), cancellationToken)));

        editorGroup.MapDelete("/articles/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IArticleService articleService,
            CancellationToken cancellationToken)
            =>
        {
            await articleService.ArchiveAsync(
                id, EndpointIdentity.GetUserId(principal), cancellationToken);
            return Results.NoContent();
        });

        endpoints.MapGet("/article-categories", async (
            [AsParameters] ArticleCategoryListRequest request,
            IArticleCategoryService categoryService,
            CancellationToken cancellationToken)
            => Results.Ok(await categoryService.GetPublicListAsync(request, cancellationToken)));

        editorGroup.MapGet("/article-categories", async (
            [AsParameters] AdminArticleCategoryListRequest request,
            IArticleCategoryService categoryService,
            CancellationToken cancellationToken)
            => Results.Ok(await categoryService.GetAdminListAsync(request, cancellationToken)));

        editorGroup.MapPost("/article-categories", async (
            CreateArticleCategoryRequest request,
            IArticleCategoryService categoryService,
            CancellationToken cancellationToken)
            =>
        {
            var response = await categoryService.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/article-categories/{response.Id}", response);
        });

        editorGroup.MapPut("/article-categories/{id:guid}", async (
            Guid id,
            UpdateArticleCategoryRequest request,
            IArticleCategoryService categoryService,
            CancellationToken cancellationToken)
            => Results.Ok(await categoryService.UpdateAsync(id, request, cancellationToken)));

        editorGroup.MapDelete("/article-categories/{id:guid}", async (
            Guid id,
            IArticleCategoryService categoryService,
            CancellationToken cancellationToken)
            =>
        {
            await categoryService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}
