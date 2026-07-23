using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Services;

public sealed class ArticleService(
    IApplicationDbContext db,
    IHtmlContentSanitizer htmlSanitizer) : IArticleService
{
    public async Task<ArticleResponse> CreateDraftAsync(
        Guid editorId,
        CreateArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = await ValidateDraftInputAsync(
            editorId,
            request,
            existingArticle: null,
            cancellationToken);
        var article = new Article
        {
            Title = NormalizeRequired(request.Title),
            Summary = NormalizeOptional(request.Summary),
            ContentHtml = sanitized.Html,
            CategoryId = request.CategoryId,
            AuthorId = editorId,
            LastEditorId = editorId,
            CoverMediaResourceId = request.CoverMediaResourceId
        };
        await SynchronizeMediaAsync(article, editorId, request, sanitized, cancellationToken);

        db.Articles.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

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

        var sanitized = await ValidateDraftInputAsync(editorId, request, article, cancellationToken);
        article.Title = NormalizeRequired(request.Title);
        article.Summary = NormalizeOptional(request.Summary);
        article.ContentHtml = sanitized.Html;
        article.CategoryId = request.CategoryId;
        article.CoverMediaResourceId = request.CoverMediaResourceId;
        article.LastEditorId = editorId;
        await SynchronizeMediaAsync(article, editorId, request, sanitized, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await GetEditorByIdAsync(article.Id, cancellationToken);
    }

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

        await EnsureCategoryUsableAsync(article.CategoryId, required: true, cancellationToken);
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

    public async Task<ArticleResponse> GetEditorByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await DetailsQuery()
            .SingleOrDefaultAsync(x => x.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);
        return ToResponse(article, includeLastEditor: true);
    }

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
            .Select(x => new ArticleListItemResponse(
                x.Id,
                x.Title,
                x.Summary,
                x.Status,
                x.Category == null ? null : new ArticleCategorySummaryResponse(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.Slug),
                x.CoverMediaResource == null ? null : x.CoverMediaResource.Url,
                new ArticleUserSummaryResponse(x.Author.Id, x.Author.Nickname, x.Author.AvatarUrl),
                x.PublishedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

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
            .Select(x => new ArticleListItemResponse(
                x.Id,
                x.Title,
                x.Summary,
                ArticleStatus.Published,
                x.Category == null ? null : new ArticleCategorySummaryResponse(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.Slug),
                x.CoverMediaResource == null ? null : x.CoverMediaResource.Url,
                new ArticleUserSummaryResponse(x.Author.Id, x.Author.Nickname, x.Author.AvatarUrl),
                x.PublishedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return ToPagedResponse(items, request.Page, request.PageSize, totalCount);
    }

    private async Task<HtmlSanitizationResult> ValidateDraftInputAsync(
        Guid editorId,
        ArticleUpsertRequest request,
        Article? existingArticle,
        CancellationToken cancellationToken)
    {
        await EnsureCategoryUsableAsync(request.CategoryId, required: false, cancellationToken);
        var sanitized = htmlSanitizer.Sanitize(request.ContentHtml);
        EnsureContentIsPublishable(sanitized);
        await ValidateRequestedMediaAsync(editorId, existingArticle, request, sanitized, cancellationToken);
        return sanitized;
    }

    private async Task EnsureCategoryUsableAsync(
        Guid? categoryId,
        bool required,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            if (required)
            {
                throw new RequestValidationException(ErrorCodes.ArticleCategoryRequired);
            }

            return;
        }

        var category = await db.ArticleCategories.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);
        if (!category.IsActive)
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryInactive);
        }
    }

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
            article.MediaResources.Add(new ArticleMediaResource
            {
                MediaResourceId = mediaResourceId
            });
        }
    }

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

    private static bool IsAllowedMediaUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task<Article> FindArticleForEditAsync(Guid articleId, CancellationToken cancellationToken)
        => await db.Articles
            .Include(x => x.MediaResources)
            .SingleOrDefaultAsync(x => x.Id == articleId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleNotFound);

    private IQueryable<Article> DetailsQuery()
        => db.Articles.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Author)
            .Include(x => x.LastEditor)
            .Include(x => x.CoverMediaResource)
            .Include(x => x.MediaResources);

    private static IQueryable<Article> ApplyListFilters(
        IQueryable<Article> query,
        ArticleListRequest request)
    {
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(x => x.CategoryId == categoryId);
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

    private static ArticleResponse ToResponse(Article article, bool includeLastEditor)
        => new(
            article.Id,
            article.Title,
            article.Summary,
            article.ContentHtml,
            article.Status,
            article.Category is null ? null : new ArticleCategorySummaryResponse(
                article.Category.Id,
                article.Category.Name,
                article.Category.Slug),
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

    private static PagedResponse<T> ToPagedResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(items, page, pageSize, totalCount, (totalCount + pageSize - 1) / pageSize);

    private static void EnsureContentIsPublishable(HtmlSanitizationResult sanitized)
    {
        if (sanitized.HasInvalidUrls ||
            (string.IsNullOrWhiteSpace(sanitized.PlainText) && sanitized.ImageSources.Count == 0))
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentInvalid);
        }
    }

    private static string NormalizeRequired(string value) => value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
