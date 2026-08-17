using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义全局词条聚合管理和登录用户查询用例。
/// </summary>
public interface IWordService
{
    Task<AdminWordResponse> CreateAsync(
        Guid adminId,
        CreateWordRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminWordResponse> UpdateAsync(
        Guid wordId,
        Guid adminId,
        UpdateWordRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid wordId,
        Guid adminId,
        DeleteWordRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminWordResponse> GetAdminByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<AdminWordListItemResponse>> GetAdminListAsync(
        AdminWordListRequest request,
        CancellationToken cancellationToken = default);

    Task<WordResponse> GetUserByIdAsync(
        Guid wordId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<WordListItemResponse>> GetUserListAsync(
        WordListRequest request,
        CancellationToken cancellationToken = default);
}
