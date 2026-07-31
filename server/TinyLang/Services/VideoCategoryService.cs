using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现视频分类规范化、管理员维护和按可见性口径的分页计数查询。
/// </summary>
/// <param name="db">应用数据库上下文契约。</param>
/// <param name="databaseExceptionClassifier">数据库约束异常分类器。</param>
public sealed class VideoCategoryService(
    IApplicationDbContext db,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : IVideoCategoryService
{
    private const string CategoryAssignmentForeignKey =
        "FK_video_category_assignments_video_categories";

    /// <inheritdoc />
    public async Task<VideoCategoryResponse> CreateAsync(
        CreateVideoCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new VideoCategory
        {
            Name = NormalizeName(request.Name),
            Slug = NormalizeSlug(request.Slug),
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };
        db.VideoCategories.Add(category);
        await SaveWithUniqueConflictMappingAsync(category, cancellationToken);
        return await GetByIdAsync(category.Id, includeInactive: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VideoCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateVideoCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await db.VideoCategories
            .SingleOrDefaultAsync(value => value.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoCategoryNotFound);
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
        var category = await db.VideoCategories
            .SingleOrDefaultAsync(value => value.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoCategoryNotFound);

        if (await db.VideoCategoryAssignments.AsNoTracking()
            .AnyAsync(value => value.VideoCategoryId == categoryId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.VideoCategoryInUse);
        }

        db.VideoCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception,
                CategoryAssignmentForeignKey))
        {
            throw ConflictException.Create(ErrorCodes.VideoCategoryInUse);
        }
    }

    /// <inheritdoc />
    public async Task<ClearVideoCategoryResponse> ClearVideosAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.VideoCategories.AsNoTracking()
            .AnyAsync(value => value.Id == categoryId, cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.VideoCategoryNotFound);
        }

        var removedCount = await db.VideoCategoryAssignments
            .Where(value => value.VideoCategoryId == categoryId)
            .ExecuteDeleteAsync(cancellationToken);
        return new ClearVideoCategoryResponse(categoryId, removedCount);
    }

    /// <inheritdoc />
    public Task<PagedResponse<VideoCategoryResponse>> GetPublicListAsync(
        VideoCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, includeInactive: false, playableOnly: true, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResponse<VideoCategoryResponse>> GetAdminListAsync(
        AdminVideoCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, request.IncludeInactive, playableOnly: false, cancellationToken);

    /// <summary>
    /// 按分类可见性和视频状态口径构造分页 projection。
    /// </summary>
    private async Task<PagedResponse<VideoCategoryResponse>> GetListAsync(
        VideoCategoryListRequest request,
        bool includeInactive,
        bool playableOnly,
        CancellationToken cancellationToken)
    {
        var query = db.VideoCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(value => value.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value =>
                value.Name.ToUpper().Contains(keyword) ||
                value.Slug.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(value => value.Name)
            .ThenBy(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new VideoCategoryResponse(
                value.Id,
                value.Name,
                value.Slug,
                value.Description,
                value.IsActive,
                playableOnly
                    ? value.VideoAssignments.Count(assignment =>
                        assignment.Video.ProcessingStatus == VideoProcessingStatus.Ready &&
                        assignment.Video.PublicationStatus == VideoPublicationStatus.Published)
                    : value.VideoAssignments.Count(assignment =>
                        assignment.Video.PublicationStatus !=
                            VideoPublicationStatus.Archived)))
            .ToListAsync(cancellationToken);

        return new PagedResponse<VideoCategoryResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (totalCount + request.PageSize - 1) / request.PageSize);
    }

    /// <summary>
    /// 按标识查询分类并应用用户或管理员可见性条件。
    /// </summary>
    private async Task<VideoCategoryResponse> GetByIdAsync(
        Guid categoryId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.VideoCategories.AsNoTracking()
            .Where(value => value.Id == categoryId);
        if (!includeInactive)
        {
            query = query.Where(value => value.IsActive);
        }

        return await query
            .Select(value => new VideoCategoryResponse(
                value.Id,
                value.Name,
                value.Slug,
                value.Description,
                value.IsActive,
                value.VideoAssignments.Count(assignment =>
                    assignment.Video.PublicationStatus !=
                        VideoPublicationStatus.Archived)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoCategoryNotFound);
    }

    /// <summary>
    /// 保存分类并将名称/slug 唯一约束映射为稳定业务冲突。
    /// </summary>
    private async Task SaveWithUniqueConflictMappingAsync(
        VideoCategory category,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_video_categories_Name"))
        {
            throw ConflictException.Create(ErrorCodes.VideoCategoryNameConflict);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_video_categories_Slug"))
        {
            throw ConflictException.Create(ErrorCodes.VideoCategorySlugConflict);
        }
    }

    /// <summary>
    /// 去除分类名称两端空白。
    /// </summary>
    private static string NormalizeName(string value) => value.Trim();

    /// <summary>
    /// 去除 slug 两端空白并持久化为小写。
    /// </summary>
    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    /// <summary>
    /// 将空白描述归一化为 null，否则去除两端空白。
    /// </summary>
    private static string? NormalizeDescription(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
