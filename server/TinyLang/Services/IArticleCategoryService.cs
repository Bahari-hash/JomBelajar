using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义文章分类创建、维护和查询的业务契约。
/// </summary>
public interface IArticleCategoryService
{
    /// <summary>
    /// 创建新的文章分类。
    /// </summary>
    /// <param name="request">分类内容。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>创建后的分类。</returns>
    Task<ArticleCategoryResponse> CreateAsync(
        CreateArticleCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新指定文章分类的内容和启用状态。
    /// </summary>
    /// <param name="categoryId">分类标识。</param>
    /// <param name="request">分类更新内容。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>更新后的分类。</returns>
    Task<ArticleCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateArticleCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除未被文章引用的指定分类。
    /// </summary>
    /// <param name="categoryId">分类标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步删除操作的任务。</returns>
    Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly removes every article association from a category without deleting either resource.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The category identifier and number of removed associations.</returns>
    Task<ClearArticleCategoryResponse> ClearArticlesAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取仅包含启用分类的公开分页列表。
    /// </summary>
    /// <param name="request">分页和关键词条件。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>公开分类分页结果。</returns>
    Task<PagedResponse<ArticleCategoryResponse>> GetPublicListAsync(
        ArticleCategoryListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的分类分页列表。
    /// </summary>
    /// <param name="request">分页、关键词和停用分类筛选条件。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>管理员分类分页结果。</returns>
    Task<PagedResponse<ArticleCategoryResponse>> GetAdminListAsync(
        AdminArticleCategoryListRequest request,
        CancellationToken cancellationToken = default);
}
