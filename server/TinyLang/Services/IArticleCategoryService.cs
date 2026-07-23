using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IArticleCategoryService
{
    Task<ArticleCategoryResponse> CreateAsync(
        CreateArticleCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<ArticleCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateArticleCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<PagedResponse<ArticleCategoryResponse>> GetPublicListAsync(
        ArticleCategoryListRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ArticleCategoryResponse>> GetAdminListAsync(
        AdminArticleCategoryListRequest request,
        CancellationToken cancellationToken = default);
}
