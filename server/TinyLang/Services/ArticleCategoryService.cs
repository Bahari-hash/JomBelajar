using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

public sealed class ArticleCategoryService(
    IApplicationDbContext db,
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IArticleCategoryService
{
    public async Task<ArticleCategoryResponse> CreateAsync(
        CreateArticleCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new ArticleCategory
        {
            Name = NormalizeName(request.Name),
            Slug = NormalizeSlug(request.Slug),
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };
        db.ArticleCategories.Add(category);
        await SaveWithUniqueConflictMappingAsync(category, cancellationToken);
        return await GetByIdAsync(category.Id, includeInactive: true, cancellationToken);
    }

    public async Task<ArticleCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateArticleCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await db.ArticleCategories
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);

        category.Name = NormalizeName(request.Name);
        category.Slug = NormalizeSlug(request.Slug);
        category.Description = NormalizeDescription(request.Description);
        category.IsActive = request.IsActive;
        await SaveWithUniqueConflictMappingAsync(category, cancellationToken);
        return await GetByIdAsync(category.Id, includeInactive: true, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await db.ArticleCategories
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);

        var assignments = await db.ArticleCategoryAssignments
            .Where(x => x.ArticleCategoryId == categoryId)
            .ToListAsync(cancellationToken);
        db.ArticleCategoryAssignments.RemoveRange(assignments);
        db.ArticleCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResponse<ArticleCategoryResponse>> GetPublicListAsync(
        ArticleCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, includeInactive: false, publishedOnly: true, cancellationToken);

    public Task<PagedResponse<ArticleCategoryResponse>> GetAdminListAsync(
        AdminArticleCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, includeInactive: request.IncludeInactive, publishedOnly: false, cancellationToken);

    private async Task<PagedResponse<ArticleCategoryResponse>> GetListAsync(
        ArticleCategoryListRequest request,
        bool includeInactive,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        var query = db.ArticleCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.Name.ToUpper().Contains(keyword) ||
                x.Slug.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ArticleCategoryResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.IsActive,
                publishedOnly
                    ? x.ArticleAssignments.Count(assignment => assignment.Article.Status == ArticleStatus.Published)
                    : x.ArticleAssignments.Count(assignment => assignment.Article.Status != ArticleStatus.Archived)))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ArticleCategoryResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (totalCount + request.PageSize - 1) / request.PageSize);
    }

    private async Task<ArticleCategoryResponse> GetByIdAsync(
        Guid categoryId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.ArticleCategories.AsNoTracking()
            .Where(x => x.Id == categoryId);
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .Select(x => new ArticleCategoryResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.IsActive,
                x.ArticleAssignments.Count(assignment => assignment.Article.Status != ArticleStatus.Archived)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);
    }

    private async Task SaveWithUniqueConflictMappingAsync(
        ArticleCategory category,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "IX_article_categories_Name"))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryNameConflict);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "IX_article_categories_Slug"))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategorySlugConflict);
        }
    }

    private static string NormalizeName(string value) => value.Trim();

    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    private static string? NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
