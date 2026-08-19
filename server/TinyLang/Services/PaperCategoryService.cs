using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

public sealed class PaperCategoryService(
    IApplicationDbContext db,
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IPaperCategoryService
{
    public async Task<PaperCategoryResponse> CreateAsync(
        CreatePaperCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new CreatePaperCategoryRequestValidator());
        var category = new PaperCategory
        {
            Name = NormalizeName(request.Name),
            Slug = NormalizeSlug(request.Slug),
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };
        db.PaperCategories.Add(category);
        await SaveAsync(cancellationToken);
        return await GetByIdAsync(category.Id, true, cancellationToken);
    }

    public async Task<PaperCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdatePaperCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(request, new UpdatePaperCategoryRequestValidator());
        var category = await db.PaperCategories.SingleOrDefaultAsync(
            x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperCategoryNotFound);
        category.Name = NormalizeName(request.Name);
        category.Slug = NormalizeSlug(request.Slug);
        category.Description = NormalizeDescription(request.Description);
        category.IsActive = request.IsActive;
        await SaveAsync(cancellationToken);
        return await GetByIdAsync(categoryId, true, cancellationToken);
    }

    public async Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await db.PaperCategories.SingleOrDefaultAsync(
            x => x.Id == categoryId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperCategoryNotFound);
        if (await db.PaperCategoryAssignments.AsNoTracking()
            .AnyAsync(x => x.PaperCategoryId == categoryId, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.PaperCategoryInUse);
        }
        db.PaperCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsForeignKeyConstraintViolation(
                exception, "FK_paper_category_assignments_paper_categories_PaperCategoryId"))
        {
            throw ConflictException.Create(ErrorCodes.PaperCategoryInUse);
        }
    }

    public Task<PagedResponse<PaperCategoryResponse>> GetPublicListAsync(
        PaperCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, false, true, cancellationToken);

    public Task<PagedResponse<PaperCategoryResponse>> GetAdminListAsync(
        AdminPaperCategoryListRequest request,
        CancellationToken cancellationToken = default)
        => GetListAsync(request, request.IncludeInactive, false, cancellationToken);

    private async Task<PagedResponse<PaperCategoryResponse>> GetListAsync(
        PaperCategoryListRequest request,
        bool includeInactive,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        ServiceRequestValidator.Validate(request, new PaperCategoryListRequestValidator());
        var query = db.PaperCategories.AsNoTracking();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(x => x.Name.ToUpper().Contains(keyword) ||
                x.Slug.ToUpper().Contains(keyword));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new PaperCategoryResponse(
                x.Id, x.Name, x.Slug, x.Description, x.IsActive,
                publishedOnly
                    ? x.PaperAssignments.Count(a => a.Paper.Status == PaperPublicationStatus.Published)
                    : x.PaperAssignments.Count(a => a.Paper.Status != PaperPublicationStatus.Archived),
                x.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResponse<PaperCategoryResponse>(items, request.Page, request.PageSize,
            total, (total + request.PageSize - 1) / request.PageSize);
    }

    private async Task<PaperCategoryResponse> GetByIdAsync(
        Guid id, bool includeInactive, CancellationToken cancellationToken)
        => await db.PaperCategories.AsNoTracking().Where(x => x.Id == id)
            .Where(x => includeInactive || x.IsActive)
            .Select(x => new PaperCategoryResponse(
                x.Id, x.Name, x.Slug, x.Description, x.IsActive,
                x.PaperAssignments.Count(a => a.Paper.Status != PaperPublicationStatus.Archived),
                x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperCategoryNotFound);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "IX_paper_categories_Name"))
        { throw ConflictException.Create(ErrorCodes.PaperCategoryNameConflict); }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueConstraintViolation(exception, "IX_paper_categories_Slug"))
        { throw ConflictException.Create(ErrorCodes.PaperCategorySlugConflict); }
    }

    private static string NormalizeName(string value) => value.Trim();
    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
