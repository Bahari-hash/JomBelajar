using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义文章草稿、发布状态和公开查询的业务契约。
/// </summary>
public interface IArticleService
{
    /// <summary>
    /// 创建文章草稿并同步其分类和媒体关联。
    /// </summary>
    /// <param name="editorId">创建草稿的编辑者标识。</param>
    /// <param name="request">文章草稿内容。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>创建后的编辑视图。</returns>
    Task<EditorArticleResponse> CreateDraftAsync(
        Guid editorId,
        CreateArticleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新可编辑文章及其分类和媒体关联。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="editorId">执行更新的编辑者标识。</param>
    /// <param name="request">新的文章内容。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>更新后的编辑视图。</returns>
    Task<EditorArticleResponse> UpdateAsync(
        Guid articleId,
        Guid editorId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验草稿内容和关联资源后发布文章。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="editorId">执行发布的编辑者标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>发布后的编辑视图。</returns>
    Task<EditorArticleResponse> PublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将已发布文章恢复为草稿。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="editorId">执行下架的编辑者标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>下架后的编辑视图。</returns>
    Task<EditorArticleResponse> UnpublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将指定文章标记为归档状态。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="editorId">执行归档的编辑者标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步归档操作的任务。</returns>
    Task ArchiveAsync(Guid articleId, Guid editorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取编辑者可见的文章详情。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>包含编辑审计信息的文章详情。</returns>
    Task<EditorArticleResponse> GetEditorByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取编辑者可见的文章分页列表。
    /// </summary>
    /// <param name="request">分页和筛选条件。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>文章分页结果。</returns>
    Task<PagedResponse<ArticleListItemResponse>> GetEditorListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定已发布文章的公开详情。
    /// </summary>
    /// <param name="articleId">文章标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>不含内部编辑信息的文章详情。</returns>
    Task<PublicArticleResponse> GetPublicByIdAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已发布文章的公开分页列表。
    /// </summary>
    /// <param name="request">分页和筛选条件；状态筛选不会应用于公开查询。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>已发布文章分页结果。</returns>
    Task<PagedResponse<ArticleListItemResponse>> GetPublicListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders Markdown through the canonical article pipeline without persisting content or media associations.
    /// </summary>
    /// <param name="request">The Markdown preview request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The canonical sanitized HTML preview.</returns>
    Task<ArticlePreviewResponse> PreviewAsync(
        ArticlePreviewRequest request,
        CancellationToken cancellationToken = default);
}
