using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Services;

/// <summary>
/// 实现文章状态转换、内容清理以及分类和媒体关联的一致性规则。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="htmlSanitizer">文章 HTML 清理和媒体源提取服务。</param>
public sealed class ArticleService(
    IApplicationDbContext db,
    IHtmlContentSanitizer htmlSanitizer) : IArticleService
{
    /// <inheritdoc />
    public async Task<ArticleResponse> CreateDraftAsync(
        Guid editorId,
        CreateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = await ValidateDraftInputAsync(
            editorId, request, existingArticle: null, cancellationToken);
        var article = new Article
        {
            Title = NormalizeRequired(request.Title),
            Summary = NormalizeOptional(request.Summary),
            ContentHtml = sanitized.Html,
            AuthorId = editorId,
            LastEditorId = editorId,
            CoverMediaResourceId = request.CoverMediaResourceId
        };
        await SynchronizeMediaAsync(article, editorId, request, sanitized, cancellationToken);
        SynchronizeCategories(article, request.CategoryIds);

        db.Articles.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ArticleResponse> UpdateAsync(
        Guid articleId,
        Guid editorId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status == ArticleStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        var sanitized = await ValidateDraftInputAsync(
            editorId, request, article, cancellationToken);
        article.Title = NormalizeRequired(request.Title);
        article.Summary = NormalizeOptional(request.Summary);
        article.ContentHtml = sanitized.Html;
        article.CoverMediaResourceId = request.CoverMediaResourceId;
        article.LastEditorId = editorId;
        await SynchronizeMediaAsync(article, editorId, request, sanitized, cancellationToken);
        SynchronizeCategories(article, request.CategoryIds);

        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ArticleResponse> PublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status != ArticleStatus.Draft)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        await EnsureCategoriesUsableAsync(
            article.CategoryAssignments.Select(x => x.ArticleCategoryId).ToArray(),
            cancellationToken);
        var sanitized = htmlSanitizer.Sanitize(article.ContentHtml);
        EnsureContentIsPublishable(sanitized);
        article.ContentHtml = sanitized.Html;
        await ValidateStoredMediaAsync(article, sanitized, cancellationToken);

        article.Status = ArticleStatus.Published;
        article.PublishedAt = DateTimeOffset.UtcNow;
        article.PublishedById = editorId;
        article.LastEditorId = editorId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ArticleResponse> UnpublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status != ArticleStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        article.Status = ArticleStatus.Draft;
        article.PublishedAt = null;
        article.PublishedById = null;
        article.LastEditorId = editorId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status == ArticleStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        article.Status = ArticleStatus.Archived;
        article.LastEditorId = editorId;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ArticleResponse> GetEditorByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await DetailsQuery()
            .SingleOrDefaultAsync(x => x.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);
        return ToResponse(article, includeLastEditor: true);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<ArticleListItemResponse>> GetEditorListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(db.Articles.AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToListItemProjection())
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<ArticleResponse> GetPublicByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await DetailsQuery()
            .SingleOrDefaultAsync(
                x => x.Id == articleId && x.Status == ArticleStatus.Published,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);
        return ToResponse(article, includeLastEditor: false);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<ArticleListItemResponse>> GetPublicListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(
            db.Articles.AsNoTracking().Where(x => x.Status == ArticleStatus.Published),
            request with { Status = null });
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToListItemProjection())
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

    /// <summary>
    /// 校验分类、清理正文并验证请求中的媒体声明和所有权。
    /// </summary>
    /// <param name="editorId">执行写入的编辑者标识。</param>
    /// <param name="request">文章写入请求。</param>
    /// <param name="existingArticle">正在更新的文章；创建草稿时为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>清理后的 HTML 检查结果。</returns>
    private async Task<HtmlSanitizationResult> ValidateDraftInputAsync(
        Guid editorId,
        ArticleUpsertRequest request,
        Article? existingArticle,
        CancellationToken cancellationToken)
    {
        await EnsureCategoriesUsableAsync(request.CategoryIds, cancellationToken);
        var sanitized = htmlSanitizer.Sanitize(request.ContentHtml);
        EnsureContentIsPublishable(sanitized);
        await ValidateRequestedMediaAsync(editorId, existingArticle, request, sanitized, cancellationToken);
        return sanitized;
    }

    /// <summary>
    /// 确保分类标识集合合法，且所有分类存在并处于启用状态。
    /// </summary>
    /// <param name="categoryIds">请求关联的分类标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>表示异步校验操作的任务。</returns>
    private async Task EnsureCategoriesUsableAsync(
        IReadOnlyCollection<Guid>? categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds ?? [];
        if (ids.Count > ArticleConstraints.MaxCategoryCount || ids.Any(x => x == Guid.Empty))
        {
            throw new RequestValidationException(ErrorCodes.ArticleCategoryInvalid);
        }
        if (ids.Count != ids.Distinct().Count())
        {
            throw new RequestValidationException(ErrorCodes.ArticleCategoryDuplicate);
        }
        if (ids.Count == 0)
        {
            return;
        }

        var categories = await db.ArticleCategories.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (categories.Count != ids.Count)
        {
            throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);
        }
        if (categories.Any(x => !x.IsActive))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryInactive);
        }
    }

    /// <summary>
    /// 校验请求媒体集合、正文图片引用、资源状态及编辑者所有权。
    /// </summary>
    /// <param name="editorId">执行写入的编辑者标识。</param>
    /// <param name="existingArticle">正在更新的文章；创建草稿时为 <see langword="null"/>。</param>
    /// <param name="request">文章写入请求。</param>
    /// <param name="sanitized">正文清理结果。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>表示异步校验操作的任务。</returns>
    private async Task ValidateRequestedMediaAsync(
        Guid editorId,
        Article? existingArticle,
        ArticleUpsertRequest request,
        HtmlSanitizationResult sanitized,
        CancellationToken cancellationToken)
    {
        var requestedMediaIds = request.MediaResourceIds ?? [];
        var bodyMediaIds = requestedMediaIds.ToHashSet();
        if (bodyMediaIds.Count != requestedMediaIds.Count || bodyMediaIds.Count > 100 ||
            bodyMediaIds.Any(x => x == Guid.Empty))
        {
            throw new RequestValidationException(ErrorCodes.ArticleMediaInvalid);
        }

        var associatedMediaIds = bodyMediaIds.ToHashSet();
        if (request.CoverMediaResourceId is { } coverMediaResourceId)
        {
            associatedMediaIds.Add(coverMediaResourceId);
        }

        var mediaResources = await LoadAndValidateMediaAsync(associatedMediaIds, cancellationToken);
        var bodyUrls = mediaResources
            .Where(x => bodyMediaIds.Contains(x.Id))
            .Select(x => x.Url!)
            .ToHashSet(StringComparer.Ordinal);
        if (!bodyUrls.SetEquals(sanitized.ImageSources))
        {
            throw ConflictException.Create(ErrorCodes.ArticleMediaNotReferenced);
        }

        var existingMediaIds = existingArticle?.MediaResources
            .Select(x => x.MediaResourceId)
            .ToHashSet() ?? [];
        if (mediaResources.Any(x =>
                !existingMediaIds.Contains(x.Id) && x.UploaderId != editorId))
        {
            throw ForbiddenException.Create(ErrorCodes.ArticleMediaOwnershipMismatch);
        }
    }

    /// <summary>
    /// 校验并同步文章当前的封面和正文媒体关联集合。
    /// </summary>
    /// <param name="article">待同步的已跟踪文章。</param>
    /// <param name="editorId">执行写入的编辑者标识。</param>
    /// <param name="request">文章写入请求。</param>
    /// <param name="sanitized">正文清理结果。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>表示异步同步操作的任务。</returns>
    private async Task SynchronizeMediaAsync(
        Article article,
        Guid editorId,
        ArticleUpsertRequest request,
        HtmlSanitizationResult sanitized,
        CancellationToken cancellationToken)
    {
        await ValidateRequestedMediaAsync(editorId, article, request, sanitized, cancellationToken);
        var desiredMediaIds = (request.MediaResourceIds ?? []).ToHashSet();
        if (request.CoverMediaResourceId is { } coverMediaResourceId)
        {
            desiredMediaIds.Add(coverMediaResourceId);
        }

        foreach (var media in article.MediaResources
                     .Where(x => !desiredMediaIds.Contains(x.MediaResourceId))
                     .ToArray())
        {
            db.ArticleMediaResources.Remove(media);
            article.MediaResources.Remove(media);
        }

        var existingMediaIds = article.MediaResources.Select(x => x.MediaResourceId).ToHashSet();
        foreach (var mediaResourceId in desiredMediaIds.Where(id => !existingMediaIds.Contains(id)))
        {
            article.MediaResources.Add(new ArticleMediaResource { MediaResourceId = mediaResourceId });
        }
    }

    /// <summary>
    /// 将文章分类关联同步为请求中的目标集合。
    /// </summary>
    /// <param name="article">待同步的已跟踪文章。</param>
    /// <param name="requestedCategoryIds">目标分类标识集合。</param>
    private void SynchronizeCategories(
        Article article,
        IReadOnlyCollection<Guid>? requestedCategoryIds)
    {
        var desiredCategoryIds = (requestedCategoryIds ?? []).ToHashSet();
        foreach (var assignment in article.CategoryAssignments
                     .Where(x => !desiredCategoryIds.Contains(x.ArticleCategoryId))
                     .ToArray())
        {
            db.ArticleCategoryAssignments.Remove(assignment);
            article.CategoryAssignments.Remove(assignment);
        }

        var existingCategoryIds = article.CategoryAssignments
            .Select(x => x.ArticleCategoryId)
            .ToHashSet();
        foreach (var categoryId in desiredCategoryIds.Where(id => !existingCategoryIds.Contains(id)))
        {
            article.CategoryAssignments.Add(new ArticleCategoryAssignment
            {
                ArticleCategoryId = categoryId
            });
        }

    }

    /// <summary>
    /// 发布前验证已存储媒体关联与清理后正文引用完全一致。
    /// </summary>
    /// <param name="article">待发布文章。</param>
    /// <param name="sanitized">正文清理结果。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>表示异步校验操作的任务。</returns>
    private async Task ValidateStoredMediaAsync(
        Article article,
        HtmlSanitizationResult sanitized,
        CancellationToken cancellationToken)
    {
        var associatedMediaIds = article.MediaResources.Select(x => x.MediaResourceId).ToHashSet();
        var mediaResources = await LoadAndValidateMediaAsync(associatedMediaIds, cancellationToken);
        var resourceIdsByUrl = mediaResources
            .GroupBy(x => x.Url!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Select(resource => resource.Id).ToArray(), StringComparer.Ordinal);
        var referencedMediaIds = new HashSet<Guid>();
        foreach (var source in sanitized.ImageSources)
        {
            if (!resourceIdsByUrl.TryGetValue(source, out var resourceIds) || resourceIds.Length != 1)
            {
                throw ConflictException.Create(ErrorCodes.ArticleMediaNotReferenced);
            }

            referencedMediaIds.Add(resourceIds[0]);
        }

        if (article.CoverMediaResourceId is { } coverMediaResourceId)
        {
            referencedMediaIds.Add(coverMediaResourceId);
        }

        if (!referencedMediaIds.SetEquals(associatedMediaIds))
        {
            throw ConflictException.Create(ErrorCodes.ArticleMediaNotReferenced);
        }
    }

    /// <summary>
    /// 加载文章图片资源并验证其存在性、模块、URL 和激活状态。
    /// </summary>
    /// <param name="mediaResourceIds">待加载的媒体资源标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>验证通过的媒体资源列表。</returns>
    private async Task<List<MediaResource>> LoadAndValidateMediaAsync(
        IReadOnlyCollection<Guid> mediaResourceIds,
        CancellationToken cancellationToken)
    {
        if (mediaResourceIds.Count == 0)
        {
            return [];
        }

        var mediaResources = await db.MediaResources
            .Where(x => mediaResourceIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (mediaResources.Count != mediaResourceIds.Count)
        {
            throw NotFoundException.Create(ErrorCodes.ArticleMediaInvalid);
        }

        foreach (var mediaResource in mediaResources)
        {
            if (mediaResource.Module != ResourceModule.ArticlePicture ||
                !IsAllowedMediaUrl(mediaResource.Url))
            {
                throw new RequestValidationException(ErrorCodes.ArticleMediaInvalid);
            }
            if (mediaResource.Status != ResourceStatus.Active)
            {
                throw ConflictException.Create(ErrorCodes.ArticleMediaNotConfirmed);
            }
        }

        return mediaResources;
    }

    /// <summary>
    /// 判断媒体地址是否为绝对 HTTP 或 HTTPS URL。
    /// </summary>
    /// <param name="value">待检查地址。</param>
    /// <returns>地址协议受支持时返回 <see langword="true"/>。</returns>
    private static bool IsAllowedMediaUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// 加载文章及编辑所需的媒体和分类关联。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>已跟踪的文章实体。</returns>
    /// <exception cref="NotFoundException">文章不存在。</exception>
    private async Task<Article> FindArticleForEditAsync(
        Guid articleId,
        CancellationToken cancellationToken)
        => await db.Articles
            .Include(x => x.MediaResources)
            .Include(x => x.CategoryAssignments)
            .SingleOrDefaultAsync(x => x.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);

    /// <summary>
    /// 创建包含文章详情投影所需关联的只读查询。
    /// </summary>
    /// <returns>尚未执行的文章详情查询。</returns>
    private IQueryable<Article> DetailsQuery()
        => db.Articles.AsNoTracking()
            .Include(x => x.CategoryAssignments)
                .ThenInclude(x => x.ArticleCategory)
            .Include(x => x.Author)
            .Include(x => x.LastEditor)
            .Include(x => x.CoverMediaResource)
            .Include(x => x.MediaResources);

    /// <summary>
    /// 将分类、状态和关键词条件应用到文章查询。
    /// </summary>
    /// <param name="query">基础文章查询。</param>
    /// <param name="request">文章列表筛选条件。</param>
    /// <returns>应用筛选后的查询。</returns>
    private static IQueryable<Article> ApplyListFilters(
        IQueryable<Article> query,
        ArticleListRequest request)
    {
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(x => x.CategoryAssignments
                .Any(assignment => assignment.ArticleCategoryId == categoryId));
        }
        if (request.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.Title.ToUpper().Contains(keyword) ||
                (x.Summary != null && x.Summary.ToUpper().Contains(keyword)));
        }

        return query;
    }

    /// <summary>
    /// 创建可由 EF Core 翻译的文章列表项投影表达式。
    /// </summary>
    /// <returns>文章实体到列表响应的投影表达式。</returns>
    private static Expression<Func<Article, ArticleListItemResponse>> ToListItemProjection()
        => article => new ArticleListItemResponse(
            article.Id,
            article.Title,
            article.Summary,
            article.Status,
            article.CategoryAssignments
                .OrderBy(assignment => assignment.ArticleCategory.Name)
                .Select(assignment => new ArticleCategorySummaryResponse(
                    assignment.ArticleCategory.Id,
                    assignment.ArticleCategory.Name,
                    assignment.ArticleCategory.Slug))
                .ToList(),
            article.CoverMediaResource == null ? null : article.CoverMediaResource.Url,
            new ArticleUserSummaryResponse(article.Author.Id, article.Author.Nickname, article.Author.AvatarUrl),
            article.PublishedAt,
            article.UpdatedAt);

    /// <summary>
    /// 将已加载的文章实体映射为详情响应。
    /// </summary>
    /// <param name="article">包含详情关联的文章实体。</param>
    /// <param name="includeLastEditor">是否包含内部最后编辑者信息。</param>
    /// <returns>文章详情响应。</returns>
    private static ArticleResponse ToResponse(Article article, bool includeLastEditor)
        => new(
            article.Id,
            article.Title,
            article.Summary,
            article.ContentHtml,
            article.Status,
            article.CategoryAssignments
                .OrderBy(assignment => assignment.ArticleCategory.Name)
                .Select(assignment => new ArticleCategorySummaryResponse(
                    assignment.ArticleCategory.Id,
                    assignment.ArticleCategory.Name,
                    assignment.ArticleCategory.Slug))
                .ToArray(),
            new ArticleUserSummaryResponse(
                article.Author.Id,
                article.Author.Nickname,
                article.Author.AvatarUrl),
            includeLastEditor ? new ArticleUserSummaryResponse(
                article.LastEditor.Id,
                article.LastEditor.Nickname,
                article.LastEditor.AvatarUrl) : null,
            article.PublishedAt,
            article.CoverMediaResource?.Url,
            article.MediaResources.Select(x => x.MediaResourceId).ToArray(),
            article.CreatedAt,
            article.UpdatedAt);

    /// <summary>
    /// 根据结果集和总数构建分页响应。
    /// </summary>
    /// <typeparam name="T">分页元素类型。</typeparam>
    /// <param name="items">当前页元素。</param>
    /// <param name="page">当前页码。</param>
    /// <param name="pageSize">每页元素数。</param>
    /// <param name="totalCount">符合条件的总数。</param>
    /// <returns>包含总页数的分页响应。</returns>
    private static PagedResponse<T> ToPagedResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(items, page, pageSize, totalCount, (totalCount + pageSize - 1) / pageSize);

    /// <summary>
    /// 确保清理后的正文包含有效文本且未发现非法 URL。
    /// </summary>
    /// <param name="sanitized">HTML 清理结果。</param>
    private static void EnsureContentIsPublishable(HtmlSanitizationResult sanitized)
    {
        if (sanitized.HasInvalidUrls ||
            (string.IsNullOrWhiteSpace(sanitized.PlainText) && sanitized.ImageSources.Count == 0))
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentInvalid);
        }
    }

    /// <summary>
    /// 去除必填文本两端空白。
    /// </summary>
    /// <param name="value">必填文本。</param>
    /// <returns>规范化文本。</returns>
    private static string NormalizeRequired(string value) => value.Trim();

    /// <summary>
    /// 将空白可选文本转换为 <see langword="null"/>，否则去除两端空白。
    /// </summary>
    /// <param name="value">可选文本。</param>
    /// <returns>规范化后的可选文本。</returns>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
