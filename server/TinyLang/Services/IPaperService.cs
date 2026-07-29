using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义试卷编辑聚合、发布生命周期和安全用户目录用例。
/// </summary>
public interface IPaperService
{
    /// <summary>
    /// 创建可暂时不满足发布数量要求的试卷草稿。
    /// </summary>
    Task<EditorPaperResponse> CreateDraftAsync(
        Guid editorId,
        CreatePaperRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用完整目标集合和并发标识更新未被测验历史锁定的试卷。
    /// </summary>
    Task<EditorPaperResponse> UpdateAsync(
        Guid paperId,
        Guid editorId,
        UpdatePaperRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完整校验试卷题型、答案和评分后幂等发布。
    /// </summary>
    Task<EditorPaperResponse> PublishAsync(
        Guid paperId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架已发布试卷并保留首次发布时间和历史测验。
    /// </summary>
    Task<EditorPaperResponse> UnpublishAsync(
        Guid paperId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除没有测验历史的 Draft 或 Unpublished 试卷。
    /// </summary>
    Task DeleteAsync(
        Guid paperId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取编辑者可见的完整试卷和标准答案。
    /// </summary>
    Task<EditorPaperResponse> GetEditorByIdAsync(
        Guid paperId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有编辑者共享的试卷管理分页列表。
    /// </summary>
    Task<PagedResponse<EditorPaperListItemResponse>> GetEditorListAsync(
        EditorPaperListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取不包含题目答案的已发布试卷分页目录。
    /// </summary>
    Task<PagedResponse<PaperCatalogItemResponse>> GetCatalogAsync(
        PaperCatalogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取不包含题目答案的已发布试卷详情。
    /// </summary>
    Task<PaperDetailsResponse> GetDetailsAsync(
        Guid paperId,
        CancellationToken cancellationToken = default);
}
