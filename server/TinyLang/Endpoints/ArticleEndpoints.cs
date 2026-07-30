using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义公开文章、编辑者文章和管理员分类管理的 HTTP endpoints。
/// </summary>
public static class ArticleEndpoints
{
    /// <summary>
    /// 注册文章和分类路由，并按公开、编辑者和管理员权限分组。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapArticlesApi(this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/articles", GetPublicArticlesAsync);

        endpoints.MapGet("/articles/{id:guid}", GetPublicArticleAsync);

        endpoints.MapGet("/article-categories", GetPublicArticleCategoriesAsync);

        var editorGroup = endpoints.MapGroup("/editor")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editorGroup.MapPost("/articles", CreateArticleDraftAsync);

        editorGroup.MapPost("/articles/preview", PreviewArticleAsync);

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

        adminGroup.MapDelete(
            "/article-categories/{id:guid}/articles",
            ClearArticleCategoryArticlesAsync);

        return endpoints;
    }

    /// <summary>
    /// 获取已发布文章的公开分页列表。
    /// </summary>
    /// <param name="request">分页和筛选条件。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文章分页成功响应。</returns>
    public static async Task<Ok<PagedResponse<ArticleListItemResponse>>> GetPublicArticlesAsync(
        [AsParameters] ArticleListRequest request,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetPublicListAsync(request, cancellationToken));

    /// <summary>
    /// 获取指定已发布文章的公开详情。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文章详情成功响应。</returns>
    public static async Task<Ok<PublicArticleResponse>> GetPublicArticleAsync(
        Guid id,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetPublicByIdAsync(id, cancellationToken));

    /// <summary>
    /// 以当前编辑者身份创建文章草稿。
    /// </summary>
    /// <param name="request">文章草稿内容。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>包含新资源位置的创建响应。</returns>
    public static async Task<Created<EditorArticleResponse>> CreateArticleDraftAsync(
        CreateArticleRequest request,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
    {
        var response = await articleService.CreateDraftAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Created($"/api/editor/articles/{response.Id}", response);
    }

    /// <summary>
    /// Renders an authenticated editor's Markdown with the canonical article pipeline without persisting it.
    /// </summary>
    /// <param name="request">The Markdown preview request.</param>
    /// <param name="articleService">The article business service.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The canonical sanitized HTML preview.</returns>
    public static async Task<Ok<ArticlePreviewResponse>> PreviewArticleAsync(
        ArticlePreviewRequest request,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.PreviewAsync(request, cancellationToken));

    /// <summary>
    /// 获取编辑者可见的文章分页列表。
    /// </summary>
    /// <param name="request">分页和筛选条件。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文章分页成功响应。</returns>
    public static async Task<Ok<PagedResponse<ArticleListItemResponse>>> GetEditorArticlesAsync(
        [AsParameters] ArticleListRequest request,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetEditorListAsync(request, cancellationToken));

    /// <summary>
    /// 获取编辑者可见的文章详情。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文章编辑详情成功响应。</returns>
    public static async Task<Ok<EditorArticleResponse>> GetEditorArticleAsync(
        Guid id,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.GetEditorByIdAsync(id, cancellationToken));

    /// <summary>
    /// 以当前编辑者身份更新文章。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="request">文章更新内容。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的文章成功响应。</returns>
    public static async Task<Ok<EditorArticleResponse>> UpdateArticleAsync(
        Guid id,
        UpdateArticleRequest request,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.UpdateAsync(
            id, EndpointIdentity.GetUserId(principal), request, cancellationToken));

    /// <summary>
    /// 以当前编辑者身份发布文章草稿。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>发布后的文章成功响应。</returns>
    public static async Task<Ok<EditorArticleResponse>> PublishArticleAsync(
        Guid id,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.PublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 以当前编辑者身份将已发布文章恢复为草稿。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>下架后的文章成功响应。</returns>
    public static async Task<Ok<EditorArticleResponse>> UnpublishArticleAsync(
        Guid id,
        ClaimsPrincipal principal,
        IArticleService articleService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await articleService.UnpublishAsync(
            id, EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 以当前编辑者身份归档文章。
    /// </summary>
    /// <param name="id">文章标识。</param>
    /// <param name="principal">当前已认证编辑者。</param>
    /// <param name="articleService">文章业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
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

    /// <summary>
    /// 获取启用文章分类的公开分页列表。
    /// </summary>
    /// <param name="request">分页和关键词条件。</param>
    /// <param name="categoryService">文章分类业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>分类分页成功响应。</returns>
    public static async Task<Ok<PagedResponse<ArticleCategoryResponse>>> GetPublicArticleCategoriesAsync(
        [AsParameters] ArticleCategoryListRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetPublicListAsync(request, cancellationToken));

    /// <summary>
    /// 获取管理员可见的文章分类分页列表。
    /// </summary>
    /// <param name="request">管理员分页和筛选条件。</param>
    /// <param name="categoryService">文章分类业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>分类分页成功响应。</returns>
    public static async Task<Ok<PagedResponse<ArticleCategoryResponse>>> GetEditorArticleCategoriesAsync(
        [AsParameters] AdminArticleCategoryListRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetAdminListAsync(request, cancellationToken));

    /// <summary>
    /// 创建文章分类。
    /// </summary>
    /// <param name="request">分类创建内容。</param>
    /// <param name="categoryService">文章分类业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>包含新资源位置的创建响应。</returns>
    public static async Task<Created<ArticleCategoryResponse>> CreateArticleCategoryAsync(
        CreateArticleCategoryRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/admin/article-categories/{response.Id}", response);
    }

    /// <summary>
    /// 更新指定文章分类。
    /// </summary>
    /// <param name="id">分类标识。</param>
    /// <param name="request">分类更新内容。</param>
    /// <param name="categoryService">文章分类业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>更新后的分类成功响应。</returns>
    public static async Task<Ok<ArticleCategoryResponse>> UpdateArticleCategoryAsync(
        Guid id,
        UpdateArticleCategoryRequest request,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// 删除指定文章分类。
    /// </summary>
    /// <param name="id">分类标识。</param>
    /// <param name="categoryService">文章分类业务服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>无响应体的成功结果。</returns>
    public static async Task<NoContent> DeleteArticleCategoryAsync(
        Guid id,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Explicitly removes all article associations from a category without deleting either resource.
    /// </summary>
    /// <param name="id">The category identifier.</param>
    /// <param name="categoryService">The article category business service.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The number of removed article associations.</returns>
    public static async Task<Ok<ClearArticleCategoryResponse>> ClearArticleCategoryArticlesAsync(
        Guid id,
        IArticleCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.ClearArticlesAsync(id, cancellationToken));
}
