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
    Task<AdminPaperResponse> CreateDraftAsync(
        Guid adminId,
        CreatePaperRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用完整目标集合和并发标识更新未被测验历史锁定的试卷。
    /// </summary>
    Task<AdminPaperResponse> UpdateAsync(
        Guid paperId,
        Guid adminId,
        UpdatePaperRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用客户端并发标识检查试卷发布要求且不修改持久化状态。
    /// </summary>
    Task<PaperValidationResponse> ValidateAsync(
        Guid paperId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完整校验试卷题型、答案和评分后幂等发布。
    /// </summary>
    Task<AdminPaperResponse> PublishAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架已发布试卷并保留首次发布时间和历史测验。
    /// </summary>
    Task<AdminPaperResponse> UnpublishAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 Draft 或 Unpublished 试卷迁移到不可恢复的归档终态。
    /// </summary>
    Task<AdminPaperResponse> ArchiveAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除没有测验历史的 Draft 或 Unpublished 试卷。
    /// </summary>
    Task DeleteAsync(
        Guid paperId,
        Guid adminId,
        PaperMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的完整试卷和标准答案。
    /// </summary>
    Task<AdminPaperResponse> GetAdminByIdAsync(
        Guid paperId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有管理员共享的试卷管理分页列表。
    /// </summary>
    Task<PagedResponse<AdminPaperListItemResponse>> GetAdminListAsync(
        AdminPaperListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取不包含题目答案的已发布试卷分页目录。
    /// </summary>
    Task<PagedResponse<PaperCatalogItemResponse>> GetCatalogAsync(
        PaperCatalogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已发布试卷使用的公开标签目录。
    /// </summary>
    Task<PagedResponse<PaperTagSummaryResponse>> GetPublicTagListAsync(
        PaperTagListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取包含全部试卷状态的管理员标签目录。
    /// </summary>
    Task<PagedResponse<PaperTagSummaryResponse>> GetAdminTagListAsync(
        PaperTagListRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取不包含题目答案的已发布试卷详情。
    /// </summary>
    Task<PaperDetailsResponse> GetDetailsAsync(
        Guid paperId,
        CancellationToken cancellationToken = default);
}
