using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Services;

/// <summary>
/// Implements canonical Markdown rendering, article state transitions, optimistic concurrency and media consistency.
/// </summary>
/// <param name="db">The application database context.</param>
/// <param name="markdownRenderer">The canonical Markdown rendering and sanitization boundary.</param>
/// <param name="logger">The structured article service logger.</param>
public sealed class ArticleService(
    IApplicationDbContext db,
    IArticleMarkdownRenderer markdownRenderer,
    ILogger<ArticleService> logger) : IArticleService
{
    /// <inheritdoc />
    public async Task<AdminArticleResponse> CreateDraftAsync(
        Guid adminId,
        CreateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var rendered = await ValidateDraftInputAsync(
            request, cancellationToken);
        var article = new Article
        {
            Title = NormalizeRequired(request.Title),
            Summary = NormalizeOptional(request.Summary),
            ContentMarkdown = request.ContentMarkdown,
            ContentHtml = rendered.Html,
            AuthorId = adminId,
            LastEditorId = adminId,
            CoverMediaResourceId = request.CoverMediaResourceId
        };
        SynchronizeBodyMedia(article, request.BodyMediaResourceIds);
        SynchronizeCategories(article, request.CategoryIds);

        db.Articles.Add(article);
        await SaveArticleChangesAsync(cancellationToken);
        return await GetAdminByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminArticleResponse> UpdateAsync(
        Guid articleId,
        Guid adminId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status != ArticleStatus.Draft)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }
        if (article.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw ConflictException.Create(ErrorCodes.ArticleConcurrencyConflict);
        }

        var rendered = await ValidateDraftInputAsync(
            request, cancellationToken);
        article.Title = NormalizeRequired(request.Title);
        article.Summary = NormalizeOptional(request.Summary);
        article.ContentMarkdown = request.ContentMarkdown;
        article.ContentHtml = rendered.Html;
        article.CoverMediaResourceId = request.CoverMediaResourceId;
        article.LastEditorId = adminId;
        article.ConcurrencyStamp = Guid.NewGuid();
        SynchronizeBodyMedia(article, request.BodyMediaResourceIds);
        SynchronizeCategories(article, request.CategoryIds);

        await SaveArticleChangesAsync(cancellationToken);
        return await GetAdminByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminArticleResponse> PublishAsync(
        Guid articleId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status != ArticleStatus.Draft)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        await EnsureCategoriesUsableAsync(
            article.CategoryAssignments.Select(value => value.ArticleCategoryId).ToArray(),
            cancellationToken);
        var rendered = markdownRenderer.Render(article.ContentMarkdown);
        await ValidateStoredMediaAsync(article, rendered, cancellationToken);

        article.ContentHtml = rendered.Html;
        article.Status = ArticleStatus.Published;
        article.PublishedAt = DateTimeOffset.UtcNow;
        article.PublishedById = adminId;
        article.LastEditorId = adminId;
        article.ConcurrencyStamp = Guid.NewGuid();
        await SaveArticleChangesAsync(cancellationToken);
        return await GetAdminByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminArticleResponse> UnpublishAsync(
        Guid articleId,
        Guid adminId,
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
        article.LastEditorId = adminId;
        article.ConcurrencyStamp = Guid.NewGuid();
        await SaveArticleChangesAsync(cancellationToken);
        return await GetAdminByIdAsync(article.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(
        Guid articleId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var article = await FindArticleForEditAsync(articleId, cancellationToken);
        if (article.Status != ArticleStatus.Draft)
        {
            throw ConflictException.Create(ErrorCodes.ArticleStatusConflict);
        }

        article.Status = ArticleStatus.Archived;
        article.LastEditorId = adminId;
        article.ConcurrencyStamp = Guid.NewGuid();
        await SaveArticleChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminArticleResponse> GetAdminByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await DetailsQuery()
            .SingleOrDefaultAsync(value => value.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);
        return ToAdminResponse(article);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<ArticleListItemResponse>> GetAdminListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(db.Articles.AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToListItemProjection())
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<PublicArticleResponse> GetPublicByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await DetailsQuery()
            .SingleOrDefaultAsync(
                value => value.Id == articleId && value.Status == ArticleStatus.Published,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);
        return ToPublicResponse(article);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<ArticleListItemResponse>> GetPublicListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyListFilters(
            db.Articles.AsNoTracking().Where(value => value.Status == ArticleStatus.Published),
            request with { Status = null });
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToListItemProjection())
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public Task<ArticlePreviewResponse> PreviewAsync(
        ArticlePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rendered = markdownRenderer.Render(request.ContentMarkdown);
        return Task.FromResult(new ArticlePreviewResponse(rendered.Html));
    }

    /// <summary>
    /// Renders content and validates categories, managed media and body URL declarations.
    /// </summary>
    /// <param name="request">The article write request.</param>
    /// <param name="cancellationToken">The token used to cancel database queries.</param>
    /// <returns>The canonical render result.</returns>
    private async Task<ArticleContentRenderResult> ValidateDraftInputAsync(
        ArticleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCategoriesUsableAsync(request.CategoryIds, cancellationToken);
        var rendered = markdownRenderer.Render(request.ContentMarkdown);
        await ValidateRequestedMediaAsync(
            request, rendered, cancellationToken);
        return rendered;
    }

    /// <summary>
    /// Ensures category identifiers are unique, present and active when supplied.
    /// </summary>
    /// <param name="categoryIds">The requested category identifiers.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    private async Task EnsureCategoriesUsableAsync(
        IReadOnlyCollection<Guid>? categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = categoryIds ?? [];
        if (ids.Count > ArticleConstraints.MaxCategoryCount || ids.Any(value => value == Guid.Empty))
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
            .Where(value => ids.Contains(value.Id))
            .ToListAsync(cancellationToken);
        if (categories.Count != ids.Count)
        {
            throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);
        }
        if (categories.Any(value => !value.IsActive))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryInactive);
        }
    }

    /// <summary>
    /// Validates requested cover and body resources and their body URL mappings.
    /// </summary>
    /// <param name="request">The article write request.</param>
    /// <param name="rendered">The canonical render result.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    private async Task ValidateRequestedMediaAsync(
        ArticleUpsertRequest request,
        ArticleContentRenderResult rendered,
        CancellationToken cancellationToken)
    {
        var requestedBodyIds = request.BodyMediaResourceIds ?? [];
        var bodyIds = requestedBodyIds.ToHashSet();
        if (bodyIds.Count != requestedBodyIds.Count ||
            bodyIds.Count > ArticleConstraints.MaxBodyMediaCount ||
            bodyIds.Any(value => value == Guid.Empty))
        {
            throw new RequestValidationException(ErrorCodes.ArticleMediaInvalid);
        }

        var requestedIds = bodyIds.ToHashSet();
        if (request.CoverMediaResourceId is { } coverMediaResourceId)
        {
            if (coverMediaResourceId == Guid.Empty)
            {
                throw new RequestValidationException(ErrorCodes.ArticleMediaInvalid);
            }
            requestedIds.Add(coverMediaResourceId);
        }

        var resources = await LoadAndValidateMediaAsync(requestedIds, cancellationToken);
        EnsureBodyMediaMatches(rendered, resources.Where(value => bodyIds.Contains(value.Id)).ToArray());

    }

    /// <summary>
    /// Replaces the tracked article body image associations without mixing in cover media.
    /// </summary>
    /// <param name="article">The tracked article.</param>
    /// <param name="requestedBodyMediaIds">The desired body media identifiers.</param>
    private void SynchronizeBodyMedia(
        Article article,
        IReadOnlyCollection<Guid>? requestedBodyMediaIds)
    {
        var desiredIds = (requestedBodyMediaIds ?? []).ToHashSet();
        foreach (var media in article.MediaResources
                     .Where(value => !desiredIds.Contains(value.MediaResourceId))
                     .ToArray())
        {
            db.ArticleMediaResources.Remove(media);
            article.MediaResources.Remove(media);
        }

        var existingIds = article.MediaResources.Select(value => value.MediaResourceId).ToHashSet();
        foreach (var mediaResourceId in desiredIds.Where(value => !existingIds.Contains(value)))
        {
            article.MediaResources.Add(new ArticleMediaResource { MediaResourceId = mediaResourceId });
        }
    }

    /// <summary>
    /// Replaces the tracked article category assignments with the requested set.
    /// </summary>
    /// <param name="article">The tracked article.</param>
    /// <param name="requestedCategoryIds">The desired category identifiers.</param>
    private void SynchronizeCategories(
        Article article,
        IReadOnlyCollection<Guid>? requestedCategoryIds)
    {
        var desiredIds = (requestedCategoryIds ?? []).ToHashSet();
        foreach (var assignment in article.CategoryAssignments
                     .Where(value => !desiredIds.Contains(value.ArticleCategoryId))
                     .ToArray())
        {
            db.ArticleCategoryAssignments.Remove(assignment);
            article.CategoryAssignments.Remove(assignment);
        }

        var existingIds = article.CategoryAssignments
            .Select(value => value.ArticleCategoryId)
            .ToHashSet();
        foreach (var categoryId in desiredIds.Where(value => !existingIds.Contains(value)))
        {
            article.CategoryAssignments.Add(new ArticleCategoryAssignment
            {
                ArticleCategoryId = categoryId
            });
        }
    }

    /// <summary>
    /// Revalidates stored body and cover resources against freshly rendered Markdown before publication.
    /// </summary>
    /// <param name="article">The tracked draft being published.</param>
    /// <param name="rendered">The canonical render result.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    private async Task ValidateStoredMediaAsync(
        Article article,
        ArticleContentRenderResult rendered,
        CancellationToken cancellationToken)
    {
        var bodyIds = article.MediaResources.Select(value => value.MediaResourceId).ToHashSet();
        var requestedIds = bodyIds.ToHashSet();
        if (article.CoverMediaResourceId is { } coverMediaResourceId)
        {
            requestedIds.Add(coverMediaResourceId);
        }

        var resources = await LoadAndValidateMediaAsync(requestedIds, cancellationToken);
        EnsureBodyMediaMatches(rendered, resources.Where(value => bodyIds.Contains(value.Id)).ToArray());
    }

    /// <summary>
    /// Ensures each rendered body image URL maps to exactly one declared managed media resource.
    /// </summary>
    /// <param name="rendered">The canonical render result.</param>
    /// <param name="bodyResources">The validated body media resources.</param>
    private static void EnsureBodyMediaMatches(
        ArticleContentRenderResult rendered,
        IReadOnlyCollection<MediaResource> bodyResources)
    {
        var urls = bodyResources.Select(value => GetRequiredMediaUrl(value)).ToArray();
        if (urls.Distinct(StringComparer.Ordinal).Count() != urls.Length ||
            !urls.ToHashSet(StringComparer.Ordinal).SetEquals(rendered.ImageSources))
        {
            throw ConflictException.Create(ErrorCodes.ArticleMediaNotReferenced);
        }
    }

    /// <summary>
    /// Loads and validates managed article pictures by identifier.
    /// </summary>
    /// <param name="mediaResourceIds">The distinct media identifiers to validate.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>The validated resources.</returns>
    private async Task<List<MediaResource>> LoadAndValidateMediaAsync(
        IReadOnlyCollection<Guid> mediaResourceIds,
        CancellationToken cancellationToken)
    {
        if (mediaResourceIds.Count == 0)
        {
            return [];
        }

        var resources = await db.MediaResources.AsNoTracking()
            .Where(value => mediaResourceIds.Contains(value.Id))
            .ToListAsync(cancellationToken);
        if (resources.Count != mediaResourceIds.Count)
        {
            throw NotFoundException.Create(ErrorCodes.ArticleMediaInvalid);
        }

        foreach (var resource in resources)
        {
            if (resource.Module != ResourceModule.ArticlePicture || !IsAllowedMediaUrl(resource.Url))
            {
                throw new RequestValidationException(ErrorCodes.ArticleMediaInvalid);
            }
            if (resource.Status != ResourceStatus.Active)
            {
                throw ConflictException.Create(ErrorCodes.ArticleMediaNotConfirmed);
            }
        }

        return resources;
    }

    /// <summary>
    /// Determines whether a media URL is an absolute HTTP or HTTPS URL.
    /// </summary>
    /// <param name="value">The URL to inspect.</param>
    /// <returns>True when the URL uses an allowed scheme.</returns>
    private static bool IsAllowedMediaUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Loads a tracked article with the associations required for writes and state transitions.
    /// </summary>
    /// <param name="articleId">The article identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>The tracked article.</returns>
    private async Task<Article> FindArticleForEditAsync(
        Guid articleId,
        CancellationToken cancellationToken)
        => await db.Articles
            .Include(value => value.MediaResources)
            .Include(value => value.CategoryAssignments)
            .SingleOrDefaultAsync(value => value.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);

    /// <summary>
    /// Creates the no-tracking query used by public and administrator detail projections.
    /// </summary>
    /// <returns>The article detail query.</returns>
    private IQueryable<Article> DetailsQuery()
        => db.Articles.AsNoTracking()
            .Include(value => value.CategoryAssignments)
                .ThenInclude(value => value.ArticleCategory)
            .Include(value => value.Author)
            .Include(value => value.LastEditor)
            .Include(value => value.CoverMediaResource)
            .Include(value => value.MediaResources)
                .ThenInclude(value => value.MediaResource);

    /// <summary>
    /// Applies category, status and keyword filters to an article query.
    /// </summary>
    /// <param name="query">The base article query.</param>
    /// <param name="request">The list filters.</param>
    /// <returns>The filtered query.</returns>
    private static IQueryable<Article> ApplyListFilters(
        IQueryable<Article> query,
        ArticleListRequest request)
    {
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(value => value.CategoryAssignments
                .Any(assignment => assignment.ArticleCategoryId == categoryId));
        }
        if (request.Status is { } status)
        {
            query = query.Where(value => value.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value =>
                value.Title.ToUpper().Contains(keyword) ||
                (value.Summary != null && value.Summary.ToUpper().Contains(keyword)));
        }

        return query;
    }

    /// <summary>
    /// Creates the translatable projection used by public and administrator article lists.
    /// </summary>
    /// <returns>The article list item projection.</returns>
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
            new ArticleUserSummaryResponse(
                article.Author.Id,
                article.Author.Nickname,
                article.Author.AvatarUrl),
            article.PublishedAt,
            article.UpdatedAt);

    /// <summary>
    /// Maps a fully loaded article to the internal editing contract.
    /// </summary>
    /// <param name="article">The fully loaded article.</param>
    /// <returns>The administrator response.</returns>
    private static AdminArticleResponse ToAdminResponse(Article article)
        => new(
            article.Id,
            article.Title,
            article.Summary,
            article.ContentMarkdown,
            article.ContentHtml,
            article.Status,
            ToCategoryResponses(article),
            ToUserResponse(article.Author),
            ToUserResponse(article.LastEditor),
            article.PublishedAt,
            article.CoverMediaResource is null
                ? null
                : new ArticleMediaReferenceResponse(
                    article.CoverMediaResource.Id,
                    GetRequiredMediaUrl(article.CoverMediaResource)),
            article.MediaResources
                .OrderBy(value => value.MediaResource.Url)
                .ThenBy(value => value.MediaResourceId)
                .Select(value => new ArticleMediaReferenceResponse(
                    value.MediaResourceId,
                    GetRequiredMediaUrl(value.MediaResource)))
                .ToArray(),
            article.ConcurrencyStamp,
            article.CreatedAt,
            article.UpdatedAt);

    /// <summary>
    /// Maps a fully loaded published article to the public contract.
    /// </summary>
    /// <param name="article">The fully loaded published article.</param>
    /// <returns>The public response.</returns>
    private static PublicArticleResponse ToPublicResponse(Article article)
        => new(
            article.Id,
            article.Title,
            article.Summary,
            article.ContentHtml,
            ToCategoryResponses(article),
            ToUserResponse(article.Author),
            article.PublishedAt,
            article.CoverMediaResource is null
                ? null
                : GetRequiredMediaUrl(article.CoverMediaResource),
            article.CreatedAt,
            article.UpdatedAt);

    /// <summary>
    /// Maps loaded category assignments in deterministic display order.
    /// </summary>
    /// <param name="article">The fully loaded article.</param>
    /// <returns>The category summaries.</returns>
    private static IReadOnlyCollection<ArticleCategorySummaryResponse> ToCategoryResponses(Article article)
        => article.CategoryAssignments
            .OrderBy(value => value.ArticleCategory.Name)
            .ThenBy(value => value.ArticleCategoryId)
            .Select(value => new ArticleCategorySummaryResponse(
                value.ArticleCategory.Id,
                value.ArticleCategory.Name,
                value.ArticleCategory.Slug))
            .ToArray();

    /// <summary>
    /// Maps a user entity to the article user summary contract.
    /// </summary>
    /// <param name="user">The loaded user.</param>
    /// <returns>The user summary.</returns>
    private static ArticleUserSummaryResponse ToUserResponse(User user)
        => new(user.Id, user.Nickname, user.AvatarUrl);

    /// <summary>
    /// Returns a validated non-null media URL or reports corrupted article media state.
    /// </summary>
    /// <param name="resource">The media resource.</param>
    /// <returns>The validated URL.</returns>
    private static string GetRequiredMediaUrl(MediaResource resource)
    {
        if (resource.Url is not { } url || !IsAllowedMediaUrl(url))
        {
            throw ConflictException.Create(ErrorCodes.ArticleMediaInvalid);
        }

        return url;
    }

    /// <summary>
    /// Builds a page response from materialized items and a total count.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The page items.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalCount">The total matching item count.</param>
    /// <returns>The page response.</returns>
    private static PagedResponse<T> ToPagedResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(items, page, pageSize, totalCount, (totalCount + pageSize - 1) / pageSize);

    /// <summary>
    /// Persists article changes and maps database optimistic concurrency failures to a stable conflict.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the save.</param>
    private async Task SaveArticleChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Article concurrency conflict affected entity types {EntityTypes}",
                string.Join(",", exception.Entries.Select(value => value.Metadata.Name)));
            throw ConflictException.Create(ErrorCodes.ArticleConcurrencyConflict);
        }
    }

    /// <summary>
    /// Trims required display text.
    /// </summary>
    /// <param name="value">The required text.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeRequired(string value) => value.Trim();

    /// <summary>
    /// Converts blank optional text to null and trims other values.
    /// </summary>
    /// <param name="value">The optional text.</param>
    /// <returns>The normalized optional text.</returns>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
