using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义视频分类管理、用户可见列表和管理员列表查询业务契约。
/// </summary>
public interface IVideoCategoryService
{
    /// <summary>
    /// 创建默认启用的视频分类。
    /// </summary>
    /// <param name="request">分类创建内容。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>创建后的分类响应。</returns>
    Task<VideoCategoryResponse> CreateAsync(
        CreateVideoCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新分类名称、slug、描述和启用状态。
    /// </summary>
    /// <param name="categoryId">分类标识。</param>
    /// <param name="request">分类更新内容。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>更新后的分类响应。</returns>
    Task<VideoCategoryResponse> UpdateAsync(
        Guid categoryId,
        UpdateVideoCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除分类并由数据库级联解除视频关联。
    /// </summary>
    /// <param name="categoryId">分类标识。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取仅包含启用分类且计数限定为可播放视频的列表。
    /// </summary>
    /// <param name="request">分页和关键词条件。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>用户分类分页结果。</returns>
    Task<PagedResponse<VideoCategoryResponse>> GetPublicListAsync(
        VideoCategoryListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的分类列表及所有关联视频计数。
    /// </summary>
    /// <param name="request">分页、关键词和停用筛选条件。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>管理员分类分页结果。</returns>
    Task<PagedResponse<VideoCategoryResponse>> GetAdminListAsync(
        AdminVideoCategoryListRequest request,
        CancellationToken cancellationToken = default);
}
