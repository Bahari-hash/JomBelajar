using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现文章分类维护、唯一约束映射和公开/管理列表查询。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="databaseExceptionClassifier">数据库约束异常分类器。</param>
public sealed class ArticleCategoryService(
    IApplicationDbContext db,
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IArticleCategoryService
{
    private const string CategoryAssignmentForeignKey =
        "FK_article_category_assignments_article_categories";

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await db.ArticleCategories
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);

        if (await db.ArticleCategoryAssignments.AsNoTracking()
            .AnyAsync(value => value.ArticleCategoryId == categoryId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryInUse);
        }

        db.ArticleCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                CategoryAssignmentForeignKey))
        {
            throw ConflictException.Create(ErrorCodes.ArticleCategoryInUse);
        }
    }

    /// <inheritdoc />
    public async Task<ClearArticleCategoryResponse> ClearArticlesAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.ArticleCategories.AsNoTracking()
            .AnyAsync(value => value.Id == categoryId, cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.ArticleCategoryNotFound);
        }

        var removedCount = await db.ArticleCategoryAssignments
            .Where(value => value.ArticleCategoryId == categoryId)
            .ExecuteDeleteAsync(cancellationToken);
        return new ClearArticleCategoryResponse(categoryId, removedCount);
    }

    /// <inheritdoc />
    public Task<PagedResponse<ArticleCategoryResponse>> GetPublicListAsync(
        ArticleCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, includeInactive: false, publishedOnly: true, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResponse<ArticleCategoryResponse>> GetAdminListAsync(
        AdminArticleCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, includeInactive: request.IncludeInactive, publishedOnly: false, cancellationToken);

    /// <summary>
    /// 根据可见性和文章状态口径查询分类分页结果。
    /// </summary>
    /// <param name="request">分页和关键词条件。</param>
    /// <param name="includeInactive">是否包含停用分类。</param>
    /// <param name="publishedOnly">分类计数是否仅统计已发布文章。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>分类分页结果。</returns>
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

    /// <summary>
    /// 按标识查询分类，并投影其非归档文章数量。
    /// </summary>
    /// <param name="categoryId">分类标识。</param>
    /// <param name="includeInactive">是否允许返回停用分类。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>分类响应。</returns>
    /// <exception cref="NotFoundException">分类不存在或不满足可见性要求。</exception>
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

    /// <summary>
    /// 保存分类变更，并将名称或 slug 唯一约束映射为业务冲突。
    /// </summary>
    /// <param name="category">正在保存的分类。</param>
    /// <param name="cancellationToken">用于取消保存的令牌。</param>
    /// <returns>表示异步保存操作的任务。</returns>
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

    /// <summary>
    /// 去除分类名称两端空白。
    /// </summary>
    /// <param name="value">分类名称。</param>
    /// <returns>规范化名称。</returns>
    private static string NormalizeName(string value) => value.Trim();

    /// <summary>
    /// 去除 slug 两端空白并转换为小写。
    /// </summary>
    /// <param name="value">分类 slug。</param>
    /// <returns>规范化 slug。</returns>
    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    /// <summary>
    /// 将空白描述转换为 <see langword="null"/>，否则去除两端空白。
    /// </summary>
    /// <param name="value">分类描述。</param>
    /// <returns>规范化描述。</returns>
    private static string? NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
