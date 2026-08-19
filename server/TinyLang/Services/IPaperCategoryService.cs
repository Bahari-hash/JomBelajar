using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IPaperCategoryService
{
    Task<PaperCategoryResponse> CreateAsync(
        CreatePaperCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<PaperCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdatePaperCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<PagedResponse<PaperCategoryResponse>> GetPublicListAsync(
        PaperCategoryListRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PaperCategoryResponse>> GetAdminListAsync(
        AdminPaperCategoryListRequest request,
        CancellationToken cancellationToken = default);
}
