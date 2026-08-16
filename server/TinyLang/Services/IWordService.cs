using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义全局词条聚合管理、发布生命周期和登录用户查询用例。
/// </summary>
public interface IWordService
{
    /// <summary>
    /// 创建可不完整的词条草稿及其当前子项集合。
    /// </summary>
    Task<AdminWordResponse> CreateDraftAsync(
        Guid adminId,
        CreateWordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用完整目标集合和并发标识更新可编辑词条。
    /// </summary>
    Task<AdminWordResponse> UpdateAsync(
        Guid wordId,
        Guid adminId,
        UpdateWordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完整校验词条内容和音频后幂等发布词条。
    /// </summary>
    Task<AdminWordResponse> PublishAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等下架已发布词条并保留其内容和首次发布时间。
    /// </summary>
    Task<AdminWordResponse> UnpublishAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将非 Published 词条归档为不可恢复终态并保留学习历史。
    /// </summary>
    Task<AdminWordResponse> ArchiveAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除 Draft 或 Unpublished 词条及其私有子项。
    /// </summary>
    Task DeleteAsync(
        Guid wordId,
        Guid adminId,
        WordMutationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的词条管理详情。
    /// </summary>
    Task<AdminWordResponse> GetAdminByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有管理员共享的词条管理分页列表。
    /// </summary>
    Task<PagedResponse<AdminWordListItemResponse>> GetAdminListAsync(
        AdminWordListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前仍可播放的已发布词条详情。
    /// </summary>
    Task<WordResponse> GetUserByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前仍可播放的已发布词条分页列表。
    /// </summary>
    Task<PagedResponse<WordListItemResponse>> GetUserListAsync(
        WordListRequest request,
        CancellationToken cancellationToken = default);
}
